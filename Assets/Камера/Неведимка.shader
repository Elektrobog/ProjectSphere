Shader "Custom/RadiusTransparencyLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        // Параметры радиальной прозрачности
        _FadeCenter ("Fade Center", Vector) = (0,0,0,0)
        _FadeRadius ("Fade Radius", Float) = 1.0
        _MinAlpha ("Min Alpha", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        LOD 300

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _Color;
        float _Glossiness;
        float _Metallic;

        float3 _FadeCenter;
        float _FadeRadius;
        float _MinAlpha;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;

            // Metallic / Smoothness
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // --- Радиальная прозрачность ---
            float dist = distance(IN.worldPos, _FadeCenter);

            float alpha = 1.0;

            if (dist < _FadeRadius)
            {
                float fadeFactor = dist / _FadeRadius;
                alpha = lerp(_MinAlpha, 1.0, fadeFactor);
            }

            o.Alpha = alpha * c.a;
        }
        ENDCG
    }

    FallBack "Transparent"
}
