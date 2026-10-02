// RE:AL STEEL - 풀 · 꽃 스프라이트 셰이더 (URP)
//
// RS 지형의 "풀 · 꽃 심기"(RSFoliage)가 만든 메시 전용. 포기 수천 개를 메시 몇 개로 합쳐 그린다.
// 포기마다 네 정점이 모두 발밑(피벗)에 있고, 셰이더가 카메라를 향해 세운다 (Y축 빌보드 + 살짝 젖힘).
//
//   정점 데이터 (RSFoliage 가 굽는다)
//     POSITION   = 발밑 피벗 (지형 로컬)
//     TEXCOORD0  = 시트 UV
//     TEXCOORD1  = x 가로 오프셋(m), y 세로 오프셋(m), z 포기 높이(m), w 무작위 (0~1)
//     COLOR      = 포기마다 색 흔들기
//
// 움직임 (전부 셰이더 — 스크립트가 매 프레임 메시를 안 건드린다)
//   · 바람: 월드 위치 따라 물결치듯 윗부분이 흔들린다. 흔들림을 픽셀 단위로 끊으면 도트 느낌이 산다
//   · 눕기: RSFoliagePusher(캐릭터)가 넣는 전역값 _RSFoliagePush[4] 둘레의 풀이 바깥으로 눕는다
//
// 알파 오려내기 + 불투명 큐 (빌보드 셰이더와 같은 이유 — 정렬 · 깊이 · DOF).
// 그림자는 받기만 한다 (드리우지 않음 — 수천 포기의 그림자는 비싸고 픽셀아트에선 지저분하다).
Shader "RE_AL STEEL/Foliage Sprite"
{
    Properties
    {
        [Header(Sprite)]
        [Space(4)]
        _MainTex     ("스프라이트 시트", 2D) = "white" {}
        _Color       ("색조", Color) = (1, 1, 1, 1)
        _Cutoff      ("오려낼 기준값", Range(0, 1)) = 0.5
        _PitchFollow ("카메라 기울기 따라가기   (0 수직 / 1 완전 빌보드)", Range(0, 1)) = 0.3

        [Header(Lighting)]
        [Space(4)]
        _LightStrength ("조명 반영   (0 원본색 / 1 완전히 조명대로)", Range(0, 1)) = 0.8
        _NormalLean    ("법선 기울이기   (0 카메라쪽 / 1 하늘쪽 — 땅과 같은 빛)", Range(0, 1)) = 0.8
        _BottomDark    ("밑동 어둡게   (땅에 박힌 느낌)", Range(0, 1)) = 0.25

        [Header(Wind)]
        [Space(4)]
        _WindStrength ("바람 세기   (윗끝이 움직이는 거리, m)", Range(0, 0.5)) = 0.06
        _WindSpeed    ("바람 빠르기", Range(0, 6)) = 1.6
        _WindWave     ("물결 크기 (m)   클수록 넓은 범위가 같이 흔들린다", Range(0.5, 30)) = 7
        _WindDir      ("바람 방향 (X, Z)", Vector) = (1, 0, 0.4, 0)
        _PixelSnap    ("흔들림을 픽셀 단위로 끊기   (PPU, 0 = 부드럽게)", Float) = 32

        [Header(Push)]
        [Space(4)]
        _PushStrength ("캐릭터가 지나가면 눕는 정도", Range(0, 1.5)) = 1

        [Header(See Through)]
        [Space(4)]
        [ToggleUI] _SeeThrough ("시야 가리면 뚫기   (RSSeeThrough)", Float) = 0
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
        #include "Include/RSSeeThrough.hlsl"   // 시야 가림 투명 (RSSeeThrough 컴포넌트)
        #include "Include/RSIndirect.hlsl"     // 간접광 (RS 간접광 컴포넌트)

        TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            half4  _Color;
            float4 _WindDir;
            float  _Cutoff;
            float  _PitchFollow;
            float  _LightStrength;
            float  _NormalLean;
            float  _BottomDark;
            float  _WindStrength;
            float  _WindSpeed;
            float  _WindWave;
            float  _PixelSnap;
            float  _PushStrength;
            float  _SeeThrough;
        CBUFFER_END

        // 캐릭터 (RSFoliagePusher) — xyz 발 위치 (월드), w 반경 (m)
        float  _RSFoliagePushCount;
        float4 _RSFoliagePush[4];

        struct FoliageIn
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
            float4 quad       : TEXCOORD1;
            half4  color      : COLOR;
        };

        /// 포기 한 정점의 월드 위치. bend = 0 밑동 ~ 1 윗끝. planeN = 카메라를 향한 면 법선
        float3 FoliagePosition(FoliageIn IN, out float bend, out float3 planeN)
        {
            float3 pivotWS = TransformObjectToWorld(IN.positionOS.xyz);
            float4 q = IN.quad;

            // Y축 빌보드 + 카메라 기울기 일부 (빌보드 셰이더와 같은 방식)
            float3 toCam = _WorldSpaceCameraPos - pivotWS;
            float3 fwd = float3(toCam.x, 0.0, toCam.z);
            float fl = length(fwd);
            fwd = fl > 1e-4 ? fwd / fl : float3(0.0, 0.0, -1.0);
            float3 camUp = float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21);
            float3 up = normalize(lerp(float3(0.0, 1.0, 0.0), camUp, _PitchFollow));
            float3 right = cross(fwd, up);
            float rl = length(right);
            right = rl > 1e-4 ? right / rl : float3(1.0, 0.0, 0.0);
            up = normalize(cross(right, fwd));
            planeN = normalize(cross(up, right));

            float h = max(q.z, 1e-3);
            bend = saturate(q.y / h);
            float b2 = bend * bend;

            // 바람 — 월드 위치를 따라 물결치고, 포기마다 조금씩 어긋난다
            float2 wd = _WindDir.xz;
            wd = dot(wd, wd) > 1e-6 ? normalize(wd) : float2(1.0, 0.0);
            float ph = dot(pivotWS.xz, wd) / max(_WindWave, 0.5) * 6.2831853;
            float t = _Time.y * _WindSpeed;
            float gust = sin(t - ph) * 0.65 + sin(t * 2.37 - ph * 1.7 + q.w * 6.2831853) * 0.35;
            float2 disp = wd * (gust * _WindStrength * b2);
            float down = 0.0;

            // 캐릭터 둘레는 바깥으로 눕는다
            int n = (int)_RSFoliagePushCount;
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                if (i < n)
                {
                    float4 P = _RSFoliagePush[i];
                    float2 d = pivotWS.xz - P.xz;
                    float dist = length(d);
                    float f = saturate(1.0 - dist / max(P.w, 0.01));
                    f = f * f * (3.0 - 2.0 * f);
                    f *= step(abs(pivotWS.y - P.y), 1.5);
                    float2 dir = dist > 1e-3 ? d / dist : float2(0.0, 1.0);
                    float k = f * _PushStrength * bend;
                    disp += dir * (k * h * 0.6);
                    down += k * h * 0.45;
                }
            }

            // 픽셀 단위로 끊기 — 부드럽게 미끄러지는 대신 도트가 한 칸씩 움직인다
            if (_PixelSnap > 0.5)
            {
                disp = round(disp * _PixelSnap) / _PixelSnap;
                down = round(down * _PixelSnap) / _PixelSnap;
            }

            float3 pos = pivotWS + right * q.x + up * q.y;
            pos.xz += disp;
            pos.y  -= down;
            return pos;
        }

        half4 SampleFoliage(float2 uv, half4 vcolor)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color * vcolor;
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
            #pragma target 3.0
            #pragma vertex   FolVert
            #pragma fragment FolFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 그림자 (RS Lighting · 라이트 쿠키)
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half4  vcolor     : TEXCOORD3;
                half2  fogBend    : TEXCOORD4;   // x fog, y bend
            };

            Varyings FolVert(FoliageIn IN)
            {
                Varyings OUT = (Varyings)0;
                float bend; float3 planeN;
                float3 posWS = FoliagePosition(IN, bend, planeN);
                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = IN.uv;
                OUT.vcolor     = IN.color;
                OUT.normalWS   = normalize(lerp(planeN, float3(0.0, 1.0, 0.0), _NormalLean));
                OUT.fogBend    = half2(ComputeFogFactor(OUT.positionCS.z), bend);
                return OUT;
            }

            half4 FolFrag(Varyings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                half4 albedo = SampleFoliage(IN.uv, IN.vcolor);
                clip(albedo.a - _Cutoff);

                // 밑동 어둡게 (땅에 박힌 느낌 — 간이 AO)
                albedo.rgb *= lerp(1.0 - _BottomDark, 1.0, saturate(IN.fogBend.y * 2.5));

                float3 n = normalize(IN.normalWS);
                InputData inputData       = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = n;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.shadowCoord     = TransformWorldToShadowCoord(IN.positionWS);
                inputData.fogCoord        = IN.fogBend.x;
                inputData.vertexLighting  = half3(0, 0, 0);
                inputData.bakedGI         = RSIndirectGI(SampleSH(n), IN.positionWS, n, _RSIndirectReceive.w);
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
                half3 col = lerp(albedo.rgb, lit.rgb, _LightStrength);
                col = MixFog(col, IN.fogBend.x);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthOnly — DOF · 깊이 프리패스용 (모양이 ForwardLit 와 똑같아야 한다)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   FolDepthVert
            #pragma fragment FolDepthFrag

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  vcolor     : TEXCOORD2;
            };

            DVaryings FolDepthVert(FoliageIn IN)
            {
                DVaryings OUT = (DVaryings)0;
                float bend; float3 planeN;
                float3 posWS = FoliagePosition(IN, bend, planeN);
                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                OUT.vcolor = IN.color;
                return OUT;
            }

            half4 FolDepthFrag(DVaryings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                clip(SampleFoliage(IN.uv, IN.vcolor).a - _Cutoff);
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
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   FolDNVert
            #pragma fragment FolDNFrag

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  vcolor     : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
            };

            DNVaryings FolDNVert(FoliageIn IN)
            {
                DNVaryings OUT = (DNVaryings)0;
                float bend; float3 planeN;
                float3 posWS = FoliagePosition(IN, bend, planeN);
                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                OUT.vcolor = IN.color;
                OUT.normalWS = normalize(lerp(planeN, float3(0.0, 1.0, 0.0), _NormalLean));
                return OUT;
            }

            half4 FolDNFrag(DNVaryings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                clip(SampleFoliage(IN.uv, IN.vcolor).a - _Cutoff);
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
