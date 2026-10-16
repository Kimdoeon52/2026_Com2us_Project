// RE:AL STEEL - 볼류메트릭 안개 (상자 볼륨, URP)
//
// 상자 하나 안을 광선으로 걸어가며(레이마칭) 안개 밀도를 쌓는다. 렌더러 기능(Renderer Feature)을 안 써도 되는
// "안개 상자" 방식이라, 씬에 오브젝트만 놓으면 된다 (RSFogVolume 컴포넌트가 상자 메시 · 머티리얼을 만든다).
//
//   · 햇빛(메인 라이트) + 그림자 샘플 → 나무 · 건물 사이로 빛줄기가 생긴다 (그림자 맵 필요)
//   · 포인트 · 스폿 라이트 → 밤에 캐릭터 빛 · 가로등 둘레가 뿌옇게 빛난다
//     걸음으로 나눠 더하지 않고 '광선 위 빛 적분'을 식으로 바로 푼다 (해석적 산란) → 디더 · 줄무늬가 없고,
//     라이트 수 × 걸음 수가 아니라 라이트 수만큼만 계산해서 훨씬 가볍다. 라이트 목록은 RSFogVolume 이 넘긴다(최대 8)
//   · 장면 깊이(_CameraDepthTexture)에서 멈춰서, 벽 뒤까지 안개가 새지 않는다
//   · 높이가 오를수록 옅어지고(높이 감쇠), 3D 노이즈(미리 구운 32³ 텍스처 한 번 읽기)가 바람에 흐른다
//   · 걸음 수는 광선 길이에 맞춰 자동(걸음 간격), 시작점은 인터리브드 그라디언트 노이즈로 흔들어 밴딩 대신 고운 결
//   · 그림자는 걸음마다 한 번만 읽는다 (안개엔 부드러운 그림자 필터가 필요 없다)
//   · 갓레이(빛줄기): 그림자 맵(건물 · 나무) × 구름 · 나뭇잎 쿠키(RS 구름 그림자)를 '보이는 정도' 로 보고,
//     햇빛이 닿는 곳만 더 밝히는 '빛줄기 강조' + 그늘 쪽 주변광을 빼는 '대비' 로 빛기둥을 세운다.
//     맵에 가릴 물체가 없어도 구름 쿠키만으로 얼룩진 빛기둥이 생긴다
//
// 필요: URP 에셋 Depth Texture 켜짐 (물 설치 메뉴가 이미 켰다). 그림자를 받으려면 메인 라이트 그림자 켜짐.
Shader "RE_AL STEEL/Fog Volume"
{
    Properties
    {
        [HideInInspector] _BoxHalf ("상자 반크기 (자동)", Vector) = (20, 4, 20, 0)
        _FogColor        ("안개 색", Color) = (0.78, 0.82, 0.9, 1)
        _Density         ("밀도 (1/m)", Range(0, 1)) = 0.08
        _HeightFalloff   ("높이 감쇠 (클수록 바닥에 깔림)", Range(0, 2)) = 0.35
        _EdgeFade        ("상자 가장자리 부드러움 (m)", Float) = 3
        _NoiseScale      ("노이즈 크기 (작을수록 큰 덩어리)", Float) = 0.12
        _NoiseStrength   ("노이즈 세기", Range(0, 1)) = 0.6
        _Wind            ("바람 (m/s)", Vector) = (0.6, 0, 0.2, 0)
        _Steps           ("최대 걸음 수", Range(4, 48)) = 16
        _StepLength      ("걸음 간격 (m)", Range(0.25, 8)) = 1.5
        _MaxDistance     ("최대 거리 (m)", Float) = 60
        _Anisotropy      ("빛 앞쪽 산란 (0 고르게 / 0.8 해를 볼 때 강하게)", Range(0, 0.9)) = 0.55
        _SunStrength     ("햇빛 · 달빛 세기", Range(0, 4)) = 1.2
        _ShadowStrength  ("그림자 (빛줄기) 세기", Range(0, 1)) = 1
        _AmbientStrength ("주변광 세기", Range(0, 2)) = 0.6
        _PointStrength   ("포인트 · 스폿 라이트 세기", Range(0, 4)) = 1.5
        _DitherPixel     ("디더 칸 크기 (화면 픽셀)", Range(1, 8)) = 2
        _DitherStyle     ("디더 무늬 (0 고운 결, 1 도트 격자, 2 무작위 알갱이)", Float) = 0
        _Occlusion       ("뒤를 가리는 정도", Range(0, 1)) = 1
        _PointCore       ("라이트 중심 부드러움 (m)", Range(0.05, 2)) = 0.35
        _ShaftBoost      ("빛줄기 강조", Range(0, 8)) = 1
        _ShaftContrast   ("빛줄기 대비 (그늘 쪽 주변광 빼기)", Range(0, 1)) = 0.3
        _ShaftSharpness  ("빛줄기 또렷함", Range(0.5, 4)) = 1.5
        _CookieShafts    ("구름 · 나뭇잎 쿠키 반영", Range(0, 1)) = 0.8
        _ShaftPattern    ("빛줄기 무늬 (가릴 물체 없이도)", Range(0, 1)) = 0.6
        _ShaftPatternScale ("빛줄기 무늬 촘촘함 (/m)", Range(0.02, 2)) = 0.6
        _ShaftDebug      ("빛줄기만 보기", Float) = 0
        _ShaftEven       ("보는 방향 고르게", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FogVolume"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha   // 미리 곱한 알파 (색은 이미 투과율을 반영)
            ZWrite Off
            ZTest Always                 // 깊이는 셰이더가 직접 본다 (상자 안에 카메라가 있어도 된다)
            Cull Front                   // 상자 뒷면을 그린다 → 카메라가 상자 안이어도 보인다

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   FogVert
            #pragma fragment FogFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 · 나뭇잎 그림자 쿠키 → 얼룩진 빛기둥
            // 부드러운 그림자 키워드는 일부러 안 받는다 → 그림자 맵 한 번 읽기 (안개 속에선 차이가 안 보이고 4 ~ 9배 싸다)
            // 포인트 · 스폿 라이트는 URP 라이트 루프 대신 RSFogVolume 이 넘기는 목록을 쓴다 → Forward · Forward+ 키워드 불필요

            // 투명 오브젝트로 표시 → 화면 공간 그림자를 켜 둬도 그림자 맵을 직접 샘플한다
            // (안개 속 한 점마다 그림자를 봐야 해서, 화면에 찍힌 그림자로는 안 된다)
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BoxHalf;
                half4  _FogColor;
                float4 _Wind;
                float  _Density;
                float  _HeightFalloff;
                float  _EdgeFade;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _Steps;
                float  _StepLength;
                float  _PointCore;
                float  _ShaftBoost;
                float  _ShaftContrast;
                float  _ShaftSharpness;
                float  _CookieShafts;
                float  _ShaftPattern;
                float  _ShaftPatternScale;
                float  _ShaftDebug;
                float  _ShaftEven;
                float  _MaxDistance;
                float  _Anisotropy;
                float  _SunStrength;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float  _PointStrength;
                float  _DitherPixel;
                float  _DitherStyle;
                float  _Occlusion;
            CBUFFER_END

            // 노이즈 (RSFogVolume 이 만든 32³ 반복 텍스처: 2 옥타브를 미리 구워 둠)
            TEXTURE3D(_FogNoise); SAMPLER(sampler_FogNoise);

            // 포인트 · 스폿 라이트 목록 (RSFogVolume 이 매 프레임 넘김)
            #define FOG_MAX_LIGHTS 8
            int    _FogLightCount;
            float4 _FogLightPos[FOG_MAX_LIGHTS];    // xyz 위치, w 범위(Range)
            float4 _FogLightColor[FOG_MAX_LIGHTS];  // rgb 색 × 세기 (선형), w 스폿 add (점광원 = 1)
            float4 _FogLightSpot[FOG_MAX_LIGHTS];   // xyz 비추는 방향, w 스폿 1/(cos안 - cos밖) (점광원 = 0)

            // 안개 구역 (RSFogZone): 네모 구역 안의 밀도 · 빛줄기 · 색을 고친다. 적용 순서대로 최대 8개
            #define FOG_MAX_ZONES 8
            int      _FogZoneCount;
            float4x4 _FogZone[FOG_MAX_ZONES];       // 월드 → 구역 로컬 (m, 가운데 0)
            float4   _FogZoneHalf[FOG_MAX_ZONES];   // xyz 반크기 (m), w 경계 부드러움 (m)
            float4   _FogZoneParam[FOG_MAX_ZONES];  // x 방식 (0 곱하기 · 1 더하기 · 2 덮어쓰기), y 값, z 빛줄기 배율
            float4   _FogZoneColor[FOG_MAX_ZONES];  // rgb 색 배율 (선형)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings FogVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            // 인터리브드 그라디언트 노이즈 (Jimenez 2014): 4×4 베이어보다 무늬가 덜 보이는 고른 흔들림
            float IGN(float2 p) { return frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715)))); }

            // 헤니-그린스타인 위상 함수 (g = 0 이면 1, 4π 를 곱해 둔 꼴)
            float Phase(float cosT, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * cosT), 1.5);
            }

            // 상자 안 한 점의 밀도 + 그 점의 빛줄기 배율 · 색 배율 (안개 구역이 고침)
            float DensityAt(float3 posWS, float3 posOS, float3 scaleWS, out float shaftK, out half3 tint)
            {
                shaftK = 1.0; tint = 1.0;
                float3 halfB = _BoxHalf.xyz;
                // 바닥에서 높이 (m)
                float h = (posOS.y + halfB.y) * scaleWS.y;
                float d = _Density * exp(-_HeightFalloff * h);
                // 가장자리 부드럽게 (옆면 · 윗면) — 기본 안개에만. 구역 값은 구역 자신의 경계 부드러움으로
                float3 edge = (halfB - abs(posOS)) * scaleWS;
                float e = min(min(edge.x, edge.z), edge.y);
                d *= saturate(e / max(0.01, _EdgeFade));
                // 안개 구역: 우선순위 순서대로 곱하기 · 더하기 · 덮어쓰기 (경계 부드러움만큼 서서히)
                int zn = min(_FogZoneCount, FOG_MAX_ZONES);
                for (int z = 0; z < zn; z++)
                {
                    float3 q = abs(mul(_FogZone[z], float4(posWS, 1.0)).xyz);
                    float3 inside = _FogZoneHalf[z].xyz - q;               // 안쪽으로 들어간 거리 (m)
                    float in0 = min(min(inside.x, inside.y), inside.z);
                    if (in0 <= 0.0) continue;
                    float w = _FogZoneHalf[z].w > 1e-3 ? saturate(in0 / _FogZoneHalf[z].w) : 1.0;
                    float4 zp = _FogZoneParam[z];
                    float v = zp.x < 0.5 ? d * zp.y : (zp.x < 1.5 ? d + zp.y : zp.y);
                    d = lerp(d, v, w);
                    shaftK *= lerp(1.0, zp.z, w);
                    tint *= lerp((half3)1.0, (half3)_FogZoneColor[z].rgb, (half)w);
                }
                // 바람에 흐르는 노이즈 (2 옥타브를 구운 텍스처 한 번 읽기. 텍스처 한 장 = 노이즈 4칸)
                if (_NoiseStrength > 0.0)
                {
                    float3 q = posWS * _NoiseScale - _Wind.xyz * _NoiseScale * _Time.y;
                    float n = SAMPLE_TEXTURE3D_LOD(_FogNoise, sampler_FogNoise, q * 0.25, 0).r;
                    d *= lerp(1.0, saturate(n * 1.6 - 0.2), _NoiseStrength);
                }
                return d;
            }

            float DensityAt(float3 posWS, float3 posOS, float3 scaleWS)
            {
                float k; half3 t;
                return DensityAt(posWS, posOS, scaleWS, k, t);
            }

            // URP 거리 감쇠 (1 − (d²/R²)²)² / d² 를 광선 위에서 적분한 식 (R 로 나눈 좌표 x = s/R, hn = h/R)
            //   ∫ = ( atan(x/hn)/hn − 2(x³/3 + hn²x) + (x⁷/7 + 0.6hn²x⁵ + hn⁴x³ + hn⁶x) ) / R
            float AttenIntegral(float x, float hn)
            {
                float x2 = x * x, h2 = hn * hn;
                return atan(x / hn) / hn
                     - 2.0 * (x * x2 / 3.0 + h2 * x)
                     + (x2 * x2 * x2 * x / 7.0 + 0.6 * h2 * x2 * x2 * x + h2 * h2 * x2 * x + h2 * h2 * h2 * x);
            }

            // 포인트 · 스폿 라이트가 광선 [t0, t1] 에 남기는 산란 (밀도는 라이트에 가장 가까운 점 값, 투과율은 평균 소광으로)
            half3 PointLightsScatter(float3 ro, float3 rd, float3 roOS, float3 rdOS, float3 scaleWS, float t0, float t1, float sigmaAvg)
            {
                half3 sum = 0;
                int n = min(_FogLightCount, FOG_MAX_LIGHTS);
                [loop]
                for (int i = 0; i < n; i++)
                {
                    float3 lp = _FogLightPos[i].xyz;
                    float  R  = max(_FogLightPos[i].w, 0.01);
                    float  tc = dot(lp - ro, rd);                    // 광선에서 라이트에 가장 가까운 거리
                    float  h2 = dot(lp - ro, lp - ro) - tc * tc;     // 그 점과 라이트 사이 거리²
                    if (h2 >= R * R) continue;                       // 광선이 라이트 범위를 안 지남
                    float  w  = sqrt(R * R - h2);                    // 범위 구 안에 든 반 길이
                    float  a  = max(t0, tc - w), b = min(t1, tc + w);
                    if (b <= a) continue;                            // 벽 뒤 · 카메라 뒤
                    float  hn = sqrt(h2 + _PointCore * _PointCore) / R;
                    float  I  = (AttenIntegral((b - tc) / R, hn) - AttenIntegral((a - tc) / R, hn)) / R;

                    float  tm = clamp(tc, a, b);                     // 빛을 가장 많이 받는 점
                    float3 pm = ro + rd * tm;
                    float  dens = DensityAt(pm, roOS + rdOS * tm, scaleWS);
                    float  T  = exp(-sigmaAvg * (tm - t0));          // 카메라 → 그 점까지 안개에 가려지는 정도

                    // 스폿: 그 점에서 본 원뿔 감쇠 (점광원은 spot.w = 0, color.w = 1 → 1)
                    float3 L  = normalize(lp - pm);
                    float  sp = saturate(dot(_FogLightSpot[i].xyz, -L) * _FogLightSpot[i].w + _FogLightColor[i].w);
                    sum += _FogLightColor[i].rgb * (I * dens * T * sp * sp);
                }
                return sum * _PointStrength;
            }

            // 해 쪽 쿠키(구름 · 나뭇잎 그림자) — URP SampleMainLightCookie 와 같은 식, 반복문 안이라 밉 고정
            half SunCookie(float3 p)
            {
            #if defined(_LIGHT_COOKIES)
                if (_CookieShafts > 0.0 && IsMainLightCookieEnabled())
                {
                    float2 uv = ComputeLightCookieUVDirectional(_MainLightWorldToLight, p, float4(1, 1, 0, 0), URP_TEXTURE_WRAP_MODE_NONE);
                    half4 c = SAMPLE_TEXTURE2D_LOD(_MainLightCookieTexture, sampler_MainLightCookieTexture, uv, 0);
                    half v = IsMainLightCookieTextureRGBFormat() ? dot(c.rgb, half3(0.299, 0.587, 0.114))
                           : IsMainLightCookieTextureAlphaFormat() ? c.a : c.r;
                    return lerp(1.0, v, _CookieShafts);
                }
            #endif
                return 1.0;
            }

            half4 FogFrag(Varyings IN) : SV_Target
            {
                float2 suv = GetNormalizedScreenSpaceUV(IN.positionCS);
                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(IN.positionWS - ro);

                // 광선을 오브젝트 공간으로 (방향은 정규화하지 않아서 t 가 월드 거리와 같다)
                float3 roOS = mul(UNITY_MATRIX_I_M, float4(ro, 1.0)).xyz;
                float3 rdOS = mul((float3x3)UNITY_MATRIX_I_M, rd);
                float3 halfB = _BoxHalf.xyz;
                float3 inv = 1.0 / (abs(rdOS) > 1e-6 ? rdOS : 1e-6);
                float3 ta = (-halfB - roOS) * inv, tb = (halfB - roOS) * inv;
                float3 tmin = min(ta, tb), tmax = max(ta, tb);
                float t0 = max(max(tmin.x, tmin.y), max(tmin.z, 0.0));
                float t1 = min(min(tmax.x, tmax.y), tmax.z);

                // 장면 깊이에서 멈춘다 (눈 깊이 → 광선 거리)
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float eye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float sceneT = eye / max(dot(rd, camFwd), 1e-4);
                t1 = min(t1, sceneT);
                t1 = min(t1, t0 + _MaxDistance);
                if (t1 <= t0) return half4(0, 0, 0, 0);

                float4x4 M = UNITY_MATRIX_M;
                float3 scaleWS = float3(length(float3(M._m00, M._m10, M._m20)),
                                        length(float3(M._m01, M._m11, M._m21)),
                                        length(float3(M._m02, M._m12, M._m22)));

                // 걸음 수: 광선 길이 / 걸음 간격 (최대 걸음 수까지). 짧은 광선은 적게 걷는다
                int steps = (int)clamp(ceil((t1 - t0) / max(0.1, _StepLength)), 2.0, clamp(_Steps, 2.0, 48.0));
                float stepLen = (t1 - t0) / steps;
                float2 px = floor(IN.positionCS.xy / max(1.0, _DitherPixel));
                // 걸음 시작점 흔들기: 고운 결(IGN, 기본) · 도트 격자(4×4 베이어) · 무작위 알갱이
                float jitter;
                if (_DitherStyle < 0.5) jitter = IGN(px);
                else if (_DitherStyle < 1.5)
                {
                    float2 bp = fmod(px, 4.0);
                    // 정식 4×4 베이어 표
                    float4x4 B = float4x4(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
                    jitter = (B[(int)bp.y][(int)bp.x] + 0.5) / 16.0;
                }
                else jitter = frac(sin(dot(px, float2(12.9898, 78.233))) * 43758.5453);
                bool useShadow = _ShadowStrength > 0.001 && _SunStrength > 0.001;
                bool useCookie = _CookieShafts > 0.001 && _SunStrength > 0.001;

                Light mainLight = GetMainLight();
                float phaseSun = Phase(dot(rd, mainLight.direction), _Anisotropy);
                half3 ambient = SampleSH(half3(0, 1, 0)) * _AmbientStrength * (1.0 - _ShaftContrast);   // 대비: 그늘 쪽 뿌연 밝기를 뺀다
                // 보는 방향 고르게(_ShaftEven) 는 앞쪽 산란(해를 향해 볼 때 몇 배 밝아짐)도 같이 평평하게 한다
                float ph = lerp(phaseSun, 1.0, _ShaftEven);
                half3 sunCol = mainLight.color * _SunStrength * ph;
                // 빛줄기 강조는 보는 방향과 상관없이 보이게 (탑다운 카메라는 대개 해를 등지고 있어 앞쪽 산란이 약하다)
                half3 shaftCol = mainLight.color * _SunStrength * lerp(ph, 1.0, 0.7);
                // 조절용 보기: 시각 때문에 강조가 0 이어도(밤 · 커브 0) 무늬가 보이게 최소 1, 밝기는 빛 세기로 나눠 늘 비슷하게
                bool dbg = _ShaftDebug > 0.5;
                float boost = dbg ? max(_ShaftBoost, 1.0) : _ShaftBoost;
                if (dbg) shaftCol /= max(0.05, dot(shaftCol, half3(0.299, 0.587, 0.114)));

                // 빛줄기 무늬: 해 방향으로 늘어진 기둥 무늬 (해 방향에 수직인 평면 좌표로 노이즈를 읽어서, 해 방향으로는 값이 같다)
                // → 맵에 가릴 물체가 없어도 빛기둥이 선다. 나무 · 건물 그림자 · 구름 쿠키와 곱해진다
                float3 Ld = mainLight.direction;
                float3 ax = normalize(cross(Ld, abs(Ld.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 ay = cross(Ld, ax);
                bool usePattern = _ShaftPattern > 0.001;

                // 보는 방향 고르게: 빛기둥은 기둥 방향으로 바라볼 때 광선이 기둥 속을 길게 지나 진하고,
                // 옆에서 가로질러 보면 얇아서 흐리다 (실제 물리). 지나는 길이 ∝ 1 / sin(각도) 를 sin 으로 되갚아
                // 옆에서도 보이게, 기둥 방향으로는 덜 과하게. 0 = 물리 그대로
                float sinV = sqrt(saturate(1.0 - dot(rd, Ld) * dot(rd, Ld)));
                float evenK = lerp(1.0, clamp(sinV, 0.2, 1.0) / 0.4, _ShaftEven);
                shaftCol *= evenK;
                half3 shaftOnly = 0;

                float trans = 1.0;
                float tau = 0.0;     // 쌓인 광학 깊이 (포인트 라이트 투과율 근사에 씀)
                half3 scat = 0;
                float tEnd = t1;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = t0 + (i + jitter) * stepLen;
                    float3 p = ro + rd * t;
                    float3 pOS = roOS + rdOS * t;
                    float zShaft; half3 zTint;
                    float dens = DensityAt(p, pOS, scaleWS, zShaft, zTint);
                    if (dens < 1e-5) continue;

                    // 햇빛 · 달빛이 닿는 정도 = 그림자 맵(건물 · 나무) × 쿠키(구름 · 나뭇잎). 또렷함으로 경계를 세운다
                    float sh = useShadow ? MainLightRealtimeShadow(TransformWorldToShadowCoord(p)) : 1.0;
                    float vis = sh * (useCookie ? SunCookie(p) : 1.0);
                    if (usePattern)
                    {
                        float2 lp = float2(dot(p, ax), dot(p, ay)) * _ShaftPatternScale;
                        float pn = SAMPLE_TEXTURE3D_LOD(_FogNoise, sampler_FogNoise, float3(lp * 0.25, _Time.y * 0.004), 0).r;
                        vis *= lerp(1.0, smoothstep(0.38, 0.62, pn), _ShaftPattern);
                    }
                    vis = pow(saturate(vis), _ShaftSharpness);
                    // 기본 햇빛 + 닿는 곳만 더 밝히는 빛줄기 강조
                    half3 shaftLight = shaftCol * (vis * boost * zShaft);
                    // 햇빛: 그림자와 상관없는 부분 + 빛기둥 부분(보는 방향 보정)
                    half3 light = sunCol * ((1.0 - _ShadowStrength) + vis * _ShadowStrength * evenK) + shaftLight + ambient;

                    // 빛을 받는 양(산란)과 뒤를 가리는 양(소광)을 따로: '뒤를 가리는 정도' 가 낮으면
                    // 빛기둥은 그대로 밝고 방 안 · 뒤 배경은 덜 어두워진다 (실내 먼지 느낌)
                    float ext = dens * stepLen * _Occlusion;
                    float absorb = 1.0 - exp(-dens * stepLen);
                    scat += trans * absorb * light * _FogColor.rgb * zTint;
                    shaftOnly += trans * absorb * shaftLight * _FogColor.rgb * zTint;
                    trans *= exp(-ext);
                    tau += ext;
                    if (trans < 0.01) { tEnd = t; break; }
                }

                // 포인트 · 스폿 라이트: 걸음과 무관하게 식으로 (디더 없음)
                if (_FogLightCount > 0 && _PointStrength > 0.0)
                {
                    float sigmaAvg = tau / max(1e-3, tEnd - t0);
                    scat += PointLightsScatter(ro, rd, roOS, rdOS, scaleWS, t0, tEnd, sigmaAvg) * _FogColor.rgb;
                }

                if (dbg) return half4(shaftOnly * 4.0, 1.0);   // 빛줄기만 크게 (조절용)
                return half4(scat, 1.0 - trans);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
