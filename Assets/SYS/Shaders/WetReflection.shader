// RE:AL STEEL - 젖은 바닥 반사 겹 (RS 지형 젖음 칠 · 젖는 바닥 컴포넌트가 씌운다)
//
// 바닥 위에 같은 메시를 한 번 더, 반투명하게 그린다. 하는 일:
//   1. 젖은 만큼 아래 바닥을 어둡게 (물이 스민 색)
//   2. 화면 공간 반사: 반사 방향으로 깊이 텍스처를 따라가 부딪힌 곳의 화면 색(Opaque Texture)을 가져온다
//      거칠기만큼 세로로 번지게 흐리고, 못 맞히면(화면 밖) 그쪽 화면 가장자리의 먼 풍경 색(주변 빛) + 앰비언트
//   2-1. 바닥 요철: 아래 바닥의 화면 색 밝기를 높이로 보고 법선을 흔든다 → 돌 윗면은 반짝이고 번지고,
//        어두운 틈(골)은 물이 고여 거울처럼 (Replaced 식 빗길)
//   2-2. 물웅덩이 얼룩: 큰 노이즈로 '웅덩이(매끈 · 잘 비침)' 와 '덜 젖은 곳(거칠 · 약하게)' 을 나눈다
//        → 전체가 고르게 매끈하면 얼음 · 유리처럼 보인다
//   합성: 바닥은 어둡게(곱하기), 반사는 대부분 더하기 — 얇은 물막 아래로 바닥이 그대로 보여야 젖은 길이다.
//        반사가 바닥을 덮어 버리면(반사 불투명도 ↑) 얼음 · 유리가 된다
//   3. 해 · 달 · 포인트/스폿 라이트(가로등 · 네온 · 캐릭터 빛) 반사 — 젖은 길처럼 카메라 쪽으로 세로로 길게 번진다
//      (라이트는 화면에 그려진 물체가 아니라서 화면 공간 반사로는 안 비친다 → 따로 계산)
//   4. 잔물결로 반사를 흔들기, 반사 좌표를 텍셀 크기로 끊어 도트 반사
//
// 젖은 정도 = 지형 젖음 텍스처(RS 지형이 넘겨줌, 없으면 정점 UV2.y) + 오브젝트별 젖음 + 비 온 정도 × 비 반응.
// 경계는 물웅덩이처럼 들쭉날쭉하게, 반사는 물이 고인 것처럼 수평으로 — 각진(평면 음영) 지형의 삼각형이 드러나지 않게.
// 불투명을 다 그린 뒤 그려야 화면 색을 읽을 수 있어서 투명 큐. 안개(Transparent+50)보다 먼저.
Shader "RE_AL STEEL/Wet Reflection"
{
    Properties
    {
        [HideInInspector] _WetAmount ("항상 젖음", Range(0, 1)) = 0
        [HideInInspector] _WetRain ("비 반응", Range(0, 1)) = 1
        [HideInInspector] _UseVertexWet ("정점 젖음 (지형 UV2.y)", Float) = 1
        [HideInInspector] _WetTint ("반사 색", Color) = (1, 1, 1, 1)
        [HideInInspector] _WetDarken ("어둡게 배율", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WetReflection"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha   // 미리 곱한 알파: 결과 = 반사 + 아래 × (1 - 알파)
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1                // 같은 면을 다시 그리므로 살짝 앞으로

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WetVert
            #pragma fragment WetFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // 투명 오브젝트라 화면 공간 그림자 텍스처 대신 그림자맵을 직접 본다
            #define _SURFACE_TYPE_TRANSPARENT 1

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Include/RSSeeThrough.hlsl"         // 캐릭터 앞을 가리는 지형이 뚫릴 때 겹도 같이 뚫린다
            #include "Include/RSScreenReflection.hlsl"   // 화면 공간 반사 (물과 같이 씀)

            CBUFFER_START(UnityPerMaterial)
                float  _WetAmount;
                float  _WetRain;
                float  _UseVertexWet;
                half4  _WetTint;
                float  _WetDarken;
            CBUFFER_END

            // 젖은 바닥 · 반사 설정(RSWetness)이 채우는 전역값
            float  _RSWetGlobal;       // 비 온 정도
            float4 _RSWetLook;         // x = 어둡게, y = 반사, z = 정면 최소 반사, w = 반짝임
            half4  _RSWetTintGlobal;   // 반사 색
            float4 _RSWetRipple;       // x = 물결, y = 촘촘함, z = 속도, w = 반짝임 날카로움
            float4 _RSWetTrace;        // x = 걸음 수, y = 최대 거리, z = 두께, w = 픽셀 밀도
            float4 _RSWetShape;        // x = 경계 흔들림, y = 경계 무늬 촘촘함(/m), z = 바닥 기울기 따라 비치기, w = 안 씀
            float4 _RSWetGlint;        // x = 라이트 반사 세기, y = 세로 번짐, z = 가로 폭(지수), w = 안 씀
            float4 _RSWetGloss;        // x = 거칠기, y = 반사 세로 번짐 배율, z = 바닥 요철, w = 틈새 물 고임
            float  _RSWetEnv;          // 주변 빛 반사 (못 맞힌 반사를 화면 가장자리 풍경 색으로)
            float4 _RSWetPuddle;       // x = 웅덩이 얼룩, y = 웅덩이 크기(/m), z = 밝은 것만 비치기, w = 반사 불투명도(바닥 가림)

            // RS 지형이 조각마다 넘겨주는 젖음 텍스처 (MaterialPropertyBlock). ST.x = 0 이면 없음 → 정점 UV2.y
            TEXTURE2D(_RSWetMap); SAMPLER(sampler_RSWetMap);
            float4 _RSWetMapST;        // 로컬 xz * xy + zw = uv

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv2        : TEXCOORD2;   // y = 지형 젖음 칠
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  wet        : TEXCOORD2;   // 정점 젖음 (텍스처 없을 때)
                float  fogCoord   : TEXCOORD3;
            };

            Varyings WetVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.wet = IN.uv2.y * _UseVertexWet;
                OUT.fogCoord = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            float WetHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float WetNoise(float2 x)   // 부드러운 값 노이즈 0 ~ 1
            {
                float2 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(WetHash(i), WetHash(i + float2(1, 0)), f.x),
                            lerp(WetHash(i + float2(0, 1)), WetHash(i + float2(1, 1)), f.x), f.y);
            }

            // 빛 반사 모양: 반사 방향 R 과 빛 방향 d 가 가로(방위각)로는 좁게, 세로(높이)로는 넓게 맞으면 밝다
            // → 젖은 길에 비친 가로등처럼 카메라 쪽으로 길쭉한 빛줄기. 3단계로 끊어 도트 느낌
            float WetGlint(float3 R, float3 d, float rough)
            {
                float2 rh = R.xz, dh = d.xz;
                float lr = length(rh), ld = length(dh);
                float az = (lr > 1e-4 && ld > 1e-4) ? saturate(dot(rh / lr, dh / ld)) : 1.0;
                float el = R.y - d.y;
                float st = max(_RSWetGlint.y, 0.02) + rough * 0.15;                 // 거칠수록 길고
                float g = pow(az, max(_RSWetGlint.z, 8.0) / (1.0 + rough * 3.0))    // 넓게
                        * exp(-el * el / (st * st));
                return g * g * (3.0 - 2.0 * g);   // 부드럽게 (가운데는 또렷, 가장자리는 스르르)
            }

            float WetLum(float2 uv) { return dot(RSSR_Color(uv), half3(0.299, 0.587, 0.114)); }

            half4 WetFrag(Varyings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, 1.0);
                float3 n = normalize(IN.normalWS);
                float up = saturate((n.y - 0.6) / 0.3);   // 윗면만 젖는다

                float3 p = IN.positionWS;
                float ppu = _RSWetTrace.w;
                float2 cell = floor(p.xz * max(ppu, 1.0));
                if (ppu > 0.0) p.xz = (cell + 0.5) / ppu;   // 텍셀 단위로 끊기 (도트)

                // 칠한 젖음: 지형 텍스처(네 모서리를 고르게 섞음) 또는 정점 값
                float painted = IN.wet;
                if (_RSWetMapST.x > 0.0)
                {
                    float3 pOS = TransformWorldToObject(p);
                    painted = SAMPLE_TEXTURE2D_LOD(_RSWetMap, sampler_RSWetMap, pOS.xz * _RSWetMapST.xy + _RSWetMapST.zw, 0).r;
                }
                float wet = saturate(painted + _WetAmount + _RSWetGlobal * _WetRain);

                // 경계를 물웅덩이처럼: 안쪽(1)과 바깥(0)은 그대로, 중간만 노이즈로 흔든다
                float edge = _RSWetShape.x;
                if (edge > 0.0)
                {
                    float2 q = p.xz * max(_RSWetShape.y, 0.01);
                    float nz = WetNoise(q) * 0.65 + WetNoise(q * 2.3 + 17.0) * 0.35;
                    wet = saturate(wet * (1.0 + edge) - nz * edge);
                }
                wet *= up;
                if (ppu > 0.0) wet = saturate(floor(wet * 4.0 + WetHash(cell)) / 4.0);   // 4 단계 + 텍셀 디더
                clip(wet - 0.01);

                // 반사 법선: 고인 물은 수평이다. 지형은 삼각형마다 법선이 달라(각진 음영)
                // 그대로 쓰면 반사가 삼각형 조각으로 깨진다 → 기본은 수평, '기울기 따라 비치기' 만큼만 바닥을 따른다
                float3 nFlat = normalize(lerp(float3(0.0, 1.0, 0.0), n, saturate(_RSWetShape.z)));

                // 물웅덩이 얼룩: 1 = 웅덩이(매끈, 잘 비침), 0 = 덜 젖은 곳(거칠, 약하게)
                float puddle = 1.0;
                if (_RSWetPuddle.x > 0.0)
                {
                    float2 q = p.xz * max(_RSWetPuddle.y, 0.01) + 31.7;
                    float pn = WetNoise(q) * 0.7 + WetNoise(q * 2.7 + 5.3) * 0.3;
                    puddle = lerp(1.0, smoothstep(0.42, 0.6, pn), _RSWetPuddle.x);
                }

                // 바닥 요철 · 틈새: 아래 바닥이 이미 그려진 화면 색의 밝기를 높이로 본다 (텍스처 도트 한 칸 간격)
                float bump = _RSWetGloss.z * (1.0 - puddle * 0.8), cavity = 0.0;   // 웅덩이 위는 평평
                float3 nb = nFlat;
                if (bump > 0.0 || _RSWetGloss.w > 0.0)
                {
                    float tx = 1.0 / (ppu > 0.0 ? ppu : 32.0);
                    float hC = WetLum(RSSR_ScreenUV(TransformWorldToHClip(p)));
                    float hL = WetLum(RSSR_ScreenUV(TransformWorldToHClip(p - float3(tx, 0, 0))));
                    float hR = WetLum(RSSR_ScreenUV(TransformWorldToHClip(p + float3(tx, 0, 0))));
                    float hD = WetLum(RSSR_ScreenUV(TransformWorldToHClip(p - float3(0, 0, tx))));
                    float hU = WetLum(RSSR_ScreenUV(TransformWorldToHClip(p + float3(0, 0, tx))));
                    float avg = (hL + hR + hD + hU) * 0.25;
                    float norm = 1.0 / (avg + 0.05);                         // 어두운 곳 · 밝은 곳 요철 크기 비슷하게
                    float2 g = float2(hR - hL, hU - hD) * 0.5 * norm;
                    cavity = saturate((avg - hC) * norm * 3.0) * _RSWetGloss.w;   // 주변보다 어두움 = 틈 = 물 고임
                    nb = normalize(nFlat + float3(-g.x, 0.0, -g.y) * bump * 1.5 * (1.0 - cavity));
                }
                float rough = saturate(_RSWetGloss.x * (1.0 - cavity));   // 고인 물은 매끈
                rough = lerp(max(rough, 0.6), rough * 0.35, puddle);       // 덜 젖은 곳은 뿌옇게, 웅덩이는 또렷

                // 잔물결
                float3 nr = nb;
                if (_RSWetRipple.x > 0.0)
                {
                    float tt = _Time.y * _RSWetRipple.z;
                    float2 q = p.xz * _RSWetRipple.y;
                    float2 d = float2(sin(q.x * 1.7 + tt) + sin(q.y * 2.3 - tt * 1.3),
                                      sin(q.y * 1.9 + tt * 0.8) + sin(q.x * 2.1 + tt * 1.1));
                    nr = normalize(nb + float3(d.x, 0.0, d.y) * 0.05 * _RSWetRipple.x);
                }

                float3 V = normalize(p - _WorldSpaceCameraPos);   // 카메라 → 바닥
                float3 R = reflect(V, nr);
                R.y = abs(R.y);                                      // 요철로 바닥 아래를 향하면 위로
                float ndv = saturate(dot(nFlat, -V));
                float fres = _RSWetLook.z + (1.0 - _RSWetLook.z) * pow(1.0 - ndv, 5.0);

                float hit;
                half3 sky = SampleSH(R);
                half3 refl = RSGlossyReflection(p + nFlat * 0.02, R, rough, _RSWetGloss.y,
                                                _RSWetTrace.x, _RSWetTrace.y, _RSWetTrace.z, _RSWetEnv, sky, hit);
                // 밝은 것만 비치기: 실제 젖은 길은 불빛 · 밝은 물체만 또렷이 비치고 어두운 곳은 거의 안 비친다
                float rl = dot(refl, half3(0.299, 0.587, 0.114));
                refl *= lerp(1.0, smoothstep(0.06, 0.7, rl), _RSWetPuddle.z);
                refl *= _WetTint.rgb * _RSWetTintGlobal.rgb;

                // 해 · 달 반사: 둥근 반짝임 + 세로 빛줄기 중 큰 쪽. 요철 법선이라 돌 윗면마다 반짝인다
                Light L = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float s = pow(saturate(dot(R, L.direction)), max(8.0, _RSWetRipple.w) / (1.0 + rough * 3.0));
                s = max(smoothstep(0.1, 0.6, s), WetGlint(R, L.direction, rough));
                half3 spec = L.color * (L.shadowAttenuation * L.distanceAttenuation * s * _RSWetLook.w);

                // 포인트 · 스폿 라이트 반사 (가로등 · 네온 · 캐릭터 밤 빛)
            #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                if (_RSWetGlint.x > 0.0)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = p;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, p);
                        // 거리 감쇠를 0~1 로 눌러서: 라이트 범위 안이면 멀어도 비치고, 범위 끝에서 사라진다
                        float at = al.distanceAttenuation;
                        at = at / (at + 0.15);
                        spec += al.color * (at * WetGlint(R, al.direction, rough) * _RSWetGlint.x);
                    LIGHT_LOOP_END
                }
            #endif

                wet = saturate(wet * (1.0 + cavity));   // 틈은 더 젖어 보이게
                float r = wet * _RSWetLook.y * fres * lerp(0.3, 1.0, puddle);
                float dk = wet * _RSWetLook.x * _WetDarken * lerp(0.7, 1.0, puddle);
                float a = 1.0 - (1.0 - dk) * (1.0 - r * _RSWetPuddle.w);   // 반사가 바닥을 가리는 정도 (낮을수록 더하기)
                half3 col = refl * r + spec * wet * lerp(0.6, 1.0, puddle);

                // 안개 속에선 겹도 안개에 묻힌다
                float fog = ComputeFogIntensity(IN.fogCoord);
            #if !defined(FOG_LINEAR) && !defined(FOG_EXP) && !defined(FOG_EXP2)
                fog = 1.0;
            #endif
                return half4(col * fog, a * fog);
            }
            ENDHLSL
        }
    }
}
