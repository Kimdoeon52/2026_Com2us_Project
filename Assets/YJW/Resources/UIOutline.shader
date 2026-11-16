// Sprites/Outline의 UI용 버전. Image·RawImage의 Material 칸에 넣어 쓴다.
// UI 기본 셰이더(UI/Default)처럼 Mask(스텐실)와 RectMask2D(클립)를 지원하고, 외곽선 값은 머티리얼 인스펙터에서 바꾼다
Shader "UI/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Toggle] _Outline ("Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        [IntRange] _OutlineSize ("Outline Size", Range(1, 16)) = 1
        // Outer = 그림 바깥쪽만, Inner = 그림 안쪽만, Both = 둘 다
        [Enum(Outer,0,Inner,1,Both,2)] _OutlineMode ("Outline Mode", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Outline;
            fixed4 _OutlineColor;
            float _OutlineSize;
            float _OutlineMode;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 상하좌우로 size칸까지 본 알파. 바깥쪽은 최댓값, 안쪽은 곱을 쓴다
            void SampleAround(float2 uv, int size, out float maxAlpha, out float productAlpha)
            {
                maxAlpha = 0;
                productAlpha = 1;
                float2 texel = _MainTex_TexelSize.xy;

                [unroll(16)]
                for (int i = 1; i <= 16; i++)
                {
                    if (i > size)
                        break;

                    float up    = tex2D(_MainTex, uv + float2(0, i * texel.y)).a;
                    float down  = tex2D(_MainTex, uv - float2(0, i * texel.y)).a;
                    float right = tex2D(_MainTex, uv + float2(i * texel.x, 0)).a;
                    float left  = tex2D(_MainTex, uv - float2(i * texel.x, 0)).a;

                    maxAlpha = max(maxAlpha, max(max(up, down), max(right, left)));
                    productAlpha *= up * down * right * left;
                }
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                if (_Outline > 0.5)
                {
                    int mode = (int)_OutlineMode;
                    float maxAlpha, productAlpha;
                    SampleAround(IN.texcoord, (int)_OutlineSize, maxAlpha, productAlpha);

                    // 외곽선도 Image 색 알파(CanvasGroup 페이드 포함)를 따라간다
                    fixed4 outline = fixed4(_OutlineColor.rgb, _OutlineColor.a * IN.color.a);

                    // 바깥쪽: 지금 픽셀은 비었는데 주변에 그림이 있으면 채운다
                    if (mode != 1 && color.a <= 0 && maxAlpha > 0)
                        color = outline;
                    // 안쪽: 지금 픽셀은 그림인데 주변에 빈 곳이 있으면 테두리로 칠한다
                    else if (mode != 0 && color.a > 0 && productAlpha <= 0)
                        color = outline;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}
