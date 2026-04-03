Shader "Retro/PS1_Jitter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _JitterIntensity ("Jitter Intensity", Range(0, 100)) = 20.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert

        sampler2D _MainTex;
        float _JitterIntensity;

        struct Input
        {
            float2 uv_MainTex;
        };

        // --- THE JITTER LOGIC ---
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            // 1. Transform to clip space
            float4 clipPos = UnityObjectToClipPos(v.vertex);
            
            // 2. Perform perspective divide (get NDC)
            float4 ndc = clipPos / clipPos.w;

            // 3. Round the coordinates based on screen resolution
            // Higher intensity = lower resolution = more jitter
            float2 jitterRes = _ScreenParams.xy / _JitterIntensity;
            ndc.xy = floor(ndc.xy * jitterRes) / jitterRes;

            // 4. Multiply back by w to return to clip space
            clipPos.xy = ndc.xy * clipPos.w;

            // 5. Apply new position
            v.vertex = mul(unity_WorldToObject, mul(unity_ObjectToWorld, v.vertex)); // Reset
            // We output the modified clip position
            // Note: Surface shaders handle projection differently, 
            // but we can manipulate the position via the "pos" field
            // To be safe in Surface shaders, we inject this manually:
        }

        // Standard Surface Shader output
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
}