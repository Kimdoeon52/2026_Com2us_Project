// RE:AL STEEL - 나노봇 보스 셰이더 (URP)
//
// RSNanoSwarm 이 Graphics.RenderPrimitives 로 봇 수만큼 사각형을 한 번에 그린다.
// 메시가 없다: 정점 번호(0~5)로 사각형 모서리를, 인스턴스 번호로 봇 데이터(_Bots)를 읽는다.
//
// 빌보드: 모든 봇이 카메라 평면(_PlaneRight · _PlaneUp, 카메라마다 스크립트가 넣음)을 공유하는 평행 빌보드.
// 봇마다: 색조(tint) · 발광(glow, 색조 색으로 빛남) · 번쩍임 · 흰 테두리(rim, 3페에서만) · 늘이기(stretch, 빠른 봇은 진행 방향으로 길게)
// 3페: _Dark 가 1 로 가면 봇이 검은 실루엣이 되고 rim 봇만 흰 빛을 낸다
//
// 조명 · 그림자 · 깊이 처리는 캐릭터 셰이더와 같다
//   알파 오려내기 + 불투명 큐 (DOF 가 보스를 인식) / 조명은 '색조로' 받기 / 그림자 캐스터 · DepthOnly · DepthNormals
//
// Unity 6000.0.x / URP 17.x 기준. StructuredBuffer 를 정점 셰이더에서 읽으므로 target 4.5.

