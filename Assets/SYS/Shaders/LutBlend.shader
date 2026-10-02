// RE:AL STEEL - LUT 두 장 섞기 (색감 컴포넌트가 시간대 사이 LUT 를 부드럽게 넘기려고 쓴다)
// LUT 형식: 가로 = 크기², 세로 = 크기 (예: 1024 x 32). 칸 안 가로 = 빨강, 세로 = 초록, 칸 번호 = 파랑.
Shader "Hidden/RE_AL STEEL/LUT Blend"
{
    Properties
    {
        _LutA ("LUT A", 2D) = "white" {}
        _LutB ("LUT B", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LutA); SAMPLER(sampler_LutA);
            TEXTURE2D(_LutB); SAMPLER(sampler_LutB);
            float4 _Params;   // x = A 세기, y = B 세기, z = 섞는 비율 (0 = A), w = LUT 크기

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float size = max(2.0, _Params.w);
                float x = floor(IN.uv.x * size * size);
                float y = floor(IN.uv.y * size);
                float slice = floor(x / size);
                float3 neutral = float3(x - slice * size, y, slice) / (size - 1.0);

                float3 a = lerp(neutral, SAMPLE_TEXTURE2D(_LutA, sampler_LutA, IN.uv).rgb, saturate(_Params.x));
                float3 b = lerp(neutral, SAMPLE_TEXTURE2D(_LutB, sampler_LutB, IN.uv).rgb, saturate(_Params.y));
                return half4(lerp(a, b, saturate(_Params.z)), 1.0);
            }
            ENDHLSL
        }
    }
}
