Shader "Custom/StereoVideoShader"
{
    Properties
    {
        _LeftEyeTex ("Left Eye Texture", 2D) = "white" {}
        _RightEyeTex ("Right Eye Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        // 默认不透明渲染（RenderQueue在代码中动态设置）
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // 当需要透明时，ZWrite关闭，开启混合
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

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
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _LeftEyeTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // 根据当前眼睛选择纹理
                // unity_StereoEyeIndex: 0 = 左眼, 1 = 右眼
                fixed4 col;

                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    if (unity_StereoEyeIndex == 0)
                    {
                        // 左眼显示左眼纹理
                        col = tex2D(_LeftEyeTex, i.uv);
                    }
                    else
                    {
                        // 右眼显示右眼纹理
                        col = tex2D(_RightEyeTex, i.uv);
                    }
                #else
                    // 非VR模式，默认显示左眼
                    col = tex2D(_LeftEyeTex, i.uv);
                #endif

                // 应用颜色和透明度（_Color.a控制整体透明度）
                col *= _Color;

                return col;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
