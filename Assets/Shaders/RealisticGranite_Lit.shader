Shader "Fraga/RealisticGranite_Lit"
{
    Properties
    {
        _BaseMap("Granite Texture", 2D) = "white" {}
        _BaseColor("Granite Tint", Color) = (0.86, 0.87, 0.89, 1.0)
        _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.45
        _MossColor("Moss Tint", Color) = (0.16, 0.34, 0.11, 1.0)
        _LichenColor("Lichen Tint", Color) = (0.70, 0.76, 0.62, 1.0)
        _MossThreshold("Moss Ground Height", Range(0.0, 3.0)) = 0.75
        _Smoothness("Base Smoothness", Range(0.0, 1.0)) = 0.16
        _WetSmoothness("Rain Smoothness", Range(0.0, 1.0)) = 0.84
        _TriplanarBlend("Triplanar Detail Weight", Range(0.0, 1.0)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 250

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

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _MossColor;
                float4 _LichenColor;
                float _BumpScale;
                float _MossThreshold;
                float _Smoothness;
                float _WetSmoothness;
                float _TriplanarBlend;
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

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
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

            half3 AccumulateStoneLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float smoothness, float wetness, float shadowFloor)
            {
                float NdotL = saturate(dot(normalWS, light.direction));
                float atten = light.distanceAttenuation * lerp(shadowFloor, 1.0, light.shadowAttenuation);
                float3 halfDir = normalize(light.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
            #if defined(SHADER_API_MOBILE)
                half spec2 = (half)(NdotH * NdotH);
                half spec4 = spec2 * spec2;
                float spec = spec4 * spec4 * smoothness * (0.06 + wetness * 0.45);
            #else
                float specPower = exp2(8.5 * smoothness + 1.0);
                float spec = pow(max(NdotH, 1e-4), specPower) * smoothness * (0.06 + wetness * 0.45);
            #endif
                return (albedo * NdotL + spec) * light.color * atten;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 geoNormal = normalize(input.normalWS);

                // 1. Blend UV sample with world-space triplanar detail
                half3 uvTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float triScale = 0.48;
            #if defined(SHADER_API_MOBILE)
                float3 absN = abs(geoNormal);
                float2 triUV = (absN.x >= absN.y && absN.x >= absN.z)
                    ? input.positionWS.zy * triScale
                    : ((absN.z >= absN.y) ? input.positionWS.xy * triScale : input.positionWS.xz * triScale);
                half3 triTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV).rgb;
            #else
                float3 triW = pow(abs(geoNormal), 4.0);
                triW /= max(triW.x + triW.y + triW.z, 1e-4);
                half3 triTex =
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.positionWS.zy * triScale).rgb * triW.x +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.positionWS.xz * triScale).rgb * triW.y +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.positionWS.xy * triScale).rgb * triW.z;
            #endif

                half3 stoneTex = lerp(uvTex, triTex, _TriplanarBlend) * _BaseColor.rgb;

                // 2. Normal Mapping (UV + triplanar blend)
                half3 uvNorm = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _BumpScale);
            #if defined(SHADER_API_MOBILE)
                half3 triNorm = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, triUV), _BumpScale);
            #else
                half3 triNorm =
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.positionWS.zy * triScale), _BumpScale) * triW.x +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.positionWS.xz * triScale), _BumpScale) * triW.y +
                    UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.positionWS.xy * triScale), _BumpScale) * triW.z;
            #endif
                half3 normalTS = normalize(lerp(uvNorm, triNorm, _TriplanarBlend));

                half3 bitangent = cross(geoNormal, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, geoNormal)));

                half3 albedo = stoneTex;

                // 3. Smooth noise for Atlantic moss & crustose lichen
            #if defined(SHADER_API_MOBILE)
                float2 ws2D = input.positionWS.xz + input.positionWS.y * 0.37;
                float mossNoise = ValueNoise2D(ws2D * 1.45);
                float lichenMacro = ValueNoise2D(ws2D * 2.3 + 17.3);
                float lichenEdge = mossNoise;
            #else
                float mossNoise = Noise3D(input.positionWS * 1.45) * 0.65 + Noise3D(input.positionWS * 4.2) * 0.35;
                float lichenMacro = Noise3D(input.positionWS * 2.1 + 17.3);
                float lichenEdge = Noise3D(input.positionWS * 7.5 + 41.2);
            #endif
                float groundMoss = saturate((_MossThreshold - input.heightOS + (mossNoise - 0.5) * 0.7) * 1.4);
                float topMoss = smoothstep(0.32, 0.82, normalWS.y + (mossNoise - 0.5) * 0.45) * 0.78;
                float totalMoss = saturate(max(groundMoss * 0.85, topMoss));

                half3 mossAlbedo = _MossColor.rgb * (0.72 + mossNoise * 0.65);
                albedo = lerp(albedo, mossAlbedo, totalMoss * 0.88);

                float lichenMask = smoothstep(0.60, 0.74, lichenMacro * 0.72 + lichenEdge * 0.28) * (1.0 - totalMoss);
                albedo = lerp(albedo, _LichenColor.rgb * (0.85 + lichenEdge * 0.3), lichenMask * 0.52);

                // Ground contact AO
                albedo *= lerp(0.70, 1.0, smoothstep(-0.05, 0.45, input.heightOS));

                // 4. Galician rain wetness
                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                float smoothness = lerp(_Smoothness, _WetSmoothness, wetness);
                albedo *= (1.0 - wetness * 0.22);

                // 5. Main light + URP Additional Lights + Fog
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 totalDirect = AccumulateStoneLight(mainLight, albedo, normalWS, viewDir, smoothness, wetness, 0.30);

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
                    totalDirect += AccumulateStoneLight(addLight, albedo, normalWS, viewDir, smoothness, wetness, 0.15);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    totalDirect += AccumulateStoneLight(addLight, albedo, normalWS, viewDir, smoothness, wetness, 0.15);
                LIGHT_LOOP_END
                #endif

                half3 ambient = max(SampleSH(normalWS) * 1.25, half3(0.18, 0.21, 0.24));
                half3 finalRGB = totalDirect + albedo * ambient;
                finalRGB = MixFog(finalRGB, input.fogFactor);

                return half4(finalRGB, 1.0);
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
