// Шейдер энергии для Unity (URP) — течение по трубам без текстур и аниматоров.
// Повторяет материал из Blender: бегущие «капли» + быстрые искры-бусины.
// Присваивается материалам из FBX:
//   Energy - Flow Vertical  ->  Flow Mode = Vertical Tube
//   Energy - Flow Ring      ->  Flow Mode = Horizontal Ring
// Bloom: Global Volume -> Bloom (Threshold ~0.8), в URP Asset включить Post Processing и HDR.

Shader "ProjectSphere/EnergyFlow"
{
    Properties
    {
        [Enum(Vertical Tube, 0, Horizontal Ring, 1)] _FlowMode("Flow Mode", Float) = 0
        _CoreRadius("Core Radius, m (Vertical)", Float) = 0.018
        _StreakSize("Blob Length, m", Float) = 0.35
        _Filaments("Filaments Around Core", Float) = 3
        _RingRadius("Ring Radius, m (Ring)", Float) = 1.46
        _Speed("Flow Speed (units/sec)", Float) = 4.0
        _Trail("Min Brightness (pauses)", Range(0, 1)) = 0.0
        _Contrast("Blob Contrast", Float) = 1.7
        _SparkPeriod("Spark Period, m", Float) = 1.5
        _RingPulses("Ring Pulses (Ring)", Float) = 6
        _Sparks("Spark Brightness", Float) = 1.5
        _Brightness("Overall Brightness", Float) = 3
        _ColorDeep("Deep Color (pauses)", Color) = (0, 0, 0, 0)
        _ColorMid("Mid Color", Color) = (0, 1, 0.1, 1)
        _ColorHot("Hot Color (cores)", Color) = (0.65, 1, 0.75, 1)
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
                mask = pow(mask, _Contrast);                 // затачиваем края капель
                mask = lerp(_Trail, 1.0, mask);              // Trail = 0 -> тёмные паузы

                // быстрые искры-бусины поверх капель
                if (_Sparks > 0.001)
                {
                    float s;
                    if (_FlowMode < 0.5)
                        s = sin(IN.positionOS.y * (6.28318 / _SparkPeriod) * 5.0 - t * 3.5 + phase);
                    else
                        s = sin(angle * _RingPulses * 5.0 - t * 3.5 + phase);
                    mask += pow(s * 0.5 + 0.5, 12.0) * _Sparks;
                }

                // центр стержня ярче, края темнее (аналог Layer Weight в Blender)
                float ndv = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float center = pow(ndv, 1.5) * 0.8 + 0.2;
                float bright = mask * center;

                // цвет: паузы чёрные -> зелёные капли -> бело-зелёные сердцевины
                float3 col = lerp(_ColorDeep.rgb, _ColorMid.rgb, saturate(bright * 2.0));
                col = lerp(col, _ColorHot.rgb, saturate((bright - 0.5) * 2.0));

                float3 emission = col * bright * _Brightness;
                return half4(emission, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
