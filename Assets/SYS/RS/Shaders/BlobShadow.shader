// RE:AL STEEL - 원형 그림자 셰이더 (URP)
//
// RSBlobShadow 가 만드는 '땅에 붙은 격자 메시'를 그린다.
//  · 모양은 메시가 아니라 픽셀마다 계산한다: 월드 x · z 가 그림자 중심에서 얼마나 떨어졌나 (카메라 좌우로 늘인 타원)
//  · 픽셀 맞추기: 월드 x · z 를 1/밀도 격자에 맞춰 끊어서 → 가장자리가 지형 텍스처 도트와 같은 크기로 끊긴다
//  · 단계: 진하기를 몇 계단으로 끊는다 (픽셀아트식)
//  · 곱하기 블렌드 → 바닥 색은 살고 어두워지기만 한다. 정점 알파 = 턱 아래로 떨어진 점 지우기
//  · 안개(Unity Fog)를 받는다: 멀수록 그림자가 안개에 묻힌다
//
// 값은 전부 RSBlobShadow 가 MaterialPropertyBlock 으로 넣는다.
// Unity 6000.0.x / URP 17.x 기준.

Shader "RE_AL STEEL/Blob Shadow"
{
    Properties
    {
        _BlobCenter  ("중심 (월드)", Vector) = (0, 0, 0, 0)
        _BlobAxis    ("좌우 축 xy · 1/반지름 zw", Vector) = (1, 0, 3, 3)
        _BlobParams  ("진하기 · 부드러움 · 단계 · 픽셀밀도", Vector) = (0.55, 0.45, 3, 32)
        _BlobParams2 ("예비", Vector) = (0, 0, 0, 0)
        _BlobColor   ("색", Color) = (0.12, 0.10, 0.20, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent-100"   // 불투명 · 캐릭터(AlphaTest) 다음, 물 · 안개 상자보다 먼저
            "RenderType"     = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend DstColor Zero      // 곱하기
            ZWrite Off
            ZTest LEqual             // 캐릭터 · 소품이 앞에 있으면 가려진다
            Cull Off
            Offset -1, -1            // 땅과 같은 높이에서 깜빡이지 않게

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   BlobVert
            #pragma fragment BlobFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BlobCenter;
                float4 _BlobAxis;
                float4 _BlobParams;
                float4 _BlobParams2;
                half4  _BlobColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half   alpha      : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings BlobVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.alpha = IN.color.a;
                OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 BlobFrag(Varyings IN) : SV_Target
            {
                float2 xz = IN.positionWS.xz;

                // 픽셀 맞추기 (월드 격자 칸의 가운데 값으로)
                float ppm = _BlobParams.w;
                if (ppm > 0.5) xz = (floor(xz * ppm) + 0.5) / ppm;

                // 카메라 좌우 축 기준 타원 거리 (1 = 가장자리)
                float2 d   = xz - _BlobCenter.xz;
                float2 rgt = _BlobAxis.xy;
                float2 fwd = float2(-rgt.y, rgt.x);
                float2 q   = float2(dot(d, rgt) * _BlobAxis.z, dot(d, fwd) * _BlobAxis.w);
                float  len = length(q);

                float soft = max(_BlobParams.y, 0.01);
                float a = 1.0 - smoothstep(1.0 - soft, 1.0, len);

                // 계단
                float bands = _BlobParams.z;
                if (bands > 0.5) a = floor(a * bands + 0.5) / bands;

                a *= _BlobParams.x * IN.alpha;
                clip(a - 0.002);

                half3 c = lerp(half3(1, 1, 1), _BlobColor.rgb, (half)a);

                // 안개: 곱하기라 '안개 색과 섞기' 대신 '1 쪽으로 되돌리기'
            #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                half vis = (half)ComputeFogIntensity(IN.fogFactor);
                c = lerp(half3(1, 1, 1), c, vis);
            #endif

                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
