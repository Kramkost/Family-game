Shader "Custom/StylizedSkybox"
{
    Properties
    {
        _TopColor ("Top Color (Зенит)", Color) = (0.05, 0.05, 0.15, 1)
        _HorizonColor ("Horizon Color (Горизонт)", Color) = (0.2, 0.3, 0.4, 1)
        _BottomColor ("Bottom Color (Земля)", Color) = (0.02, 0.02, 0.05, 1)
        _Transition ("Sharpness (Резкость перехода)", Range(0.1, 10)) = 2.0
    }
    SubShader
    {
        
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0; 
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            float _Transition;

            fixed4 frag (v2f i) : SV_Target
            {
               
                float3 dir = normalize(i.texcoord);

                
                float p = pow(abs(dir.y), _Transition);

                fixed4 skyColor;
                if (dir.y > 0)
                {
                   
                    skyColor = lerp(_HorizonColor, _TopColor, p);
                }
                else
                {
                   
                    skyColor = lerp(_HorizonColor, _BottomColor, p);
                }

                return skyColor;
            }
            ENDCG
        }
    }
}