// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The class that controls the transitions that must happen in the "game".
/// </summary>
public class SceneController : MonoBehaviour
{
    #region Defs

    /// <summary>
    /// Configuration for one state of the whole scene.
    /// </summary>
    [Serializable]
    private class SynthwaveConfig
    {
        public float camFov = 75.0f;
        public Vector3 sunRotation = new Vector3(168.5f, 0.0f, 0.0f);
        [Range(0.0f, 1.0f)]
        public float mountainGradient = 0.05f;
        [Range(0.0f, 1.0f)]
        public float sunDiscSize = 0.265f;
        [Range(0.0f, 1.0f)]
        public float sunGradientMidPoint = 0.375f;
        [Range(0.0f, 1.0f)]
        public float skyTintsSun = 0.4f;
        [Range(0.0f, 1.0f)]
        public float horizonHeight = 1.0f;

        public Quaternion GetSunRotation()
        {
            return Quaternion.Euler(sunRotation);
        }
    }

    #endregion

    #region Private Attributes

    [Header("URP Asset")]
    [SerializeField] private UniversalRenderPipelineAsset urpAsset = null;

    [Header("Components")]
    [SerializeField] private Camera mainCam = null;
    [SerializeField] private Transform sun = null;
    [SerializeField] private TitleText titleText = null;
    [SerializeField] private AudioMesh audioMesh = null;
    [SerializeField] private Material gridMaterial = null;
    [SerializeField] private Material skyboxMaterial = null;
    [SerializeField] private Material buildingMaterial = null;

    [Header("Config")]
    [SerializeField] private float transitionTime = 120.0f;
    [SerializeField] private SynthwaveConfig sunsetConfig = null;
    [SerializeField] private SynthwaveConfig zenithConfig = null;

    [Range(0.0f, 0.5f)]
    [SerializeField] private float corridorSmoothness = 0.01f;
    [SerializeField] private float openedCorridorWidth = 9.0f;
    [SerializeField] private float closedCorridorWidth = -3.0f;

    private readonly int mountainGradientId = Shader.PropertyToID("_MountainHeightPeak");
    private readonly int sunDiscSizeId = Shader.PropertyToID("_SunDiscSize");
    private readonly int sunGradientMidPointId = Shader.PropertyToID("_SunGradientMidpoint");
    private readonly int skyTintsSunId = Shader.PropertyToID("_SkyTintsSun");
    private readonly int horizonHeightId = Shader.PropertyToID("_HorizonHeight");
    private readonly int windowLightenOffsetId = Shader.PropertyToID("_WindowLightenOffset");

    private SynthwaveConfig currSynthwaveConfig = new SynthwaveConfig();

    private float currCorridorTargetWidth;
    private float timer;

    private float minTimeBetweenBeats = 0.25f;
    private float beatTimer;

    #endregion

    #region MonoBehaviour Methods

    private void Start()
    {
        // Note: I know in some mobile GPUs (Mali?) msaa x2-x4 is close to being free but my mobile
        // seems to have a reasonable hit in performance.
#if UNITY_STANDALONE
        Cursor.visible = false;
        urpAsset.msaaSampleCount = 2;
#elif UNITY_ANDROID
        urpAsset.msaaSampleCount = 0;
#endif
        currCorridorTargetWidth = audioMesh.MiddleCorridorWidth;
        audioMesh.SpectrumAnalyzer.OnBeat += OnBandBeat;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        beatTimer += Time.deltaTime;

        float time = Time.time;
        float clipLength = audioMesh.AudioSrc.clip.length;

        // hacky stuff that works for the audio clip that I have used
        if (time % clipLength >= 38.0f)
        {
            if (time % clipLength >= 327.0f)
            {
                // close corridor at second 327
                currCorridorTargetWidth = closedCorridorWidth;
                titleText.Hide();
            }
            else
            {
                // open corridor at second 38
                currCorridorTargetWidth = openedCorridorWidth;
                titleText.Show();
            }
        }
        else
        {
            currCorridorTargetWidth = closedCorridorWidth;
        }

        bool shouldSunset = (time % clipLength + transitionTime) / clipLength >= 1.0f;
        float dir = shouldSunset ? -1.0f : 1.0f;

        timer += (dt * dir);
        timer = Mathf.Clamp(timer, 0.0f, transitionTime);
        float t = timer / transitionTime;
        t = EaseOutSine(t);

        UpdateConfig(sunsetConfig, zenithConfig, t);
        UpdateCorridorWidth();

#if UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        audioMesh.SpectrumAnalyzer.OnBeat -= OnBandBeat;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Update the whole configuration between two scene states using t.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="t"></param>
    private void UpdateConfig(SynthwaveConfig a, SynthwaveConfig b, float t)
    {
        currSynthwaveConfig.mountainGradient = Mathf.Lerp(a.mountainGradient, b.mountainGradient, t);
        currSynthwaveConfig.sunDiscSize = Mathf.Lerp(a.sunDiscSize, b.sunDiscSize, t);
        currSynthwaveConfig.sunGradientMidPoint = Mathf.Lerp(a.sunGradientMidPoint, b.sunGradientMidPoint, t);
        currSynthwaveConfig.skyTintsSun = Mathf.Lerp(a.skyTintsSun, b.skyTintsSun, t);
        currSynthwaveConfig.horizonHeight = Mathf.Lerp(a.horizonHeight, b.horizonHeight, t);

        mainCam.fieldOfView = Mathf.Lerp(a.camFov, b.camFov, t);
        Quaternion sunRot = Quaternion.Slerp(a.GetSunRotation(), b.GetSunRotation(), t);
        sun.eulerAngles = sunRot.eulerAngles;

        gridMaterial.SetFloat(mountainGradientId, currSynthwaveConfig.mountainGradient);
        skyboxMaterial.SetFloat(sunDiscSizeId, currSynthwaveConfig.sunDiscSize);
        skyboxMaterial.SetFloat(sunGradientMidPointId, currSynthwaveConfig.sunGradientMidPoint);
        skyboxMaterial.SetFloat(skyTintsSunId, currSynthwaveConfig.skyTintsSun);
        skyboxMaterial.SetFloat(horizonHeightId, currSynthwaveConfig.horizonHeight);
    }

    /// <summary>
    /// Updates the corridor width smoothly.
    /// </summary>
    private void UpdateCorridorWidth()
    {
        float width = Mathf.Lerp(audioMesh.MiddleCorridorWidth, currCorridorTargetWidth, 1.0f - Mathf.Pow(corridorSmoothness, Time.deltaTime));
        audioMesh.MiddleCorridorWidth = width;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Easing.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private float EaseOutSine(float t)
    {
        return Mathf.Sin(t * (Mathf.PI / 2.0f));
    }

    #endregion

    #region Callbacks

    /// <summary>
    /// Called when a beat is detected in any of the 32 band indices.
    /// </summary>
    /// <param name="bandIndex"></param>
    private void OnBandBeat(int bandIndex)
    {
        // I'm only interested in some of the lows, this may not work with other songs. As a TODO, I
        // could also implement the 10 band standard, since 32 bands is leaving a good part without
        // much detail.
        if (bandIndex < 3 || bandIndex > 11 || beatTimer < minTimeBetweenBeats)
            return;

        float currOffset = buildingMaterial.GetFloat(windowLightenOffsetId);
        buildingMaterial.SetFloat(windowLightenOffsetId, (currOffset + 10.0f) % 512.0f);
        beatTimer = 0.0f;
    }

    #endregion
}