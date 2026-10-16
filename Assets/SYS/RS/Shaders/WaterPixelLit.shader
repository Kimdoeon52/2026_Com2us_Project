// RE:AL STEEL - 픽셀 물 셰이더 (RS 지형 배수로 · 웅덩이 수면용)
//
//  · 굴절: 물 밑 지형이 물결 따라 일렁여 보인다 (URP Opaque Texture 필요)
//  · 깊이 색: 얕은 곳은 맑은 청록, 깊을수록 짙은 색 (URP Depth Texture 필요)
//  · 가장자리 거품: 물이 땅에 닿는 곳에 밝은 테
//  · 반사: RSWaterReflection 이 있으면 주변 풍경(둑 · 소품 · 캐릭터)을 반사 — 기본은 화면 공간 반사, 선택으로 평면 반사(거울 카메라). 없으면 하늘(시간대 앰비언트) · 반사 프로브
//  · 햇빛 반짝임: 해 방향에 따라 픽셀 단위로 반짝인다. 그림자 · 구름 그림자를 받는다
//  · 물결은 텍스처 없이 셰이더가 만든다. 픽셀 밀도로 계단지게 해서 픽셀아트와 맞춘다
Shader "RE_AL STEEL/Water Pixel Lit"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("얕은 물 색", Color) = (0.20, 0.40, 0.40, 1)
        _DeepColor ("깊은 물 색", Color) = (0.03, 0.08, 0.11, 1)
        _DepthDistance ("깊은 색이 되는 깊이 (m)", Range(0.05, 5)) = 0.6
        _Clarity ("맑기 (0 탁함 · 1 바닥이 잘 보임)", Range(0, 1)) = 0.35

        [Header(Waves)]
        _WaveScale ("물결 크기 (클수록 잘다)", Float) = 1.3
        _WaveSpeed ("물결 속도", Float) = 0.35
        _WaveStrength ("물결 세기", Range(0, 2)) = 0.6
        _FlowDir ("흐름 방향 (XZ)", Vector) = (0.4, 0.15, 0, 0)
        _PixelDensity ("픽셀 밀도 (칸/m, 0 = 부드럽게)", Float) = 16

        [Header(Refraction)]
        [Toggle(_REFRACTION)] _UseRefraction ("굴절 (Opaque Texture 필요)", Float) = 1
        _Refraction ("굴절 세기", Range(0, 0.1)) = 0.025

        [Header(Reflection)]
        _ReflectionStrength ("반사 세기", Range(0, 1)) = 0.9
        _FresnelPower ("반사 각도 효과 (클수록 비스듬히 볼 때만)", Range(0.5, 8)) = 2
        _ReflectionMin ("정면에서도 남는 반사 (낮에 반사가 약하면 올린다)", Range(0, 1)) = 0.4
        _ReflectionDistort ("반사 일렁임", Range(0, 0.1)) = 0.02
        _ReflectionTint ("반사 색 곱하기", Color) = (0.9, 0.95, 1, 1)
        _SkyFromAmbient ("하늘 반사 = 시간대 앰비언트 (0 = 반사 프로브)", Range(0, 1)) = 0.7

        [Header(Sun Glint)]
        _GlintStrength ("햇빛 반짝임 세기", Range(0, 8)) = 2.5
        _GlintSharpness ("반짝임 날카로움", Range(8, 512)) = 160
        _GlintThreshold ("반짝임 문턱 (클수록 드문드문)", Range(0, 1)) = 0.35

        [Header(Foam)]
        [Toggle(_DEPTH_EFFECTS)] _UseDepth ("깊이 효과: 깊이 색 · 거품 (Depth Texture 필요)", Float) = 1
        _FoamColor ("거품 색", Color) = (0.85, 0.92, 0.88, 1)
        _FoamWidth ("거품 폭 (m)", Range(0, 1)) = 0.18
        _FoamNoise ("거품 들쭉날쭉", Range(0, 1)) = 0.6
        _EdgeFade ("가장자리 투명해지는 폭 (m)", Range(0.001, 0.5)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _REFRACTION
            #pragma shader_feature_local _DEPTH_EFFECTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Include/RSScreenReflection.hlsl"   // 화면 공간 반사 (젖은 바닥과 같이 씀)

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthDistance;
                half _Clarity;
                float _WaveScale;
                float _WaveSpeed;
                half _WaveStrength;
                float4 _FlowDir;
                float _PixelDensity;
                half _Refraction;
                half _ReflectionStrength;
                half _FresnelPower;
                half _ReflectionMin;
                half _ReflectionDistort;
                half4 _ReflectionTint;
                half _SkyFromAmbient;
                half _GlintStrength;
                half _GlintSharpness;
                half _GlintThreshold;
                half4 _FoamColor;
                half _FoamWidth;
                half _FoamNoise;
                half _EdgeFade;
            CBUFFER_END

            // RSWaterReflection 이 넣는 전역값
            TEXTURE2D(_RSReflectionTex);
            SAMPLER(sampler_RSReflectionTex);
            float _RSReflectionOn;      // 0 = 하늘만, 1 = 평면 반사 텍스처, 2 = 화면 공간 반사
            float4 _RSWaterSSR;         // 화면 반사: x = 걸음 수, y = 최대 거리 (m), z = 두께 (m)

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  fogCoord   : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            // ── 노이즈 ──
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 두 겹이 서로 다른 방향으로 흐르는 물결 높이
            float WaveHeight(float2 p, float t)
            {
                float2 flow = _FlowDir.xy;
                float h = VNoise(p * _WaveScale + flow * t);
                h += 0.6 * VNoise(p * _WaveScale * 2.13 + float2(-flow.y, flow.x) * t * 1.37 + 17.0);
                h += 0.3 * VNoise(p * _WaveScale * 4.7 - flow * t * 1.9 + 41.0);
                return h;
            }

            // 화면 깊이 → 눈 기준 거리 (원근 · 직교 둘 다)
            float SceneEyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > 0.5)
                {
                #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 posWS = IN.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float t = _Time.y * _WaveSpeed;

                // 픽셀 칸에 맞춰 물결을 계단지게
                float2 p = posWS.xz;
                if (_PixelDensity > 0.5) p = (floor(p * _PixelDensity) + 0.5) / _PixelDensity;

                // 물결 법선 (높이의 기울기)
                float e = _PixelDensity > 0.5 ? 1.0 / _PixelDensity : 0.05;
                float h0 = WaveHeight(p, t);
                float hx = WaveHeight(p + float2(e, 0), t);
                float hz = WaveHeight(p + float2(0, e), t);
                float2 grad = float2(hx - h0, hz - h0) / e * (_WaveStrength * 0.08);
                float3 N = normalize(float3(-grad.x, 1.0, -grad.y));

                float3 V = GetWorldSpaceNormalizeViewDir(posWS);
                float waterEye = -TransformWorldToView(posWS).z;

                // ── 물 밑 (굴절 + 깊이) ──
                float depthDiff = _DepthDistance * 0.6;   // 물 표면에서 바닥까지 (시선 방향, m). 깊이 효과를 끄면 중간값
                float2 refrUV = screenUV;
            #if defined(_DEPTH_EFFECTS)
                depthDiff = max(0.0, SceneEyeDepth(screenUV) - waterEye);
            #endif
            #if defined(_REFRACTION)
                refrUV = screenUV + N.xz * _Refraction * saturate(depthDiff * 2.0);
                #if defined(_DEPTH_EFFECTS)
                    // 굴절된 자리가 물 앞쪽 물체면 원래 자리로 (가장자리 번짐 방지)
                    if (SceneEyeDepth(refrUV) < waterEye) refrUV = screenUV;
                    else depthDiff = max(0.0, SceneEyeDepth(refrUV) - waterEye);
                #endif
            #endif

                // 실제 수직 깊이에 가깝게 (비스듬히 볼수록 시선 거리가 길다)
                float depth = depthDiff * saturate(V.y + 0.15);
                half deepK = saturate(depth / max(0.01, _DepthDistance));
                deepK = sqrt(deepK);

                // 빛
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light mainLight = GetMainLight(shadowCoord, posWS, half4(1, 1, 1, 1));
                half shadow = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 ambient = SampleSH(N);
                half3 lightCol = mainLight.color * shadow;

                half3 deepLit = _DeepColor.rgb * (ambient + lightCol * saturate(dot(N, mainLight.direction)) * 0.6);
                half3 below;
            #if defined(_REFRACTION)
                half3 sceneCol = SampleSceneColor(refrUV);
                half3 tinted = sceneCol * lerp(half3(1, 1, 1), _ShallowColor.rgb * 1.6, 1.0 - _Clarity * 0.6);
                below = lerp(tinted, deepLit, deepK * (1.0 - _Clarity * 0.5));
                below = lerp(below, deepLit, saturate(deepK * deepK));
            #else
                below = lerp(_ShallowColor.rgb * (ambient + lightCol * 0.6), deepLit, deepK);
            #endif

                // ── 반사 ──
                half NdotV = saturate(dot(N, V));
                half fresnel = _ReflectionMin + (1.0 - _ReflectionMin) * pow(1.0 - NdotV, _FresnelPower);
                float3 R = reflect(-V, N);
                half3 sky = lerp(GlossyEnvironmentReflection(R, 0.15h, 1.0h), unity_AmbientSky.rgb * 1.25, _SkyFromAmbient);
                half3 refl = sky;
                if (_RSReflectionOn > 1.5)
                {
                    // 화면 공간 반사: 둑 · 소품 · 캐릭터처럼 화면에 보이는 것을 비추고, 못 맞히면 하늘
                    float hit;
                    half3 ssr = RSTraceReflection(posWS + float3(0, 0.02, 0), R, _RSWaterSSR.x, _RSWaterSSR.y, _RSWaterSSR.z, hit);
                    refl = lerp(sky, ssr, hit);
                }
                else if (_RSReflectionOn > 0.5)
                {
                    float2 ruv = screenUV + N.xz * _ReflectionDistort;
                    refl = SAMPLE_TEXTURE2D_LOD(_RSReflectionTex, sampler_RSReflectionTex, ruv, 0).rgb;
                }
                refl *= _ReflectionTint.rgb;
                half3 col = lerp(below, refl, saturate(fresnel * _ReflectionStrength));

                // ── 햇빛 반짝임 (픽셀 단위로 톡톡) ──
                float3 H = normalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), _GlintSharpness);
                half sparkle = step(_GlintThreshold, VNoise(p * 7.0 + t * 3.0));
                col += lightCol * spec * sparkle * _GlintStrength;

                // ── 가장자리 거품 + 투명 ──
                half alpha = 1.0;
            #if defined(_DEPTH_EFFECTS)
                half foamN = VNoise(p * 3.1 + float2(t * 0.8, -t * 0.5));
                half foamMask = 1.0 - saturate(depthDiff / max(0.001, _FoamWidth));
                half foam = step(0.5, foamMask - foamN * _FoamNoise * 0.6);
                col = lerp(col, _FoamColor.rgb * (ambient + lightCol * 0.8), foam * _FoamColor.a);
                alpha = saturate(depthDiff / max(0.001, _EdgeFade));
            #endif
            #if !defined(_REFRACTION)
                alpha *= lerp(0.55, 0.95, deepK);
            #endif

                col = MixFog(col, IN.fogCoord);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
