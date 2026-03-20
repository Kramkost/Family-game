Shader "Kotenkoff/BreathingHeightFog"
{
    Properties
    {
        // Скрытая текстура экрана, которую Unity передает автоматически
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {}
        
        [Header(Fog Appearance)]
        _FogColor ("Fog Color", Color) = (0.1, 0.2, 0.3, 1.0)
        _FogDensity ("Distance Density (Depth)", Range(0, 0.1)) = 0.02
        _MaxFogOpacity ("Max Opacity (Preserves Outlines)", Range(0, 1)) = 0.95
        
        [Header(Height Settings)]
        _HeightStart ("Height Dense (Y)", Float) = 0.0
        _HeightEnd ("Height Clear (Y)", Float) = 10.0
        _HeightDensity ("Height Fog Density", Range(0, 2)) = 1.0
        
        [Header(Breathing Animation)]
        _BreathSpeed ("Breathing Speed", Float) = 1.5
        _BreathAmplitude ("Breathing Amplitude", Range(0, 1)) = 0.2
    }
    SubShader
    {
        // Настройки пост-процесса: игнорируем геометрию, рисуем поверх всего
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // Глобальные текстуры экрана и глубины
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            
            // Матрица из нашего C# скрипта
            float4x4 _InverseViewProj;

            float4 _FogColor;
            float _FogDensity;
            float _MaxFogOpacity;
            
            float _HeightStart;
            float _HeightEnd;
            float _HeightDensity;
            
            float _BreathSpeed;
            float _BreathAmplitude;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                
                fixed4 originalColor = tex2D(_MainTex, i.uv);

                
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float linearDepth = LinearEyeDepth(rawDepth);

               
                if (linearDepth > 999.0) return originalColor;

                
                #if defined(UNITY_REVERSED_Z)
                    float clipDepth = rawDepth;
                #else
                    float clipDepth = rawDepth * 2.0 - 1.0;
                #endif
                
                float4 clipPos = float4(i.uv.x * 2.0 - 1.0, i.uv.y * 2.0 - 1.0, clipDepth, 1.0);
                float4 worldPos = mul(_InverseViewProj, clipPos);
                worldPos /= worldPos.w; 

              
                float distanceFog = 1.0 - exp(-linearDepth * _FogDensity);

                
                float heightFactor = smoothstep(_HeightEnd, _HeightStart, worldPos.y);
                float heightFog = heightFactor * _HeightDensity;

                
                float breath = 1.0 + (sin(_Time.y * _BreathSpeed) * _BreathAmplitude);

                
                float finalFogFactor = saturate((distanceFog + heightFog) * breath);
                
                
                finalFogFactor = min(finalFogFactor, _MaxFogOpacity);

                
                return lerp(originalColor, _FogColor, finalFogFactor);
            }
            ENDCG
        }
    }
}