Shader "Fraga/FoliageSway_Instanced"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.88, 0.94, 0.84, 1)
        _TranslucencyColor("SSS Color", Color) = (0.72, 0.90, 0.25, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.35
        _SwaySpeed("Sway Speed", Float) = 2.0
        _SwayAmount("Sway Amount", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        LOD 200

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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _TranslucencyColor;
                float _Cutoff;
                float _SwaySpeed;
                float _SwayAmount;
            CBUFFER_END

            float _GlobalWindSpeed;
            float4 _FragaPlayerPos;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;
                float3 worldPre = TransformObjectToWorld(pos);

                float speed = _SwaySpeed + _GlobalWindSpeed * 0.7;
                float phase = input.color.g * 6.2831;
                float sway = sin(_Time.y * speed + worldPre.x * 0.8 + worldPre.z * 0.7 + phase) * _SwayAmount * (1.0 + _GlobalWindSpeed * 0.3);
                float heightMask = max(input.color.r, smoothstep(0.0, 0.8, pos.y));

                pos.x += sway * heightMask;
                pos.z += sway * 0.65 * heightMask;

                // Interacao fisica com as pernas do jogador (dobra o capim para fora)
                float2 toTuft = worldPre.xz - _FragaPlayerPos.xz;
                float distSq = dot(toTuft, toTuft);
                float pushMask = (1.0 - saturate(distSq / 2.56)) * heightMask;
                float2 pushDir = toTuft / max(0.08, sqrt(distSq));
                pos.x += pushDir.x * pushMask * 0.42;
                pos.z += pushDir.y * pushMask * 0.42;
                pos.y -= pushMask * 0.14;

                VertexPositionInputs vtx = GetVertexPositionInputs(pos);
                output.positionCS = vtx.positionCS;
                output.positionWS = vtx.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.fogFactor = ComputeFogFactor(vtx.positionCS.z);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                float hasCol = step(0.01, input.color.b + input.color.r);
                float ao = lerp(1.0, lerp(0.55, 1.05, input.color.b), hasCol);
                albedo.rgb *= ao;

                float3 normalWS = normalize(input.normalWS);
                if (facing < 0) normalWS = -normalWS;

                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float atten = lerp(0.38, 1.0, mainLight.shadowAttenuation);
                float sss = pow(saturate(dot(viewDir, -mainLight.direction)), 2.2) * 0.9;
                half3 direct = (albedo.rgb * NdotL + _TranslucencyColor.rgb * albedo.rgb * sss) * mainLight.color * atten;

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
                    float aNdotL = saturate(dot(normalWS, addLight.direction));
                    direct += albedo.rgb * addLight.color * (aNdotL * addLight.distanceAttenuation);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float aNdotL = saturate(dot(normalWS, addLight.direction));
                    direct += albedo.rgb * addLight.color * (aNdotL * addLight.distanceAttenuation);
                LIGHT_LOOP_END
                #endif

                half3 ambient = max(SampleSH(normalWS) * 1.2, half3(0.20, 0.24, 0.25)) * ao;
                half3 finalColor = direct + albedo.rgb * ambient;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
