Shader "Custom/RetroUI_Fixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Render Texture", 2D) = "white" {}
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
        ZTest Always // Игнорируем всё и рисуем поверх экрана

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _PosterizationSteps;
            float _Saturation;
            float _GrainIntensity;
            float _AberrationStrength;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                return OUT;
            }

            float hash(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 1. Аберрация
                float2 distFromCenter = uv - 0.5;
                float2 offset = distFromCenter * _AberrationStrength;
                
                float r = tex2D(_MainTex, uv + offset).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - offset).b;
                float3 col = float3(r, g, b);

                // 2. Зерно
                float noise = hash(uv + frac(_Time.y * 10.0));
                col += (noise - 0.5) * _GrainIntensity;

                // 3. Десатурация
                float3 grayscale = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(grayscale, col, 1.0 + _Saturation);

                // 4. Постеризация
                float steps = max(2.0, _PosterizationSteps);
                col = floor(col * steps) / steps;
                
                // ЖЕСТКИЙ ВЫВОД: игнорируем IN.color и прозрачность Canvas.
                // Альфа всегда 1.0 (полностью непрозрачно).
                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
}