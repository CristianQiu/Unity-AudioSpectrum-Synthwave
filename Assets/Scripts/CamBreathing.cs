// Copyright (c) 2021 Cristian Qiu Félez
// https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
using UnityEngine;

/// <summary>
/// Simple script to give the camera a floating / breathing behaviour using perlin noise.
/// </summary>
public class CamBreathing : MonoBehaviour
{
    #region Public Attributes

    [Header("Noise Settings")]
    public float centerCamHeight = 2.5f;
    public float posFrequency = 0.4f;
    public float posAmplitude = 0.3f;
    public float rotFrequency = 0.5f;
    public float rotAmplitudeEulers = 0.75f;

    #endregion

    #region MonoBehaviour Methods

    private void Update()
    {
        Breathe(Time.time);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Animates the position and rotation using perlin noise.
    /// </summary>
    /// <param name="time"></param>
    private void Breathe(float time)
    {
        float x = (Mathf.PerlinNoise(time * posFrequency, time * posFrequency) - 0.5f) * posAmplitude;
        float y = (Mathf.PerlinNoise((128.0f + time) * posFrequency, (128.0f + time) * posFrequency) - 0.5f) * posAmplitude;
        float z = 0.0f;

        float xRot = (Mathf.PerlinNoise((64.0f + time) * rotFrequency, (64.0f + time) * rotFrequency) - 0.5f) * rotAmplitudeEulers;
        float yRot = (Mathf.PerlinNoise((192.0f + time) * rotFrequency, (192.0f + time) * rotFrequency) - 0.5f) * rotAmplitudeEulers;
        float zRot = 0.0f;

        Vector3 pos = new Vector3(x, centerCamHeight + y, z);
        Quaternion rot = Quaternion.Euler(xRot, yRot, zRot);

        transform.localPosition = pos;
        transform.localRotation = rot;
    }

    #endregion
}