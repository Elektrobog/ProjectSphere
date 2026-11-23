// SciFi/EnergyTube — стекло + объёмная энергия в ОДНОМ проходе (Unity URP).
// Никакой внутренней геометрии: энергия считается лучом (raymarch) внутри самой стеклянной трубки.
// Требования к мешу: UV.x = расстояние вдоль трубки в МЕТРАХ, UV.y = угол вокруг трубки (0..1).
// Это делает Blender-скрипт SciFiEnergyColumn_v2.py.

Shader "SciFi/EnergyTube"
{
    Properties
    {
        [Header(Tube)]
        _TubeRadius ("Радиус трубки, м (как TUBE_RADIUS в Blender)", Float) = 0.13
        _InnerRatio ("Радиус энергии (доля радиуса трубки)", Range(0.3, 0.95)) = 0.78
        _Length     ("Длина трубки, м (вертикальная; 0 для кольца)", Float) = 50
        _Period     ("Длина кольца в UV, м (для кольца; 0 для вертикальной) — ТОЧНО как печатает Blender", Float) = 0
        _EndFade    ("Затухание у концов, м", Float) = 1.5

        [Header(Energy Colors)]
        [HDR] _CoreColor ("Сердцевина", Color) = (0.55, 0.85, 1.0, 1)
        [HDR] _MidColor  ("Основной цвет", Color) = (0.05, 0.38, 1.0, 1)
        [HDR] _EdgeColor ("Края", Color) = (0.0, 0.08, 0.45, 1)
        _Intensity   ("Яркость (HDR, для Bloom)", Range(0, 8)) = 2.0
        _BaseGlow    ("Свечение между импульсами", Range(0, 0.6)) = 0.15
        _ThreadWidth ("Толщина ядра (доля радиуса энергии)", Range(0.05, 1.0)) = 0.4

        [Header(Pulses)]
        _Speed     ("Скорость, м/с", Float) = 4.0
        _Spacing   ("Шаг импульсов, м", Float) = 3.0
        _PacketLen ("Длина импульса (доля шага)", Range(0.1, 1)) = 0.6
        _Density   ("Доля заполненных ячеек", Range(0, 1)) = 0.8
        _TailPow   ("Резкость хвоста", Range(0.5, 6)) = 2.5

        [Header(Plasma)]
        _NoiseScale ("Масштаб плазмы, 1/м", Float) = 5.0
        _Crackle    ("Электрические прожилки", Range(0, 3)) = 1.3
        _Spin       ("Вращение плазмы", Float) = 1.5
        _Flicker    ("Мерцание", Range(0, 0.5)) = 0.12

        [Header(Glass)]
        _GlassTint    ("Тон стекла (A = плотность)", Color) = (0.5, 0.72, 1.0, 0.05)
        [HDR] _RimColor ("Цвет кромки (A = сила)", Color) = (0.6, 0.85, 1.0, 0.55)
        _RimPower     ("Резкость кромки", Range(0.5, 8)) = 3.0
        _SpecPower    ("Блик: резкость", Range(4, 256)) = 90
        _SpecStrength ("Блик: сила", Range(0, 3)) = 1.0
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
            Name "EnergyTube"
            Tags { "LightMode" = "UniversalForward" }

            // Премультипл. альфа: стекло ложится поверх фона, энергия добавляется (свечение).
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define STEPS 6 // PC/1080p: на 40% меньше raymarch-шагов
            #define TAU   6.2831853

            CBUFFER_START(UnityPerMaterial)
                float _TubeRadius, _InnerRatio, _Length, _Period, _EndFade;
                half4 _CoreColor, _MidColor, _EdgeColor;
                float _Intensity, _BaseGlow, _ThreadWidth;
                float _Speed, _Spacing, _PacketLen, _Density, _TailPow;
                float _NoiseScale, _Crackle, _Spin, _Flicker;
                half4 _GlassTint, _RimColor;
                float _RimPower, _SpecPower, _SpecStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                float  phase      : TEXCOORD4;
            };

            // ---------- хэши и шум ----------
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise; по X может быть периодическим (per > 0 — число ячеек в периоде), чтобы кольцо было без шва.
            float Cn(float3 c, float per)
            {
                if (per > 0.0) c.x -= per * floor(c.x / per);
                return Hash31(c);
            }

            float VNoise(float3 p, float per)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Cn(i + float3(0, 0, 0), per);
                float n100 = Cn(i + float3(1, 0, 0), per);
                float n010 = Cn(i + float3(0, 1, 0), per);
                float n110 = Cn(i + float3(1, 1, 0), per);
                float n001 = Cn(i + float3(0, 0, 1), per);
                float n101 = Cn(i + float3(1, 0, 1), per);
                float n011 = Cn(i + float3(0, 1, 1), per);
                float n111 = Cn(i + float3(1, 1, 1), per);
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS  = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.uv         = IN.uv;
                float3 pv = TransformObjectToWorld(float3(0, 0, 0));
                OUT.phase = Hash11(pv.x * 1.73 + pv.y * 0.37 + pv.z * 2.31);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // ---------- локальный базис трубки в мировом пространстве ----------
                float3 N = normalize(IN.normalWS);
                float3 T = IN.tangentWS - N * dot(IN.tangentWS, N);
                T = normalize(T + 1e-6);                       // вдоль трубки (направление роста UV.x)
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // B направлена в сторону роста UV.y — определяем знак через производные (не зависит от зеркалирования FBX)
                float3 B = cross(N, T);
                float3 dPdTheta = ddx(IN.positionWS) * ddx(IN.uv.y) + ddy(IN.positionWS) * ddy(IN.uv.y);
                B *= (dot(B, dPdTheta) >= 0.0) ? 1.0 : -1.0;

                // ---------- луч внутри трубки (в плоскости сечения N-B) ----------
                float R   = max(_TubeRadius, 1e-4);
                float rIn = R * _InnerRatio;
                float2 d  = float2(-dot(V, N), -dot(V, B));    // направление луча в сечении
                float  dz = -dot(V, T);                        // и вдоль оси
                float  a  = dot(d, d);
                float  bq = R * d.x;                           // точка входа = (R, 0)
                float  disc = bq * bq - a * (R * R - rIn * rIn);

                half3 energy = 0;

                if (a > 1e-4 && disc > 0.0)
                {
                    float sq = sqrt(disc);
                    float s0 = max((-bq - sq) / a, 0.0);
                    float s1 = (-bq + sq) / a;

                    if (s1 > s0)
                    {
                        float ds = (s1 - s0) / STEPS;

                        // шаг импульсов (для кольца подгоняем под целое число ячеек — без шва)
                        float Ncell = 1e6;
                        float sp = max(_Spacing, 1e-3);
                        float noisePer = 0.0;
                        float fx = _NoiseScale;
                        if (_Period > 0.0)
                        {
                            Ncell    = max(1.0, round(_Period / sp));
                            sp       = _Period / Ncell;
                            noisePer = max(1.0, round(_Period * _NoiseScale));
                            fx       = noisePer / _Period;
                        }

                        float w = max(_ThreadWidth, 0.05);

                        // Угол UV и вращение постоянны для всех шагов.
                        // Поворотом xy заменяем atan2 + cos + sin внутри raymarch-цикла.
                        float spinSin, spinCos;
                        sincos(IN.uv.y * TAU + t * _Spin, spinSin, spinCos);
                        float invRIn = rcp(max(rIn, 1e-5));
                        float pulseScroll = t * _Speed / sp;
                        float noiseScroll = t * _Speed * 0.5;

                        for (int i = 0; i < STEPS; i++)
                        {
                            float s   = s0 + (i + 0.5) * ds;
                            float2 xy = float2(R, 0.0) + d * s;
                            float rho = saturate(sqrt(dot(xy, xy)) * invRIn);
                            float us  = IN.uv.x + dz * s;                       // позиция вдоль трубки, м

                            // --- импульсы-кометы ---
                            float uu   = us / sp + IN.phase * 10.0 - pulseScroll;
                            float cell = floor(uu);
                            if (_Period > 0.0) cell -= Ncell * floor(cell / Ncell);
                            float h1 = Hash11(cell * 1.37 + IN.phase * 17.0);
                            float h2 = Hash11(cell * 2.11 + 5.3 + IN.phase * 3.0);
                            float f   = frac(uu);
                            float len = max(_PacketLen * (0.7 + 0.6 * h2), 1e-4);
                            float g   = saturate((f - (1.0 - len)) / len);
                            float shape = pow(max(g, 1e-5), _TailPow) * (1.0 - smoothstep(0.55, 1.0, g));
                            float pkt = shape * (0.55 + 0.45 * h2) * step(h1, _Density);

                            // --- плазма: два масштаба 3D-шума вместо трёх ---
                            // Поворот xy математически эквивалентен прежнему углу,
                            // но не требует atan2/cos/sin на каждом шаге.
                            float2 spunXY = float2(
                                xy.x * spinCos - xy.y * spinSin,
                                xy.x * spinSin + xy.y * spinCos);
                            float2 cs = spunXY * (invRIn * 2.5);
                            float3 p  = float3((us - noiseScroll) * fx, cs.x, cs.y);
                            float n1  = VNoise(p, noisePer);
                            float n2  = VNoise(float3(p.x * 2.0, p.y * 3.0 + 31.7, p.z * 3.0 + 4.4), noisePer * 2.0);
                            float turb  = saturate(0.65 + 1.1 * (n1 - 0.5));
                            float ridgeBase = saturate(1.0 - abs(2.0 * n2 - 1.0));
                            float ridge2 = ridgeBase * ridgeBase;
                            float ridge = ridge2 * ridge2 * ridgeBase; // x^5, «молнии»

                            // --- радиальный профиль ---
                            float core = exp(-(rho * rho) / (w * w));
                            float radP = saturate(1.0 - rho / lerp(min(w * 1.6, 1.0), 1.0, pkt));
                            radP *= radP;

                            float dens = _BaseGlow * core * turb * (1.0 + 0.5 * _Crackle * ridge)
                                       + pkt * radP * (turb + _Crackle * ridge);

                            // --- цвет по плотности ---
                            float tt = saturate(dens * 1.2);
                            half3 c  = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.5, tt));
                            c        = lerp(c, _CoreColor.rgb, smoothstep(0.5, 1.0, tt));
                            energy  += c * dens;
                        }

                        energy *= (ds / rIn) * _Intensity;

                        float fade = 1.0;
                        if (_Period <= 0.0 && _Length > 0.0)
                        {
                            float e = max(_EndFade, 1e-3);
                            fade = smoothstep(0.0, e, IN.uv.x) * smoothstep(0.0, e, _Length - IN.uv.x);
                        }
                        energy *= fade * (1.0 + _Flicker * sin(t * 17.0 + IN.phase * 40.0) * sin(t * 5.3 + IN.phase * 9.0));
                    }
                }

                // ---------- стекло ----------
                float nv   = saturate(dot(N, V));
                float fres = pow(1.0 - nv, _RimPower);
                float3 Hh  = normalize(_MainLightPosition.xyz + V);
                float spec = pow(saturate(dot(N, Hh)), _SpecPower) * _SpecStrength;

                float  glassA = saturate(_GlassTint.a + fres * _RimColor.a + spec);
                half3  glass  = _GlassTint.rgb * _GlassTint.a
                              + _RimColor.rgb * (fres * _RimColor.a)
                              + _MainLightColor.rgb * spec;

                return half4(glass + energy, glassA);
            }
            ENDHLSL
        }
    }
}