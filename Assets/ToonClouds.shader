Shader "Kotenkoff/ToonClouds"
{
    Properties
    {
        [Header(Cloud Textures)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        
        [Header(Cloud Settings)]
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 0.9)
        _CloudDensity ("Cloud Density", Range(0, 1)) = 0.5
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.05
        
        [Header(Wind Animation)]
        _SpeedX ("Wind Speed X", Float) = 0.02
        _SpeedY ("Wind Speed Y", Float) = 0.01
        
        [Header(Morphing Effect)]
        _MorphSpeed ("Morph Speed", Float) = 0.015
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 uv : TEXCOORD0; 
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            
            fixed4 _CloudColor;
            float _CloudDensity;
            float _Softness;
            float _SpeedX;
            float _SpeedY;
            float _MorphSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Движение по ветру
                float2 windOffset = float2(_Time.y * _SpeedX, _Time.y * _SpeedY);
                o.uv.xy = TRANSFORM_TEX(v.uv, _NoiseTex) + windOffset;
                
                // Искажение формы
                float2 morphOffset = float2(_Time.y * -_MorphSpeed, _Time.y * _MorphSpeed * 0.5);
                o.uv.zw = TRANSFORM_TEX(v.uv, _NoiseTex) * 1.5 + morphOffset; 
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed noise1 = tex2D(_NoiseTex, i.uv.xy).r;
                fixed noise2 = tex2D(_NoiseTex, i.uv.zw).r;
                fixed combinedNoise = noise1 * noise2 * 2.0;

                float threshold = 1.0 - _CloudDensity;
                float alpha = smoothstep(threshold, threshold + _Softness, combinedNoise);

                fixed4 col = _CloudColor;
                col.a = alpha * _CloudColor.a;

                return col;
            }
            ENDCG
        }
    }
}