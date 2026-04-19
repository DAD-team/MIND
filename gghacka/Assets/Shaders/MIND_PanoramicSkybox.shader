Shader "MIND/Panoramic Skybox"
{
    Properties
    {
        _MainTex ("Panoramic Texture (HDR)", 2D) = "white" {}
        [NoScaleOffset] _MainTex_HDR ("Decode Instructions", Vector) = (0, 0, 0, 0)
        _Rotation ("Horizontal Rotation", Range(0, 360)) = 0
        _Pitch ("Vertical Pitch", Range(-90, 90)) = 0
        _Exposure ("Exposure", Range(0.1, 8)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            float _Rotation;
            float _Pitch;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Rotation matrix quanh truc Y (horizontal)
            float3 RotateY(float3 v, float angleDeg)
            {
                float rad = angleDeg * UNITY_PI / 180.0;
                float s = sin(rad);
                float c = cos(rad);
                return float3(
                    v.x * c - v.z * s,
                    v.y,
                    v.x * s + v.z * c
                );
            }

            // Rotation matrix quanh truc X (vertical pitch)
            float3 RotateX(float3 v, float angleDeg)
            {
                float rad = angleDeg * UNITY_PI / 180.0;
                float s = sin(rad);
                float c = cos(rad);
                return float3(
                    v.x,
                    v.y * c - v.z * s,
                    v.y * s + v.z * c
                );
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);

                // Xoay huong nhin: pitch (doc) truoc, rotation (ngang) sau
                float3 dir = v.vertex.xyz;
                dir = RotateX(dir, _Pitch);
                dir = RotateY(dir, _Rotation);
                o.texcoord = dir;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.texcoord);

                // Equirectangular mapping (lat-long)
                float2 uv;
                uv.x = atan2(dir.z, dir.x) / (2.0 * UNITY_PI) + 0.5;
                uv.y = asin(dir.y) / UNITY_PI + 0.5;

                half4 tex = tex2D(_MainTex, uv);

                // HDR decode
                half3 color = DecodeHDR(tex, _MainTex_HDR);
                color *= _Exposure;

                return half4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
