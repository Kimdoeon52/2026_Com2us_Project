// RE:AL STEEL - 나노봇 보스 셰이더 (URP) — 2D 판 알갱이
//
// RSNanoSwarm 이 Graphics.RenderPrimitives 로 봇 수만큼 사각형을 한 번에 그린다.
// 메시가 없다: 정점 번호(0~5)로 사각형 모서리를, 인스턴스 번호로 봇 데이터(_Bots)를 읽는다.
//
// 2D 판 (11-10): 봇 하나 = 칸 하나 = 월드 XY 평면의 정사각형 (회전 · 텍스처 없음, 색 하나).
//   위치는 스크립트가 칸 중심에 맞춰 넣는다 → 판 위 봇은 픽셀처럼 딱 맞물린다.
//   open (비어 있는 이웃 칸: 1 위 · 2 아래 · 4 왼 · 8 오른) 으로 가장자리 법선을 만들어 3D 장면의 조명을 받는다.
//     윗면은 밝게 · 아랫면은 어둡게 (모래 더미 · 실루엣이 입체로 읽힌다)
//   3페: _Dark 가 1 로 가면 검은 실루엣, 가장자리 봇(rim)만 흰 빛
//
// 조명 · 그림자 · 깊이 처리는 캐릭터 셰이더와 같다
//   불투명 큐 (DOF 가 보스를 인식) / 그림자 캐스터 · DepthOnly · DepthNormals
//
// Unity 6000.0.x / URP 17.x 기준. StructuredBuffer 를 정점 셰이더에서 읽으므로 target 4.5.

Shader "RE_AL STEEL/NanoBot Pixel Lit"
{
    Properties
    {
        [Header(Color)]
        [Space(4)]
        _Color      ("색조", Color) = (1, 1, 1, 1)

        [Header(Lighting)]
        [Space(4)]
        _LightStrength ("조명 반영   (0 원본색 / 1 완전히 조명대로)", Range(0, 1)) = 0.6
        _EdgeTilt      ("가장자리 법선 기울기", Range(0, 1)) = 0.7
        _TopLight      ("윗면 밝게", Range(0, 1)) = 0.35
        _BottomDark    ("아랫면 어둡게", Range(0, 1)) = 0.3

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

        CBUFFER_START(UnityPerMaterial)
            half4  _Color;
            float  _LightStrength;
            float  _EdgeTilt;
            float  _TopLight;
            float  _BottomDark;
            float  _GlowStrength;
            float  _FlashStrength;
            float  _RimStrength;
        CBUFFER_END

        // ── 스크립트가 MaterialPropertyBlock 으로 넣는 값 ──
        struct NanoBot
        {
            float3 pos;
            float  size;
            float  flash;
            float  glow;
            float  rim;
            uint   open;
            uint   color;
            float3 pad;
        };
        StructuredBuffer<NanoBot> _Bots;

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
            float3 normalWS;
            half   flash;
            half   glow;
            half   rim;
            half   shade;     // 윗면 + / 아랫면 −
            half4  tint;
        };

        BotVertex BuildBotVertex(uint vertexID, uint instanceID)
        {
            NanoBot b = _Bots[instanceID];
            float2 c = kCorners[vertexID % 6];

            BotVertex o;
            float2 q = (c - 0.5) * b.size;
            o.positionWS = b.pos + float3(q.x, q.y, 0.0);

            // 비어 있는 이웃 → 바깥을 향한 법선 (판 법선은 카메라 쪽 -z)
            float up = (b.open & 1u) ? 1.0 : 0.0;
            float dn = (b.open & 2u) ? 1.0 : 0.0;
            float lf = (b.open & 4u) ? 1.0 : 0.0;
            float rt = (b.open & 8u) ? 1.0 : 0.0;
            float2 e = float2(rt - lf, up - dn);
            o.normalWS = normalize(float3(e * _EdgeTilt, -1.0));
            o.shade = (half)(up * _TopLight - dn * _BottomDark);

            o.flash = (half)b.flash;
            o.glow  = (half)b.glow;
            o.rim   = (half)(b.rim * (b.open != 0u ? 1.0 : 0.0));
            o.tint  = half4(b.color & 0xFF, (b.color >> 8) & 0xFF, (b.color >> 16) & 0xFF, 255) / 255.0;
            return o;
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
                float3 positionWS : TEXCOORD1;
                nointerpolation float3 normalWS : TEXCOORD2;
                nointerpolation half4  fx       : TEXCOORD3;   // x 번쩍임, y 발광, z 테두리, w 윗면/아랫면
                half   fogCoord   : TEXCOORD4;
                nointerpolation half4  tint     : TEXCOORD5;
            };

            Varyings NbVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                Varyings OUT = (Varyings)0;
                OUT.positionWS = v.positionWS;
                OUT.positionCS = TransformWorldToHClip(v.positionWS);
                OUT.normalWS   = v.normalWS;
                OUT.fx         = half4(v.flash, v.glow, v.rim, v.shade);
                OUT.tint       = v.tint;
                OUT.fogCoord   = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 NbFrag(Varyings IN) : SV_Target
            {
                half3 albedo = IN.tint.rgb * _Color.rgb;
                // 윗면 밝게 · 아랫면 어둡게
                albedo = IN.fx.w >= 0 ? lerp(albedo, albedo * 1.6 + 0.04, IN.fx.w) : albedo * (1.0 + IN.fx.w);
                half3 tintRGB = albedo;
                albedo = lerp(albedo, albedo * _DarkTint, _Dark);   // 3페 검은 실루엣

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
                sd.albedo     = albedo;
                sd.alpha      = 1.0;
                sd.specular   = half3(0, 0, 0);
                sd.metallic   = 0;
                sd.smoothness = 0;
                sd.normalTS   = half3(0, 0, 1);
                sd.emission   = half3(0, 0, 0);
                sd.occlusion  = 1;

                half4 lit = UniversalFragmentBlinnPhong(inputData, sd);

                // 원본 색과 조명 받은 색 사이
                half3 col = lerp(albedo, lit.rgb, _LightStrength);

                // 발광 — 봇 색으로 빛난다 (반짝이 봇 · 빔 · 눈). 블룸이 받도록 1 을 넘길 수 있다
                col += tintRGB * IN.fx.y * _GlowStrength * 0.5;

                // 3페 흰 테두리 — 검은 실루엣의 가장자리 칸
                col += _RimColor.rgb * (IN.fx.z * _Dark * _RimStrength);

                // 번쩍임 (피격)
                col = lerp(col, _BossFlashColor.rgb, saturate(IN.fx.x) * _FlashStrength);

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
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

            float4 ShadowVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                float3 posWS = v.positionWS;

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - posWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif

                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, float3(0, 0, -1), lightDirWS));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
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

            float4 DepthVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                return TransformWorldToHClip(v.positionWS);
            }

            half4 DepthFrag() : SV_Target { return 0; }
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
                nointerpolation float3 normalWS : TEXCOORD1;
            };

            DNVaryings DNVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                BotVertex v = BuildBotVertex(vertexID, instanceID);
                DNVaryings OUT;
                OUT.positionCS = TransformWorldToHClip(v.positionWS);
                OUT.normalWS = v.normalWS;
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
