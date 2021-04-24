// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Class to debug the frequency bands from the SpectrumBandsAnalyzer in the UI.
/// </summary>
public class SpectrumBandsDebugger : MonoBehaviour
{
    #region Public Attributes

    public float scaleBoost = 50.0f;

    #endregion

    #region Private Attributes

    [SerializeField] private SpectrumBandsAnalyzer analyzer = null;
    [SerializeField] private GameObject debugBarPrefab = null;
    [SerializeField] private RectTransform debugBarsParent = null;

    private List<RectTransform> debugBars;
    private bool disableUI = true;

    #endregion

    #region MonoBehaviour Methods

    private void Awake()
    {
        CreateDebugBars();
    }

    private void Update()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            disableUI = !disableUI;
#elif UNITY_ANDROID
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began)
            disableUI = !disableUI;
#endif
    }

    private void LateUpdate()
    {
        UpdateDebugBars();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates the debug bars in the UI.
    /// </summary>
    private void CreateDebugBars()
    {
        // create the list or delete the items already there
        if (debugBars == null)
        {
            debugBars = new List<RectTransform>(SpectrumBandsAnalyzer.NumberOfBands);
        }
        else
        {
            for (int i = debugBars.Count - 1; i <= 0; --i)
            {
                RectTransform bar = debugBars[i];
                debugBars.RemoveAt(i);
                Destroy(bar.gameObject);
            }
        }

        // and populate it again
        for (int i = 0; i < debugBars.Capacity; ++i)
            debugBars.Add(Instantiate(debugBarPrefab, debugBarsParent).transform as RectTransform);
    }

    /// <summary>
    /// Updates the bars sizes according to the current data from the analyzer.
    /// </summary>
    private void UpdateDebugBars()
    {
        NativeArray<float> means = analyzer.SmoothedMeans;
        float scaleMask = (!means.IsCreated || disableUI) ? 0.0f : 1.0f;

        for (int i = 0; i < debugBars.Count; ++i)
        {
            Vector3 scale = debugBars[i].localScale;
            scale.y = means[i] * scaleBoost * scaleMask;
            debugBars[i].localScale = scale;
        }
    }

    #endregion
}