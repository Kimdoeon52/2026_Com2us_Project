// RE:AL STEEL - 2.5D 스프라이트 빌보드 셰이더 (URP)
//
// 3D 씬 안에 픽셀 스프라이트를 세운다. HD-2D 카메라(낮은 FOV + 내려다보는 피치)
// 전제로 기본값을 잡았다.
//
// 설계 결정 세 가지 - 이게 이 셰이더의 핵심이다
//
//  1) Y축 빌보드 + 부분 기울기
//     완전 빌보드(카메라를 정면으로 마주보기)는 카메라가 내려다볼 때 스프라이트가
//     같이 누워서 발이 바닥에서 뜬다. Y축만 돌리면 발은 붙지만 피치만큼 눌려 보인다.
//     그래서 카메라 기울기를 '일부만' 따라간다. 옥토패스도 캐릭터를 살짝 뒤로 젖혀 둔다.
//
//  2) 알파 블렌딩이 아니라 알파 오려내기 + 불투명 큐
//     반투명으로 그리면 3D 지오메트리와 정렬이 깨지고(앞뒤가 뒤집히고) 깊이 버퍼에
//     안 써져서 DOF 가 캐릭터를 무시한다. 픽셀아트는 경계가 어차피 딱 떨어지므로
//     오려내기가 맞고, 그러면 깊이 정렬이 3D 오브젝트와 똑같이 동작한다.
//
//  3) 조명을 '색조로' 받는다
//     스프라이트에는 이미 명암이 그려져 있다. 여기에 조명을 100% 곱하면 그림이 망가진다.
//     원본 색과 조명 받은 색 사이를 조절해서, 캐릭터가 씬의 분위기색을 입되
//     원화는 살아 있게 한다. 그림자는 받으므로 처마 밑에 들어가면 어두워진다.
//
// Unity 6000.0.x / URP 17.x 기준. URP 14 이상에서 그대로 동작하도록 썼다.

