// Copyright (c) 2021 Cristian Qiu Félez https://github.com/CristianQiu/Unity-AudioSpectrum-Synthwave. Licensed under MIT license.
Shader "UnityLibrary/URP/SynthwaveBuilding"
{
    Properties
    {
        [HDR] _WindowColor1("Window color 1", Color) = (1.0, 1.0, 1.0, 1.0)
        [HDR] _WindowColor2("Window color 2", Color) = (1.0, 1.0, 1.0, 1.0)
        _BuildingFrontColor("Building front color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BuildingSidesColor("Building sides color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Space]
        _WindowProbLightenUp("Window lighten up probability", Range(0.0, 1.0)) = 0.3
        _WindowLightenOffset("Window lighten offset", Float) = 1.0
        _WindowSize("Window size", Vector) = (0.35, 0.0, 0.0, 0.0)
        _WindowRepeat("Window repeat", Vector) = (0.115, 0.036, 0.0, 0.0)
        _CutBottomHeight("Cut bottom windows", Float) = 4.36
        _CutTopHeight("Cut top windows", Float) = 13.2
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

            half3 _WindowColor1;
            half3 _WindowColor2;
            half3 _BuildingFrontColor;
            half3 _BuildingSidesColor;

            float _WindowProbLightenUp;
            float _WindowLightenOffset;
            float2 _WindowSize;
            float2 _WindowRepeat;
            float _CutBottomHeight;
            float _CutTopHeight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normal : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normal = IN.normal;

                return OUT;
            }

            float random(float2 xy)
            {
                return frac(sin(dot(xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // just a grid like its done in the SynthwaveGrid shader, but reversed for the windows
                float2 pos = IN.positionOS.xy / _WindowRepeat;
                float2 dist0 = frac(pos);
                float2 dist1 = 1.0 - dist0;

                float2 dist = min(dist0, dist1);
                float grid = min(dist.x, dist.y);

                // the face pointing towards (0.0, 0.0, -1.0) would require clamp,
                // is not going to be seen so ignore it, I'm interested in deleting windows from the sides
                float aaWindow = fwidth(dist0) * 3.0;
                float window = smoothstep(_WindowSize.x, _WindowSize.x + aaWindow, grid) * IN.normal.z;

                // for some god knows why reason the scale of the cubes is negative because it glitches otherwise...
                _CutTopHeight = -_CutTopHeight;
                float2 cutBottomTop = float2(IN.positionWS.y - _CutBottomHeight, pos.y - _CutTopHeight);
                cutBottomTop = max(cutBottomTop, 0.0);

                window = min(cutBottomTop.x, window);
                window = min(cutBottomTop.y, window);

                // noise to simulate windows that are not lighten up
                float2 ipos = floor(pos + _WindowLightenOffset);
                float noise = random(ipos);

                // we use two colors for windows, divide the range so that half of it has one color, and the rest other color
                float validWindowRangeStart = (1.0 - _WindowProbLightenUp);
                float windowOneColorRange = _WindowProbLightenUp * 0.5;

                half3 windowColor = lerp(_WindowColor1, _WindowColor2, step(validWindowRangeStart + windowOneColorRange, noise));

                // consider the top of the building shares the color with the sides
                half3 buildingColor = lerp(_BuildingSidesColor, _BuildingFrontColor, min(IN.normal.z * cutBottomTop.y, 1.0));

                window = step(validWindowRangeStart, noise) * window;
                half3 color = lerp(buildingColor, windowColor, window);

                return half4(color, 1.0);

            }

            ENDHLSL
        }
    }
}