// SciFi/EnergyFlow — синяя «живая» энергия внутри стеклянной трубки (Unity URP).
// Рисуется аддитивно ДО стекла (Queue Transparent-10), стекло ложится сверху.

Shader "SciFi/EnergyFlow"
{
    Properties
    {
        [KeywordEnum(Vertical, Ring)] _Mode ("Режим", Float) = 0
        [KeywordEnum(Y, X, Z, Custom)] _Axis ("Ось трубки", Float) = 0
        _CustomAxis  ("Своя ось (для Custom)", Vector) = (0, 1, 0, 0)
        _PivotOffset ("Смещение центра оси (локальное)", Vector) = (0, 0, 0, 0)

        [HDR] _CoreColor ("Сердцевина (бледно-голубой)", Color) = (0.55, 0.85, 1.0, 1)
        [HDR] _MidColor  ("Основной цвет (синий)", Color)       = (0.05, 0.38, 1.0, 1)
        [HDR] _EdgeColor ("Края (тёмно-синий)", Color)          = (0.0, 0.08, 0.45, 1)

        _Intensity   ("Яркость (HDR)", Range(0, 5)) = 1.2
        _BaseGlow    ("Яркость нити между импульсами", Range(0, 0.5)) = 0.10
        _ThreadWidth ("Толщина нити (доля радиуса)", Range(0.05, 1.0)) = 0.45

        _Speed     ("Скорость, м/с", Float) = 4.0
        _Spacing   ("Шаг импульсов, м", Float) = 3.0
        _PacketLen ("Длина импульса (доля шага)", Range(0.1, 1)) = 0.6
        _Density   ("Доля заполненных ячеек", Range(0, 1)) = 0.75
        _TailPow   ("Резкость хвоста", Range(0.5, 6)) = 2.5

        _Filament  ("Сила прожилок", Range(0, 2)) = 0.8
        _Spin      ("Вращение прожилок", Float) = 2.0
        _Flicker   ("Мерцание", Range(0, 0.5)) = 0.12

        _RingRadius ("Радиус кольца (для Ring)", Float) = 4.87
        _HalfLength ("Полудлина трубки в метрах (для Vertical)", Float) = 25
        _EndFade    ("Затухание у концов, м", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "Energy"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _MODE_VERTICAL _MODE_RING
            #pragma multi_compile_local _AXIS_Y _AXIS_X _AXIS_Z _AXIS_CUSTOM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float _Axis;
                float4 _CustomAxis;
                float4 _PivotOffset;
                half4 _CoreColor, _MidColor, _EdgeColor;
                float _ThreadWidth;
                float _Intensity, _BaseGlow, _Speed, _Spacing, _PacketLen, _Density, _TailPow;
                float _Filament, _Spin, _Flicker, _RingRadius, _HalfLength, _EndFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  phase      : TEXCOORD3;
                nointerpolation float3 objScale : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
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
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS   = IN.normalOS;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);

                float4x4 m = GetObjectToWorldMatrix();
                OUT.objScale = float3(
                    length(float3(m[0].x, m[1].x, m[2].x)),
                    length(float3(m[0].y, m[1].y, m[2].y)),
                    length(float3(m[0].z, m[1].z, m[2].z))
                );

                float3 pivot = TransformObjectToWorld(float3(0, 0, 0));
                OUT.phase = Hash11(pivot.x * 1.73 + pivot.y * 0.37 + pivot.z * 2.31);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                const float TAU = 6.2831853;
                float t = _Time.y;

                // Выбираем ось течения и автоматически строим ортогональную плоскость U-V
                #if defined(_AXIS_X)
                    float3 axisDir = float3(1.0, 0.0, 0.0);
                #elif defined(_AXIS_Z)
                    float3 axisDir = float3(0.0, 0.0, 1.0);
                #elif defined(_AXIS_CUSTOM)
                    float3 axisDir = normalize(_CustomAxis.xyz + 1e-6);
                #else
                    float3 axisDir = float3(0.0, 1.0, 0.0);
                #endif

                float3 upRef  = abs(axisDir.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 planeU = normalize(cross(upRef, axisDir));
                float3 planeV = cross(axisDir, planeU);

                float3 scale = max(IN.objScale, 1e-5);
                float3 p     = (IN.positionOS - _PivotOffset.xyz) * scale;

                float pu = dot(p, planeU);
                float pv = dot(p, planeV);
                float h  = dot(p, axisDir);

                float spacing = max(_Spacing, 1e-3);
                float u, tubeAng, endFade = 1.0, N = 1e6;

                #if defined(_MODE_RING)
                    float ringScale  = 0.5 * (length(planeU * scale) + length(planeV * scale));
                    float ringRadius = _RingRadius * ringScale;
                    float ang        = atan2(pv, pu + 1e-6);
                    float r          = length(float2(pu, pv)) - ringRadius;
                    tubeAng          = atan2(h, r + 1e-6);
                    N                = max(1.0, round(TAU * ringRadius / spacing));
                    u                = ang / TAU * N;
                #else
                    tubeAng          = atan2(pv, pu + 1e-6);
                    u                = h / spacing;
                    endFade          = smoothstep(0.0, max(_EndFade, 1e-3), _HalfLength - abs(h));
                #endif

                u += IN.phase * 10.0 - t * _Speed / spacing;

                // --- импульсы-кометы ---
                float cell = floor(u);
                #if defined(_MODE_RING)
                    cell -= N * floor(cell / N);
                #endif

                float h1 = Hash11(cell * 1.37 + IN.phase * 17.0);
                float h2 = Hash11(cell * 2.11 + 5.3 + IN.phase * 3.0);

                float f     = frac(u);
                float len   = max(_PacketLen * (0.7 + 0.6 * h2), 1e-4);
                float g     = saturate((f - (1.0 - len)) / len);
                float shape = pow(max(g, 1e-5), _TailPow) * (1.0 - smoothstep(0.9, 1.0, g));
                float pkt   = shape * (0.55 + 0.45 * h2) * step(h1, _Density);

                // --- турбулентность ---
                float turb = saturate(0.5
                    + 0.25 * sin(u * TAU * 3.0 + tubeAng * 2.0 + t * 5.0 + h2 * 6.0)
                    + 0.25 * sin(u * TAU * 5.0 - tubeAng * 3.0 - t * 7.0));

                // --- радиальный профиль с яркой сердцевиной ---
                float3 V_WS       = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 viewScaled = normalize(TransformWorldToObjectDir(V_WS) * scale + 1e-6);
                float3 normScaled = normalize(IN.normalOS / scale);

                #if defined(_MODE_RING)
                    float2 rad2D         = normalize(float2(pu, pv) + 1e-6);
                    float3 tangentScaled = normalize(-rad2D.y * planeU + rad2D.x * planeV);
                #else
                    float3 tangentScaled = axisDir;
                #endif

                float nvRaw = saturate(dot(normScaled, viewScaled));
                float vt    = dot(viewScaled, tangentScaled);
                float sinVT = sqrt(saturate(1.0 - vt * vt));
                float nv    = saturate(nvRaw / max(sinVT, 0.25));
                float rho   = sqrt(saturate(1.0 - nv * nv));

                float w        = max(_ThreadWidth, 0.05);
                float profBase = exp(-(rho * rho) / (w * w));
                float profPkt  = saturate(1.0 - rho / lerp(min(w * 1.5, 1.0), 1.0, pkt));
                profPkt *= profPkt;

                // --- спиральные прожилки вокруг сердцевины ---
                float filWave = saturate(0.5 + 0.5 * sin(tubeAng * 3.0 + u * TAU + t * _Spin));
                float fil     = pow(max(filWave, 1e-5), 4.0);
                fil *= smoothstep(0.1, 0.5, rho) * (1.0 - smoothstep(0.7, 1.0, rho));

                float energy = _BaseGlow * profBase * (0.4 + 0.6 * turb)
                             + pkt * (profPkt * (0.6 + 0.4 * turb) + _Filament * fil * profPkt);

                energy *= 1.0 + _Flicker * sin(t * 17.0 + IN.phase * 40.0) * sin(t * 5.3 + h1 * 9.0);
                energy *= endFade;

                // --- объёмный градиент + HDR для Bloom ---
                float tt  = saturate(energy * 1.2);
                half3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.5, tt));
                col       = lerp(col, _CoreColor.rgb, smoothstep(0.5, 1.0, tt));
                col      *= (1.0 - exp(-energy * 2.0)) * _Intensity;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}