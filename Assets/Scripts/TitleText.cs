// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using TMPro;
using UnityEngine;

/// <summary>
/// The synthwave title text.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class TitleText : MonoBehaviour
{
    #region Private Attributes

    [Range(0.0f, 0.95f)]
    [SerializeField] private float smoothness = 0.9f;
    [SerializeField] private Vector3 shownPos = Vector3.zero;
    [SerializeField] private Vector3 hiddenPos = Vector3.zero;

    private Vector3 targetPos;
    private RectTransform rt;

    #endregion

    #region MonoBehaviour Methods

    private void Awake()
    {
        rt = transform as RectTransform;
        rt.anchoredPosition = hiddenPos;
        Hide();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdatePos(dt);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Shows the text.
    /// </summary>
    public void Show()
    {
        targetPos = shownPos;
    }

    /// <summary>
    /// Hides the text.
    /// </summary>
    public void Hide()
    {
        targetPos = hiddenPos;
    }

    /// <summary>
    /// Smoothly updates the position.
    /// </summary>
    /// <param name="dt"></param>
    private void UpdatePos(float dt)
    {
        rt.anchoredPosition = Vector3.Lerp(rt.anchoredPosition, targetPos, 1.0f - Mathf.Pow(smoothness, dt));
    }

    #endregion
}