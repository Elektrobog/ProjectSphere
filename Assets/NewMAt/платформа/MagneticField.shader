// SciFi/MagneticField — магнитное поле вдоль выреза платформы (Unity URP).
// Визуально согласовано с SciFi/EnergyFlow: поле исходит из труб на концах дуги,
// стягивается у сопел и расходится силовыми линиями вдоль кромок платформы.
//
// Требует UV от меша Magnetic_Field из Blender:
//   U — вдоль дуги (0..1, концы примыкают к трубам), V — по высоте (0..1).

Shader "SciFi/MagneticField"
{
    Properties
    {
        [HDR] _CoreColor ("Сердцевина / разряд у труб", Color) = (0.55, 0.85, 1.0, 1)
        [HDR] _MidColor  ("Основной цвет поля", Color)        = (0.05, 0.38, 1.0, 1)
        [HDR] _EdgeColor ("Края ореола", Color)               = (0.0, 0.08, 0.45, 1)

        _Intensity ("Яркость (HDR)", Range(0, 5)) = 1.6

        [Header(Platform Edges)]
        _BandA     ("Нижний край платформы (v)", Range(0, 1)) = 0.286
        _BandB     ("Верхний край платформы (v)", Range(0, 1)) = 0.714
        _BandWidth ("Ширина ядра на кромке", Range(0.005, 0.2)) = 0.035
        _Halo      ("Сила ореола", Range(0, 1)) = 0.35
        _Fill      ("Заливка между краями", Range(0, 1)) = 0.14

        [Header(Tube Emitters and Shape)]
        _EmitterBoost ("Яркость у выхода из труб", Range(0, 3)) = 1.1
        _EmitterPinch ("Стяжка линий к трубам", Range(0, 0.8)) = 0.35
        _ArcSag       ("Прогиб поля в центре дуги", Range(-0.15, 0.15)) = 0.03

        [Header(Flux Lines)]
        _Waves        ("Плотность силовых линий", Float) = 28.0
        _WaveSpeed    ("Скорость течения от труб", Float) = 0.45
        _LineContrast ("Выраженность прожилок", Range(0, 1)) = 0.35
        [Toggle] _OneWayFlow ("Течение от одной трубы к другой", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Field"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _MidColor, _EdgeColor;
                float _Intensity;
                float _BandA, _BandB, _BandWidth, _Halo, _Fill;
                float _EmitterBoost, _EmitterPinch, _ArcSag;
                float _Waves, _WaveSpeed, _LineContrast, _OneWayFlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = IN.uv.x;
                float v = IN.uv.y;

                // 0 на концах у труб (u = 0 и u = 1), 1 в центре дуги (u = 0.5)
                float arcMid = saturate(4.0 * u * (1.0 - u));
                float pinch  = 1.0 - arcMid; // 1 прямо у труб, 0 в центре

                // Геометрия силового контура: у труб линии стягиваются к оси,
                // а в середине пролёта чуть прогибаются под натяжением
                float centerV = 0.5 * (_BandA + _BandB);
                float bandA   = lerp(_BandA, centerV, pinch * _EmitterPinch) + arcMid * _ArcSag;
                float bandB   = lerp(_BandB, centerV, pinch * _EmitterPinch) - arcMid * _ArcSag;

                // У труб ядро уже и плотнее, в центре дуги чуть рассеивается
                float wCore = max(_BandWidth * lerp(0.65, 1.15, arcMid), 1e-4);
                float wHalo = max(_BandWidth * 4.5 * lerp(0.85, 1.25, arcMid), 1e-4);

                float dA = v - bandA;
                float dB = v - bandB;

                // Яркое ядро вдоль верхней и нижней кромок платформы
                float core = exp(-(dA * dA) / (wCore * wCore))
                           + exp(-(dB * dB) / (wCore * wCore));

                // Широкий мягкий ореол
                float halo = exp(-(dA * dA) / (wHalo * wHalo))
                           + exp(-(dB * dB) / (wHalo * wHalo));

                // Мягкая заливка строго МЕЖДУ кромками bandA и bandB
                float fill = saturate(
                    smoothstep(bandA - 0.05, bandA + 0.05, v) -
                    smoothstep(bandB - 0.05, bandB + 0.05, v)
                );

                // Нормализованная координата поперёк поля (-1 на bandA, +1 на bandB):
                // благодаря ей силовые линии повторяют стяжку у труб и расхождение в центре
                float halfSpan  = max(0.5 * abs(bandB - bandA), 0.03);
                float fluxCoord = (v - centerV) / halfSpan;

                // Плавное движение волн вдоль силовых линий (от обеих труб к центру или в одну сторону)
                float alongCoord = lerp(abs(u - 0.5) * 2.0, 1.0 - u, _OneWayFlow);
                float flowPhase  = alongCoord * 6.0 + _Time.y * _WaveSpeed;

                float wave1 = 0.5 + 0.5 * sin(fluxCoord * _Waves + flowPhase);
                float wave2 = 0.5 + 0.5 * sin(fluxCoord * (_Waves * 1.618) - flowPhase * 0.7 + u * 6.2831853);
                float fluxLines = lerp(1.0, 0.55 + 0.45 * (0.65 * wave1 + 0.35 * wave2), _LineContrast);

                // Сгусток энергии в местах примыкания к трубам (u = 0 и u = 1)
                float emitterMask = pow(max(pinch, 1e-5), 4.5);
                float emitterGlow = emitterMask * _EmitterBoost
                                  * exp(-((v - centerV) * (v - centerV)) / max(wHalo * wHalo * 0.6, 1e-4));

                // Суммарная плотность энергии
                float energy = (core + _Halo * halo + _Fill * fill) * fluxLines + emitterGlow;

                // Мягкое гашение у верхнего/нижнего краёв UV-меша и микро-сглаживание на стыке с трубами
                float vFade = smoothstep(0.0, 0.08, v) * smoothstep(0.0, 0.08, 1.0 - v);
                float uFade = smoothstep(0.0, 0.015, u) * smoothstep(0.0, 0.015, 1.0 - u);
                energy *= vFade * uFade;

                // Температурный градиент как в EnergyFlow (от тёмно-синего края к бледно-голубому ядру)
                float tt  = saturate(energy * 0.85);
                half3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.45, tt));
                col       = lerp(col, _CoreColor.rgb, smoothstep(0.45, 1.0, tt));
                col      *= (1.0 - exp(-energy * 1.8)) * _Intensity;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}