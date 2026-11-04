Shader "Fraga/GalicianWater_Lit"
{
    Properties
    {
        _DeepWaterColor("Deep Water Color", Color) = (0.02, 0.08, 0.12, 0.95)
        _ShallowWaterColor("Shallow Water Color", Color) = (0.08, 0.22, 0.28, 0.75)
        _FoamColor("Foam Color", Color) = (0.85, 0.9, 0.92, 1.0)
        _WaveSpeed("Wave Speed", Float) = 0.8
        _WaveScale("Wave Scale", Float) = 2.5
        _WaveHeight("Wave Height", Float) = 0.04
        _Smoothness("Water Smoothness", Range(0.0, 1.0)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepWaterColor;
                float4 _ShallowWaterColor;
                float4 _FoamColor;
                float _WaveSpeed;
                float _WaveScale;
                float _WaveHeight;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(pos);

                // Ondulações de rio de montanha
                float wave1 = sin(_Time.y * _WaveSpeed + worldPos.x * _WaveScale + worldPos.z * (_WaveScale * 0.5));
                float wave2 = cos(_Time.y * (_WaveSpeed * 1.3) + worldPos.z * _WaveScale);
                pos.y += (wave1 + wave2) * _WaveHeight;

                output.positionCS = TransformObjectToHClip(pos);
                output.positionWS = TransformObjectToWorld(pos);
                
                // Normal ondulante
                float3 n = input.normalOS;
                n.x += wave1 * 0.2;
                n.z += wave2 * 0.2;
                output.normalWS = normalize(TransformObjectToWorldNormal(n));
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                // Fresnel (Reflexo de céu nas margens / vista em ângulo)
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 3.0);
                half4 waterCol = lerp(_DeepWaterColor, _ShallowWaterColor, fresnel);

                // Brilho solar especular nítido sobre as ondas
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), 64.0) * _Smoothness;

                half3 finalColor = waterCol.rgb + (mainLight.color * spec * 1.2);
                float alpha = saturate(waterCol.a + fresnel * 0.3);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
