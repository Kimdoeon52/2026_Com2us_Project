// RE:AL STEEL - 나노봇 3페 글리치 오라 (URP, 빛 안 받음)
//
// RSNanoSwarm.Aura 가 칸마다 사각형 두 장을 메시로 만든다.
//   uv.z = 0 : 칸 (정사각형, 오른쪽 · 아래 1줄은 조금 어둡게 — 봇 칸과 같은 눈금 느낌)
//   uv.z = 1 : 번짐 (칸보다 큰 사각형, 가운데서 흐려지는 빛, 더하기)
// 섞기: 미리 곱한 알파 (One, OneMinusSrcAlpha) — 칸은 덮고, 번짐은 알파 0 이라 더하기가 된다.
// 봇(기사 픽셀)은 건드리지 않는다. 기사 바로 앞(z 조금 앞)에 따로 그린다.

Shader "RE_AL STEEL/NanoBot Glitch Aura"
{
    Properties
    {
        _Intensity ("밝기", Range(0.2, 6)) = 1.6
        _GlowStrength ("번짐 세기", Range(0, 2)) = 0.5
        _Gap ("칸 눈금 어둡기", Range(0, 1)) = 0.28
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Name "GlitchAura"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _GlowStrength;
                float _Gap;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv.xy;
                float a = i.color.a;
                if (i.uv.z > 0.5)
                {
                    float2 d = uv * 2.0 - 1.0;
                    float g = exp(-dot(d, d) * 4.0) * _GlowStrength * a;
                    return half4(i.color.rgb * g * _Intensity, 0.0);
                }
                // 칸 눈금: 오른쪽 · 아래 1/4 줄을 조금 어둡게 (봇 칸 4px 중 1px)
                float edge = (uv.x > 0.75 || uv.y < 0.25) ? (1.0 - _Gap) : 1.0;
                float3 c = i.color.rgb * _Intensity * edge;
                return half4(c * a, a);
            }
            ENDHLSL
        }
    }
}
