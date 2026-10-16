// RE:AL STEEL - 화면 공간 반사 (젖은 바닥 · 물이 같이 쓴다)
//
// 반사 방향으로 깊이 텍스처를 따라가다 화면에 그려진 물체에 부딪히면 그 자리의 화면 색(Opaque Texture)을 가져온다.
// 거울 카메라를 한 번 더 그리지 않아서 가볍고, 다른 카메라 렌더에 끼어들지 않는다.
// 화면 밖에 있는 것은 못 비춘다 (hit = 0 → 부르는 쪽이 하늘색으로 채운다).
//
// 필요: 이 파일 앞에 DeclareDepthTexture.hlsl · DeclareOpaqueTexture.hlsl include, URP 에셋 Depth · Opaque Texture 켜짐.
// 반복문 안에서 읽으므로 전부 밉 단계를 직접 지정한다 (DX11 FXC 규칙).
#ifndef RS_SCREEN_REFLECTION_INCLUDED
#define RS_SCREEN_REFLECTION_INCLUDED

float RSSR_Depth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv), 0).r;
}

half3 RSSR_Color(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, UnityStereoTransformScreenSpaceTex(uv), 0).rgb;
}

float2 RSSR_ScreenUV(float4 cs)
{
    float4 sp = ComputeScreenPos(cs);
    return sp.xy / sp.w;
}

// origin 에서 dir 로 최대 maxDist(m) 를 steps 걸음으로 따라간다. thickness = 물체 두께로 볼 깊이 (m).
// hit = 0 (못 맞힘) ~ 1 (확실히 맞힘, 화면 가장자리 · 먼 거리는 옅게). hitUV = 맞은 화면 위치, hitDist = 맞은 거리 (m)
half3 RSTraceReflectionEx(float3 origin, float3 dir, float steps, float maxDist, float thickness,
                          out float hit, out float2 hitUV, out float hitDist)
{
    hit = 0.0; hitUV = float2(0.5, 0.5); hitDist = 0.0;
    if (unity_OrthoParams.w > 0.5) return half3(0, 0, 0);   // 직교 카메라는 안 함

    int n = (int)clamp(steps, 4.0, 48.0);
    float maxD = max(1.0, maxDist);
    float thick = max(0.02, thickness);
    float stepLen = maxD / n;
    float t = stepLen * 0.6;
    float prevT = 0.0;

    [loop]
    for (int i = 0; i < 48; i++)
    {
        if (i >= n) break;
        float4 cs = TransformWorldToHClip(origin + dir * t);
        if (cs.w <= 0.0) break;
        float2 uv = RSSR_ScreenUV(cs);
        if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) break;

        float diff = cs.w - LinearEyeDepth(RSSR_Depth(uv), _ZBufferParams);   // + = 광선이 화면의 물체 뒤로 들어감
        if (diff > 0.0 && diff < thick)
        {
            // 이분 탐색으로 경계 다듬기
            float a = prevT, b = t;
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float m = (a + b) * 0.5;
                float4 cm = TransformWorldToHClip(origin + dir * m);
                float dz = cm.w - LinearEyeDepth(RSSR_Depth(RSSR_ScreenUV(cm)), _ZBufferParams);
                if (dz > 0.0) b = m; else a = m;
            }
            float2 hu = RSSR_ScreenUV(TransformWorldToHClip(origin + dir * b));
            float2 edge = min(hu, 1.0 - hu);
            hit = saturate(min(edge.x, edge.y) / 0.06) * saturate(1.0 - b / maxD);
            hitUV = hu; hitDist = b;
            return RSSR_Color(hu);
        }
        prevT = t;
        t += stepLen;
    }
    return half3(0, 0, 0);
}

half3 RSTraceReflection(float3 origin, float3 dir, float steps, float maxDist, float thickness, out float hit)
{
    float2 uv; float d;
    return RSTraceReflectionEx(origin, dir, steps, maxDist, thickness, hit, uv, d);
}

// 화면 색을 세로로 긴 타원 모양으로 흐리게 (젖은 바닥 반사가 세로로 번지는 것). radius = (가로, 세로) uv
static const float2 kRSSRTaps[8] = {
    float2( 0.00,  0.35), float2( 0.00, -0.35), float2( 0.00,  0.80), float2( 0.00, -0.80),
    float2( 0.55,  0.12), float2(-0.55, -0.12), float2( 0.35, -0.55), float2(-0.35,  0.55) };

half3 RSSR_BlurColor(float2 uv, float2 radius)
{
    half3 c = RSSR_Color(uv) * 2.0;
    [unroll]
    for (int i = 0; i < 8; i++) c += RSSR_Color(saturate(uv + kRSSRTaps[i] * radius));
    return c / 10.0;
}

// 반사 광선이 화면을 벗어나는 자리 (화면 가장자리 조금 안쪽). 못 맞힌 반사를 '그쪽 방향의 먼 풍경 색' 으로 채울 때 쓴다
float2 RSSR_ExitUV(float3 origin, float3 dir, float maxDist)
{
    float2 a = RSSR_ScreenUV(TransformWorldToHClip(origin));
    float len = max(1.0, maxDist);
    float4 cs = TransformWorldToHClip(origin + dir * len);
    [unroll]
    for (int k = 0; k < 4; k++) { if (cs.w > 0.05) break; len *= 0.5; cs = TransformWorldToHClip(origin + dir * len); }
    float2 b = RSSR_ScreenUV(cs);
    float2 d = b - a;
    const float m = 0.03;
    float s = 1.0;
    if (d.x >  1e-5) s = min(s, (1.0 - m - a.x) / d.x);
    if (d.x < -1e-5) s = min(s, (m - a.x) / d.x);
    if (d.y >  1e-5) s = min(s, (1.0 - m - a.y) / d.y);
    if (d.y < -1e-5) s = min(s, (m - a.y) / d.y);
    return clamp(a + d * saturate(s), m, 1.0 - m);
}

// 번진 반사 (거칠기 · 세로 번짐 · 못 맞히면 주변 빛).
//   rough  : 0 = 거울 ~ 1 = 뿌옇게. 멀리 맞을수록 더 번진다
//   stretch: 세로 번짐 배율 (1 = 동그랗게)
//   envMix : 못 맞힌 곳을 화면 가장자리의 먼 풍경 색으로 채우는 정도 (0 = ambient 만)
//   ambient: 못 맞힌 곳의 기본 색 (보통 SampleSH(R))
half3 RSGlossyReflection(float3 origin, float3 dir, float rough, float stretch, float steps, float maxDist, float thickness,
                         float envMix, half3 ambient, out float hit)
{
    float2 hu; float hd;
    half3 c = RSTraceReflectionEx(origin, dir, steps, maxDist, thickness, hit, hu, hd);
    float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
    float st = max(stretch, 1.0);
    if (hit > 0.0 && rough > 0.001)
    {
        float r = rough * (0.004 + hd * 0.006);   // 화면 높이 비율
        c = RSSR_BlurColor(hu, float2(r / aspect, r * st));
    }
    half3 miss = ambient;
    if (envMix > 0.0 && hit < 0.999)   // 확실히 맞힌 곳은 건너뜀
    {
        float2 eu = RSSR_ExitUV(origin, dir, maxDist);
        float r = 0.025 + rough * 0.05;
        miss = lerp(ambient, RSSR_BlurColor(eu, float2(r / aspect, r * st)), envMix);
    }
    return lerp(miss, c, hit);
}

#endif
