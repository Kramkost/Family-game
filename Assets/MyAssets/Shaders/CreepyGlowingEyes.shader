Shader "Custom/CreepyGlowingEyes"
{
    Properties
    {
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 0, 0, 1)
        _PulseSpeed ("Pulse Speed", Float) = 3.0
        _PulseMinMax ("Pulse Min (X) / Max (Y)", Vector) = (0.2, 1.2, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _EmissionColor;
            float _PulseSpeed;
            float4 _PulseMinMax;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float pulseFactor = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float intensity = lerp(_PulseMinMax.x, _PulseMinMax.y, pulseFactor);

                float3 finalRGB = _EmissionColor.rgb * intensity;
                
                return fixed4(finalRGB, 1.0);
            }
            ENDCG
        }
    }
}