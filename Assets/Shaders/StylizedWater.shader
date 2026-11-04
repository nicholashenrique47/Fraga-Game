Shader "Fraga/StylizedWater_Mobile"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.1, 0.4, 0.5, 0.6)
        _DeepColor("Deep Color", Color) = (0.02, 0.08, 0.15, 0.85)
        _FoamColor("Foam Color", Color) = (0.8, 0.9, 0.95, 0.7)
        _WaveSpeed("Wave Speed", Float) = 1.5
        _WaveAmplitude("Wave Amplitude", Float) = 0.15
        _WaveFrequency("Wave Frequency", Float) = 3.0
        _RippleScale("Ripple Scale", Float) = 5.0
        _FresnelPower("Fresnel Power", Float) = 3.0
        _ReflectionColor("Reflection Color", Color) = (0.6, 0.7, 0.8, 0.3)
        _Transparency("Transparency", Range(0,1)) = 0.7
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
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }

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
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _ReflectionColor;
                float _WaveSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _RippleScale;
                float _FresnelPower;
                float _Transparency;
            CBUFFER_END

            // Hash simples para ruído procedural
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Ruído Simplex 2D simplificado
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(pos);

                // Ondulação com múltiplas oitavas de seno
                float time = _Time.y * _WaveSpeed;
                float wave1 = sin(worldPos.x * _WaveFrequency + time) * _WaveAmplitude;
                float wave2 = sin(worldPos.z * _WaveFrequency * 0.7 + time * 1.3) * _WaveAmplitude * 0.5;
                float wave3 = noise(worldPos.xz * 0.5 + time * 0.3) * _WaveAmplitude * 0.3;

                pos.y += wave1 + wave2 + wave3;

                output.positionCS = TransformObjectToHClip(pos);
                output.positionWS = TransformObjectToWorld(pos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldPos = input.positionWS;
                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);

                // Ripples (ondulações de superfície via noise)
                float time = _Time.y * _WaveSpeed;
                float ripple1 = noise(worldPos.xz * _RippleScale + time * float2(0.5, 0.3));
                float ripple2 = noise(worldPos.xz * _RippleScale * 1.5 - time * float2(0.3, 0.5));
                float ripplePattern = (ripple1 + ripple2) * 0.5;

                // Perturbar a normal com as ondulações
                float3 perturbedNormal = normalize(normal + float3(
                    (ripple1 - 0.5) * 0.3,
                    1.0,
                    (ripple2 - 0.5) * 0.3
                ));

                // Fresnel (reflexão nas margens)
                float fresnel = pow(1.0 - saturate(dot(viewDir, perturbedNormal)), _FresnelPower);

                // Mistura de cor rasa/profunda baseada na posição Y
                float depthFactor = saturate(worldPos.y * 2.0 + 0.5);
                half4 waterColor = lerp(_DeepColor, _ShallowColor, depthFactor);

                // Aplicar reflexão Fresnel
                waterColor.rgb = lerp(waterColor.rgb, _ReflectionColor.rgb, fresnel * _ReflectionColor.a);

                // Espuma nas cristas das ondas
                float foamMask = smoothstep(0.55, 0.65, ripplePattern);
                waterColor.rgb = lerp(waterColor.rgb, _FoamColor.rgb, foamMask * _FoamColor.a * 0.5);

                // Cintilação (specular simplificado)
                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(max(dot(perturbedNormal, halfDir), 0.0), 64.0);
                waterColor.rgb += spec * mainLight.color * 0.3;

                // Transparência final
                waterColor.a = _Transparency + fresnel * 0.2;

                return waterColor;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
