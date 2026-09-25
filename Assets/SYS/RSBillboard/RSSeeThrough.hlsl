// RE:AL STEEL - 시야 가림 투명 (RS 셰이더 공용)
//
// 캐릭터와 카메라 사이에 있는 벽 · 건물 · 절벽을 캐릭터 둘레만 디더 구멍으로 뚫는다.
// 값은 전부 RSSeeThrough 컴포넌트(캐릭터에 붙음)가 전역으로 넣어 준다. 컴포넌트가 없으면 아무 일도 안 한다.
//
// 뚫리는 조건 (셋 다 만족해야 뚫린다)
//  · 화면에서 캐릭터 둘레 원 안
//  · 캐릭터보다 카메라 쪽으로 '앞쪽 여유' 이상 가까움   → 캐릭터 뒤 배경은 안 뚫림
//  · 캐릭터 발보다 '바닥 여유' 이상 높음               → 캐릭터가 서 있는 바닥은 안 뚫림
// 그림자 패스에는 넣지 않았다 — 벽이 투명해져도 그림자는 그대로 남는다.
//
// Core.hlsl 뒤에 include 할 것.
#ifndef RS_SEETHROUGH_INCLUDED
#define RS_SEETHROUGH_INCLUDED

float  _RSSeeThroughOn;        // 0 / 1 — 카메라마다 RSSeeThrough 가 켜고 끈다
float  _RSSeeThroughCount;     // 대상 수 (최대 4)
float4 _RSSeeThroughA[4];      // xyz 몸 가운데 (월드), w 구멍 반지름 (m)
float4 _RSSeeThroughB[4];      // x 발 높이 (월드 y), y 바닥 여유 (m), z 앞쪽 여유 (m), w 세기 (0~1)
float4 _RSSeeThroughParams;    // x 가장자리 부드러움 (0~1), y 디더 칸 크기 (화면 픽셀)

static const float RS_BAYER4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float RSBayer4(uint2 p)
{
    return (RS_BAYER4[(p.y & 3u) * 4u + (p.x & 3u)] + 0.5) / 16.0;
}

// 0 = 그대로, 1 = 완전히 뚫림
float RSSeeThroughAmount(float3 positionWS)
{
    float amount = 0.0;

    float4 pcs = TransformWorldToHClip(positionWS);
    float2 pn  = pcs.xy / pcs.w;
    float  pz  = -TransformWorldToView(positionWS).z;

    float p11    = abs(UNITY_MATRIX_P[1][1]);
    float aspect = p11 / max(abs(UNITY_MATRIX_P[0][0]), 1e-5);
    float soft   = clamp(_RSSeeThroughParams.x, 0.01, 1.0);
    int   n      = (int)_RSSeeThroughCount;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i < n)
        {
            float4 A = _RSSeeThroughA[i];
            float4 B = _RSSeeThroughB[i];

            float  cz  = -TransformWorldToView(A.xyz).z;
            float4 ccs = TransformWorldToHClip(A.xyz);
            float2 cn  = ccs.xy / ccs.w;

            // 월드 반지름 → 화면(NDC 세로 기준) 반지름. 원근이면 거리로 나누고 직교면 그대로.
            float rN = A.w * p11 / lerp(max(cz, 1e-3), 1.0, unity_OrthoParams.w);
            float2 d = pn - cn;
            d.x *= aspect;
            float r = length(d) / max(rN, 1e-5);

            float radial = 1.0 - smoothstep(1.0 - soft, 1.0, r);
            float front  = saturate((cz - pz - B.z) / 0.4);
            float above  = saturate((positionWS.y - (B.x + B.y)) / 0.3);
            float alive  = step(1e-3, cz);

            amount = max(amount, radial * front * above * B.w * alive);
        }
    }
    return amount;
}

// enabled = 머티리얼의 _SeeThrough (0 이면 이 머티리얼은 안 뚫림)
void RSSeeThroughClip(float3 positionWS, float4 svPosition, float enabled)
{
    if (_RSSeeThroughOn < 0.5 || enabled < 0.5) return;
    float a = RSSeeThroughAmount(positionWS);
    if (a <= 0.0) return;
    float cell = max(1.0, _RSSeeThroughParams.y);
    uint2 px = (uint2)(svPosition.xy / cell);
    clip(RSBayer4(px) - a);
}

#endif
