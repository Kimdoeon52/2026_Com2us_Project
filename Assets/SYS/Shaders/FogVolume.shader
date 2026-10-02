// RE:AL STEEL - 볼류메트릭 안개 (상자 볼륨, URP)
//
// 상자 하나 안을 광선으로 걸어가며(레이마칭) 안개 밀도를 쌓는다. 렌더러 기능(Renderer Feature)을 안 써도 되는
// "안개 상자" 방식이라, 씬에 오브젝트만 놓으면 된다 (RSFogVolume 컴포넌트가 상자 메시 · 머티리얼을 만든다).
//
//   · 햇빛(메인 라이트) + 그림자 샘플 → 나무 · 건물 사이로 빛줄기가 생긴다 (그림자 맵 필요)
//   · 포인트 · 스폿 라이트 → 밤에 캐릭터 빛 · 가로등 둘레가 뿌옇게 빛난다
//   · 장면 깊이(_CameraDepthTexture)에서 멈춰서, 벽 뒤까지 안개가 새지 않는다
//   · 높이가 오를수록 옅어지고(높이 감쇠), 3D 노이즈가 바람에 흐른다
//   · 걸음 시작점을 4×4 디더로 흔들어 줄무늬(밴딩) 대신 도트 느낌의 결로
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
        _Steps           ("걸음 수 (많을수록 곱고 무겁다)", Range(4, 48)) = 16
        _MaxDistance     ("최대 거리 (m)", Float) = 60
        _Anisotropy      ("빛 앞쪽 산란 (0 고르게 / 0.8 해를 볼 때 강하게)", Range(0, 0.9)) = 0.55
        _SunStrength     ("햇빛 · 달빛 세기", Range(0, 4)) = 1.2
        _ShadowStrength  ("그림자 (빛줄기) 세기", Range(0, 1)) = 1
        _AmbientStrength ("주변광 세기", Range(0, 2)) = 0.6
        _PointStrength   ("포인트 · 스폿 라이트 세기", Range(0, 4)) = 1.5
        _DitherPixel     ("디더 칸 크기 (화면 픽셀)", Range(1, 8)) = 2
        [Toggle(_FOG_POINT_LIGHTS)] _UsePointLights ("포인트 · 스폿 라이트 받기", Float) = 1
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma shader_feature_local_fragment _FOG_POINT_LIGHTS

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
                float  _MaxDistance;
                float  _Anisotropy;
                float  _SunStrength;
                float  _ShadowStrength;
                float  _AmbientStrength;
                float  _PointStrength;
                float  _DitherPixel;
                float  _UsePointLights;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings FogVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            // ── 3D 값 노이즈 ──
            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }
            float Noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = Hash31(i), n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0)), n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1)), n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1)), n111 = Hash31(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                            lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            static const float FOG_BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            // 헤니-그린스타인 위상 함수 (g = 0 이면 1, 4π 를 곱해 둔 꼴)
            float Phase(float cosT, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * cosT), 1.5);
            }

            // 상자 안 한 점의 밀도
            float DensityAt(float3 posWS, float3 posOS, float3 scaleWS)
            {
                float3 halfB = _BoxHalf.xyz;
                // 바닥에서 높이 (m)
                float h = (posOS.y + halfB.y) * scaleWS.y;
                float d = _Density * exp(-_HeightFalloff * h);
                // 가장자리 부드럽게 (옆면 · 윗면)
                float3 edge = (halfB - abs(posOS)) * scaleWS;
                float e = min(min(edge.x, edge.z), edge.y);
                d *= saturate(e / max(0.01, _EdgeFade));
                // 바람에 흐르는 노이즈 2겹
                float3 q = posWS * _NoiseScale - _Wind.xyz * _NoiseScale * _Time.y;
                float n = Noise3(q) * 0.65 + Noise3(q * 2.3 + 7.1) * 0.35;
                d *= lerp(1.0, saturate(n * 1.6 - 0.2), _NoiseStrength);
                return d;
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

                int steps = (int)clamp(_Steps, 4, 48);
                float stepLen = (t1 - t0) / steps;
                uint2 px = (uint2)(IN.positionCS.xy / max(1.0, _DitherPixel));
                float jitter = (FOG_BAYER[(px.y & 3u) * 4u + (px.x & 3u)] + 0.5) / 16.0;

                Light mainLight = GetMainLight();
                float phaseSun = Phase(dot(rd, mainLight.direction), _Anisotropy);
                half3 ambient = SampleSH(half3(0, 1, 0)) * _AmbientStrength;
                half3 sunCol = mainLight.color * _SunStrength * phaseSun;

                float trans = 1.0;
                half3 scat = 0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float t = t0 + (i + jitter) * stepLen;
                    float3 p = ro + rd * t;
                    float3 pOS = roOS + rdOS * t;
                    float dens = DensityAt(p, pOS, scaleWS);
                    if (dens < 1e-5) continue;

                    // 햇빛 · 달빛 + 그림자
                    float sh = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    half3 light = sunCol * lerp(1.0, sh, _ShadowStrength) + ambient;

                    // 포인트 · 스폿 라이트 (캐릭터 빛 · 가로등)
                #if defined(_FOG_POINT_LIGHTS) && defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = p;
                    inputData.normalizedScreenSpaceUV = suv;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, p);
                        light += al.color * al.distanceAttenuation * _PointStrength;
                    LIGHT_LOOP_END
                #endif

                    float ext = dens * stepLen;
                    float absorb = 1.0 - exp(-ext);
                    scat += trans * absorb * light * _FogColor.rgb;
                    trans *= exp(-ext);
                    if (trans < 0.01) break;
                }

                return half4(scat, 1.0 - trans);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
