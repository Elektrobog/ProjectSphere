// Шейдер энергии для Unity (URP) — течение по трубам без текстур и аниматоров.
// Назначается материалам из FBX:
//   Energy - Flow Vertical  ->  ② Куда течёт = Vertical Tube
//   Energy - Flow Ring      ->  ② Куда течёт = Horizontal Ring
//
// Параметры в материале подписаны по-русски и пронумерованы ①–⑮:
//   ①–⑩ — движение и форма, ⑪–⑮ — внешний вид.
//
// Быстрые рецепты:
//   Ровно светящиеся трубы (убрать потоки) -> ① Стиль энергии = Steady Glow
//   Кольцо светится целиком, не течёт      -> ② Куда течёт = Horizontal Ring, (Кольца) Длина капли = 1.2
//   Все трубки текут одновременно           -> ⑤ галочка «Все трубки синхронно»
//   Медленнее течение                      -> ③ Скорость: 2–3
//   Быстрее                                -> ③ Скорость: 6–8
//   Крупные редкие капли                   -> ④ Длина капли: 0.7–1.0
//   Мелкие частые                          -> ④ Длина капли: 0.2
//   Труба светится целиком                 -> ⑦ Свечение пауз: 0.1–0.3
//   Убрать искры                           -> ⑧ Яркость искр: 0
//   Bloom не берёт                         -> ⑩ Общая яркость: 4–6
//   Другой цвет                            -> ⑬ Цвет энергии + ⑭ Цвет сердцевины

