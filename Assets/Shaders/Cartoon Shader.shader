Shader "Kotenkoff/CartoonToonTexture" 
{
    Properties 
    {
        [Header(Base Textures)]
        _MainTex ("Texture (Albedo)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        
        [Header(Toon Lighting)]
        _ShadowColor ("Shadow Tint Color", Color) = (0.4, 0.4, 0.5, 1)
        _ToonThreshold ("Shadow Threshold", Range(-1, 1)) = 0.0
        _ToonSmoothness ("Shadow Smoothness", Range(0, 0.5)) = 0.05

        [Header(Toon Specular)]
        _HlColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Gloss ("Glossiness", Range(1, 256)) = 32
        _SpecSmoothness ("Specular Smoothness", Range(0, 0.5)) = 0.02

        [Header(Wind Animation)]
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.05
    }
    
    SubShader 
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // ДОБАВЛЕНО: vertex:vert указывает, что мы хотим манипулировать геометрией
        #pragma surface surf ToonSpecular fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _ShadowColor;
        float _ToonThreshold;
        float _ToonSmoothness;

        fixed4 _HlColor;
        float _Gloss;
        float _SpecSmoothness;

        // Переменные ветра
        float _WindSpeed;
        float _WindStrength;

        struct Input 
        {
            float2 uv_MainTex;
        };

        // --- ЛОГИКА ВЕТРА (Манипуляция вершинами) ---
        void vert (inout appdata_full v) 
        {
            // Берем высоту вершины (v.vertex.y). 
            // Корни (y = 0) не двигаются, макушка (y > 0) гнется сильно.
            float heightMask = max(0, v.vertex.y);
            
            // Генерируем волну на основе времени (_Time.y), мировых координат и скорости
            float wave = sin(_Time.y * _WindSpeed + v.vertex.x + v.vertex.z);
            
            // Сдвигаем вершины по X и Z
            v.vertex.x += wave * _WindStrength * heightMask;
            v.vertex.z += wave * _WindStrength * heightMask * 0.5; // Чуть меньше по Z для хаоса
        }

        // --- ОСВЕЩЕНИЕ (Без изменений) ---
        float4 LightingToonSpecular(SurfaceOutput s, float3 lightDir, float3 viewDir, float atten) 
        {
            float NdotL = dot(s.Normal, lightDir);
            float lightIntensity = smoothstep(_ToonThreshold - _ToonSmoothness, _ToonThreshold + _ToonSmoothness, NdotL);
            float4 diffuseColor = lerp(_ShadowColor, _LightColor0, lightIntensity);

            float3 halfDir = normalize(lightDir + viewDir);
            float NdotH = max(0, dot(s.Normal, halfDir));
            
            float specBase = pow(NdotH, _Gloss);
            float specIntensity = smoothstep(0.5 - _SpecSmoothness, 0.5 + _SpecSmoothness, specBase);
            float4 specColor = specIntensity * _HlColor * _LightColor0;

            float4 c;
            c.rgb = (s.Albedo * diffuseColor.rgb + specColor.rgb) * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o) 
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}