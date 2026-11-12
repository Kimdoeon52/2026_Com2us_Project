// 스프라이트 실루엣 + 거리 어둡게 (2026-11-12, 쿄다이 2D). URP Sprite-Unlit-Default 바탕.
// 그림 모양(투명도)은 그대로 두고 색만 어둡게 칠한다:
//   어둡기 = 기본 실루엣(_Silhouette) + 카메라에서 멀수록 더(_DepthStart → _DepthEnd 사이에서 _DepthStrength까지)
// SpriteRenderer.color는 어둡게 칠한 뒤에 곱한다. 색이 들어간 틴트(맞을 때 붉게 번쩍·정지 때 푸르게)는
// _TintThrough만큼 실루엣을 뚫고 보이게 — 흰색·회색 틴트는 그대로 어둡다.
Shader "NYH/Sprite Depth Shade"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShadeColor ("실루엣 색", Color) = (0.02, 0.02, 0.04, 1)
        _Silhouette ("기본 어둡기 (0 = 원래 그림, 1 = 완전 실루엣)", Range(0, 1)) = 0.7
        _DepthStart ("어두워지기 시작하는 거리 (카메라에서)", Float) = 11
        _DepthEnd ("가장 어두워지는 거리", Float) = 18
        _DepthStrength ("거리로 더 어두워지는 양", Range(0, 1)) = 0.3
        _TintThrough ("색 틴트(번쩍임)가 실루엣을 뚫는 정도", Range(0, 1)) = 1

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
            UNITY_SKINNED_VERTEX_INPUTS
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _ShadeColor;
            half _Silhouette;
            float _DepthStart;
            float _DepthEnd;
            half _DepthStrength;
            half _TintThrough;
        CBUFFER_END

        Varyings ShadeVertex(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            UNITY_SKINNED_VERTEX_COMPUTE(v);
            SetUpSpriteInstanceProperties();
            v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
            o.positionWS = TransformObjectToWorld(v.positionOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.uv = v.uv;
            o.color = v.color * _Color * unity_SpriteColor;
            return o;
        }

        half4 ShadeFragment(Varyings i) : SV_Target
        {
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

            // 카메라에서의 거리로 더 어둡게 (뒤에 있는 본체일수록 진하게)
            float dist = distance(i.positionWS, _WorldSpaceCameraPos);
            half far = saturate((dist - _DepthStart) / max(0.001, _DepthEnd - _DepthStart));
            half shade = saturate(_Silhouette + far * _DepthStrength);

            // 색 틴트(채도)가 있으면 그만큼 실루엣을 걷어낸다 — 맞을 때 번쩍임이 어둠에 묻히지 않게
            half3 c = i.color.rgb;
            half hi = max(c.r, max(c.g, c.b)), lo = min(c.r, min(c.g, c.b));
            half tint = hi > 0.001 ? 1 - lo / hi : 0;
            shade *= 1 - saturate(tint * 2) * _TintThrough;

            half3 rgb = lerp(tex.rgb, _ShadeColor.rgb, shade) * c;
            return half4(rgb, tex.a * i.color.a);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex ShadeVertex
            #pragma fragment ShadeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex ShadeVertex
            #pragma fragment ShadeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            ENDHLSL
        }
    }
}
