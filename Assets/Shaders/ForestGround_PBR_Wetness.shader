Shader "Fraga/ForestGround_PBR_Wetness"
{
    Properties
    {
        _BaseMap("Ground Albedo (Humus)", 2D) = "white" {}
        _BaseColor("Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.35
        _DetailNormalMap("Detail Normal (Micro-Relief)", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0.0, 2.0)) = 0.85
        _DetailTiling("Detail Tiling", Float) = 3.4
        _RockMap("Rock Albedo (Granite)", 2D) = "gray" {}
        _RockNormalMap("Rock Normal", 2D) = "bump" {}
        _RockTriplanarScale("Rock Triplanar Scale", Float) = 0.35
        _BaseSmoothness("Base Dry Smoothness", Range(0.0, 1.0)) = 0.14
        _WetSmoothness("Max Rain Wet Smoothness", Range(0.0, 1.0)) = 0.90
        _RockSlopeThreshold("Rock Slope Angle", Range(0.0, 1.0)) = 0.42
        _PuddleDepthThreshold("Puddle Depth Threshold", Range(0.0, 1.0)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }
        LOD 300

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

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_DetailNormalMap);  SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_RockMap);          SAMPLER(sampler_RockMap);
            TEXTURE2D(_RockNormalMap);    SAMPLER(sampler_RockNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _DetailNormalScale;
                float _DetailTiling;
                float _RockTriplanarScale;
                float _BaseSmoothness;
                float _WetSmoothness;
                float _RockSlopeThreshold;
                float _PuddleDepthThreshold;
            CBUFFER_END

            float _GlobalRainWetness;
            float _GlobalWetness;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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

            half3 AccumulateLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float smoothness, float puddleMask, float shadowFloor)
            {
                float NdotL = saturate(dot(normalWS, light.direction));
                float atten = light.distanceAttenuation * lerp(shadowFloor, 1.0, light.shadowAttenuation);
                float3 halfDir = normalize(light.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
            #if defined(SHADER_API_MOBILE)
                half spec2 = (half)(NdotH * NdotH);
                half spec4 = spec2 * spec2;
                float spec = spec4 * spec4 * smoothness * (0.06 + puddleMask * 0.42);
            #else
                float specPower = exp2(9.0 * smoothness + 1.0);
                float spec = pow(max(NdotH, 1e-4), specPower) * smoothness * (0.06 + puddleMask * 0.42);
            #endif
                return (albedo * NdotL + spec) * light.color * atten;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldNormal = normalize(input.normalWS);

                // Macro noise breakup in world-space to eliminate visible tiling
            #if defined(SHADER_API_MOBILE)
                float macroNoise = ValueNoise2D(input.positionWS.xz * 0.08);
            #else
                float macroNoise = ValueNoise2D(input.positionWS.xz * 0.08) * 0.65 + ValueNoise2D(input.positionWS.xz * 0.23) * 0.35;
            #endif
                float macroTint = lerp(0.84, 1.16, macroNoise);

                // 1. Slope detection for cliff granite exposure
                float slope = 1.0 - saturate(dot(worldNormal, float3(0, 1, 0)));
                float rockWeight = smoothstep(_RockSlopeThreshold - 0.14, _RockSlopeThreshold + 0.10, slope + (macroNoise - 0.5) * 0.08);

                // 2. Soil sampling (planar UVs; PC also blends far-scale detail)
                half4 soilNear = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 biomeTint = lerp(half3(1.0, 1.0, 1.0), input.color.rgb * 2.8, 0.55);
            #if defined(SHADER_API_MOBILE)
                half4 soilColor = soilNear * _BaseColor * half4(biomeTint * macroTint, 1.0);
            #else
                half4 soilFar  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * 0.27 + 0.37);
                half4 soilColor = lerp(soilNear, soilFar, 0.32) * _BaseColor * half4(biomeTint * macroTint, 1.0);
            #endif

                // 3. Triplanar Granite Rock sampling (dominant-axis 1-tap on Mobile, 3-tap blend on PC)
                float2 uvX = input.positionWS.zy * _RockTriplanarScale;
                float2 uvY = input.positionWS.xz * _RockTriplanarScale;
                float2 uvZ = input.positionWS.xy * _RockTriplanarScale;

            #if defined(SHADER_API_MOBILE)
                float3 absN = abs(worldNormal);
                float2 rockUV = (absN.x >= absN.y && absN.x >= absN.z) ? uvX : ((absN.z >= absN.y) ? uvZ : uvY);
                half3 rockRGB = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, rockUV).rgb * lerp(0.76, 0.96, macroNoise);
            #else
                float3 triW = pow(abs(worldNormal), 4.0);
                triW /= max(triW.x + triW.y + triW.z, 1e-4);
                half3 rockX = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, uvX).rgb;
                half3 rockY = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, uvY).rgb;
                half3 rockZ = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, uvZ).rgb;
                half3 rockRGB = (rockX * triW.x + rockY * triW.y + rockZ * triW.z) * lerp(0.76, 0.96, macroNoise);
            #endif

                half3 albedo = lerp(soilColor.rgb, rockRGB, rockWeight);

                // 4. Normal mapping
                half3 soilNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
            #if defined(SHADER_API_MOBILE)
                half3 rockNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormalMap, sampler_RockNormalMap, rockUV), 1.45);
            #else
                half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.uv * _DetailTiling), _DetailNormalScale);
                soilNormalTS = normalize(float3(soilNormalTS.xy + detailNormalTS.xy, soilNormalTS.z * detailNormalTS.z));

                half3 rNormX = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormalMap, sampler_RockNormalMap, uvX), 1.45);
                half3 rNormY = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormalMap, sampler_RockNormalMap, uvY), 1.45);
                half3 rNormZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNormalMap, sampler_RockNormalMap, uvZ), 1.45);
                half3 rockNormalTS = normalize(rNormX * triW.x + rNormY * triW.y + rNormZ * triW.z);
            #endif

                half3 normalTS = lerp(soilNormalTS, rockNormalTS, rockWeight);

                // 5. Dynamic Rain Wetness & Puddles in low-slope cavities
                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                float cavity = input.color.a;
                float flatMask = saturate(1.0 - slope * 3.5);
            #if defined(SHADER_API_MOBILE)
                float puddleNoise = macroNoise;
            #else
                float puddleNoise = ValueNoise2D(input.positionWS.xz * 0.45);
            #endif
                float puddleMask = saturate((cavity * 1.4 + puddleNoise * 0.35 - _PuddleDepthThreshold) * wetness * 2.5) * flatMask;

                normalTS = normalize(lerp(normalTS, float3(0, 0, 1), puddleMask * 0.92));

                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));

                albedo *= lerp(1.0, 0.66, wetness * 0.65);
                float smoothness = lerp(_BaseSmoothness, _WetSmoothness, wetness);
                smoothness = lerp(smoothness, 0.97, puddleMask);

                // 6. Lighting (Main Light + URP Forward/Forward+ Additional Lights + Fresnel Wet Sheen)
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half3 totalDirect = AccumulateLight(mainLight, albedo, normalWS, viewDir, smoothness, puddleMask, 0.34);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    totalDirect += AccumulateLight(addLight, albedo, normalWS, viewDir, smoothness, puddleMask, 0.15);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    totalDirect += AccumulateLight(addLight, albedo, normalWS, viewDir, smoothness, puddleMask, 0.15);
                LIGHT_LOOP_END
                #endif

                half fInv = 1.0 - saturate(dot(normalWS, viewDir));
                half f2 = fInv * fInv;
                float fresnel = f2 * f2;
                half3 ambient = max(SampleSH(normalWS) * 1.25, half3(0.18, 0.21, 0.24));
                half3 puddleSkyReflection = ambient * fresnel * (wetness * 0.25 + puddleMask * 0.85);

                half3 finalColor = totalDirect + albedo * ambient + puddleSkyReflection;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
