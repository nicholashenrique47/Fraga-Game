Shader "Fraga/ForestGround_PBR_Wetness"
{
    Properties
    {
        _BaseMap("Ground Albedo (Humus)", 2D) = "white" {}
        _BaseColor("Tint Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.35
        _DetailNormalMap("Detail Normal (Micro-Relief)", 2D) = "bump" {}
        _DetailNormalScale("Detail Normal Scale", Range(0.0, 2.0)) = 0.8
        _DetailTiling("Detail Tiling", Float) = 3.4
        _RockMap("Rock Albedo (Granite)", 2D) = "gray" {}
        _RockNormalMap("Rock Normal", 2D) = "bump" {}
        _BaseSmoothness("Base Dry Smoothness", Range(0.0, 1.0)) = 0.12
        _WetSmoothness("Max Rain Wet Smoothness", Range(0.0, 1.0)) = 0.88
        _RockSlopeThreshold("Rock Slope Angle", Range(0.0, 1.0)) = 0.52
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

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
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_DetailNormalMap);
            SAMPLER(sampler_DetailNormalMap);

            TEXTURE2D(_RockMap);
            SAMPLER(sampler_RockMap);

            TEXTURE2D(_RockNormalMap);
            SAMPLER(sampler_RockNormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _DetailNormalScale;
                float _DetailTiling;
                float _BaseSmoothness;
                float _WetSmoothness;
                float _RockSlopeThreshold;
                float _PuddleDepthThreshold;
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
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldNormal = normalize(input.normalWS);
                
                // 1. Inclinação para detecção de rocha nua nas encostas íngremes
                float slope = 1.0 - saturate(dot(worldNormal, float3(0, 1, 0)));
                float rockWeight = smoothstep(_RockSlopeThreshold - 0.15, _RockSlopeThreshold + 0.1, slope);

                // 2. Amostragem de texturas do solo florestal (humus) e rocha (granito)
                half4 soilColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * half4(input.color.rgb, 1.0);
                half4 rockColor = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, input.uv * 1.5) * half4(0.85, 0.85, 0.85, 1.0);
                half4 albedo = lerp(soilColor, rockColor, rockWeight);

                // 3. Normal mapping com detalhe de micro-relevo e granito nas encostas
                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 soilNormalTS = UnpackNormalScale(nMap, _BumpScale);

                half4 detailN = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.uv * _DetailTiling);
                half3 detailNormalTS = UnpackNormalScale(detailN, _DetailNormalScale);
                soilNormalTS = normalize(float3(soilNormalTS.xy + detailNormalTS.xy, soilNormalTS.z * detailNormalTS.z));

                half4 rockN = SAMPLE_TEXTURE2D(_RockNormalMap, sampler_RockNormalMap, input.uv * 1.5);
                half3 rockNormalTS = UnpackNormalScale(rockN, 1.4);

                half3 normalTS = lerp(soilNormalTS, rockNormalTS, rockWeight);

                // 4. Efeito de chuva dinâmica (Wetness & Poças PBR nas cavidades)
                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                // input.color.a guarda a máscara de cavidade gerada pelo terreno
                float cavity = input.color.a;
                float puddleMask = saturate(cavity * wetness * 2.2);

                // A água nas poças aplana a normal para espelho
                normalTS = normalize(lerp(normalTS, float3(0, 0, 1), puddleMask * 0.90));

                // Reconstrução da normal mundial a partir da tangente
                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));

                // Escurecimento realista de solo e rocha encharcados
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * 0.72, wetness * 0.6);

                // Suavidade com espelhamento intenso nas poças d'água
                float smoothness = lerp(_BaseSmoothness, _WetSmoothness, wetness);
                smoothness = lerp(smoothness, 0.96, puddleMask);

                // 5. Iluminação Direcional e Sombras Suaves (The Forest Lighting)
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                
                // Suaviza a atenuação da sombra para evitar escuridão total no chão (mínimo 38% de iluminação difusa indireta)
                float effectiveShadow = lerp(0.38, 1.0, mainLight.shadowAttenuation);

                // Especular GGX para solo molhado e espelho d'água nas poças
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(10.0 * smoothness + 1.0);
                float specular = pow(NdotH, specPower) * smoothness * (0.05 + puddleMask * 0.35);

                // Ambiência clara e natural vinda do céu e copas das árvores
                half3 ambient = max(SampleSH(normalWS) * 1.35, half3(0.22, 0.25, 0.28));

                half3 direct = albedo.rgb * mainLight.color * (NdotL * effectiveShadow) + (specular * effectiveShadow * mainLight.color);
                half3 finalColor = direct + albedo.rgb * ambient;

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
