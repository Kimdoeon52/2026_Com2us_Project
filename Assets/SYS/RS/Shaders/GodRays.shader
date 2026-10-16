// RE:AL STEEL - 갓레이 (화면 공간 빛살)
//
// 해(또는 해 반대점)를 향해 화면을 따라 걸으며 '빛이 나오는 곳'(하늘 · 밝게 빛나는 곳)을 모아,
// 건물 · 나무 윤곽 사이로 퍼지는 빛살을 더한다 (크라이시스식 화면 공간 갓레이).
//  · 해가 화면 안 · 근처: 해에서 퍼지는 빛살
//  · 해가 카메라 뒤(탑다운에서 흔함): 해 반대점으로 모이는 빛살 (반대 빛살, 세기 따로)
//  · 화면 전체 삼각형 하나를 투명 큐 맨 뒤(안개 다음)에 더하기로 그린다 — 렌더러 기능 불필요
// 필요: URP 에셋 Opaque Texture · Depth Texture. 그리는 쪽: RSGodRays 컴포넌트
Shader "RE_AL STEEL/God Rays (Screen)"
{
    Properties
    {
        [HideInInspector] _RayColor   ("빛 색 × 세기", Color) = (1, 0.9, 0.7, 1)
        [HideInInspector] _Samples    ("샘플 수", Float) = 24
        [HideInInspector] _Length     ("길이 (해까지 비율)", Float) = 0.6
        [HideInInspector] _Decay      ("감쇠", Float) = 0.94
        [HideInInspector] _Threshold  ("밝기 문턱", Float) = 0.8
        [HideInInspector] _Weights    ("x 하늘, y 밝은 곳, z 화면 밖 페이드, w 반대 빛살", Vector) = (1, 0.6, 0.6, 0.5)
        [HideInInspector] _DitherPixel ("디더 칸", Float) = 1
        [HideInInspector] _DebugView ("빛살만 보기", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GodRays"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GRVert
            #pragma fragment GRFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _RayColor;
                float  _Samples;
                float  _Length;
                float  _Decay;
                float  _Threshold;
                float4 _Weights;
                float  _DitherPixel;
                float  _DebugView;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            // 메시는 (-1,-1) (3,-1) (-1,3) 삼각형 하나 — 그대로 화면 전체를 덮는다
            Varyings GRVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = float4(IN.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                return OUT;
            }

            float IGN(float2 p) { return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))); }

            // 빛이 나오는 정도: 하늘(먼 깊이) + 밝은 곳(문턱 넘는 밝기)
            float RayMask(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv), 0).r;
            #if UNITY_REVERSED_Z
                float sky = raw <= 1e-5 ? 1.0 : 0.0;
            #else
                float sky = raw >= 1.0 - 1e-5 ? 1.0 : 0.0;
            #endif
                half3 c = SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, UnityStereoTransformScreenSpaceTex(uv), 0).rgb;
                float lum = dot(c, half3(0.299, 0.587, 0.114));
                float bright = smoothstep(_Threshold, _Threshold + 0.35, lum);
                return sky * _Weights.x + bright * _Weights.y;
            }

            half4 GRFrag(Varyings IN) : SV_Target
            {
                if (_RayColor.a <= 0.0) return 0;
                float2 uv = GetNormalizedScreenSpaceUV(IN.positionCS);

                // 해가 화면 어디에 있나 (카메라 뒤면 해 반대점)
                float3 L = _MainLightPosition.xyz;
                float4 sc = TransformWorldToHClip(_WorldSpaceCameraPos + L * 1000.0);
                float side = 1.0;
                if (sc.w <= 0.0)
                {
                    if (_Weights.w <= 0.0) return 0;
                    sc = TransformWorldToHClip(_WorldSpaceCameraPos - L * 1000.0);
                    side = _Weights.w;
                    if (sc.w <= 0.0) return 0;
                }
                float4 sp4 = ComputeScreenPos(sc);
                float2 sp = sp4.xy / sp4.w;

                // 해(점)가 화면 밖으로 멀어질수록 옅게
                float2 off = max(abs(sp - 0.5) - 0.5, 0.0);
                float vis = saturate(1.0 - length(off) / max(0.01, _Weights.z)) * side;
                if (vis <= 0.0) return 0;

                int n = (int)clamp(_Samples, 4.0, 64.0);
                float2 delta = (sp - uv) * (_Length / n);
                float2 p = uv + delta * IGN(floor(IN.positionCS.xy / max(1.0, _DitherPixel)));

                float w = 1.0, sum = 0.0, wsum = 0.0;
                [loop]
                for (int i = 0; i < 64; i++)
                {
                    if (i >= n) break;
                    if (any(p < 0.0) || any(p > 1.0)) break;
                    sum += RayMask(p) * w;
                    wsum += w;
                    w *= _Decay;
                    p += delta;
                }
                float rays = wsum > 0.0 ? sum / wsum : 0.0;
                if (_DebugView > 0.5) return half4(rays * vis * 2.0, rays * vis * 0.5, 0.0, 0.0);   // 조절용: 빛살을 주황으로 크게
                return half4(_RayColor.rgb * (rays * vis), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