Shader "RE_AL STEEL/Billboard Pixel Lit"
{
    Properties
    {
        [Header(Sprite)]
        [Space(4)]
        _MainTex    ("스프라이트", 2D) = "white" {}
        _Color      ("색조", Color) = (1, 1, 1, 1)
        _Cutoff     ("오려낼 기준값", Range(0, 1)) = 0.5

        [Header(Billboard)]
        [Space(4)]
        _PitchFollow ("카메라 기울기 따라가기   (0 수직 / 1 완전 빌보드)", Range(0, 1)) = 0.35
        [Toggle(_FLIP_X)] _FlipX ("좌우 반전", Float) = 0

        [Header(Lighting)]
        [Space(4)]
        _LightStrength ("조명 반영   (0 원본색 / 1 완전히 조명대로)", Range(0, 1)) = 0.7
        _NormalLean    ("법선 기울이기   (0 카메라쪽 / 1 하늘쪽)", Range(0, 1)) = 0.5

        [Header(See Through)]
        [Space(4)]
        [ToggleUI] _SeeThrough ("시야 가리면 뚫기   (캐릭터 앞을 가릴 때 점무늬 구멍 · RSSeeThrough)", Float) = 0

        [Header(FX)]
        [Space(4)]
        _FlashColor  ("피격 플래시 색", Color) = (1, 1, 1, 1)
        _FlashAmount ("피격 플래시", Range(0, 1)) = 0
        [Toggle(_OUTLINE_ON)] _UseOutline ("외곽선", Float) = 0
        _OutlineColor ("외곽선 색", Color) = (0, 0, 0, 1)
        _OutlineWidth ("외곽선 두께 (텍셀)", Range(0, 4)) = 1
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
        LOD 200

        // ─────────────────────────────────────────────────────────────
        // 공통: 빌보드 계산 + 스프라이트 샘플링
        // ─────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/RSSeeThrough.hlsl"   // 시야 가림 투명 (RSSeeThrough 컴포넌트)
        #include "Include/RSIndirect.hlsl"     // 간접광 (RS 간접광 컴포넌트)

        TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4  _Color;
            half4  _FlashColor;
            half4  _OutlineColor;
            float  _Cutoff;
            float  _PitchFollow;
            float  _SeeThrough;
            float  _LightStrength;
            float  _NormalLean;
            float  _FlashAmount;
            float  _OutlineWidth;
            float  _FlipX;
            float  _UseOutline;
        CBUFFER_END

        /// <summary>
        /// 쿼드의 로컬 x/y 를 카메라를 향한 평면 위로 옮긴다.
        /// 피벗(오브젝트 원점)은 그대로 두므로 발밑 위치가 안 흔들린다.
        ///
        /// useCamTilt: 그림자 패스에서는 false. 그림자를 그릴 때의 시점은 광원이라
        /// 카메라 기울기를 따라가면 본체와 그림자 방향이 어긋난다.
        /// </summary>
        float3 Billboard(float3 positionOS, bool useCamTilt, out float3 planeNormalWS)
        {
            // 피벗의 월드 위치 (행렬의 이동 성분)
            float3 pivotWS = float3(unity_ObjectToWorld._m03,
                                    unity_ObjectToWorld._m13,
                                    unity_ObjectToWorld._m23);

            // 트랜스폼 스케일을 손으로 반영한다 - 정점을 직접 배치하므로
            // 행렬 곱을 안 타서 스케일이 그냥은 안 먹는다.
            float sx = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
            float sy = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));

            float2 local = positionOS.xy * float2(sx, sy);
        #ifdef _FLIP_X
            local.x = -local.x;
        #endif

            // 수평면에서 카메라를 향하는 방향. y 를 버리는 게 Y축 빌보드다.
            float3 toCam = _WorldSpaceCameraPos - pivotWS;
            float3 fwd = float3(toCam.x, 0.0, toCam.z);
            float fl = length(fwd);
            fwd = fl > 1e-4 ? fwd / fl : float3(0.0, 0.0, -1.0);

            // 카메라의 up 을 얼마나 섞을지 = 얼마나 뒤로 젖힐지
            float tilt = useCamTilt ? _PitchFollow : 0.0;
            float3 camUp = float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21);
            float3 up = normalize(lerp(float3(0.0, 1.0, 0.0), camUp, tilt));

            float3 right = cross(fwd, up);
            float rl = length(right);
            right = rl > 1e-4 ? right / rl : float3(1.0, 0.0, 0.0);
            up = normalize(cross(right, fwd));          // 직교화

            planeNormalWS = normalize(cross(up, right)); // 카메라를 향한다

            return pivotWS + right * local.x + up * local.y;
        }

        /// <summary>스프라이트 색. 외곽선이 켜져 있으면 실루엣 바깥 한 겹을 채운다.</summary>
        half4 SampleSprite(float2 uv)
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;

        #ifdef _OUTLINE_ON
            // 자기 알파가 비어 있고 이웃에 그림이 있으면 외곽선으로 채운다
            if (c.a < _Cutoff)
            {
                float2 t = _MainTex_TexelSize.xy * _OutlineWidth;
                half a =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( t.x, 0)).a +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-t.x, 0)).a +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0,  t.y)).a +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -t.y)).a;

                if (a > _Cutoff) c = half4(_OutlineColor.rgb, 1.0);
            }
        #endif

            return c;
        }
        ENDHLSL

        // ─────────────────────────────────────────────────────────────
        // 메인 패스
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off        // 뒤에서 봐도 보이게. 빌보드라 어차피 정면만 보인다.
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   BbVert
            #pragma fragment BbFrag

            #pragma shader_feature_local _FLIP_X
            #pragma shader_feature_local_fragment _OUTLINE_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 그림자 (RS Lighting · 라이트 쿠키)

            // Forward+ 키워드는 URP 버전마다 이름이 다르다. 둘 다 선언해 둔다.
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;      // SpriteRenderer 의 Color 를 받는다
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half4  vcolor     : TEXCOORD3;
                half   fogCoord   : TEXCOORD4;
            };

            Varyings BbVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                float3 planeN;
                float3 posWS = Billboard(IN.positionOS.xyz, true, planeN);

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.vcolor     = IN.color;

                // 조명용 법선은 카메라쪽과 하늘쪽 사이에서 고른다.
                // 카메라쪽으로 두면 균일하게(픽셀아트에 유리), 하늘쪽으로 두면
                // 바닥과 같은 빛을 받아 씬에 더 잘 녹는다.
                OUT.normalWS = normalize(lerp(planeN, float3(0.0, 1.0, 0.0), _NormalLean));

                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 BbFrag(Varyings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                half4 albedo = SampleSprite(IN.uv) * IN.vcolor;
                clip(albedo.a - _Cutoff);

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
                sd.specular   = half3(0, 0, 0);   // 스프라이트에 하이라이트가 생기면 안 된다
                sd.metallic   = 0;
                sd.smoothness = 0;
                sd.normalTS   = half3(0, 0, 1);
                sd.emission   = half3(0, 0, 0);
                sd.occlusion  = 1;

                half4 lit = UniversalFragmentBlinnPhong(inputData, sd);

                // 원화 보존: 원본 색과 조명 받은 색 사이를 고른다
                half3 col = lerp(albedo.rgb, lit.rgb, _LightStrength);

                // 피격 플래시 - 스크립트에서 _FlashAmount 만 0→1→0 으로 흔들면 된다
                col = lerp(col, _FlashColor.rgb, saturate(_FlashAmount));

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 그림자 캐스터 - 스프라이트 실루엣 모양 그림자가 진다
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
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #pragma shader_feature_local _FLIP_X
            #pragma shader_feature_local_fragment _OUTLINE_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            SVaryings ShadowVert(SAttributes IN)
            {
                SVaryings OUT = (SVaryings)0;

                float3 planeN;
                // 그림자 패스에서는 카메라 기울기를 따라가지 않는다 (위 주석 참고)
                float3 posWS = Billboard(IN.positionOS.xyz, false, planeN);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirWS = normalize(_LightPosition - posWS);
            #else
                float3 lightDirWS = _LightDirection;
            #endif

                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, planeN, lightDirWS));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = cs;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target
            {
                half4 c = SampleSprite(IN.uv);
                clip(c.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthOnly - 이게 없으면 DOF 가 캐릭터를 무시한다
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
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local _FLIP_X
            #pragma shader_feature_local_fragment _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            DVaryings DepthVert(DAttributes IN)
            {
                DVaryings OUT = (DVaryings)0;
                float3 planeN;
                float3 posWS = Billboard(IN.positionOS.xyz, true, planeN);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                half4 c = SampleSprite(IN.uv);
                clip(c.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthNormals - SSAO 등으로 렌더러가 '깊이+법선' 프리패스를 쓰면 깊이 텍스처를 이 패스가 만든다.
        // 없으면 FallBack(Simple Lit)의 패스가 빌보드 변환 없이 네모로 그려져 DOF · 안개가 캐릭터 깊이를 잘못 읽는다.
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DNVert
            #pragma fragment DNFrag

            #pragma shader_feature_local _FLIP_X
            #pragma shader_feature_local_fragment _OUTLINE_ON

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
            };

            DNVaryings DNVert(DNAttributes IN)
            {
                DNVaryings OUT = (DNVaryings)0;
                float3 planeN;
                float3 posWS = Billboard(IN.positionOS.xyz, true, planeN);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = normalize(lerp(planeN, float3(0.0, 1.0, 0.0), _NormalLean));
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                half4 c = SampleSprite(IN.uv);
                clip(c.a - _Cutoff);
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
