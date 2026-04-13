Shader "Kotenkoff/LowPolyHurricane"
{
    Properties
    {
        [Header(Main Settings)]
        _MainTex ("Cloud Noise Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (0.3, 0.4, 0.5, 1.0)
        [HDR] _Emission ("Emission Tint", Color) = (0, 0, 0, 1)
        
        [Header(Vertex Animation)]
        _WobbleStrength ("Wobble Strength", Float) = 0.5
        _WobbleSpeed ("Wobble Speed", Float) = 3.0
        _Jaggedness ("Mesh Jaggedness", Range(0, 1)) = 0.2
        
        [Header(Fragment Shading)]
        _RotationSpeed ("Spiral Rotation Speed", Float) = 1.5
        _BandingSteps ("Posterization Steps", Range(1, 10)) = 4.0
        
        [Header(Stylized Outlines)]
        _RimColor ("Rim Glow Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.1, 1.0)) = 0.4
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

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
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Emission)
                UNITY_DEFINE_INSTANCED_PROP(float, _WobbleStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _WobbleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _Jaggedness)
                UNITY_DEFINE_INSTANCED_PROP(float, _RotationSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _BandingSteps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _RimPower)
            UNITY_INSTANCING_BUFFER_END(Props)

            float hash(float3 pos) 
            {
                return frac(sin(dot(pos, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float wobbleStr = UNITY_ACCESS_INSTANCED_PROP(Props, _WobbleStrength);
                float wobbleSpd = UNITY_ACCESS_INSTANCED_PROP(Props, _WobbleSpeed);
                float jaggedness = UNITY_ACCESS_INSTANCED_PROP(Props, _Jaggedness);

               
                float randomOffset = (hash(v.vertex.xyz) * 2.0 - 1.0); 
                float3 displacedPos = v.vertex.xyz + (v.normal * randomOffset * jaggedness * v.vertex.y);

               
                float swayX = sin(_Time.y * wobbleSpd + displacedPos.y) * wobbleStr * displacedPos.y;
                float swayZ = cos(_Time.y * (wobbleSpd * 0.8) + displacedPos.y) * wobbleStr * displacedPos.y;
                
                displacedPos.x += swayX;
                displacedPos.z += swayZ;

                o.pos = UnityObjectToClipPos(float4(displacedPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(float4(displacedPos, 1.0));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 emission = UNITY_ACCESS_INSTANCED_PROP(Props, _Emission);
                float rotSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _RotationSpeed);
                float steps = UNITY_ACCESS_INSTANCED_PROP(Props, _BandingSteps);
                float4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                float rimPower = UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower);


                float2 spiralUV = i.uv;
                spiralUV.x += spiralUV.y * 1.5; 
                spiralUV.x -= _Time.y * rotSpeed;
                spiralUV.y -= _Time.y * (rotSpeed * 0.5);

                float rawNoise = tex2D(_MainTex, spiralUV).r;
                float rawAlpha = rawNoise * baseColor.a;

          
                float bandedAlpha = floor(rawAlpha * steps) / steps;
                
                clip(bandedAlpha - 0.05);

           
                float3 nNormal = normalize(i.worldNormal);
                float3 nViewDir = normalize(i.viewDir);
                
                float NdotV = 1.0 - saturate(dot(nNormal, nViewDir));
                float rim = smoothstep(1.0 - rimPower, 1.0 - rimPower + 0.05, NdotV);

                fixed3 finalColor = (baseColor.rgb * rawNoise) + emission.rgb + (rimColor.rgb * rim);

                return fixed4(finalColor, bandedAlpha);
            }
            ENDCG
        }
    }
}