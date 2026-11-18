Shader "Fraga/Foliage_TheForest_SSS"
{
    Properties
    {
        _BaseMap("Foliage Texture (RGBA)", 2D) = "white" {}
        _BaseColor("Base Tint", Color) = (0.85, 0.92, 0.80, 1.0)
        _TranslucencyColor("Translucency SSS Color", Color) = (0.78, 0.94, 0.24, 1.0)
        _TranslucencyPower("Translucency Power", Range(0.0, 5.0)) = 2.2
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.35
        _NormalScale("Normal Scale", Range(0.0, 2.0)) = 1.15
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.24
        _WindBoughSpeed("Wind Bough Speed", Float) = 1.8
        _WindLeafFlutter("Wind Leaf Flutter Speed", Float) = 8.5
        _WindStrength("Wind Strength", Float) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

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
                float4 _TranslucencyColor;
                float _TranslucencyPower;
                float _Cutoff;
                float _NormalScale;
                float _Smoothness;
                float _WindBoughSpeed;
                float _WindLeafFlutter;
                float _WindStrength;
            CBUFFER_END

            float _GlobalWindSpeed;
            float _GlobalRainWetness;
            float _GlobalWetness;
            float4 _FragaPlayerPos;

            float3 ApplyFoliageWind(float3 posOS, float4 vColor)
            {
                float3 worldPos = TransformObjectToWorld(posOS);
                float windSpeed = _WindBoughSpeed + _GlobalWindSpeed * 0.8;
                float phase = vColor.g * 6.2831;
                float boughWave = sin(_Time.y * windSpeed + worldPos.x * 0.4 + worldPos.z * 0.3);
                float flutterWave = cos(_Time.y * _WindLeafFlutter + worldPos.y * 2.0 + phase);

                // If vertex R carries wind weight, use it; fallback to object height for uncolored meshes
                float weight = max(vColor.r, saturate(posOS.y * 0.25));
                float totalSway = (boughWave * 0.7 + flutterWave * 0.3) * _WindStrength * (1.0 + _GlobalWindSpeed * 0.35) * weight;

                posOS.x += totalSway;
                posOS.z += totalSway * 0.65;
                posOS.y += flutterWave * _WindStrength * 0.18 * weight;

                // Afastamento interativo quando o jogador passa por relva/arbustos baixos
                float2 toLeaf = worldPos.xz - _FragaPlayerPos.xz;
                float distSq = dot(toLeaf, toLeaf);
                float yMask = saturate(1.0 - abs(worldPos.y - _FragaPlayerPos.y) * 0.45);
                float pushMask = (1.0 - saturate(distSq / 2.56)) * weight * yMask;
                float2 pushDir = toLeaf / max(0.08, sqrt(distSq));
                posOS.x += pushDir.x * pushMask * 0.36;
                posOS.z += pushDir.y * pushMask * 0.36;
                posOS.y -= pushMask * 0.10;
                return posOS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = ApplyFoliageWind(input.positionOS.xyz, input.color);
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(posOS);
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

            half3 AccumulateFoliageLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float leafSmoothness, float wetness, float aoFactor, float shadowFloor)
            {
                float3 lightDir = normalize(light.direction);
                float NdotL = saturate(dot(normalWS, lightDir));
                float atten = light.distanceAttenuation * lerp(shadowFloor, 1.0, light.shadowAttenuation);

                float SSS_dot = saturate(dot(viewDir, -lightDir));
            #if defined(SHADER_API_MOBILE)
                float spec = 0.0;
                float SSS = (SSS_dot * SSS_dot) * 1.45 * (0.55 + 0.45 * aoFactor);
            #else
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(7.0 * leafSmoothness + 1.0);
                float spec = pow(max(NdotH, 1e-4), specPower) * leafSmoothness * (0.10 + wetness * 0.35);
                float SSS = pow(max(SSS_dot, 1e-4), _TranslucencyPower) * 1.45 * (0.55 + 0.45 * aoFactor);
            #endif
                half3 SSS_contrib = SSS * _TranslucencyColor.rgb * albedo * 1.8;

                return (albedo * NdotL + spec + SSS_contrib) * light.color * atten;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = tex * _BaseColor;

                clip(albedo.a - _Cutoff);

                // Baked vertex color AO (B channel, fallback 1.0 if uncolored) + subtle leaf hue variation (G)
                float hasVertexColor = step(0.01, input.color.b + input.color.r);
                float aoFactor = lerp(1.0, lerp(0.45, 1.05, input.color.b), hasVertexColor);
                float tintShift = lerp(1.0, lerp(0.88, 1.12, input.color.g), hasVertexColor);
                albedo.rgb *= aoFactor * tintShift;

                float3 worldNormal = normalize(input.normalWS);
            #if defined(SHADER_API_MOBILE)
                float3 normalWS = worldNormal;
            #else
                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _NormalScale);
                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));
            #endif

                if (facing < 0) normalWS = -normalWS;

                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                float leafSmoothness = lerp(_Smoothness, 0.76, wetness);
                albedo.rgb *= lerp(1.0, 0.80, wetness * 0.5);

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 totalDirect = AccumulateFoliageLight(mainLight, albedo.rgb, normalWS, viewDir, leafSmoothness, wetness, aoFactor, 0.36);

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
                    totalDirect += AccumulateFoliageLight(addLight, albedo.rgb, normalWS, viewDir, leafSmoothness, wetness, aoFactor, 0.20);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    totalDirect += AccumulateFoliageLight(addLight, albedo.rgb, normalWS, viewDir, leafSmoothness, wetness, aoFactor, 0.20);
                LIGHT_LOOP_END
                #endif

                half3 ambient = max(SampleSH(normalWS) * 1.25, half3(0.20, 0.24, 0.26)) * aoFactor;
                half3 finalColor = totalDirect + albedo.rgb * ambient;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off

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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _TranslucencyColor;
                float _TranslucencyPower;
                float _Cutoff;
                float _NormalScale;
                float _Smoothness;
                float _WindBoughSpeed;
                float _WindLeafFlutter;
                float _WindStrength;
            CBUFFER_END

            float _GlobalWindSpeed;

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 worldPre = TransformObjectToWorld(posOS);
                float windSpeed = _WindBoughSpeed + _GlobalWindSpeed * 0.8;
                float boughWave = sin(_Time.y * windSpeed + worldPre.x * 0.4 + worldPre.z * 0.3);
                float flutterWave = cos(_Time.y * _WindLeafFlutter + worldPre.y * 2.0 + input.color.g * 6.2831);
                float weight = max(input.color.r, saturate(posOS.y * 0.25));
                float totalSway = (boughWave * 0.7 + flutterWave * 0.3) * _WindStrength * (1.0 + _GlobalWindSpeed * 0.35) * weight;
                posOS.x += totalSway;
                posOS.z += totalSway * 0.65;

                float3 posWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
