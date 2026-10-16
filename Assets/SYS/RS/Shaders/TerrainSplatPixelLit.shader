// RE:AL STEEL - 지형 스플랫 픽셀 셰이더 (URP)
//
// 한 메시 위에서 정점 컬러로 지형 레이어를 나눈다. 옥토패스식 "길 따로, 풀 따로" 표현용.
//
//   정점 컬러 0 (검정) = 바탕  → 윗면은 흙, 가파른 면은 절벽 (법선으로 자동)
//   R = 길       G = 콘크리트       B = 고물       A = 진흙·기름
//   UV2.x = 풀 (다섯 번째 레이어 — 정점 컬러 채널이 모자라서 UV 채널 2 에 굽는다. 가파른 면은 절벽으로)
//
// 핵심: 레이어를 부드럽게 "섞지" 않는다. 텍셀마다 한 레이어를 "고른다".
//   부드럽게 섞으면 경계에서 두 픽셀 텍스처가 반투명하게 겹쳐 뭉개진다 — 픽셀 아트에는 독.
//   여기서는 텍셀 하나하나가 (정점 컬러 가중치 + 노이즈 + 텍스처 밝기) 점수로 승자를 뽑는다.
//   그래서 경계는 격자와 상관없이 들쭉날쭉하고, 각 픽셀은 원래 텍스처 픽셀 그대로 선명하다.
//
// 텍셀 밀도: UV 를 안 쓴다. 월드 좌표를 주축으로 투영하고, 텍스처마다 자기 픽셀 크기로
//   "PPU ÷ 텍스처 픽셀" 스케일을 자동 적용한다. 16px 텍스처든 128px 이든 화면 픽셀 크기가 같다.
//   노이즈도 같은 텍셀 격자에 스냅되므로 경계 톱니가 텍스처 픽셀과 정확히 맞물린다.
//
// 정점 컬러는 RS 지형(Assets/SYS/RSTerrain)이 굽는다.
// 손으로 칠하려면 Polybrush 의 정점 컬러 브러시를 쓰면 된다 (채널 R/G/B/A).
//
// Unity 6000.0.x / URP 17.x 기준. URP 14 이상이면 그대로 동작하도록 키워드를 양쪽 다 선언했다.

