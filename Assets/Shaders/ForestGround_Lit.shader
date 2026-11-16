Shader "Fraga/ForestGround_Lit"
{
    Properties
    {
        _BaseMap("Ground Albedo", 2D) = "white" {}
        _BaseColor("Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.35
        _Smoothness("Base Smoothness", Range(0.0, 1.0)) = 0.15
        _WetSmoothness("Rain Wet Smoothness", Range(0.0, 1.0)) = 0.85
        _RockSlopeThreshold("Rock Slope Angle", Range(0.0, 1.0)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Smoothness;
                float _WetSmoothness;
                float _RockSlopeThreshold;
            CBUFFER_END

            float _GlobalRainWetness;
            float _GlobalWetness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.fogFactor = ComputeFogFactor(vertexInputs.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _BumpScale);

                half4 detailNMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv * 3.2);
                half3 detailTS = UnpackNormalScale(detailNMap, _BumpScale * 0.45);
                normalTS = normalize(half3(normalTS.xy + detailTS.xy, normalTS.z));

                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                half3 vertexTint = lerp(half3(1.0, 1.0, 1.0), input.color.rgb * 2.2, 0.40);
                half3 groundAlbedo = texCol.rgb * vertexTint;

                float slope = saturate(dot(input.normalWS, float3(0, 1, 0)));
                if (slope < _RockSlopeThreshold)
                {
                    float rockBlend = smoothstep(_RockSlopeThreshold, _RockSlopeThreshold - 0.25, slope);
                    half3 rockColor = half3(0.48, 0.48, 0.50) * (texCol.r * 1.3);
                    groundAlbedo = lerp(groundAlbedo, rockColor, rockBlend * 0.75);
                }

                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                float puddleMask = saturate((1.0 - normalTS.z) * 1.5 + wetness * 0.8);
                float activeWetness = max(wetness, puddleMask * wetness);

                float smoothness = lerp(_Smoothness, _WetSmoothness, activeWetness);
                groundAlbedo *= (1.0 - activeWetness * 0.20);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = max(SampleSH(normalWS) * 1.25, half3(0.18, 0.21, 0.24));
                float3 lightColor = mainLight.color * (NdotL * lerp(0.34, 1.0, mainLight.shadowAttenuation)) + ambient;

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specPower = lerp(16.0, 64.0, activeWetness);
                float spec = pow(saturate(dot(normalWS, halfDir)), specPower) * smoothness;
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 4.0) * activeWetness;

                float3 specularHighlight = mainLight.color * (spec * (activeWetness * 1.4 + 0.10) + fresnel * 0.35);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 addAccum = half3(0, 0, 0);
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float aNdotL = saturate(dot(normalWS, addLight.direction));
                    addAccum += groundAlbedo * addLight.color * (aNdotL * addLight.distanceAttenuation);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float aNdotL = saturate(dot(normalWS, addLight.direction));
                    addAccum += groundAlbedo * addLight.color * (aNdotL * addLight.distanceAttenuation);
                LIGHT_LOOP_END
                #endif

                float3 finalRGB = groundAlbedo * lightColor + specularHighlight + addAccum;
                finalRGB = MixFog(finalRGB, input.fogFactor);

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
