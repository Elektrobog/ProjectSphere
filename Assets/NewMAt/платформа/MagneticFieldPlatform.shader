Shader "SciFi/MagneticFieldPlatform"
{
    Properties
    {
        [Header(Colors and Glow)]
        _DeepColor ("Глубокий цвет поля", Color) = (0.04, 0.28, 0.68, 1.0)
        _CoreColor ("Цвет энергетических нитей", Color) = (0.35, 0.88, 1.0, 1.0)
        _HotColor  ("Цвет горячего ядра (у труб)", Color) = (0.85, 0.98, 1.0, 1.0)
        _EmissionIntensity ("Яркость свечения (HDR)", Range(0.5, 8.0)) = 2.6
        _Opacity ("Общая плотность поля", Range(0.1, 2.0)) = 1.0

        [Header(Flow from Tubes to Platform)]
        _FlowSpeed ("Скорость потока от труб", Range(0.0, 5.0)) = 1.35
        _StreamCount ("Количество силовых лучей", Range(2.0, 24.0)) = 10.0
        _StreamFocus ("Фокусировка лучей от труб", Range(0.2, 4.0)) = 1.6
        _TubeAnchorBoost ("Яркость у выхода из труб", Range(0.0, 4.0)) = 2.0
        _PlatformGripBoost ("Свечение захвата платформы", Range(0.0, 4.0)) = 1.7

        [Header(Plasma Filaments)]
        _PlasmaScale ("Масштаб плазменных нитей", Range(1.0, 16.0)) = 5.5
        _PlasmaSharpness ("Чёткость прожилок", Range(1.0, 6.0)) = 2.8
        _EdgeSoftness ("Мягкость боковых краёв", Range(0.05, 0.5)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MagneticField"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0; // x: 0 (у труб колонны) -> 1 (у платформы), y: 0..1 по ширине дуги
                float4 uv1        : TEXCOORD1; // x: тип слоя (0 = мост, 1 = дуга захвата, 2 = вуаль), y: индекс слоя -1..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv0        : TEXCOORD0;
                float4 uv1        : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                half3 viewDirWS   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _CoreColor;
                half4 _HotColor;
                half _EmissionIntensity;
                half _Opacity;
                half _FlowSpeed;
                half _StreamCount;
                half _StreamFocus;
                half _TubeAnchorBoost;
                half _PlatformGripBoost;
                half _PlasmaScale;
                half _PlasmaSharpness;
                half _EdgeSoftness;
            CBUFFER_END

            // Процедурный плазменный / каустический узор в стиле стеклянных труб колонны
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Двухслойные плазменные прожилки, вытянутые вдоль силовых линий
            float PlasmaFilaments(float2 uv, float time)
            {
                // Искажение координат (domain warp) для органичных магнитных дуг
                float2 warpUv = uv * float2(1.4, 2.2) + float2(-time * 0.45, time * 0.15);
                float w1 = ValueNoise(warpUv);
                // Вторую компоненту domain-warp получаем из первой через плавное
                // преобразование. Это убирает один полный ValueNoise (4 Hash21).
                float w2 = sin((w1 * 1.7 + warpUv.y * 0.35) * 6.2831853) * 0.5 + 0.5;
                float2 warp = float2(w1 - 0.5, w2 - 0.5) * 0.45;

                // Слой 1: основные потоки от колонны к платформе
                float2 p1 = (uv + warp) * float2(0.85, 2.6) - float2(time * 1.15, 0.0);
                float n1 = ValueNoise(p1);
                float ridge1 = 1.0 - abs(n1 * 2.0 - 1.0);

                // Слой 2: тонкие высокочастотные разряды
                float2 p2 = (uv - warp * 0.7) * float2(1.6, 4.5) - float2(time * 1.75, 0.2);
                float n2 = ValueNoise(p2);
                float ridge2 = 1.0 - abs(n2 * 2.0 - 1.0);

                float combined = pow(saturate(ridge1), _PlasmaSharpness) * 0.75
                               + pow(saturate(ridge2), _PlasmaSharpness * 1.2) * 0.55;
                return saturate(combined);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = saturate(input.uv0.x); // 0 = труба колонны, 1 = край платформы
                float s = saturate(input.uv0.y); // 0..1 по ширине платформы (0 и 1 — у вертикальных труб)
                float layerType = input.uv1.x;
                float layerNorm = input.uv1.y;
                float time = _Time.y * _FlowSpeed;

                // 1. Мягкое затухание по краям, чтобы не было жёстких границ меша
                float sideMask = smoothstep(0.0, _EdgeSoftness, s) * smoothstep(1.0, 1.0 - _EdgeSoftness, s);
                // На концах по радиусу тоже мягко входим в трубу и в платформу
                float endMask = smoothstep(0.0, 0.06, t) * smoothstep(1.0, 0.93, t);

                // 2. Силовые лучи (магнитные нити), идущие от труб к платформе
                // Стягиваем лучи ближе к краям (где стоят вертикальные трубы) и к центру
                float centerDist = abs(s - 0.5) * 2.0; // 0 в центре, 1 у вертикальных труб
                float tubeProximity = pow(centerDist, 1.6); // сильнее у вертикальных труб по бокам

                // Силовые линии слегка сужаются к платформе, "обхватывая" её
                float pinchedS = lerp(s, 0.5 + (s - 0.5) * 0.88, t);
                float streamWave = sin(pinchedS * _StreamCount * 6.2831853 + sin(t * 4.0 - time * 1.5) * 0.6);
                float beamBase = saturate(streamWave * 0.5 + 0.5);
                // x^2.5 = x*x*sqrt(x): без общего pow() с фиксированным показателем.
                float beams = beamBase * beamBase * sqrt(beamBase) * _StreamFocus;
                // Боковые магнитные жгуты, идущие прямо от левой и правой вертикальных труб
                float sideTethers = smoothstep(0.62, 0.96, centerDist) * smoothstep(1.0, 0.82, centerDist);
                beams = saturate(beams * (0.45 + 0.75 * tubeProximity) + sideTethers * 1.15);

                // 3. Бегущие импульсы энергии от трубы (t=0) к платформе (t=1)
                float pulse = sin((t * 3.2 - time * 1.8) * 6.2831853) * 0.5 + 0.5;
                pulse = lerp(0.65, 1.25, pulse * pulse);

                // 4. Органичные плазменные прожилки (как в стеклянных трубах колонны)
                float2 plasmaCoord = float2(t, pinchedS) * _PlasmaScale;
                float filaments = PlasmaFilaments(plasmaCoord, time);

                // 5. Яркие точки выхода из труб (t -> 0) и зона магнитного захвата платформы (t -> 1)
                float tubeOriginGlow = exp(-t * 6.5) * (0.55 + 0.85 * tubeProximity) * _TubeAnchorBoost;
                float platformGripGlow = exp(-(1.0 - t) * 8.5) * _PlatformGripBoost;

                // Для дуг захвата (layerType == 1) усиливаем свечение самой магнитной стяжки
                float isArcLayer = step(0.5, layerType) * (1.0 - step(1.5, layerType));
                float arcBoost = lerp(0.9, 1.25, isArcLayer);

                // Мягкое затухание при взгляде строго в торец листа, чтобы слои не полосили
                float ndotv = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                float angleFade = smoothstep(0.04, 0.35, ndotv);

                // Сборка интенсивности
                float baseField = (0.22 + beams * 0.35 + filaments * 0.45) * arcBoost;
                float hotVeins = (filaments * (0.65 + beams * 0.85) * pulse) + tubeOriginGlow + platformGripGlow;

                half3 col = _DeepColor.rgb * baseField
                          + _CoreColor.rgb * (beams * 0.55 + filaments * 0.75) * pulse
                          + _HotColor.rgb  * saturate(hotVeins * 0.75);

                col *= _EmissionIntensity;

                // Premultiplied Alpha: глубокий синий объём + яркие аддитивные прожилки без белёсой пелены
                float alpha = saturate((baseField * 0.65 + hotVeins * 0.45) * sideMask * endMask * angleFade * _Opacity);
                return half4(col * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}