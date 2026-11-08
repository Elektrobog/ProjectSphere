// SciFi/MagneticField — мягкое магнитное поле вдоль выреза платформы (URP).
// Аддитивное свечение: яркие линии по краям платформы, ореол и слабая
// заливка между ними, очень медленные линии поля вдоль дуги. Без мерцания.
//
// Требует UV от меша Magnetic_Field из Blender:
//   U — вдоль дуги (0..1), V — по высоте (0..1).
// _BandA / _BandB — положение нижнего и верхнего края платформы по V.
// Значения печатает скрипт Blender (при PAD 0.4 и толщине 0.6: 0.286 и 0.714).
Shader "SciFi/MagneticField"
{
    Properties
    {
        _Color ("Цвет", Color) = (0.1, 0.45, 1, 1)
        _Intensity ("Яркость", Range(0, 4)) = 1.5
        _BandA ("Нижний край платформы (v)", Range(0, 1)) = 0.286
        _BandB ("Верхний край платформы (v)", Range(0, 1)) = 0.714
        _BandWidth ("Ширина ядра", Range(0.01, 0.3)) = 0.05
        _Halo ("Сила ореола", Range(0, 1)) = 0.35
        _Fill ("Заливка между краями", Range(0, 1)) = 0.12
        _Waves ("Плотность линий поля", Float) = 60
        _WaveSpeed ("Скорость линий", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Field"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity, _BandA, _BandB, _BandWidth, _Halo, _Fill, _Waves, _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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

                // яркое ядро на верхнем и нижнем краю платформы
                float a = (v - _BandA) / _BandWidth;
                float b = (v - _BandB) / _BandWidth;
                float core = exp(-a * a) + exp(-b * b);

                // широкий мягкий ореол вокруг ядра
                float ha = (v - _BandA) / (_BandWidth * 5.0);
                float hb = (v - _BandB) / (_BandWidth * 5.0);
                float halo = exp(-ha * ha) + exp(-hb * hb);

                // слабая заливка между краями
                float fill = smoothstep(_BandA - 0.05, _BandA + 0.05, v)
                           * (1.0 - smoothstep(_BandB - 0.05, _BandB + 0.05, v));

                // медленные линии поля, текут вдоль дуги
                float lines = 0.8 + 0.2 * sin(u * _Waves + v * 8.0 - _Time.y * _WaveSpeed);

                // плавное затухание на концах дуги
                float edge = smoothstep(0.0, 0.08, u) * smoothstep(0.0, 0.08, 1.0 - u);

                float energy = (core + _Halo * halo + _Fill * fill) * lines * edge;

                return half4(_Color.rgb * energy * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
