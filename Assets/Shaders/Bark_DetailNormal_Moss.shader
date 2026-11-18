Shader "Fraga/Bark_DetailNormal_Moss"
{
    Properties
    {
        _BaseMap("Bark Albedo", 2D) = "white" {}
        _BaseColor("Bark Tint", Color) = (0.72, 0.68, 0.64, 1.0)
        _BumpMap("Bark Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.45
        _MossColor("Moss Tint", Color) = (0.16, 0.34, 0.11, 1.0)
        _MossHeightCutoff("Moss Max Height (Meters)", Float) = 3.2
        _MossHeightTransition("Moss Height Transition", Range(0.1, 3.0)) = 1.1
        _MossDirection("Moss North Direction (XYZ)", Vector) = (0.0, 0.35, 0.94, 0.0)
        _Smoothness("Bark Smoothness", Range(0.0, 1.0)) = 0.14
        _MossSmoothness("Moss Smoothness", Range(0.0, 1.0)) = 0.08
        _WindSway("Trunk Wind Sway", Range(0.0, 0.5)) = 0.035
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float heightOS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _MossColor;
                float4 _MossDirection;
                float _BumpScale;
                float _MossHeightCutoff;
                float _MossHeightTransition;
                float _Smoothness;
                float _MossSmoothness;
                float _WindSway;
            CBUFFER_END

            float _GlobalWindSpeed;
            float _GlobalRainWetness;
            float _GlobalWetness;

            float3 ApplyTrunkSway(float3 posOS)
            {
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float swayMask = smoothstep(1.5, 12.0, posOS.y);
                float windSpd = 1.4 + _GlobalWindSpeed * 0.55;
                float wave = sin(_Time.y * windSpd + originWS.x * 0.35 + originWS.z * 0.25);
                posOS.x += wave * _WindSway * swayMask * (1.0 + _GlobalWindSpeed * 0.25);
                posOS.z += cos(_Time.y * windSpd * 0.8 + originWS.z * 0.35) * _WindSway * 0.6 * swayMask;
                return posOS;
            }

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

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(i + float3(0,0,0));
                float n100 = Hash31(i + float3(1,0,0));
                float n010 = Hash31(i + float3(0,1,0));
                float n110 = Hash31(i + float3(1,1,0));
                float n001 = Hash31(i + float3(0,0,1));
                float n101 = Hash31(i + float3(1,0,1));
                float n011 = Hash31(i + float3(0,1,1));
                float n111 = Hash31(i + float3(1,1,1));
                return lerp(
                    lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                    lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
                    f.z
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 swayedOS = ApplyTrunkSway(input.positionOS.xyz);
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(swayedOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.heightOS = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(vertexInputs.positionCS.z);

                return output;
            }

            half3 AccumulateBarkLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float smoothness, float wetness, float shadowFloor)
            {
                float NdotL = saturate(dot(normalWS, light.direction));
                float atten = light.distanceAttenuation * lerp(shadowFloor, 1.0, light.shadowAttenuation);
                float3 halfDir = normalize(light.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
            #if defined(SHADER_API_MOBILE)
                half spec2 = (half)(NdotH * NdotH);
                half spec4 = spec2 * spec2;
                float spec = spec4 * spec4 * smoothness * (0.04 + wetness * 0.30);
            #else
                float specPower = exp2(8.0 * smoothness + 1.0);
                float spec = pow(max(NdotH, 1e-4), specPower) * smoothness * (0.04 + wetness * 0.30);
            #endif
                return (albedo * NdotL + spec) * light.color * atten;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldNormal = normalize(input.normalWS);

                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _BumpScale);

                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));

                // Smooth organic moss in object-space height + humid North side
            #if defined(SHADER_API_MOBILE)
                float mossNoise = ValueNoise2D(input.positionWS.xz * 1.35 + input.positionWS.y * 0.5);
            #else
                float mossNoise = Noise3D(input.positionWS * 1.35) * 0.65 + Noise3D(input.positionWS * 3.8) * 0.35;
            #endif
                float3 mossDir = normalize(_MossDirection.xyz);
                float dirFactor = saturate(dot(normalWS, mossDir) * 0.8 + 0.35);
                float heightFactor = 1.0 - smoothstep(_MossHeightCutoff - _MossHeightTransition, _MossHeightCutoff, input.heightOS + (mossNoise - 0.5) * 1.1);
                float baseCollarMoss = 1.0 - smoothstep(0.15, 0.95, input.heightOS + (mossNoise - 0.5) * 0.4);
                float mossWeight = saturate(max(dirFactor * heightFactor * (0.55 + mossNoise * 0.6), baseCollarMoss * 0.85));

                half4 barkCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = lerp(barkCol.rgb, _MossColor.rgb * (0.75 + mossNoise * 0.5), mossWeight * 0.88);
                float smoothness = lerp(_Smoothness, _MossSmoothness, mossWeight);

                // Root AO darkening near ground
                float rootAO = smoothstep(-0.1, 0.7, input.heightOS);
                albedo *= lerp(0.68, 1.0, rootAO);

                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                albedo = lerp(albedo, albedo * 0.65, wetness * 0.70);
                float barkSmoothness = lerp(smoothness, 0.72, wetness);

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 totalDirect = AccumulateBarkLight(mainLight, albedo, normalWS, viewDir, barkSmoothness, wetness, 0.35);

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
                    totalDirect += AccumulateBarkLight(addLight, albedo, normalWS, viewDir, barkSmoothness, wetness, 0.15);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    totalDirect += AccumulateBarkLight(addLight, albedo, normalWS, viewDir, barkSmoothness, wetness, 0.15);
                LIGHT_LOOP_END
                #endif

                half3 ambient = max(SampleSH(normalWS) * 1.25, half3(0.18, 0.21, 0.24));
                half3 finalColor = totalDirect + albedo * ambient;
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _MossColor;
                float4 _MossDirection;
                float _BumpScale;
                float _MossHeightCutoff;
                float _MossHeightTransition;
                float _Smoothness;
                float _MossSmoothness;
                float _WindSway;
            CBUFFER_END

            float _GlobalWindSpeed;

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 originWS = TransformObjectToWorld(float3(0, 0, 0));
                float swayMask = smoothstep(1.5, 12.0, posOS.y);
                float windSpd = 1.4 + _GlobalWindSpeed * 0.55;
                posOS.x += sin(_Time.y * windSpd + originWS.x * 0.35 + originWS.z * 0.25) * _WindSway * swayMask * (1.0 + _GlobalWindSpeed * 0.25);

                float3 posWS = TransformObjectToWorld(posOS);
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
