Shader "Simulation/FullScreen Water"
{
    Properties
    {
        _UnderwaterColor ("Underwater Color", Color) = (0.07775717, 0.43598196, 0.6150943, 1)
        _FogNear ("Fog Start (m)", Float) = 0
        _FogFar ("Fog End (m)", Float) = 20
        _TintStrength ("Tint Strength", Range(0, 1)) = 1
        _YTreshold ("Y Threshold", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "FullScreenWater"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _UnderwaterColor;
            float _FogNear;
            float _FogFar;
            float _TintStrength;
            float _YTreshold;

            float GetRawEyeDepth(float rawDepth)
            {
                if (unity_OrthoParams.w == 0)
                    return LinearEyeDepth(rawDepth, _ZBufferParams);

                return LinearDepthToEyeDepth(rawDepth);
            }

            float GetDeviceDepth(float rawDepth)
            {
            #if UNITY_REVERSED_Z
                return rawDepth;
            #else
                return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                float cameraUnderwater = _WorldSpaceCameraPos.y < _YTreshold ? 1.0 : 0.0;

                float depthMeters = GetRawEyeDepth(rawDepth);
                float hasGeometry;
            #if UNITY_REVERSED_Z
                hasGeometry = rawDepth > 0.0001 ? 1.0 : 0.0;
            #else
                hasGeometry = rawDepth < 0.9999 ? 1.0 : 0.0;
            #endif
                // Sky/background has no depth hit, so treat it as the camera far plane distance.
                depthMeters = lerp(_ProjectionParams.z, depthMeters, hasGeometry);

                float fogSpanMeters = max(_FogFar - _FogNear, 0.001);
                float fogDistanceMeters = max(depthMeters - _FogNear, 0.0);
                // Exp fog that reaches ~98% by Fog End.
                float fogDensity = 4.0 / fogSpanMeters;
                float fogAmount = 1.0 - exp(-fogDistanceMeters * fogDensity);
                fogAmount = saturate(fogAmount * saturate(_TintStrength) * cameraUnderwater);

                half3 finalColor = lerp(sceneColor.rgb, _UnderwaterColor.rgb, fogAmount);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
