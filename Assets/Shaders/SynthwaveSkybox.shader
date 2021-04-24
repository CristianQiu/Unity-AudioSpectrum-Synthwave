// Copyright (c) 2021 Cristian Qiu Félez https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
Shader "UnityLibrary/URP/SynthwaveSkybox"
{
    Properties
    {
        _SunDiscSize("Sun disc size", Range(0.0, 1.0)) = 0.265
        _SunAntialiasing("Sun blur", Range(0.1, 4.0)) = 3.0
        _SunStripeHeights("Sun stripe positions", Vector) = (-0.26, -0.44, -0.63, -0.82)
        _SunStripeWidths("Sun stripe widths", Vector) = (0.03, 0.04, 0.05, 0.06)
        [Space]
        [HDR]_SunBottomColor("Sun bottom color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR]_SunMidColor("Sun mid color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR]_SunTopColor("Sun top color", Color) = (1.0, 1.0, 1.0, 1.0)
        _SunGradientMidpoint("Sun gradient midpoint", Range(0.0, 1.0)) = 0.375
        [Space]
        _SkyTintsSun("Sky tints sun", Range(0.0, 1.0)) = 0.4
        _HorizonHeight("Horizon height", Range(0.0, 1.0)) = 1.0
        [HDR]_NadirColor("Nadir color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR]_HorizonColor("Horizon color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR]_ZenithColor("Zenith color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Space]
        _SkyStripesWeight("Sky retro stripes weight", Range(0.0, 1.0)) = 0.2
        _SkyStripesWidth("Sky retro stripes width", Range(0.001, 0.005)) = 0.003
        _SkyStripesRepetition("Sky retro stripes repetition", Range(0.0075, 0.1)) = 0.0075
        [Space]
        [HDR]_FlareColor("Flare color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FlarePosition("Flare position", Range(-1.0, 1.0)) = -0.0175
        _FlareWidth("Flare width", Range(0.0, 0.01)) = 0.0075
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Cull Off 
            ZWrite Off
            ZTest Less

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _SunDiscSize;
            float _SunAntialiasing;
            float4 _SunStripeHeights;
            float4 _SunStripeWidths;

            half3 _SunBottomColor;
            half3 _SunMidColor;
            half3 _SunTopColor;
            float _SunGradientMidpoint;

            float _SkyTintsSun;
            float _HorizonHeight;
            half3 _NadirColor;
            half3 _HorizonColor;
            half3 _ZenithColor;

            float _SkyStripesWeight;
            float _SkyStripesWidth;
            float _SkyStripesRepetition;

            half3 _FlareColor;
            float _FlarePosition;
            float _FlareWidth;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;

                return OUT;
            }

            float remap(float a, float b, float c, float d, float t)
            {
                float s = saturate((t - a) / (b - a));

                return lerp(c, d, s);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 uv = normalize(IN.uv);
                Light light = GetMainLight();
                
                // sun
                float4 stripeHeightsNorm = lerp(0.0, _SunStripeHeights, _SunDiscSize);
                float4 stripeDists = abs(uv.yyyy - stripeHeightsNorm - light.direction.yyyy);
                float4 stripeAntialias = fwidth(stripeDists) * _SunAntialiasing;
                float4 stripeWidthsNorm = lerp(0.0, _SunStripeWidths, _SunDiscSize);
                stripeDists = smoothstep(stripeWidthsNorm - stripeAntialias, stripeWidthsNorm, stripeDists);

                float d = length(uv - light.direction.xyz);
                float sunAntialias = fwidth(d) * _SunAntialiasing;
                float sun = 1.0 - smoothstep(_SunDiscSize - sunAntialias, _SunDiscSize, d);
                sun *= stripeDists.x * stripeDists.y * stripeDists.z * stripeDists.w;

                // [0, 1] sun bottom to sun top
                float tSun = (((uv.y - light.direction.y) / _SunDiscSize) + 1.0) * 0.5;
                tSun = saturate(tSun);

                float tSunBotMid = remap(0.0, _SunGradientMidpoint, 0.0, 1.0, tSun);
                half3 sunBottomMidColor = lerp(_SunBottomColor, _SunMidColor, tSunBotMid);
                float tSunMidTop = remap(_SunGradientMidpoint, 1.0, 0.0, 1.0, tSun);
                half3 sunMidTopColor = lerp(_SunMidColor, _SunTopColor, tSunMidTop);

                half3 sunColor = lerp(sunBottomMidColor, sunMidTopColor, step(_SunGradientMidpoint, tSun));

                // [0, 1] sky nadir to sky zenith
                float sky = 1.0 - sun;
                float yPos01 = (uv.y + 1.0) * 0.5;

                float tSkyNadirHorizon = remap(0.0, _HorizonHeight, 0.0, 1.0, yPos01);
                half3 nadirHorizonColor = lerp(_NadirColor, _HorizonColor, tSkyNadirHorizon);
                float tSkyHorizonZenith = remap(_HorizonHeight, 1.0, 0.0, 1.0, yPos01);
                half3 horizonZenithColor = lerp(_HorizonColor, _ZenithColor, tSkyHorizonZenith);

                half3 skyColor = lerp(nadirHorizonColor, horizonZenithColor, smoothstep(0.0, 1.0, yPos01));

                // sky retro stripes
                float skyStripes = fmod(yPos01, _SkyStripesRepetition);
                skyStripes = step(_SkyStripesWidth, skyStripes);
                skyStripes = 1.0 - skyStripes;
                skyColor = lerp(skyColor, skyColor * (1.0 - _SkyStripesWeight), skyStripes);

                // static horizon flare line, which is slightly thickened towards the sides of the view
                float yAbs = abs(uv.y - _FlarePosition);

                const float flareFalloff = 0.0035;
                float flareModifier = smoothstep(0.0, 1.0, abs(uv.x)) + 1.0;
                float flare = smoothstep(flareModifier * _FlareWidth - flareFalloff, flareModifier * _FlareWidth, yAbs);
                flare = (1.0 - flare);

                // make the sun be tinted by the sky behind
                sunColor = lerp(sunColor, sunColor * skyColor, smoothstep(0.0, 1.0, _SkyTintsSun));

                return half4((sun * sunColor) + (sky * skyColor) + (flare * _FlareColor), 1.0);
            }

            ENDHLSL
        }
    }
}