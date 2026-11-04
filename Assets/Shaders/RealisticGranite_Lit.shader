Shader "Fraga/RealisticGranite_Lit"
{
    Properties
    {
        _BaseMap("Granite Texture", 2D) = "white" {}
        _BaseColor("Granite Tint", Color) = (0.75, 0.77, 0.80, 1.0)
        _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 3.0)) = 1.35
        _MossColor("Moss Tint", Color) = (0.22, 0.45, 0.16, 1.0)
        _LichenColor("Lichen Tint", Color) = (0.78, 0.82, 0.70, 1.0)
        _MossThreshold("Moss Ground Height", Range(0.0, 3.0)) = 0.9
        _Smoothness("Base Smoothness", Range(0.0, 1.0)) = 0.15
        _WetSmoothness("Rain Smoothness", Range(0.0, 1.0)) = 0.82
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

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _MossColor;
                float4 _LichenColor;
                float _BumpScale;
                float _MossThreshold;
                float _Smoothness;
                float _WetSmoothness;
            CBUFFER_END

            float _GlobalRainWetness;

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

                // 1. Textura do granito com quartzo e mica
                half4 stoneTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 2. Normal Mapping Tangent Space
                half4 nMap = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _BumpScale);

                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));

                half3 albedo = stoneTex.rgb;

                // 3. Musgo atlântico acumulado no solo e nas frestas horizontais da rocha
                float groundMoss = saturate((_MossThreshold - input.positionWS.y) * 1.8);
                float topMoss = saturate(normalWS.y * 1.6 - 0.2) * 0.65;
                float totalMoss = max(groundMoss, topMoss);

                albedo = lerp(albedo, _MossColor.rgb * albedo * 1.5, totalMoss * 0.88);

                // 4. Líquenes nas faces expostas de pedra
                float lichenNoise = frac(sin(dot(input.positionWS.xz, float2(12.9898, 78.233))) * 43758.5453);
                if (lichenNoise > 0.62 && totalMoss < 0.25)
                {
                    albedo = lerp(albedo, _LichenColor.rgb, 0.45);
                }

                // 5. Efeito de rocha molhada pela chuva galega
                float wetness = saturate(_GlobalRainWetness);
                float smoothness = lerp(_Smoothness, _WetSmoothness, wetness);
                albedo *= (1.0 - wetness * 0.20);

                // 6. Iluminação URP com Sombras + Ambiência Suave
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS);
                float3 lightColor = mainLight.color * (NdotL * mainLight.shadowAttenuation) + ambient * 1.25;

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(normalWS, halfDir)), 32.0 * smoothness) * smoothness;

                float3 finalRGB = albedo * lightColor + (mainLight.color * spec * (wetness * 1.3 + 0.08));

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
