// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Class that takes the spectrum output from AudioSource.GetSpectrumData and computes it using jobs
/// to extract useful data for sound visualization.
/// </summary>
public class SpectrumBandsAnalyzer : MonoBehaviour
{
    #region Jobs

    /// <summary>
    /// Job that calculates the means for the frequency bands. Also registers whether there is a
    /// change in the mean of each band that can be considered as a beat.
    /// </summary>
    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    private struct CalculateBandsMeansJob : IJobFor
    {
        public NativeArray<float> rawMeans;
        public NativeArray<float> smoothedMeans;
        [WriteOnly] public NativeArray<bool> isBeatPerBand;

        [ReadOnly] public NativeArray<float> spectrum;
        [ReadOnly] public NativeArray<float> bands;

        public float sampleRate;
        public float bandFrequencyPeakInfluence;
        public float smoothness;
        public float diffToConsiderBeat;
        public float dt;

        /// <inheritdoc/>
        public void Execute(int index)
        {
            // Calculate the lower and upper bounds of this band using the center frequency, and
            // translate that to the minimum and maximum indices from the spectrum. Nyquist's
            // theorem states that a periodic signal must be sampled at more than twice the highest
            // frequency component of the signal, so at 44.1 KHz sampling rate the maximum sound
            // frequency in the spectrum should be 22.05 KHz. If spectrum length is 8192 we have
            // ~2.69165 Hz of difference between contiguous positions in the spectrum array.
            float2 lowerUpperBound = new float2(bands[index] / BandBoundsFactor, bands[index] * BandBoundsFactor);
            int2 minMaxIndex = new int2(math.floor((lowerUpperBound / sampleRate) * 2.0f * spectrum.Length));
            minMaxIndex = math.clamp(minMaxIndex, 0, spectrum.Length - 1);

            float mean = 0.0f;
            float meanFactor = 1.0f / ((float)(minMaxIndex.y - minMaxIndex.x) + 1.0f);
            float max = 0.0f;

            for (int j = minMaxIndex.x; j <= minMaxIndex.y; ++j)
            {
                mean = mean + spectrum[j] * meanFactor;
                max = math.max(spectrum[j], max);
            }

            mean = math.lerp(mean, max, bandFrequencyPeakInfluence);
            float smoothedMean = math.lerp(smoothedMeans[index], mean, 1.0f - math.pow(smoothness, dt));

            isBeatPerBand[index] = mean - rawMeans[index] >= diffToConsiderBeat;
            smoothedMeans[index] = smoothedMean;
            rawMeans[index] = mean;
        }
    }

    #endregion

    #region Events

    public delegate void OnBeatDelegate(int band);
    public event OnBeatDelegate OnBeat;

    #endregion

    #region Public Attributes

    // More info: https://en.wikipedia.org/wiki/Octave_band
    public const int NumberOfBands = 32;
    public const float BandBoundsFactor = 1.12246f; // < 2^(1/6)
    public static readonly float[] Bands = new float[NumberOfBands]
    {
        16.0f, 20.0f, 25.0f, 31.5f,
        40.0f, 50.0f, 63.0f, 80.0f,
        100.0f, 125.0f, 160.0f, 200.0f,
        250.0f, 315.0f, 400.0f, 500.0f,
        630.0f, 800.0f, 1000.0f, 1250.0f,
        1600.0f, 2000.0f, 2500.0f, 3150.0f,
        4000.0f, 5000.0f, 6300.0f, 8000.0f,
        10000.0f, 12500.0f, 16000.0f, 20000.0f
    };

    #endregion

    #region Private Attributes

    [Tooltip("This value can be used to shift the mean towards the peak of each frequency band, resulting in values that pop out more.")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float bandFrequencyPeakInfluence = 1.0f;
    [Range(0.00001f, 0.05f)]
    [SerializeField] private float smoothness = 0.00001f;
    [Range(0.005f, 0.015f)]
    [SerializeField] private float minDiffToConsiderBeat = 0.01f;

    private NativeArray<float> bands;
    private NativeArray<float> rawMeans;
    private NativeArray<float> smoothedMeans;
    private NativeArray<bool> isBeatPerBand;

    #endregion

    #region Properties

    public NativeArray<float> RawMeans { get { return rawMeans; } }
    public NativeArray<float> SmoothedMeans { get { return smoothedMeans; } }

    #endregion

    #region MonoBehaviour Methods

    private void Awake()
    {
        AllocateNativeDatastructures();
    }

    private void Update()
    {
        for (int i = 0; i < isBeatPerBand.Length; ++i)
        {
            if (isBeatPerBand[i])
                OnBeat?.Invoke(i);
        }
    }

    private void OnDestroy()
    {
        DisposeNativeDatastructures();
    }

    #endregion

    #region Native Datastructures Methods

    /// <summary>
    /// Allocates and prepares the needed native datastructures.
    /// </summary>
    private void AllocateNativeDatastructures()
    {
        bands = new NativeArray<float>(NumberOfBands, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        rawMeans = new NativeArray<float>(NumberOfBands, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        smoothedMeans = new NativeArray<float>(NumberOfBands, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        isBeatPerBand = new NativeArray<bool>(NumberOfBands, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        bands.CopyFrom(Bands);
    }

    /// <summary>
    /// Disposes the allocated native datastructures.
    /// </summary>
    private void DisposeNativeDatastructures()
    {
        if (bands.IsCreated)
            bands.Dispose();
        if (rawMeans.IsCreated)
            rawMeans.Dispose();
        if (smoothedMeans.IsCreated)
            smoothedMeans.Dispose();
        if (isBeatPerBand.IsCreated)
            isBeatPerBand.Dispose();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Schedules the calculation of means for the frequency bands using jobs, using the given
    /// sample rate and sound spectrum. The updated means can be accessed through the means property
    /// of this class when the job is done.
    /// </summary>
    /// <param name="sampleRate"></param>
    /// <param name="spectrum"></param>
    /// <param name="deps"></param>
    /// <returns></returns>
    public JobHandle ScheduleSpectrumBandMeans(float sampleRate, NativeArray<float> spectrum, JobHandle deps = default)
    {
        // Higher frequency bands cover more indices in the array, so try to balance
        // innerLoopBatchCount for the worst case scenario (one thread iterating over the last 4
        // bands). Actually, not sure if in mobile this will make a substantial difference, but on
        // my PC, running it single threaded is marginally faster.
        return new CalculateBandsMeansJob
        {
            rawMeans = rawMeans,
            smoothedMeans = smoothedMeans,
            isBeatPerBand = isBeatPerBand,
            spectrum = spectrum,
            bands = bands,
            sampleRate = sampleRate,
            bandFrequencyPeakInfluence = bandFrequencyPeakInfluence,
            smoothness = smoothness,
            diffToConsiderBeat = minDiffToConsiderBeat,
            dt = Time.deltaTime,
        }
        .Schedule(NumberOfBands, deps);
    }

    #endregion
}