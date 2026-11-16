Shader "Sprites/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [PerRendererData] _Outline("Outline", Float) = 0
        [PerRendererData] _OutlineColor("Outline Color", Color) = (1,1,1,1)
        [PerRendererData] _OutlineSize("Outline Size", int) = 1
        // 0 = 바깥쪽으로만, 1 = 안쪽으로만, 2 = 양쪽 다
        [PerRendererData] _OutlineMode("Outline Mode", int) = 0
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
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _Outline;
            fixed4 _OutlineColor;
            int _OutlineSize;
            int _OutlineMode;
            float4 _MainTex_TexelSize;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                if (_Outline > 0)
                {
                    // 바깥쪽: 지금 픽셀은 비어있는데 주변에 그림이 있으면 채운다
                    if ((_OutlineMode == 0 || _OutlineMode == 2) && c.a == 0)
                    {
                        float maxAlpha = 0;
                        [unroll(16)]
                        for (int i = 1; i < _OutlineSize + 1; i++)
                        {
                            fixed4 pixelUp    = tex2D(_MainTex, IN.texcoord + fixed2(0, i * _MainTex_TexelSize.y));
                            fixed4 pixelDown  = tex2D(_MainTex, IN.texcoord - fixed2(0, i * _MainTex_TexelSize.y));
                            fixed4 pixelRight = tex2D(_MainTex, IN.texcoord + fixed2(i * _MainTex_TexelSize.x, 0));
                            fixed4 pixelLeft  = tex2D(_MainTex, IN.texcoord - fixed2(i * _MainTex_TexelSize.x, 0));
                            maxAlpha = max(maxAlpha, max(max(pixelUp.a, pixelDown.a), max(pixelRight.a, pixelLeft.a)));
                        }
                        if (maxAlpha > 0)
                        {
                            c = _OutlineColor;
                            c.a = 1;
                        }
                    }
                    // 안쪽: 지금 픽셀은 그림이 있는데 주변에 빈 곳이 있으면 깎아낸다
                    else if ((_OutlineMode == 1 || _OutlineMode == 2) && c.a != 0)
                    {
                        float totalAlpha = 1.0;
                        [unroll(16)]
                        for (int i = 1; i < _OutlineSize + 1; i++)
                        {
                            fixed4 pixelUp    = tex2D(_MainTex, IN.texcoord + fixed2(0, i * _MainTex_TexelSize.y));
                            fixed4 pixelDown  = tex2D(_MainTex, IN.texcoord - fixed2(0, i * _MainTex_TexelSize.y));
                            fixed4 pixelRight = tex2D(_MainTex, IN.texcoord + fixed2(i * _MainTex_TexelSize.x, 0));
                            fixed4 pixelLeft  = tex2D(_MainTex, IN.texcoord - fixed2(i * _MainTex_TexelSize.x, 0));
                            totalAlpha = totalAlpha * pixelUp.a * pixelDown.a * pixelRight.a * pixelLeft.a;
                        }
                        if (totalAlpha == 0)
                        {
                            c.rgba = fixed4(1, 1, 1, 1) * _OutlineColor;
                        }
                    }
                }

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}