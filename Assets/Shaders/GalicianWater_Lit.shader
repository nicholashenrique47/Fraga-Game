Shader "Fraga/GalicianWater_Lit"
{
    Properties
    {
        _DeepWaterColor("Deep Water Color", Color) = (0.02, 0.08, 0.12, 0.94)
        _ShallowWaterColor("Shallow Water Color", Color) = (0.08, 0.24, 0.28, 0.72)
        _FoamColor("Foam Color", Color) = (0.85, 0.92, 0.94, 1.0)
        _WaveSpeed("Wave Speed", Float) = 0.85
        _WaveScale("Wave Scale", Float) = 2.4
        _WaveHeight("Wave Height", Float) = 0.035
        _Smoothness("Water Smoothness", Range(0.0, 1.0)) = 0.94
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 250

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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

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
                float fogFactor : TEXCOORD3;
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

            float _GlobalRainWetness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(pos);

                float wave1 = sin(_Time.y * _WaveSpeed + worldPos.x * _WaveScale + worldPos.z * (_WaveScale * 0.5));
                float wave2 = cos(_Time.y * (_WaveSpeed * 1.3) + worldPos.z * _WaveScale);
                pos.y += (wave1 + wave2) * _WaveHeight;

                VertexPositionInputs vtx = GetVertexPositionInputs(pos);
                output.positionCS = vtx.positionCS;
                output.positionWS = vtx.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(vtx.positionCS.z);

                return output;
            }

            float3 ComputeWaterRippleNormal(float3 worldPos, float3 baseNormal)
            {
                float t = _Time.y * _WaveSpeed;
                float s1 = _WaveScale * 1.6;
                float s2 = _WaveScale * 3.1;
                float s3 = _WaveScale * 5.7;

                float dx = cos(worldPos.x * s1 + worldPos.z * 0.9 + t) * 0.06
                         + sin(worldPos.x * s2 - worldPos.z * 1.8 - t * 1.3) * 0.04
                         + cos(worldPos.x * s3 + worldPos.z * 4.2 + t * 2.1) * 0.025;

                float dz = sin(worldPos.z * s1 - worldPos.x * 0.7 + t * 1.1) * 0.06
                         + cos(worldPos.z * s2 + worldPos.x * 2.1 - t * 1.5) * 0.04
                         + sin(worldPos.z * s3 - worldPos.x * 3.9 + t * 2.3) * 0.025;

                // Subtle raindrop ripple boost during storms
                float rainBoost = 1.0 + saturate(_GlobalRainWetness) * 0.9;
                return normalize(baseNormal + float3(-dx * rainBoost, 0.0, -dz * rainBoost));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 normal = ComputeWaterRippleNormal(input.positionWS, normalize(input.normalWS));
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                // Fresnel sky & environment reflection
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 3.5);
                half4 waterCol = lerp(_DeepWaterColor, _ShallowWaterColor, saturate(fresnel * 1.2));

                half3 skyReflect = max(SampleSH(reflect(-viewDir, normal)) * 1.15, half3(0.14, 0.20, 0.26));
                half3 litWater = lerp(waterCol.rgb, skyReflect, saturate(fresnel * 0.72));

                // Specular highlight from sun/moon
                float3 halfDir = normalize(lightDir + viewDir);
                float spec = pow(saturate(dot(normal, halfDir)), 128.0) * _Smoothness * 1.6;
                half3 specularAccum = mainLight.color * spec;

                // Additional Lights (campfires, torches, Farol Celta shimmering on water!)
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
                    float3 addHalf = normalize(addLight.direction + viewDir);
                    float addSpec = pow(saturate(dot(normal, addHalf)), 96.0) * _Smoothness;
                    specularAccum += addLight.color * addLight.distanceAttenuation * (addSpec + 0.08 * saturate(dot(normal, addLight.direction)));
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float3 addHalf = normalize(addLight.direction + viewDir);
                    float addSpec = pow(saturate(dot(normal, addHalf)), 96.0) * _Smoothness;
                    specularAccum += addLight.color * addLight.distanceAttenuation * (addSpec + 0.08 * saturate(dot(normal, addLight.direction)));
                LIGHT_LOOP_END
                #endif

                // Subtle shore/edge foam shimmer near UV rim
                float edgeDist = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y, 1.0 - input.uv.y));
                float foamWave = sin(_Time.y * 2.2 + input.positionWS.x * 3.0 + input.positionWS.z * 3.0) * 0.5 + 0.5;
                float shoreFoam = smoothstep(0.10, 0.01, edgeDist) * (0.45 + 0.55 * foamWave);

                half3 finalColor = litWater + specularAccum;
                finalColor = lerp(finalColor, _FoamColor.rgb * (mainLight.color * 0.6 + skyReflect * 0.7), shoreFoam * 0.65);
                finalColor = MixFog(finalColor, input.fogFactor);

                float alpha = saturate(waterCol.a + fresnel * 0.28 + shoreFoam * 0.20);
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
