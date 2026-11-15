// RE:AL STEEL - 시간대 하늘 (스카이박스, URP)
//
// RSSkyTimeOfDay 컴포넌트가 시간대(RSTimeOfDay)를 읽어 색 · 해 · 달 · 별 · 노을 값을 매 시각 넣는다. 직접 고칠 값은 컴포넌트에서.
//  · 꼭대기 → 지평선 그라데이션, 해 쪽 지평선 노을, 지평선 아래는 땅색
//  · 해(낮) 또는 달(밤) 원반 — 경계가 딱 끊기는 도트풍, 둘레 빛
//  · 별(밤) — 방향 격자마다 하나, 반짝임. 지평선 근처와 구름 뒤에서는 흐려짐
//  · 구름 — 하늘 평면에 투영한 값 노이즈, 시간대 색을 받음
//  · 색 단계 + 4×4 디더 → 매끈한 그라데이션 대신 픽셀 아트 같은 띠 (단계 0 = 매끈)
// 게임 카메라(35° 내려다봄)에는 하늘이 직접 안 보인다. 씬 뷰 · 반사 · 낮은 각도 카메라용
Shader "RE_AL STEEL/Sky Time Of Day"
{
    Properties
    {
        _ZenithColor  ("하늘 꼭대기", Color) = (0.30, 0.50, 0.82, 1)
        _HorizonColor ("지평선", Color) = (0.72, 0.82, 0.92, 1)
        _GroundColor  ("지평선 아래 (땅)", Color) = (0.22, 0.21, 0.22, 1)
        _HorizonHeight ("지평선 띠 두께", Range(0.02, 1)) = 0.35
        _LightDir     ("빛 쪽 방향 (해 · 달)", Vector) = (0, 0.5, 0.8, 0)
        _SunColor     ("해 · 달 색", Color) = (1, 0.92, 0.75, 1)
        _SunSize      ("해 크기 (라디안)", Range(0, 0.3)) = 0.045
        _MoonSize     ("달 크기 (라디안)", Range(0, 0.3)) = 0.035
        _SunGlow      ("둘레 빛", Range(0, 4)) = 1
        _IsMoon       ("달 (1) / 해 (0)", Range(0, 1)) = 0
        _GlowColor    ("해 쪽 지평선 노을 색", Color) = (1, 0.55, 0.3, 1)
        _GlowAmount   ("노을 세기", Range(0, 2)) = 0
        _Stars        ("별", Range(0, 1)) = 0
        _StarDensity  ("별 밀도", Range(0, 1)) = 0.35
        _Clouds       ("구름 양", Range(0, 1)) = 0.35
        _CloudScale   ("구름 크기", Float) = 1.6
        _CloudSpeed   ("구름 속도", Float) = 0.008
        _CloudBright  ("구름 밝기", Range(0, 2)) = 1
        _Steps        ("색 단계 (0 = 매끈)", Range(0, 64)) = 28
        _Dither       ("디더 세기", Range(0, 1)) = 0.7
        _DitherPixel  ("디더 칸 크기 (화면 픽셀)", Range(1, 8)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _GlowColor;
                float4 _LightDir;
                float _HorizonHeight, _SunSize, _MoonSize, _SunGlow, _IsMoon, _GlowAmount;
                float _Stars, _StarDensity, _Clouds, _CloudScale, _CloudSpeed, _CloudBright;
                float _Steps, _Dither, _DitherPixel;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash12(i), b = Hash12(i + float2(1, 0)), c = Hash12(i + float2(0, 1)), d = Hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += VNoise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            float Bayer4(float2 px)
            {
                int2 q = int2(fmod(px, 4.0));
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return m[q.y * 4 + q.x] / 16.0;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 L = normalize(_LightDir.xyz + float3(0, 1e-4, 0));

                // 꼭대기 · 지평선
                float t = saturate(max(y, 0.0) / max(_HorizonHeight, 1e-3));
                t = 1.0 - pow(1.0 - t, 2.0);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);

                // 해 쪽 지평선 노을 (방위가 가까울수록 · 지평선에 가까울수록)
                float2 dh = normalize(d.xz + 1e-5), lh = normalize(L.xz + 1e-5);
                float az = dot(dh, lh) * 0.5 + 0.5;
                float glow = pow(az, 5.0) * (1.0 - t) * _GlowAmount;
                col = lerp(col, _GlowColor.rgb, saturate(glow));

                // 구름 (하늘 평면 투영)
                float cloud = 0.0;
                if (_Clouds > 0.001 && y > 0.0)
                {
                    float2 uv = d.xz / (y + 0.18) * (_CloudScale * 0.35) + _Time.y * _CloudSpeed * float2(1.0, 0.35);
                    float n = Fbm(uv);
                    float th = 1.0 - _Clouds;
                    cloud = smoothstep(th, th + 0.12, n) * saturate(y * 6.0);
                    float3 lit = lerp(_HorizonColor.rgb, float3(1, 1, 1), 0.35) * _CloudBright;
                    lit = lerp(lit, _GlowColor.rgb, saturate(glow) * 0.6);
                    float3 shade = lerp(_ZenithColor.rgb, _HorizonColor.rgb, 0.4) * 0.85 * _CloudBright;
                    float lightSide = saturate(dot(dh, lh) * 0.5 + 0.5);
                    float3 cc = lerp(shade, lit, saturate((n - th) * 4.0) * (0.55 + 0.45 * lightSide));
                    col = lerp(col, cc, cloud * 0.9);
                }

                // 별 (밤)
                if (_Stars > 0.001 && y > 0.0)
                {
                    float3 p = d * 140.0;
                    float3 cell = floor(p);
                    float h = Hash13(cell);
                    if (h < _StarDensity * 0.06)
                    {
                        float3 c = cell + 0.5 + (float3(Hash13(cell + 7.1), Hash13(cell + 3.7), Hash13(cell + 9.3)) - 0.5) * 0.6;
                        float s = saturate(1.0 - length(p - c) * 3.2);
                        float tw = 0.65 + 0.35 * sin(_Time.y * (1.5 + h * 40.0) + h * 300.0);
                        float b = step(0.5, s) * tw * (0.5 + h / max(_StarDensity * 0.06, 1e-4) * 0.5);
                        col += b * _Stars * saturate(y * 5.0) * (1.0 - cloud) * float3(0.95, 0.97, 1.0);
                    }
                }

                // 해 · 달
                float ang = acos(clamp(dot(d, L), -1.0, 1.0));
                float size = lerp(_SunSize, _MoonSize, _IsMoon);
                float disc = step(ang, size);
                float halo = exp(-ang / max(size * 1.6, 1e-3)) * _SunGlow * lerp(0.35, 0.18, _IsMoon);
                float3 body = _SunColor.rgb * lerp(2.2, 1.15, _IsMoon);
                if (_IsMoon > 0.5)
                {
                    // 달 표면 얼룩 (도트풍 두 단계)
                    float3 local = (d - L) / max(size, 1e-3);
                    float spot = step(0.62, VNoise(local.xy * 2.3 + 5.0));
                    body *= lerp(1.0, 0.78, spot);
                }
                float above = saturate(y * 20.0 + 0.4);
                col += (body * disc + _SunColor.rgb * halo) * (1.0 - cloud * 0.85) * above;

                // 지평선 아래 = 땅
                col = lerp(col, _GroundColor.rgb, saturate(-y * 8.0));

                // 색 단계 + 디더 (감마 공간에서 끊어야 띠 간격이 고르게 보인다)
                if (_Steps > 0.5)
                {
                    float3 g = pow(max(col, 0.0), 1.0 / 2.2);
                    float dz = (Bayer4(floor(i.positionCS.xy / max(_DitherPixel, 1.0))) - 0.5) * _Dither;
                    g = floor(g * _Steps + 0.5 + dz) / _Steps;
                    col = pow(max(g, 0.0), 2.2);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
