// SciFi/EnergyFlow — синяя «живая» энергия внутри стеклянной трубки (URP).
// Рисуется аддитивно ДО стекла (Queue Transparent-10), стекло ложится сверху.
//
// Оси: шейдер считает направления в ЛОКАЛЬНЫХ осях меша.
//   Axis Dir  — вдоль трубки (для вертикали) / ось кольца
//   Plane U/V — плоскость, перпендикулярная Axis Dir
// Если после импорта FBX течение идёт «не туда», поменяйте эти три вектора.
Shader "SciFi/EnergyFlow"
{
    Properties
    {
        [KeywordEnum(Vertical, Ring)] _Mode ("Режим", Float) = 0

        _CoreColor ("Сердцевина (бледно-голубой)", Color) = (0.55, 0.85, 1.0, 1)
        _MidColor  ("Основной цвет (синий)", Color)       = (0.05, 0.38, 1.0, 1)
        _EdgeColor ("Края (тёмно-синий)", Color)          = (0.0, 0.08, 0.45, 1)

        _Intensity ("Яркость", Range(0, 3)) = 0.9
        _BaseGlow  ("Яркость нити между импульсами", Range(0, 0.5)) = 0.10
        _ThreadWidth ("Толщина нити (доля радиуса)", Range(0.1, 1.0)) = 0.45

        _Speed     ("Скорость, м/с", Float) = 4.0
        _Spacing   ("Шаг импульсов, м", Float) = 3.0
        _PacketLen ("Длина импульса (доля шага)", Range(0.1, 1)) = 0.6
        _Density   ("Доля заполненных ячеек", Range(0, 1)) = 0.75
        _TailPow   ("Резкость хвоста", Range(0.5, 6)) = 2.5

        _Filament  ("Сила прожилок", Range(0, 2)) = 0.8
        _Spin      ("Вращение прожилок", Float) = 2.0
        _Flicker   ("Мерцание", Range(0, 0.5)) = 0.12

        _RingRadius ("Радиус кольца (для Ring)", Float) = 4.87
        _HalfLength ("Полудлина трубки (для Vertical)", Float) = 25
        _EndFade    ("Затухание у концов, м", Float) = 1.5

        _AxisDir ("Axis Dir", Vector) = (0, 1, 0, 0)
        _PlaneU  ("Plane U", Vector)  = (1, 0, 0, 0)
        _PlaneV  ("Plane V", Vector)  = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Energy"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _MODE_VERTICAL _MODE_RING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _MidColor, _EdgeColor;
                float _ThreadWidth;
                float _Intensity, _BaseGlow, _Speed, _Spacing, _PacketLen, _Density, _TailPow;
                float _Filament, _Spin, _Flicker, _RingRadius, _HalfLength, _EndFade;
                float4 _AxisDir, _PlaneU, _PlaneV;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  phase      : TEXCOORD3;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                // своя фаза у каждой трубки — из позиции пивота объекта
                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                OUT.phase = Hash11(pivot.x * 1.73 + pivot.y * 0.37 + pivot.z * 2.31);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                const float TAU = 6.2831853;
                float t = _Time.y;
                float3 p = IN.positionOS;

                float pu = dot(p, _PlaneU.xyz);
                float pv = dot(p, _PlaneV.xyz);
                float h  = dot(p, _AxisDir.xyz);

                // --- координаты вдоль трубки (u, в «ячейках») и вокруг неё (tubeAng) ---
                float u, tubeAng, endFade = 1.0, N = 1e6;
                #if defined(_MODE_RING)
                    float ang = atan2(pv, pu);
                    float r   = length(float2(pu, pv)) - _RingRadius;
                    tubeAng   = atan2(h, r);
                    N         = max(1.0, round(TAU * _RingRadius / _Spacing));
                    u         = ang / TAU * N;
                #else
                    tubeAng   = atan2(pv, pu);
                    u         = h / _Spacing;
                    endFade   = smoothstep(0.0, max(_EndFade, 1e-3), _HalfLength - abs(h));
                #endif
                u += IN.phase * 10.0 - t * _Speed / _Spacing;   // движение в сторону +оси

                // --- импульсы-кометы ---
                float cell = floor(u);
                #if defined(_MODE_RING)
                    cell -= N * floor(cell / N);                // шов кольца без разрыва
                #endif
                float h1 = Hash11(cell * 1.37 + IN.phase * 17.0);
                float h2 = Hash11(cell * 2.11 + 5.3 + IN.phase * 3.0);

                float f   = frac(u);
                float len = _PacketLen * (0.7 + 0.6 * h2);
                float g   = saturate((f - (1.0 - len)) / len);  // 0 в хвосте -> 1 у головы
                float shape = pow(g, _TailPow) * (1.0 - smoothstep(0.9, 1.0, g));
                float pkt = shape * (0.55 + 0.45 * h2) * step(h1, _Density);

                // --- турбулентность (целые множители u — без шва на кольце) ---
                float turb = saturate(0.5
                    + 0.25 * sin(u * TAU * 3.0 + tubeAng * 2.0 + t * 5.0 + h2 * 6.0)
                    + 0.25 * sin(u * TAU * 5.0 - tubeAng * 3.0 - t * 7.0));

                // --- радиальный профиль по виду на цилиндр: rho 0 в центре, 1 у края ---
                float3 N3 = normalize(IN.normalWS);
                float3 V  = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float nv  = saturate(dot(N3, V));
                float rho = sqrt(saturate(1.0 - nv * nv));

                float w = max(_ThreadWidth, 0.05);
                float profBase = exp(-(rho * rho) / (w * w));               // нить: толщина = _ThreadWidth
                float profPkt  = saturate(1.0 - rho / lerp(min(w * 1.5, 1.0), 1.0, pkt)); // импульс «раздувается»
                profPkt *= profPkt;

                // спиральные прожилки вокруг сердцевины
                float fil = pow(saturate(0.5 + 0.5 * sin(tubeAng * 3.0 + u * TAU + t * _Spin)), 4.0);
                fil *= smoothstep(0.1, 0.5, rho) * (1.0 - smoothstep(0.7, 1.0, rho));

                float energy = _BaseGlow * profBase * (0.4 + 0.6 * turb)
                             + pkt * (profPkt * (0.6 + 0.4 * turb) + _Filament * fil * profPkt);

                energy *= 1.0 + _Flicker * sin(t * 17.0 + IN.phase * 40.0) * sin(t * 5.3 + h1 * 9.0);
                energy *= endFade;

                // --- цвет и мягкое ограничение яркости (≈ не выше 1.0) ---
                float tt = saturate(energy * 1.2);
                half3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.5, tt));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.5, 1.0, tt));
                col *= 1.0 - exp(-energy * _Intensity * 2.0);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
