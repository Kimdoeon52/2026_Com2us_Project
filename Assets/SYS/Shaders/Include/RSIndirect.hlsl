// RE:AL STEEL - 간접광 받기 (RS 간접광 컴포넌트가 채우는 전역값을 읽는다)
//
// 앰비언트(SampleSH) 를 계산한 직후에 부른다:
//     inputData.bakedGI = RSIndirectGI(inputData.bakedGI, positionWS, normalWS, _RSIndirectReceive.x);
//   받는 쪽 세기: x = 지형 · y = 건물(트라이플래너) · z = 캐릭터(빌보드) · w = 풀꽃
//
// 하는 일
//   1. 하늘 가림  바닥 칸의 '하늘 보임' 만큼 앰비언트를 줄인다. 바닥보다 높이 올라갈수록 원래대로.
//                 바닥보다 아래(지붕 · 다리 밑)면 '위가 막힘' 밝기로.
//   2. 튄 빛      햇빛 받은 바닥 색을 더한다. 옆 · 아래를 보는 면일수록, 바닥에 가까울수록 많이.
// 간접광 컴포넌트가 없으면 (_RSIndirectParams.x = 0) 원래 앰비언트 그대로.
#ifndef RS_INDIRECT_INCLUDED
#define RS_INDIRECT_INCLUDED

TEXTURE2D(_RSIndirectTex);          SAMPLER(sampler_RSIndirectTex);   // rgb = 튄 빛, a = 하늘 보임
TEXTURE2D(_RSIndirectHeightTex);                                        // r = 바닥 높이 (월드 Y)
float4 _RSIndirectRect;      // xy = 범위 최소 XZ, zw = 1 / 크기
float4 _RSIndirectParams;    // x = 켜짐, y = 하늘 가림 세기, z = 튄 빛 세기, w = 튄 빛 높이 (m)
float4 _RSIndirectReceive;   // 받는 쪽 세기 (지형, 건물, 캐릭터, 풀꽃)
float4 _RSIndirectOutside;   // rgb = 범위 밖 튄 빛, w = 가장자리 페이드 (uv 역수)
float4 _RSIndirectExtra;     // x = 위가 막힌 곳 하늘빛 밝기, yz = 칸 하나 (uv)

half3 RSIndirectGI(half3 bakedGI, float3 positionWS, half3 normalWS, half receive)
{
    if (_RSIndirectParams.x < 0.5 || receive <= 0.001) return bakedGI;

    float2 uv = (positionWS.xz - _RSIndirectRect.xy) * _RSIndirectRect.zw;
    float2 e = min(uv, 1.0 - uv);
    float inside = saturate(min(e.x, e.y) * _RSIndirectOutside.w);
    float2 uvc = saturate(uv);

    half4 g = SAMPLE_TEXTURE2D_LOD(_RSIndirectTex, sampler_RSIndirectTex, uvc, 0);

    // 바닥 높이 = 둘레 네 곳 중 가장 낮은 값. 벽 · 절벽 옆면에서 '위(지붕 · 절벽 윗면)가 바닥' 으로 잘못 읽혀
    // 막힌 곳 취급(어두워짐)되는 것을 막는다. 다리 · 지붕 밑은 네 곳 모두 위가 막혀 있어서 그대로 어두워진다.
    float2 tx = _RSIndirectExtra.yz * 0.75;
    float ground = SAMPLE_TEXTURE2D_LOD(_RSIndirectHeightTex, sampler_RSIndirectTex, uvc, 0).r;
    ground = min(ground, SAMPLE_TEXTURE2D_LOD(_RSIndirectHeightTex, sampler_RSIndirectTex, saturate(uv + float2( tx.x,  tx.y)), 0).r);
    ground = min(ground, SAMPLE_TEXTURE2D_LOD(_RSIndirectHeightTex, sampler_RSIndirectTex, saturate(uv + float2(-tx.x,  tx.y)), 0).r);
    ground = min(ground, SAMPLE_TEXTURE2D_LOD(_RSIndirectHeightTex, sampler_RSIndirectTex, saturate(uv + float2( tx.x, -tx.y)), 0).r);
    ground = min(ground, SAMPLE_TEXTURE2D_LOD(_RSIndirectHeightTex, sampler_RSIndirectTex, saturate(uv + float2(-tx.x, -tx.y)), 0).r);

    float dy = positionWS.y - ground;
    float fadeH = max(0.1, _RSIndirectParams.w);
    float above = saturate(dy / fadeH);

    // 하늘 보임: 바닥 근처는 칸 값, 높이 올라가면 트임. 바닥보다 확실히 아래면 위가 막힌 곳
    half vis = lerp(g.a, 1.0, above * above);
    if (dy < -0.4) vis = min(vis, (half)_RSIndirectExtra.x);
    vis = lerp(1.0, vis, inside);

    // 튄 빛: 위를 보는 면 조금, 옆 · 아래를 보는 면 많이. 바닥에서 멀어질수록 줄어듦
    half3 bounce = lerp((half3)_RSIndirectOutside.rgb, g.rgb, inside) * (1.0 - above * 0.8);
    half facing = lerp(0.15, 1.0, saturate(0.5 - 0.5 * normalWS.y));

    half3 gi = bakedGI * lerp(1.0, vis, _RSIndirectParams.y) + bounce * facing * _RSIndirectParams.z;
    return lerp(bakedGI, gi, saturate(receive));
}

#endif
