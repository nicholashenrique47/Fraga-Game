Shader "Fraga/FoliageSway_Instanced"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _SwaySpeed("Sway Speed", Float) = 2.0
        _SwayAmount("Sway Amount", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _SwaySpeed;
                float _SwayAmount;
            CBUFFER_END
            
            float _GlobalWindSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                
                float3 worldPos = TransformObjectToWorld(pos);
                
                float speed = _SwaySpeed + _GlobalWindSpeed;
                float sway = sin(_Time.y * speed + worldPos.x + worldPos.z) * _SwayAmount * (1.0 + _GlobalWindSpeed * 0.2);
                
                float heightMask = smoothstep(0.0, 1.0, pos.y);
                pos.x += sway * heightMask;
                pos.z += sway * heightMask;

                output.positionCS = TransformObjectToHClip(pos);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 finalColor = albedoAlpha * _BaseColor;
                
                clip(finalColor.a - _Cutoff);
                // Alternativa para texturas que nÇœo tem alpha perfeito mas sim pixeis pretos
                // clip((finalColor.r + finalColor.g + finalColor.b)/3.0 - 0.1); 
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}