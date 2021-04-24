// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

/// <summary>
/// The class that displays the procedural mesh. Its vertices are animated using the audio spectrum
/// and the means of each of the 32 bands that the spectrum is split into.
/// </summary>
[RequireComponent(typeof(SpectrumBandsAnalyzer), typeof(MeshRenderer), typeof(MeshFilter))]
public class AudioMesh : MonoBehaviour
{
    #region Jobs

    /// <summary>
    /// The job that setups the indices of the mesh.
    /// </summary>
    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    private struct SetupIndicesJob : IJobFor
    {
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<ushort> indices;

        public int resolution;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            // each time a row finishes, vertices on the edge are not shared
            int vertexIndexOffset = index / (resolution - 1);
            ushort startTriIndex = (ushort)(index * 6);

            // quad first triangle
            indices[startTriIndex] = (ushort)(index + vertexIndexOffset);
            indices[startTriIndex + 1] = (ushort)(index + resolution + vertexIndexOffset);
            indices[startTriIndex + 2] = (ushort)(index + 1 + vertexIndexOffset);

            // quad second triangle
            indices[startTriIndex + 3] = (ushort)(index + 1 + vertexIndexOffset);
            indices[startTriIndex + 4] = (ushort)(index + resolution + vertexIndexOffset);
            indices[startTriIndex + 5] = (ushort)(index + 1 + resolution + vertexIndexOffset);
        }
    }

    /// <summary>
    /// The job that animates the vertices of the mesh with the data from the spectrum analyzer.
    /// </summary>
    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    private struct AnimateVerticesJob : IJobFor
    {
        public NativeArray<float3> vertices;

        [ReadOnly] public NativeArray<float> means;

        public int resolution;
        public float scale;
        public float middleCorridorWidth;
        public float mountainNoiseWeight;
        public float mountainNoiseFreq;
        public float mountainEdgeSmoothness;

        public float time;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            float halfRes = (float)resolution * 0.5f;
            int col = index % resolution;

            float x = math.remap(0.0f, (float)(resolution - 1), -halfRes, halfRes, (float)col);
            float xAbs = math.abs(x);

            int z = index / resolution;

            // each row of vertices has the same frequency along it, and is repeated three times at
            // each three contiguous rows. Start from the end instead of the start.
            float y = means[(means.Length - 1) - (z / 3) % means.Length] * scale;

            float2 p = new float2((x + time) * mountainNoiseFreq, ((float)z + time) * mountainNoiseFreq);
            float cnoise = ((noise.cnoise(p) * mountainNoiseWeight) + 1.0f) * 0.5f;
            y = y + cnoise * y;

            // leave a flat quad between each frequency band representation
            //y = math.select(0.0f, y, z % 3 == 1);

            // smoothly flatten the mountains at their edges and flatten the middle corridor
            float2 logCorridorEdge = new float2(xAbs - middleCorridorWidth + 1.0f, halfRes - xAbs + 1.0f);
            logCorridorEdge = math.clamp(logCorridorEdge, 0.0f, math.abs(logCorridorEdge));
            logCorridorEdge = math.log(logCorridorEdge);

            float2 corridorEdge = math.smoothstep(0.0f, mountainEdgeSmoothness, logCorridorEdge);
            float finalCorridorEdge = math.cmin(corridorEdge);

            vertices[index] = new float3(x, finalCorridorEdge * (y + cnoise), z);
        }
    }

    #endregion

    #region Private Attributes

    // 8192 is the maximum samples according to Unity's doc. It runs fine on my snapdragon 670 (Mono
    // + arm7 dev build, I can't IL2CPP + arm64 for some reason, looks like a bug). It takes an
    // average of ~4 to 6 ms to get the spectrum data with the highest quality FFT.
    private const int SpectrumSamples = 8192;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSrc;

    private float[] spectrumManaged = new float[SpectrumSamples];
    private NativeArray<float> spectrum;

    private SpectrumBandsAnalyzer spectrumAnalyzer;

    [Header("Mesh")]
    [Range(32, 105)] // < 105 x 105 is the maximum vertices resolution for UInt16 index format
    [SerializeField] private int resolution = 105;
    [Range(0.01f, 100.0f)]
    [SerializeField] private float scale = 50.0f;
    [SerializeField] private float middleCorridorWidth = -5.0f;
    [SerializeField] private float mountainNoiseWeight = 1.1f;
    [Range(0.0f, 0.5f)]
    [SerializeField] private float mountainNoiseFrequency = 0.15f;
    [Range(0.0f, 2.0f)]
    [SerializeField] private float mountainEdgeSmoothness = 1.75f;
    [SerializeField] private float noiseTimeSpeed = 1.0f;

    private int prevFrameResolution;

    private MeshFilter meshFilter;
    private Mesh mesh;

    private NativeArray<float3> vertices;
    private NativeArray<ushort> indices;

    private CustomSampler getSpectrumSampler = CustomSampler.Create("AudioMesh.GetSpectrumData");
    private CustomSampler copySpectrumSampler = CustomSampler.Create("AudioMesh.CopySpectrum");

    #endregion

    #region Properties

    public AudioSource AudioSrc { get { return audioSrc; } }
    public SpectrumBandsAnalyzer SpectrumAnalyzer { get { return spectrumAnalyzer; } }
    public float MiddleCorridorWidth
    {
        get { return middleCorridorWidth; }
        set { middleCorridorWidth = value; }
    }

    #endregion

    #region MonoBehaviour Methods

    private void Awake()
    {
        spectrumAnalyzer = GetComponent<SpectrumBandsAnalyzer>();
        meshFilter = GetComponent<MeshFilter>();
        Init();
    }

    private void Update()
    {
        if (prevFrameResolution != resolution)
            Init();

        AnimateVertices();
        prevFrameResolution = resolution;
    }

    private void OnDestroy()
    {
        DestroyMesh();
        DisposeNativeDatastructures();
    }

    #endregion

    #region Native Datastructures Methods

    /// <summary>
    /// Allocates the needed native datastructures.
    /// </summary>
    private void AllocateNativeDatastructures()
    {
        spectrum = new NativeArray<float>(SpectrumSamples, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        vertices = new NativeArray<float3>(resolution * resolution, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        indices = new NativeArray<ushort>(GetNumIndices(), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    /// <summary>
    /// Disposes the allocated native datastructures.
    /// </summary>
    private void DisposeNativeDatastructures()
    {
        if (spectrum.IsCreated)
            spectrum.Dispose();
        if (vertices.IsCreated)
            vertices.Dispose();
        if (indices.IsCreated)
            indices.Dispose();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initializes all the stuff related to the mesh.
    /// </summary>
    private void Init()
    {
        DisposeNativeDatastructures();
        AllocateNativeDatastructures();

        ResetMesh();

        new SetupIndicesJob
        {
            indices = indices,
            resolution = resolution,
        }
        .ScheduleParallel(GetNumQuads(), 64, default(JobHandle))
        .Complete();

        VertexAttributeDescriptor vertexDesc = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        mesh.SetVertexBufferParams(vertices.Length, vertexDesc);
        mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);

        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, MeshUpdateFlags.Default);
        mesh.SetIndexBufferData(indices, 0, 0, indices.Length, MeshUpdateFlags.Default);

        SubMeshDescriptor submeshDesc = new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles);
        mesh.SetSubMesh(0, submeshDesc, MeshUpdateFlags.Default);

        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000.0f);
    }

    /// <summary>
    /// Animates the vertices of the procedural mesh analyzing the spectrum data, and updates the
    /// mesh vertices.
    /// </summary>
    private void AnimateVertices()
    {
        getSpectrumSampler.Begin();
        audioSrc.GetSpectrumData(spectrumManaged, 0, FFTWindow.BlackmanHarris);
        getSpectrumSampler.End();

        copySpectrumSampler.Begin();
        spectrum.CopyFrom(spectrumManaged);
        copySpectrumSampler.End();

        JobHandle meansHandle = spectrumAnalyzer.ScheduleSpectrumBandMeans(audioSrc.clip.frequency, spectrum);

        new AnimateVerticesJob
        {
            vertices = vertices,
            means = spectrumAnalyzer.SmoothedMeans,
            resolution = resolution,
            scale = scale,
            middleCorridorWidth = middleCorridorWidth,
            mountainNoiseWeight = mountainNoiseWeight,
            mountainNoiseFreq = mountainNoiseFrequency,
            mountainEdgeSmoothness = mountainEdgeSmoothness,
            time = Time.time * noiseTimeSpeed,
        }
        .ScheduleParallel(vertices.Length, 64, meansHandle)
        .Complete();

        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, ~MeshUpdateFlags.Default);
    }

    /// <summary>
    /// Resets the mesh, creating it if needed, or clearing it otherwise.
    /// </summary>
    private void ResetMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "AudioMesh";
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear(false);
        }
    }

    /// <summary>
    /// Destroys the mesh.
    /// </summary>
    private void DestroyMesh()
    {
        if (mesh != null)
            Destroy(mesh);
    }

    /// <summary>
    /// Gets the number of quads given the resolution of vertices from the mesh.
    /// </summary>
    /// <returns></returns>
    private int GetNumQuads()
    {
        int res = resolution - 1;

        return res * res;
    }

    /// <summary>
    /// Gets the number of indices given the resolution of vertices from the mesh.
    /// </summary>
    /// <returns></returns>
    private int GetNumIndices()
    {
        return GetNumQuads() * 6;
    }

    #endregion
}