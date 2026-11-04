Shader "Fraga/Foliage_TheForest_SSS"
{
    Properties
    {
        _BaseMap("Foliage Texture (RGBA)", 2D) = "white" {}
        _BaseColor("Base Tint", Color) = (0.28, 0.48, 0.18, 1.0)
        _TranslucencyColor("Translucency SSS Color", Color) = (0.85, 0.96, 0.28, 1.0)
        _TranslucencyPower("Translucency Power", Range(0.0, 5.0)) = 2.0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.35
        _NormalScale("Normal Scale", Range(0.0, 2.0)) = 1.15
        _BumpMap("Normal Map", 2D) = "bump" {}
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.25
        _WindBoughSpeed("Wind Bough Speed", Float) = 1.8
        _WindLeafFlutter("Wind Leaf Flutter Speed", Float) = 8.5
        _WindStrength("Wind Strength", Float) = 0.12
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="TransparentCutout" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="AlphaTest" 
            "IgnoreProjector"="True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _TranslucencyColor;
                float _TranslucencyPower;
                float _Cutoff;
                float _NormalScale;
                float _Smoothness;
                float _WindBoughSpeed;
                float _WindLeafFlutter;
                float _WindStrength;
            CBUFFER_END

            float _GlobalWindSpeed;
            float _GlobalRainWetness;
            float _GlobalWetness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS = input.positionOS.xyz;
                float3 worldPos = TransformObjectToWorld(posOS);

                // Wind aerodynamic simulation: Bough sway + micro leaf flutter
                float windSpeed = _WindBoughSpeed + _GlobalWindSpeed * 0.8;
                float boughWave = sin(_Time.y * windSpeed + worldPos.x * 0.4 + worldPos.z * 0.3);
                float flutterWave = cos(_Time.y * _WindLeafFlutter + worldPos.y * 2.0);

                float heightFactor = saturate(posOS.y * 0.25);
                float totalSway = (boughWave * 0.7 + flutterWave * 0.3) * _WindStrength * (1.0 + _GlobalWindSpeed * 0.3) * heightFactor;

                posOS.x += totalSway;
                posOS.z += totalSway * 0.6;

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = tex * _BaseColor;

                // Alpha Cutout
                clip(albedo.a - _Cutoff);

                // Normal mapping para relevo 3D nas folhas e nervuras
                float3 worldNormal = normalize(input.normalWS);
                half4 nMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(nMap, _NormalScale);
                float3 bitangent = cross(worldNormal, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(input.tangentWS.xyz, bitangent, worldNormal)));

                // Double-sided normal inversion
                if (facing < 0) normalWS = -normalWS;

                // Wetness e chuva nas folhas
                float wetness = saturate(max(_GlobalRainWetness, _GlobalWetness));
                float leafSmoothness = lerp(_Smoothness, 0.78, wetness);

                // Directional lighting
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                // N dot L standard diffuse
                float NdotL = saturate(dot(normalWS, lightDir));
                
                // Suaviza a atenuação da sombra nas folhas
                float effectiveShadow = lerp(0.40, 1.0, mainLight.shadowAttenuation);

                // Brilho ceroso / especular cuticular (Waxy cuticle specular sheen)
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(7.0 * leafSmoothness + 1.0);
                float spec = pow(NdotH, specPower) * leafSmoothness * (0.12 + wetness * 0.35);

                // Subsurface Scattering (SSS transmission against the light direction)
                float3 backLightDir = -lightDir;
                float SSS_dot = saturate(dot(viewDir, backLightDir));
                float SSS = pow(SSS_dot, _TranslucencyPower) * 1.8;
                half3 SSS_contrib = SSS * _TranslucencyColor.rgb * mainLight.color;

                // Ambiência rica e luminosa da copa das árvores
                half3 ambient = max(SampleSH(normalWS) * 1.35, half3(0.25, 0.28, 0.32));

                // Direct diffuse + Specular sheen
                half3 direct = (albedo.rgb * (NdotL * effectiveShadow) + spec * effectiveShadow) * mainLight.color + SSS_contrib * effectiveShadow;
                half3 finalColor = direct + albedo.rgb * ambient;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off

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
                float4 _TranslucencyColor;
                float _TranslucencyPower;
                float _Cutoff;
                float _NormalScale;
                float _Smoothness;
                float _WindBoughSpeed;
                float _WindLeafFlutter;
                float _WindStrength;
            CBUFFER_END

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _MainLightPosition.xyz));
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
