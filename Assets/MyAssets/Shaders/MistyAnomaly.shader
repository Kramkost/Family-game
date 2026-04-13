Shader "Custom/MistyAnomaly"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.7, 0.8, 0.9, 0.5)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.1
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0.2
        _FresnelPower ("Edge Fade (Fresnel Power)", Range(0.1, 10.0)) = 3.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _BaseColor;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float _ScrollSpeedX;
            float _ScrollSpeedY;
            float _FresnelPower;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Стандартные функции Built-in для трансформации
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
                
                // Получаем нормаль и направление взгляда в мировых координатах для Френеля
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDirWS = normalize(_WorldSpaceCameraPos - worldPos);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 scrollOffset = float2(_ScrollSpeedX, _ScrollSpeedY) * _Time.y;
                float2 scrolledUV = i.uv + scrollOffset;

                // Сэмпл текстуры шума
                fixed4 noiseVal = tex2D(_NoiseTex, scrolledUV);

                float3 normal = normalize(i.normalWS);
                float3 viewDir = normalize(i.viewDirWS);
                
                // abs() нужен, так как включен Cull Off (двусторонний рендер)
                float fresnel = saturate(abs(dot(normal, viewDir)));
                float edgeFade = pow(fresnel, _FresnelPower);

                fixed4 finalColor = _BaseColor;
                finalColor.a = _BaseColor.a * noiseVal.r * edgeFade;

                return finalColor;
            }
            ENDCG
        }
    }
}