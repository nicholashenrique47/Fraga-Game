Shader "Fraga/VultoShadow_Instanced"
{
    Properties
    {
        _BaseColor("Shadow Color", Color) = (0, 0, 0, 0.8)
        _JitterSpeed("Jitter Speed", Float) = 15.0
        _JitterAmount("Jitter Amount", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _JitterSpeed;
                float _JitterAmount;
            CBUFFER_END

            // Random number generator simples no shader
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                
                // Jitter (Tremor constante não-físico que causa desconforto visual)
                float timeVar = _Time.y * _JitterSpeed;
                float noiseX = rand(float2(pos.x + timeVar, pos.y)) * 2.0 - 1.0;
                float noiseZ = rand(float2(pos.z + timeVar, pos.y)) * 2.0 - 1.0;
                
                pos.x += noiseX * _JitterAmount;
                pos.z += noiseZ * _JitterAmount;

                output.positionCS = TransformObjectToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _BaseColor; // Retorna cor escura com transparência
            }
            ENDHLSL
        }
    }
}