Shader "RE_AL STEEL/Terrain Splat Pixel Lit"
{
    Properties
    {
        [Header(Base   vertex color 0)]
        _BaseTop      ("바탕 윗면 - 흙", 2D) = "white" {}
        _BaseTopColor ("  색조", Color) = (1, 1, 1, 1)
        _BaseSide     ("바탕 옆면 - 절벽", 2D) = "white" {}
        _BaseSideColor("  색조", Color) = (1, 1, 1, 1)

        [Header(Layers)]
        _LayerR       ("R  길", 2D) = "white" {}
        _ColorR       ("  색조", Color) = (1, 1, 1, 1)
        _LayerG       ("G  콘크리트", 2D) = "white" {}
        _ColorG       ("  색조", Color) = (1, 1, 1, 1)
        _LayerB       ("B  고물", 2D) = "white" {}
        _ColorB       ("  색조", Color) = (1, 1, 1, 1)
        _LayerA       ("A  진흙 / 기름", 2D) = "white" {}
        _ColorA       ("  색조", Color) = (1, 1, 1, 1)
        _LayerGrass   ("풀  (UV2)", 2D) = "white" {}
        _ColorGrass   ("  색조", Color) = (1, 1, 1, 1)

        [Header(Pixel Density)]
        _PPU          ("PPU  (픽셀 / 유닛)", Float) = 32

        [Header(Boundary)]
        _EdgeNoise    ("경계 흐트러짐   (0 = 정점 컬러 경계 그대로)", Range(0, 0.6)) = 0.35
        _NoiseSize    ("  노이즈 덩어리 크기 (텍셀)", Range(0.5, 16)) = 4
        _PixelGrain   ("  낱알 섞기   (0 = 덩어리만 / 1 = 텍셀마다 흩뿌림)", Range(0, 1)) = 0.3
        _HeightBlend  ("텍스처 밝기 반영   (밝은 돌·자갈이 경계를 뚫고 나옴)", Range(0, 0.6)) = 0.2

        [Header(Cliff)]
        _CliffY       ("절벽 기준   (면 법선 y 가 이보다 작으면 옆면 텍스처)", Range(0, 1)) = 0.62
        _CliffDither  ("  절벽 윗선 흐트러짐", Range(0, 0.3)) = 0.1

        [Header(See Through)]
        [ToggleUI] _SeeThrough ("시야 가리면 뚫기   (캐릭터 앞을 가릴 때 점무늬 구멍 · RSSeeThrough)", Float) = 1

        [Header(Debug)]
        [Toggle(_SPLAT_DEBUG)] _SplatDebug ("레이어 색으로 보기", Float) = 0
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("그림자 안 받기", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "Queue"          = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/RSSeeThrough.hlsl"   // 시야 가림 투명 (RSSeeThrough 컴포넌트)
        #include "Include/RSIndirect.hlsl"     // 간접광 (RS 간접광 컴포넌트)

        TEXTURE2D(_BaseTop);  SAMPLER(sampler_BaseTop);
        TEXTURE2D(_BaseSide); SAMPLER(sampler_BaseSide);
        TEXTURE2D(_LayerR);   SAMPLER(sampler_LayerR);
        TEXTURE2D(_LayerG);   SAMPLER(sampler_LayerG);
        TEXTURE2D(_LayerB);   SAMPLER(sampler_LayerB);
        TEXTURE2D(_LayerA);   SAMPLER(sampler_LayerA);
        TEXTURE2D(_LayerGrass); SAMPLER(sampler_LayerGrass);

        // SRP Batcher: 머티리얼 프로퍼티(TexelSize 포함) 전부 여기에
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseTop_ST;  float4 _BaseTop_TexelSize;
            float4 _BaseSide_ST; float4 _BaseSide_TexelSize;
            float4 _LayerR_ST;   float4 _LayerR_TexelSize;
            float4 _LayerG_ST;   float4 _LayerG_TexelSize;
            float4 _LayerB_ST;   float4 _LayerB_TexelSize;
            float4 _LayerA_ST;   float4 _LayerA_TexelSize;
            float4 _LayerGrass_ST; float4 _LayerGrass_TexelSize;
            half4  _BaseTopColor;
            half4  _BaseSideColor;
            half4  _ColorR;
            half4  _ColorG;
            half4  _ColorB;
            half4  _ColorA;
            half4  _ColorGrass;
            float  _PPU;
            float  _EdgeNoise;
            float  _NoiseSize;
            float  _PixelGrain;
            float  _HeightBlend;
            float  _CliffY;
            float  _CliffDither;
            float  _SplatDebug;
            float  _ReceiveShadowsOff;
            float  _SeeThrough;
        CBUFFER_END
        ENDHLSL

        // ─────────────────────────────────────────────────────────────
        // 메인 패스
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   SplatVert
            #pragma fragment SplatFrag

            #pragma shader_feature_local_fragment _SPLAT_DEBUG
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 그림자 (RS Lighting · 라이트 쿠키)
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv2        : TEXCOORD2;    // x = 풀 레이어
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                half4  splat       : TEXCOORD2;
                half4  fogAndLight : TEXCOORD3;   // x = fog, yzw = 버텍스 라이팅
                half   grass       : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── 노이즈 (텍셀 격자에 스냅된 좌표로만 부른다) ──
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 레이어마다 다른 노이즈. 덩어리(value noise 2옥타브) + 텍셀 낱알(hash) 섞기
            float LayerNoise(float2 cell, float k)
            {
                float2 q = cell / max(_NoiseSize, 0.5) + k * float2(37.1, 17.9);
                float v = ValueNoise(q) * 0.65 + ValueNoise(q * 2.1 + 5.2) * 0.35;
                float g = Hash21(cell + k * 13.7);
                return lerp(v, g, _PixelGrain);
            }

            half Luma(half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

            // 월드 좌표 2D → 이 텍스처의 UV. 텍스처 픽셀 수에서 스케일을 자동으로 잡는다.
            float2 TexUV(float2 coord, float4 texelSize)
            {
                return coord * _PPU * texelSize.xy;
            }

            Varyings SplatVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.splat      = IN.color;
                OUT.grass      = IN.uv2.x;

                OUT.fogAndLight.x   = ComputeFogFactor(pos.positionCS.z);
                OUT.fogAndLight.yzw = VertexLighting(pos.positionWS, nrm.normalWS);
                return OUT;
            }

            half4 SplatFrag(Varyings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normalWS = normalize(IN.normalWS);
                float3 p  = IN.positionWS;
                float3 an = abs(normalWS);
                float3 sg = step(0.0, normalWS) * 2.0 - 1.0;

                // 주축 투영 (TriplanarPixelLit 와 같은 부호 규칙 — 반대편에서 거울상 안 됨)
                float2 coord;
                if (an.y >= an.x && an.y >= an.z) coord = float2(p.x, p.z * sg.y);
                else if (an.x >= an.z)            coord = float2(p.z * -sg.x, p.y);
                else                              coord = float2(p.x * sg.z, p.y);

                // 이 픽셀이 속한 텍셀 칸 (노이즈는 전부 이 칸 단위 → 경계 톱니 = 텍셀 크기)
                float2 cell = floor(coord * _PPU) + 0.5;

                half4 cTop  = SAMPLE_TEXTURE2D(_BaseTop,  sampler_BaseTop,  TexUV(coord, _BaseTop_TexelSize))  * _BaseTopColor;
                half4 cSide = SAMPLE_TEXTURE2D(_BaseSide, sampler_BaseSide, TexUV(coord, _BaseSide_TexelSize)) * _BaseSideColor;
                half4 cR    = SAMPLE_TEXTURE2D(_LayerR,   sampler_LayerR,   TexUV(coord, _LayerR_TexelSize))   * _ColorR;
                half4 cG    = SAMPLE_TEXTURE2D(_LayerG,   sampler_LayerG,   TexUV(coord, _LayerG_TexelSize))   * _ColorG;
                half4 cB    = SAMPLE_TEXTURE2D(_LayerB,   sampler_LayerB,   TexUV(coord, _LayerB_TexelSize))   * _ColorB;
                half4 cA    = SAMPLE_TEXTURE2D(_LayerA,   sampler_LayerA,   TexUV(coord, _LayerA_TexelSize))   * _ColorA;
                half4 cGr   = SAMPLE_TEXTURE2D(_LayerGrass, sampler_LayerGrass, TexUV(coord, _LayerGrass_TexelSize)) * _ColorGrass;

                // 바탕: 윗면 흙 / 절벽. 기준선 근처 면은 텍셀 단위로 섞여 절벽 윗선이 들쭉날쭉해진다.
                bool cliff  = normalWS.y < _CliffY + (Hash21(cell + 91.7) - 0.5) * _CliffDither;
                half4 cBase = cliff ? cSide : cTop;

                // 점수 = 정점 컬러 가중치 + 노이즈 + 텍스처 밝기. 최고점 하나만 고른다.
                half4 w  = saturate(IN.splat);
                float wg = saturate(IN.grass);
                float wb = saturate(1.0 - (w.r + w.g + w.b + w.a + wg));
                float spread = _EdgeNoise * 2.0;

                float4 nz = float4(LayerNoise(cell, 1), LayerNoise(cell, 2), LayerNoise(cell, 3), LayerNoise(cell, 4));
                float  nb = LayerNoise(cell, 0);
                float4 lum = float4(Luma(cR.rgb), Luma(cG.rgb), Luma(cB.rgb), Luma(cA.rgb));

                float4 sc = (float4)w + (nz - 0.5) * spread + (lum - 0.5) * _HeightBlend;
                sc = lerp(float4(-10, -10, -10, -10), sc, step(0.02, (float4)w));   // 가중치 0 인 레이어는 절대 안 나온다
                float  sb = wb + (nb - 0.5) * spread + (Luma(cBase.rgb) - 0.5) * _HeightBlend;
                float  sgr = wg + (LayerNoise(cell, 5) - 0.5) * spread + (Luma(cGr.rgb) - 0.5) * _HeightBlend;
                if (wg < 0.02) sgr = -10.0;

                half4 albedo = cBase;
                float best = sb;
                half3 dbg = cliff ? half3(0.35, 0.35, 0.38) : half3(0.55, 0.42, 0.28);
                if (sc.x > best) { best = sc.x; albedo = cR; dbg = half3(0.95, 0.85, 0.35); }
                if (sc.y > best) { best = sc.y; albedo = cG; dbg = half3(0.80, 0.80, 0.80); }
                if (sc.z > best) { best = sc.z; albedo = cB; dbg = half3(0.85, 0.40, 0.15); }
                if (sc.w > best) { best = sc.w; albedo = cA; dbg = half3(0.20, 0.30, 0.55); }
                // 풀은 가파른 면에선 절벽 텍스처 (풀이 벽에 붙어 자라지 않게)
                if (sgr > best)  { best = sgr;  albedo = cliff ? cSide : cGr; dbg = cliff ? half3(0.35, 0.35, 0.38) : half3(0.35, 0.75, 0.25); }

            #ifdef _SPLAT_DEBUG
                albedo.rgb = dbg;
            #endif

                InputData inputData     = (InputData)0;
                inputData.positionWS    = IN.positionWS;
                inputData.normalWS      = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

            #if defined(_RECEIVE_SHADOWS_OFF)
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #else
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #endif

                inputData.fogCoord       = IN.fogAndLight.x;
                inputData.vertexLighting = IN.fogAndLight.yzw;
                inputData.bakedGI        = RSIndirectGI(SampleSH(normalWS), IN.positionWS, normalWS, _RSIndirectReceive.x);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);

            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
            #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo.rgb;
                surfaceData.alpha       = 1;
                surfaceData.specular    = half3(0, 0, 0);   // 번들거림 원천 차단
                surfaceData.metallic    = 0;
                surfaceData.smoothness  = 0;
                surfaceData.normalTS    = half3(0, 0, 1);
                surfaceData.emission    = half3(0, 0, 0);
                surfaceData.occlusion   = 1;

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 그림자 캐스터
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            SVaryings ShadowVert(SAttributes IN)
            {
                SVaryings OUT = (SVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthOnly — DOF 등 깊이를 쓰는 포스트용
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthVert(DAttributes IN)
            {
                DVaryings OUT = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthNormals — SSAO 등 노멀 버퍼용
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DNVert(DNAttributes IN)
            {
                DNVaryings OUT = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
