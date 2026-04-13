Shader "Hidden/RetroPostProcess"


Shader "UI/RetroEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Retro Settings)]
        _PosterizationSteps ("Posterization Steps", Range(2, 16)) = 8
        _Saturation ("Saturation", Range(-1, 1)) = -0.4
        _GrainIntensity ("Grain Intensity", Range(0, 0.2)) = 0.08
        _AberrationStrength ("Aberration Strength", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            float _PosterizationSteps;
            float _Saturation;
            float _GrainIntensity;
            float _AberrationStrength;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                
                // ВАЖНО: Просто передаем UV, никакого дрожания вершин здесь быть не должно!
                OUT.texcoord = v.texcoord; 
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Быстрый шум
            float hash(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. Хроматическая аберрация
                float2 distFromCenter = uv - 0.5;
                float2 offset = distFromCenter * _AberrationStrength;
                
                float r = tex2D(_MainTex, uv + offset).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - offset).b;
                float3 col = float3(r, g, b);

                // 2. Зерно (Шум)
                float noise = hash(uv + frac(_Time.y * 10.0));
                col += (noise - 0.5) * _GrainIntensity;

                // 3. Десатурация (Обесцвечивание)
                float3 grayscale = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(grayscale, col, 1.0 + _Saturation);

                // 4. Постеризация
                float steps = max(2.0, _PosterizationSteps);
                col = floor(col * steps) / steps;
                
                // Смешиваем с цветом RawImage (если нужен Tint)
                half4 finalColor = half4(saturate(col), 1.0) * IN.color;
                
                return finalColor;
            }
            ENDCG
        }
    }
}