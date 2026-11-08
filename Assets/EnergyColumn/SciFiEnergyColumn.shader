// Шейдер энергии для Unity (URP) — течение по трубам без текстур и аниматоров.
// Повторяет материал из Blender: бегущие «капли» + быстрые искры-бусины.
// Назначается материалам из FBX:
//   Energy - Flow Vertical  ->  ① Режим течения = Vertical Tube
//   Energy - Flow Ring      ->  ① Режим течения = Horizontal Ring
//
// В материале параметры подписаны по-русски и пронумерованы ①–⑬:
//   ①–⑧ — движение и форма капель, ⑨–⑬ — внешний вид.
//   «(Трубки)» — действует только в режиме Vertical Tube, «(Кольца)» — только в Horizontal Ring.
//
// Быстрые рецепты:
//   Медленнее течение       -> ② Скорость: 2–3
//   Быстрее                 -> ② Скорость: 6–8
//   Крупные редкие капли    -> ③ Длина капли 0.5, ④ Резкость 2.5
//   Мелкие частые           -> ③ Длина капли 0.2
//   Труба светится целиком  -> ⑤ Свечение пауз 0.1–0.3 (выше = снова «палка»)
//   Убрать искры            -> ⑥ Яркость искр 0
//   Bloom не светится        -> ⑧ Общая яркость 4–6
//   Другой цвет             -> ⑪ Цвет энергии + ⑫ Цвет сердцевины (оранжевый = «лава»)

Shader "ProjectSphere/EnergyFlow"
{
    Properties
    {
        // ───────── ГЛАВНОЕ: движение и форма ────────
        [Enum(Vertical Tube, 0, Horizontal Ring, 1)] _FlowMode("① Режим течения (Vertical = трубки, Horizontal = кольца)", Float) = 0
        _Speed("② Скорость течения, м/с", Range(0, 15)) = 4.0
        _StreakSize("③ Длина капли, м", Range(0.05, 1.5)) = 0.35
        _Contrast("④ Резкость капель (1 = мягкие, 3 = чёткие)", Range(0.5, 5)) = 1.7
        _Trail("⑤ Свечение пауз (0 = тёмные промежутки)", Range(0, 1)) = 0.0
        _Sparks("⑥ Яркость искр (0 = выключить)", Range(0, 5)) = 1.5
        _SparkPeriod("⑦ Шаг искр, м (меньше = чаще)", Range(0.2, 5)) = 1.5
        _Brightness("⑧ Общая яркость (сила свечения, для Bloom)", Range(0.5, 8)) = 3

        // ───────── ВНЕШНИЙ ВИД ────────
        [Toggle] _UseHotColor("⑨ Белые сердцевины у крупных капель", Float) = 1
        _CenterBrightness("⑩ Объёмность стержня (0 = плоская заливка)", Range(0, 1)) = 1
        _ColorMid("⑪ Цвет энергии (капли)", Color) = (0, 1, 0.1, 1)
        _ColorHot("⑫ Цвет сердцевины (яркая середина)", Color) = (0.65, 1, 0.75, 1)
        _ColorDeep("⑬ Цвет пауз (обычно чёрный)", Color) = (0, 0, 0, 0)

        // ───────── ТОЛЬКО ДЛЯ РЕЖИМА «VERTICAL TUBE» (трубки) ────────
        _CoreRadius("(Трубки) Толщина стержня, м", Range(0.005, 0.1)) = 0.018
        _Filaments("(Трубки) Прожилки вокруг стержня (0–6)", Range(0, 6)) = 3

        // ───────── ТОЛЬКО ДЛЯ РЕЖИМА «HORIZONTAL RING» (кольца) ────────
        _RingRadius("(Кольца) Радиус кольца, м", Float) = 1.46
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
                float _FlowMode;
                float _CoreRadius;
                float _StreakSize;
                float _Filaments;
                float _RingRadius;
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

                // фаза каждой трубки из позиции объекта:
                // соседние трубки текут с разным сдвигом, не синхронно
                float3 objPosWS = float3(UNITY_MATRIX_M[0].w, UNITY_MATRIX_M[1].w, UNITY_MATRIX_M[2].w);
                float phase = frac(sin(dot(objPosWS, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.28318;

                float angle = atan2(IN.positionOS.z, IN.positionOS.x);

                float mask;
                if (_FlowMode < 0.5)
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
                    // кольцо: выборка шума идёт по окружности — шва нет
                    float a = angle - t * _StreakSize / _RingRadius + phase;
                    float k = _RingRadius / _StreakSize;
                    mask = vnoise3(float3(cos(a) * k, sin(a) * k, phase * 0.37));
                }

                // контраст: мягкий шум -> резкие капли, между ними тёмные паузы
                mask = saturate((mask - 0.38) / 0.30);      // 0.38..0.68 -> 0..1
                mask = pow(mask, _Contrast);                // ④ затачиваем края капель
                mask = lerp(_Trail, 1.0, mask);             // ⑤ Trail = 0 -> тёмные паузы

                // быстрые искры-бусины поверх капель (⑥⑦)
                if (_Sparks > 0.001)
                {
                    float s;
                    if (_FlowMode < 0.5)
                        s = sin(IN.positionOS.y * (6.28318 / _SparkPeriod) * 5.0 - t * 3.5 + phase);
                    else
                        s = sin(angle * _RingPulses * 5.0 - t * 3.5 + phase);
                    mask += pow(s * 0.5 + 0.5, 12.0) * _Sparks;
                }

                // объёмный стержень: центр трубки ярче краёв (⑩)
                float ndv = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float centerEffect = pow(ndv, 1.5) * 0.8 + 0.2;
                float center = lerp(1.0, centerEffect, _CenterBrightness);

                float bright = mask * center;

                // цвет: паузы чёрные -> цветные капли -> светлые сердцевины (⑪⑫⑬, ⑨)
                float3 col = lerp(_ColorDeep.rgb, _ColorMid.rgb, saturate(bright * 2.0));
                float hotBlend = saturate((bright - 0.5) * 2.0) * _UseHotColor;
                col = lerp(col, _ColorHot.rgb, hotBlend);

                float3 emission = col * bright * _Brightness;   // ⑧
                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
