Shader "SciFi/CameraFadeLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (0.14, 0.19, 0.25, 1)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.58
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.15
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Ghost Fade Settings)]
        _FadedAlpha("Faded Opacity", Range(0.02, 1.0)) = 0.22
        [HDR] _FresnelRimColor("Ghost Edge Glow", Color) = (0.12, 0.75, 1.0, 1.0)
        _FresnelPower("Silhouette Sharpness", Range(1.0, 6.0)) = 2.8
        _BackFaceDim("Back Wall Brightness", Range(0.1, 1.0)) = 0.55

        [Header(Near Camera Protection)]
        _NearCamRadius("Near Camera Fade Radius", Float) = 1.0

        [HideInInspector] _ObjectFadeAmount("Fade Amount", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float4 _CamCutoutOrigin;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            half4 _EmissionColor;
            half _FadedAlpha;
            half4 _FresnelRimColor;
            half _FresnelPower;
            half _BackFaceDim;
            half _NearCamRadius;
            float _ObjectFadeAmount;
        CBUFFER_END
        ENDHLSL

        // ПРОХОД 1: Z-Prepass без clip().
        // При растворении геометрия схлопывается в вершинном шейдере (ветка uniform, Early-Z сохраняется).
        Pass
        {
            Name "FastOpaqueDepthGuard"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Instance ID в фрагменте не нужен — не тратим интерполятор
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                // Объект растворяется — выкидываем все вершины в одну точку (вырожденные треугольники),
                // глубина не пишется и видна обратная сторона / внутренности
                if (_ObjectFadeAmount > 0.01)
                {
                    output.positionCS = float4(0.0, 0.0, 0.0, 0.0);
                    return output;
                }

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ПРОХОД 2: Основная отрисовка (передние + дальние стенки при растворении)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Instance ID в фрагменте не используется (нет instanced-свойств) — убран из Varyings
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                half fade = (half)saturate(_ObjectFadeAmount);
                bool isFading = fade > 0.01h;

                // Непрозрачный объект: задние грани и так режутся Z-буфером из Прохода 1,
                // это страховка
                if (!isFrontFace && !isFading)
                    discard;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Альфа считается ДО освещения, чтобы полностью прозрачные пиксели
                // (зона у камеры) не тратили время на шейдинг
                half alpha = lerp(1.0h, _FadedAlpha, fade) * tex.a;

                if (_NearCamRadius > 0.01h && _CamCutoutOrigin.w > 0.5)
                {
                    float camDist = distance(input.positionWS, _CamCutoutOrigin.xyz);
                    alpha *= (half)smoothstep(_NearCamRadius * 0.4, _NearCamRadius, camDist);
                }

                // Результат при alpha≈0 с блендингом SrcAlpha/OneMinusSrcAlpha равен фону — выходим без discard
                if (alpha < 0.004h)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                half3 N = normalize(input.normalWS) * (isFrontFace ? 1.0h : -1.0h);
                half3 L = _MainLightPosition.xyz;
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 H = normalize(L + V);

                half NdotL = saturate(dot(N, L));
                half NdotH = saturate(dot(N, H));

                // ambient + diffuse с одним умножением на albedo
                half3 ambient = lerp(unity_AmbientGround.rgb, unity_AmbientSky.rgb, N.y * 0.5h + 0.5h);
                half3 lightTerm = ambient + _MainLightColor.rgb * (NdotL * 0.85h + 0.15h);
                half3 finalColor = tex.rgb * lightTerm;

                half specPower = exp2(10.0h * _Smoothness + 1.0h);
                half specScale = _Smoothness * (0.25h + 0.75h * _Metallic);
                finalColor += _MainLightColor.rgb * (pow(NdotH, specPower) * specScale);

                finalColor += _EmissionColor.rgb;

                // Френель (pow) нужен только пока объект растворяется
                if (isFading)
                {
                    half fresnel = pow(1.0h - saturate(dot(N, V)), _FresnelPower);
                    finalColor += _FresnelRimColor.rgb * (fresnel * fade);
                }

                // Дальняя стенка колонны слегка затемняется, чтобы читался объём
                finalColor *= isFrontFace ? 1.0h : _BackFaceDim;

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}