Shader "ProjectSphere/EnergyFlow"
{
    Properties
    {
        // ───────── ГЛАВНОЕ: движение и форма ────────
        [Enum(Flow Blobs, 0, Steady Glow, 1)] _GlowMode("① Стиль энергии (Flow = бегущие капли, Steady = ровное свечение)", Float) = 0
        [Enum(Vertical Tube, 0, Horizontal Ring, 1)] _FlowMode("② Куда течёт (Vertical = трубки, Horizontal = кольца)", Float) = 0
        _Speed("③ Скорость течения, м/с", Range(0, 15)) = 4.0
        _StreakSize("④ Длина капли, м (длина одного бегущего сгустка)", Range(0.05, 1.5)) = 0.35
        [Toggle] _SyncFlow("⑤ Все трубки синхронно (энергия идёт по всем трубам сразу)", Float) = 0
        _Contrast("⑥ Резкость капель (1 = мягкие, 3 = чёткие)", Range(0.5, 5)) = 1.7
        _Trail("⑦ Свечение пауз (0 = тёмные промежутки между каплями)", Range(0, 1)) = 0.0
        _Sparks("⑧ Яркость искр (0 = выключить)", Range(0, 5)) = 1.5
        _SparkPeriod("⑨ Шаг искр, м (меньше = чаще)", Range(0.2, 5)) = 1.5
        _Brightness("⑩ Общая яркость (сила свечения, для Bloom)", Range(0.5, 8)) = 3

        // ───────── ВНЕШНИЙ ВИД ────────
        [Toggle] _UseHotColor("⑪ Белые сердцевины у крупных капель", Float) = 1
        _CenterBrightness("⑫ Объёмность стержня (0 = плоская заливка)", Range(0, 1)) = 1
        _ColorMid("⑬ Цвет энергии (капли)", Color) = (0, 1, 0.1, 1)
        _ColorHot("⑭ Цвет сердцевины (яркая середина)", Color) = (0.65, 1, 0.75, 1)
        _ColorDeep("⑮ Цвет пауз (обычно чёрный)", Color) = (0, 0, 0, 0)

        // ───────── ТОЛЬКО ДЛЯ РЕЖИМА «VERTICAL TUBE» (трубки) ────────
        _CoreRadius("(Трубки) Толщина стержня, м", Range(0.005, 0.1)) = 0.018
        _Filaments("(Трубки) Прожилки вокруг стержня (0–6)", Range(0, 6)) = 3

        // ───────── ТОЛЬКО ДЛЯ РЕЖИМА «HORIZONTAL RING» (кольца) ────────
        _RingRadius("(Кольца) Радиус кольца, м", Float) = 1.46
        _RingStreakSize("(Кольца) Длина капли, м (размер бегущих вспышек)", Range(0.2, 3)) = 1.2
        _RingPulses("(Кольца) Вспышек по кругу", Range(0, 20)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"   // фаза течения берётся из позиции объекта
        }

        Pass
        {
            Name "EnergyFlow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _GlowMode;
                float _FlowMode;
                float _CoreRadius;
                float _StreakSize;
                float _Filaments;
                float _RingRadius;
                float _RingStreakSize;
                float _Speed;
                float _Trail;
                float _Contrast;
                float _SparkPeriod;
                float _RingPulses;
                float _Sparks;
                float _Brightness;
                float4 _ColorDeep;
                float4 _ColorMid;
                float4 _ColorHot;
                float _UseHotColor;
                float _CenterBrightness;
                float _SyncFlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = _WorldSpaceCameraPos.xyz - posWS;
                return OUT;
            }

            // 3D value-noise без текстур (аналог Noise-текстуры в Blender)
            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float vnoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                float xy0 = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
                float xy1 = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
                return lerp(xy0, xy1, f.z);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // время — аналог Scene Time * скорость из Blender
                float t = _Time.y * _Speed;

                // ⑤ фаза каждой трубки из её позиции: соседние трубки текут
                // с разным сдвигом. Галочка «Все трубки синхронно» обнуляет
                // сдвиг — вся энергия идёт по всем трубам одновременно.
                float3 objPosWS = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
                float phase = frac(sin(dot(objPosWS, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.28318;
                if (_SyncFlow > 0.5) phase = 0.0;

                float angle = atan2(IN.positionOS.z, IN.positionOS.x);

                float mask;
                if (_GlowMode > 0.5)
                {
                    // ① Steady Glow: ровное свечение, без капель и потоков
                    mask = 1.0;
                }
                else if (_FlowMode < 0.5)
                {
                    // вертикальная трубка: шум вытянут вдоль Y (капли) и сжат вокруг оси
                    float crossFreq = _Filaments / (6.28318 * _CoreRadius);
                    float3 np = float3(
                        IN.positionOS.x * crossFreq,
                        IN.positionOS.z * crossFreq,
                        IN.positionOS.y / _StreakSize - t + phase);
                    mask = vnoise3(np);
                }
                else
                {
                    // кольцо: выборка шума идёт по окружности — шва нет.
                    // длина капли у кольца СВОЯ (по умолчанию 1.2 м): на окружности
                    // в ~9 м капли по 0.35 м сливаются в сплошное свечение
                    float a = angle - t * _RingStreakSize / _RingRadius + phase;
                    float k = _RingRadius / _RingStreakSize;
                    mask = vnoise3(float3(cos(a) * k, sin(a) * k, phase * 0.37));
                }

                // контраст: мягкий шум -> резкие капли, между ними тёмные паузы
                mask = saturate((mask - 0.38) / 0.30);      // 0.38..0.68 -> 0..1
                mask = pow(mask, _Contrast);                // ⑥ затачиваем края капель
                mask = lerp(_Trail, 1.0, mask);             // ⑦ Trail = 0 -> тёмные паузы

                // быстрые искры-бусины поверх капель (⑧⑨)
                if (_Sparks > 0.001)
                {
                    float s;
                    if (_FlowMode < 0.5)
                        s = sin(IN.positionOS.y * (6.28318 / _SparkPeriod) * 5.0 - t * 3.5 + phase);
                    else
                        s = sin(angle * _RingPulses * 5.0 - t * 3.5 + phase);
                    mask += pow(s * 0.5 + 0.5, 12.0) * _Sparks;
                }

                // объёмный стержень: центр трубки ярче краёв (⑫)
                float ndv = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float centerEffect = pow(ndv, 1.5) * 0.8 + 0.2;
                float center = lerp(1.0, centerEffect, _CenterBrightness);

                float bright = mask * center;

                // цвет: паузы чёрные -> цветные капли -> светлые сердцевины (⑬⑭⑮, ⑪)
                float3 col = lerp(_ColorDeep.rgb, _ColorMid.rgb, saturate(bright * 2.0));
                float hotBlend = saturate((bright - 0.5) * 2.0) * _UseHotColor;
                col = lerp(col, _ColorHot.rgb, hotBlend);

                float3 emission = col * bright * _Brightness;   // ⑩
                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