Shader "RE_AL STEEL/NanoBot Pixel Lit"
{
    Properties
    {
        [Header(Tile)]
        [Space(4)]
        _Color      ("색조", Color) = (1, 1, 1, 1)
        _Cutoff     ("오려낼 기준값", Range(0, 1)) = 0.5

        [Header(Lighting)]
        [Space(4)]
        _LightStrength ("조명 반영   (0 원본색 / 1 완전히 조명대로)", Range(0, 1)) = 0.7
        _NormalLean    ("법선 기울이기   (0 카메라쪽 / 1 하늘쪽)", Range(0, 1)) = 0.5

        [Header(FX)]
        [Space(4)]
        _GlowStrength ("발광 세기   (파츠 '발광' 값에 곱함)", Range(0, 8)) = 1.5
        _FlashStrength ("번쩍임 세기", Range(0, 1)) = 0.85
        _RimStrength ("3페 흰 테두리 세기", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "Queue"          = "AlphaTest"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/RSIndirect.hlsl"     // 간접광 (RS 간접광 컴포넌트)

        TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4  _Color;
            float  _Cutoff;
            float  _LightStrength;
            float  _NormalLean;
            float  _GlowStrength;
            float  _FlashStrength;
            float  _RimStrength;
        CBUFFER_END

        // ── 보스마다 스크립트가 MaterialPropertyBlock 으로 넣는 값 ──
        struct NanoBot
        {
            float3 pos;
            float  roll;
            float  scale;
            float  flash;
            float  glow;
            float  rim;
            float  stretch;
            uint   tile;
            uint   color;
            float  pad;
        };
        StructuredBuffer<NanoBot> _Bots;

        float4 _PlaneRight;     // 보스 평면 오른쪽 (월드)
        float4 _PlaneUp;        // 보스 평면 위쪽
        float4 _PlaneNormal;    // 카메라 쪽
        float4 _AtlasInfo;      // x 열 수, y 행 수, z 칸 크기(px, 여백 포함), w 여백(px)
        float4 _AtlasSize;      // x 너비(px), y 높이(px), z 타일 크기(px)
        float  _TileWorld;      // 타일 한 변 (m)
        float  _BossFlip;       // 1 = 왼쪽을 봄 (그림 좌우 반전)
        half4  _BossFlashColor;
        float  _Dark;           // 0 금속 봇 / 1 검은 실루엣 (3페)
        float  _DarkTint;       // 검게 될 때 남는 밝기
        half4  _RimColor;

        static const float2 kCorners[6] =
        {
            float2(0, 0), float2(1, 0), float2(1, 1),
            float2(0, 0), float2(1, 1), float2(0, 1)
        };

        struct BotVertex
        {
            float3 positionWS;
            float2 uv;
            half   flash;
            half   glow;
            half   rim;
            half4  tint;
        };

        BotVertex BuildBotVertex(uint vertexID, uint instanceID)
        {
            NanoBot b = _Bots[instanceID];
            float2 c = kCorners[vertexID % 6];

            // 타일 중심 기준 사각형 → 봇 회전 → 보스 평면 위로
            float2 q = (c - 0.5) * (_TileWorld * b.scale);
            q.x *= 1.0 + b.stretch;     // 진행 방향(회전 전 x)으로 늘인다 — 스미어
            float s, co;
            sincos(b.roll, s, co);
            float2 r = float2(q.x * co - q.y * s, q.x * s + q.y * co);

            BotVertex o;
            o.positionWS = b.pos + _PlaneRight.xyz * r.x + _PlaneUp.xyz * r.y;

            // 아틀라스 칸 (이미지 왼쪽 위 기준으로 채워져 있다)
            uint cols = max(1u, (uint)_AtlasInfo.x);
            float col = (float)(b.tile % cols);
            float row = (float)(b.tile / cols);
            float u = _BossFlip > 0.5 ? 1.0 - c.x : c.x;
            float2 px = float2(col * _AtlasInfo.z + _AtlasInfo.w + u * _AtlasSize.z,
                               row * _AtlasInfo.z + _AtlasInfo.w + (1.0 - c.y) * _AtlasSize.z);
            o.uv = float2(px.x / _AtlasSize.x, 1.0 - px.y / _AtlasSize.y);

            o.flash = (half)b.flash;
            o.glow  = (half)b.glow;
            o.rim   = (half)b.rim;
            o.tint  = half4(b.color & 0xFF, (b.color >> 8) & 0xFF, (b.color >> 16) & 0xFF, (b.color >> 24) & 0xFF) / 255.0;
            return o;
        }

        half4 SampleTile(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
        }
        ENDHLSL

        // ─────────────────────────────────────────────────────────────
        // 메인 패스
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   NbVert
            #pragma fragment NbFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            // Forward+ 키워드는 URP 버전마다 이름이 다르다. 둘 다 선언해 둔다.
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half3  fx         : TEXCOORD3;   // x 번쩍임, y 발광, z 테두리
                half   fogCoord   : TEXCOORD4;
                half4  tint       : TEXCOORD5;
            };

            Varyings NbVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                Varyings OUT = (Varyings)0;
                OUT.positionWS = v.positionWS;
                OUT.positionCS = TransformWorldToHClip(v.positionWS);
                OUT.uv         = v.uv;
                OUT.normalWS   = normalize(lerp(_PlaneNormal.xyz, float3(0.0, 1.0, 0.0), _NormalLean));
                OUT.fx         = half3(v.flash, v.glow, v.rim);
                OUT.tint       = v.tint;
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 NbFrag(Varyings IN) : SV_Target
            {
                half4 albedo = SampleTile(IN.uv);
                clip(albedo.a - _Cutoff);
                half3 tintRGB = IN.tint.rgb;
                albedo.rgb *= tintRGB;
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * _DarkTint, _Dark);   // 3페 검은 실루엣

                float3 n = normalize(IN.normalWS);

                InputData inputData       = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = n;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = IN.fogCoord;
                inputData.vertexLighting  = half3(0, 0, 0);
                inputData.bakedGI         = RSIndirectGI(SampleSH(n), IN.positionWS, n, _RSIndirectReceive.z);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask      = half4(1, 1, 1, 1);

                SurfaceData sd = (SurfaceData)0;
                sd.albedo     = albedo.rgb;
                sd.alpha      = 1.0;
                sd.specular   = half3(0, 0, 0);
                sd.metallic   = 0;
                sd.smoothness = 0;
                sd.normalTS   = half3(0, 0, 1);
                sd.emission   = half3(0, 0, 0);
                sd.occlusion  = 1;

                half4 lit = UniversalFragmentBlinnPhong(inputData, sd);

                // 원화 보존: 원본 색과 조명 받은 색 사이
                half3 col = lerp(albedo.rgb, lit.rgb, _LightStrength);

                // 발광 — 색조 색으로 빛난다 (반짝이 봇 · 빔 · 눈). 블룸이 받도록 1 을 넘길 수 있다
                col += tintRGB * IN.fx.y * _GlowStrength * 0.5;

                // 3페 흰 테두리 — 검은 실루엣의 윤곽 · 탄
                col += _RimColor.rgb * (IN.fx.z * _Dark * _RimStrength);

                // 번쩍임 (피격 · 다시 붙음)
                col = lerp(col, _BossFlashColor.rgb, saturate(IN.fx.x) * _FlashStrength);

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 그림자 캐스터 — 타일 모양 그대로 그림자가 진다
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            SVaryings ShadowVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                float3 posWS = v.positionWS;

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - posWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif

                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, _PlaneNormal.xyz, lightDirWS));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                SVaryings OUT;
                OUT.positionCS = cs;
                OUT.uv = v.uv;
                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target
            {
                clip(SampleTile(IN.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthOnly — 이게 없으면 DOF 가 보스를 무시한다
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            DVaryings DepthVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                DVaryings OUT;
                OUT.positionCS = TransformWorldToHClip(v.positionWS);
                OUT.uv = v.uv;
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                clip(SampleTile(IN.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthNormals — SSAO 등으로 '깊이+법선' 프리패스를 쓸 때
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   DNVert
            #pragma fragment DNFrag

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            DNVaryings DNVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                DNVaryings OUT;
                OUT.positionCS = TransformWorldToHClip(v.positionWS);
                OUT.uv = v.uv;
                OUT.normalWS = normalize(lerp(_PlaneNormal.xyz, float3(0.0, 1.0, 0.0), _NormalLean));
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                clip(SampleTile(IN.uv).a - _Cutoff);
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
