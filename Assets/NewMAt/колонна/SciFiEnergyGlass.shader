Shader "SciFi/EnergyGlass"
{
    Properties
    {
        _GlassTint     ("Оттенок стекла", Color) = (0.6, 0.8, 1.0, 1)
        _InnerColor    ("Подсветка изнутри (синий)", Color) = (0.1, 0.4, 1.0, 1)
        _InnerGlow     ("Сила подсветки изнутри", Range(0, 0.5)) = 0.10
        _BaseAlpha     ("Базовая непрозрачность", Range(0, 0.3)) = 0.05
        _FresnelPower  ("Fresnel: степень", Range(1, 8)) = 3.0
        _RimStrength   ("Яркость кромки", Range(0, 2)) = 0.6
        _EnvStrength   ("Отражение окружения", Range(0, 2)) = 0.5
        _Roughness     ("Шероховатость", Range(0, 1)) = 0.08
        _SpecPower     ("Размер блика", Range(10, 500)) = 220
        _SpecStrength  ("Сила блика", Range(0, 2)) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlassTint, _InnerColor;
                float _InnerGlow, _BaseAlpha, _FresnelPower, _RimStrength;
                float _EnvStrength, _Roughness, _SpecPower, _SpecStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float nv = saturate(dot(N, V));
                float fres = pow(1.0 - nv, _FresnelPower);

                // блик от главного света
                Light l = GetMainLight();
                float3 H = normalize(l.direction + V);
                half3 spec = saturate(pow(saturate(dot(N, H)), _SpecPower) * _SpecStrength) * l.color;

                // отражение окружения (reflection probe / skybox)
                half3 env = GlossyEnvironmentReflection(reflect(-V, N), _Roughness, 1.0)
                          * (0.04 + fres) * _EnvStrength;

                // слабый голубой свет «изнутри»: видно, что стекло заполнено энергией
                half3 inner = _InnerColor.rgb * _InnerGlow * pow(nv, 1.5);

                half3 rim = _GlassTint.rgb * fres * _RimStrength;

                float alpha = saturate(_BaseAlpha + fres * 0.5);
                half3 rgb = _GlassTint.rgb * _BaseAlpha * 0.5 + rim + env + spec + inner;

                return half4(saturate(rgb), alpha);
            }
            ENDHLSL
        }
    }
}
