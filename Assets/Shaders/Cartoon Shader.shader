Shader "Kotenkoff/CartoonToonTexture_WithDirt" 
{
    Properties 
    {
        [Header(Base Textures)]
        _MainTex ("Texture (Albedo)", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        
        // --- DIRT SYSTEM START ---
        [Header(Dirt System)]
        _DirtTex ("Dirt Texture (Albedo)", 2D) = "brown" {} // Текстура самой грязи
        _DirtMask ("Dirt Mask (R)", 2D) = "white" {}     // ЧБ Маска, где грязь появится
        _DirtColor ("Dirt Tint", Color) = (0.5, 0.4, 0.3, 1) // Цвет грязи
        _DirtLevel ("Dirt Level", Range(0, 1)) = 0.0      // Главный параметр (0 - чисто, 1 - грязно)
        // --- DIRT SYSTEM END ---

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
        #pragma surface surf ToonSpecular fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        // --- DIRT SYSTEM START ---
        sampler2D _DirtTex;
        sampler2D _DirtMask;
        fixed4 _DirtColor;
        float _DirtLevel;
        // --- DIRT SYSTEM END ---

        fixed4 _ShadowColor;
        float _ToonThreshold;
        float _ToonSmoothness;

        fixed4 _HlColor;
        float _Gloss;
        float _SpecSmoothness;

        float _WindSpeed;
        float _WindStrength;

        struct Input 
        {
            float2 uv_MainTex;
            // --- DIRT SYSTEM START ---
            float2 uv_DirtTex; 
            // --- DIRT SYSTEM END ---
        };

        // --- ЛОГИКА ВЕТРА  ---
        void vert (inout appdata_full v) 
        {
            float heightMask = max(0, v.vertex.y);
            float wave = sin(_Time.y * _WindSpeed + v.vertex.x + v.vertex.z);
            v.vertex.x += wave * _WindStrength * heightMask;
            v.vertex.z += wave * _WindStrength * heightMask * 0.5;
        }

        // --- ОСВЕЩЕНИ---
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
            // Учитываем Альбедо с грязью в финальном освещении
            c.rgb = (s.Albedo * diffuseColor.rgb + specColor.rgb) * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o) 
        {
          
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // --- DIRT SYSTEM START ---
            
   
            fixed4 dirtColorSample = tex2D(_DirtTex, IN.uv_DirtTex) * _DirtColor;
            float dirtMaskSample = tex2D(_DirtMask, IN.uv_DirtTex).r; // Берем красный канал маски

   
 

            
            float dirtThreshold = 1.0 - _DirtLevel; // Переворачиваем: 1-чисто, 0-грязно
            float dirtAmount = smoothstep(dirtThreshold, dirtThreshold + 0.1, dirtMaskSample);
            
        
            fixed3 finalAlbedo = lerp(baseColor.rgb, dirtColorSample.rgb, dirtAmount);
            
     
            o.Albedo = finalAlbedo;
            
         
            o.Alpha = lerp(baseColor.a, 0.0, dirtAmount * 0.5); 
            
            // --- DIRT SYSTEM END ---
        }
        ENDCG
    }
    FallBack "Diffuse"
}