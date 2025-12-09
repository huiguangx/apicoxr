Shader "Custom/StereoVideoShader"
{
    Properties
    {
        _LeftEyeTex ("Left Eye Texture", 2D) = "white" {}
        _RightEyeTex ("Right Eye Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _LeftEyeTex;
            sampler2D _RightEyeTex;
            float4 _LeftEyeTex_ST;
            float4 _RightEyeTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _LeftEyeTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                fixed4 col;

                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    if (unity_StereoEyeIndex == 0)
                    {
                        col = tex2D(_LeftEyeTex, i.uv);
                    }
                    else
                    {
                        col = tex2D(_RightEyeTex, i.uv);
                    }
                #else
                    col = tex2D(_LeftEyeTex, i.uv);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
