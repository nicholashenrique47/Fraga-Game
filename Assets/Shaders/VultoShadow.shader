Shader "Fraga/VultoShadow_Instanced"
{
    Properties
    {
        _BaseColor("Shadow Color", Color) = (0.03, 0.02, 0.04, 0.90)
        _RimColor("Shadow Mist Rim Color", Color) = (0.18, 0.14, 0.26, 0.85)
        _EyeColor("Glowing Eye Color", Color) = (1.8, 0.12, 0.06, 1.0)
        _JitterSpeed("Jitter Speed", Float) = 9.0
        _JitterAmount("Jitter Amount", Float) = 0.025
        _WispSpeed("Wisp Flow Speed", Float) = 1.8
        _FresnelPower("Fresnel Power", Float) = 2.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float3 posOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float4 _EyeColor;
                float _JitterSpeed;
                float _JitterAmount;
                float _WispSpeed;
                float _FresnelPower;
            CBUFFER_END

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;

                // Keep eyes rock-steady while undulating tattered shadow body
                bool isEye = (input.uv.x > 0.85 && input.uv.y > 0.85);
                if (!isEye)
                {
                    float t = _Time.y * _WispSpeed;
                    float sway = sin(t + pos.y * 2.8 + pos.z * 3.1) * cos(t * 0.7 + pos.x * 2.4);
                    float lowerFactor = saturate(1.2 - pos.y * 0.55);

                    float timeVar = floor(_Time.y * _JitterSpeed);
                    float jitter = (hash12(float2(pos.y * 7.1 + timeVar, pos.x * 5.3)) * 2.0 - 1.0) * _JitterAmount;

                    pos.x += sway * 0.045 * lowerFactor + jitter * 0.6;
                    pos.z += cos(t * 1.1 + pos.y * 2.3) * 0.035 * lowerFactor + jitter * 0.6;
                }

                VertexPositionInputs posInputs = GetVertexPositionInputs(pos);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.posOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // UV island reserved for eyes inside the hood (single-submesh instanced glow)
                if (input.uv.x > 0.85 && input.uv.y > 0.85)
                {
                    half4 eyeCol = half4(_EyeColor.rgb, 1.0);
                    eyeCol.rgb = MixFog(eyeCol.rgb, input.fogFactor);
                    return eyeCol;
                }

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndotv = saturate(dot(N, V));
                float fresnel = pow(1.0 - ndotv, _FresnelPower);

                // Smooth swirling shadow wisp modulation
                float wispNoise = sin(input.posOS.y * 6.5 - _Time.y * _WispSpeed * 1.4 + input.posOS.x * 5.0) * 0.5 + 0.5;
                float groundFade = smoothstep(0.0, 0.35, input.posOS.y);
                float hoodCore = smoothstep(0.5, 1.85, input.posOS.y);

                half3 col = lerp(_BaseColor.rgb, _RimColor.rgb, fresnel * (0.45 + 0.55 * wispNoise));
                float alpha = _BaseColor.a * lerp(0.55, 1.0, groundFade) * lerp(0.75, 1.0, ndotv * 0.5 + hoodCore * 0.5);
                alpha = saturate(alpha + fresnel * 0.18 * wispNoise);

                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
