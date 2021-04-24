// Copyright (c) 2021 Cristian Qiu Félez https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
Shader "UnityLibrary/URP/SynthwaveGrid"
{
    Properties
    {
        [HDR] _GridColor("Grid color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _GridSweepLineColor("Grid sweep line color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FloorColor("Floor color", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR]_MountainColor("Mountain top color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Space]
        _InvGridSize("Grid size", Range(0.25, 10.0)) = 2.0
        _LineWidth("Line width", Range(0.25, 1.0)) = 0.4
        _GridHeightFaded("Grid faded at height", Range(0.0, 10.0)) = 0.75
        _GridSweepLineSpeed("Grid sweep line speed", Range(0.0, 100.0)) = 30.0
        _GridSweepLineWidth("Grid sweep line width", Range(0.5, 5.0)) = 2.0
        _GridSweepLineMaxDist("Grid sweep line max distance", Range(25.0, 500.0)) = 250.0
        _MountainHeightPeak("Mountain gradient", Range(0.0, 1.0)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalRenderPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half3 _GridColor;
            half3 _GridSweepLineColor;
            half3 _FloorColor;
            half3 _MountainColor;

            float _InvGridSize;
            float _LineWidth;
            float _GridHeightFaded;
            float _GridSweepLineSpeed;
            float _GridSweepLineWidth;
            float _GridSweepLineMaxDist;
            float _MountainHeightPeak;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // move the grid
                float3 osPos = IN.positionOS;
                float z = osPos.z;
                osPos.z += _Time.y;

                // calculate the grid
                float2 dist0 = frac(osPos.xz / _InvGridSize);
                float2 dist1 = 1.0 - dist0;
                float2 grid = min(dist0, dist1);

                float2 antialias = fwidth(osPos.xz);
                grid = smoothstep(0.0, _LineWidth * antialias, grid);

                // grid sweep line
                float tz = fmod(_Time.y * _GridSweepLineSpeed, _GridSweepLineMaxDist + _GridSweepLineWidth) - _GridSweepLineWidth;
                float sweepLine = abs(tz - z);

                half3 finalGridColor = lerp(_GridSweepLineColor, _GridColor, step(_GridSweepLineWidth, sweepLine));

                // make the grid be faded at a certain height and color floor and mountains where there's no grid
                float gridHeightFade = smoothstep(0.0, _GridHeightFaded, osPos.y);
                float gridIntensity = (1.0 - min(grid.x, grid.y)) * (1.0 - gridHeightFade);

                float tMountain = lerp(0.0, _MountainHeightPeak, sign(osPos.y) * pow(osPos.y, 2.0));
                tMountain = saturate(tMountain);

                half3 mountainOrFloorColor = lerp(_FloorColor, _MountainColor, tMountain);
                half3 color = lerp(mountainOrFloorColor, finalGridColor, gridIntensity);

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }
}