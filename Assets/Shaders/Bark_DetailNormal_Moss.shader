Shader "Fraga/Bark_DetailNormal_Moss"
{
    Properties
    {
        _BaseMap("Bark Albedo", 2D) = "white" {}
        _BaseColor("Bark Tint", Color) = (0.28, 0.20, 0.14, 1.0)
        _BumpMap("Bark Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.4
        _MossColor("Moss Tint", Color) = (0.16, 0.32, 0.10, 1.0)
        _MossHeightCutoff("Moss Max Height (Meters)", Float) = 2.4
        _MossHeightTransition("Moss Height Transition", Range(0.1, 2.0)) = 0.8
        _MossDirection("Moss North Direction (XYZ)", Vector) = (0.0, 0.3, 0.95, 0.0)
        _Smoothness("Bark Smoothness", Range(0.0, 1.0)) = 0.15
        _MossSmoothness("Moss Smoothness", Range(0.0, 1.0)) = 0.08
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

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

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldNormal = normalize(input.normalWS);

                // Normal mapping
                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _BumpScale);

                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));

                // Moss blend based on height and compass direction (facing North / humid side)
                float3 mossDir = normalize(_MossDirection.xyz);
                float dirFactor = saturate(dot(normalWS, mossDir));
                
                // Height decay: only on the lower trunk
                float heightFactor = 1.0 - smoothstep(_MossHeightCutoff - _MossHeightTransition, _MossHeightCutoff, input.positionWS.y);
                float mossWeight = dirFactor * heightFactor * 0.85;

                // Bark Albedo
                half4 barkCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 albedo = lerp(barkCol, _MossColor, mossWeight);
                float smoothness = lerp(_Smoothness, _MossSmoothness, mossWeight);

                // Chuva e umidade nas cascas das árvores (The Forest wet bark)
                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * 0.65, wetness * 0.7);
                float barkSmoothness = lerp(smoothness, 0.68, wetness);

                // Lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                
                // Suaviza a atenuação da sombra no tronco
                float effectiveShadow = lerp(0.38, 1.0, mainLight.shadowAttenuation);

                // Especular de casca molhada sob a chuva
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(8.0 * barkSmoothness + 1.0);
                float spec = pow(NdotH, specPower) * barkSmoothness * (0.04 + wetness * 0.28);

                half3 ambient = max(SampleSH(normalWS) * 1.35, half3(0.22, 0.25, 0.28));
                half3 direct = albedo.rgb * mainLight.color * (NdotL * effectiveShadow) + (spec * effectiveShadow * mainLight.color);
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
