// RE:AL STEEL - 소품 표준 픽셀 라이팅 셰이더 (URP)
//
// 외부 모델 · AI 생성 모델(메쉬 AI 등) · 산 에셋에 붙이는 표준 셰이더.
// 조명은 다른 RS 셰이더(트라이플래너 · 조립)와 같다 — 번들거림 없는 BlinnPhong, 그림자 · 점광 · 구름 쿠키 ·
// 간접광(건물 칸 = _RSIndirectReceive.y) · 시야 가림. 그래서 스테이지 안에서 따로 놀지 않는다.
//
// 텍스처 붙이는 방식 (텍스처 방식)
//   · UV          모델 UV 그대로. 텍스처까지 같이 받은 모델 · 직접 그린 텍스처용. 노멀 맵도 이 방식에서만
//   · Triplanar   UV 를 안 쓰고 월드 좌표를 세 방향에서 투영. AI 자동 UV 처럼 조각난 UV · UV 없는 모델용.
//                 타일 크기 = 텍스처 픽셀 / 32 (1m = 32px) 로 두면 다른 오브젝트와 픽셀 크기가 같아진다
//
// 윗면 덮개 (이끼 · 풀 · 눈 · 먼지)
//   위를 보는 면에 덮개 텍스처를 얹는다. 경계는 1m = 32칸 격자마다 문턱을 흔들어 픽셀 디더로 끊는다
//   (부드러운 그라데이션 대신 도트 경계 — 픽셀 텍스처와 맞춤). 덮는 양 · 들쭉날쭉으로 조절
//
// 픽셀 맞추기 (고해상도 AI 텍스처를 픽셀 룩에 맞출 때)
//   · UV 픽셀화   텍스처를 N 칸으로 뭉갠다 (UV 방식만)
//   · 색 단계     색을 단계로 끊는다
//
// Unity 6000.0.x / URP 17.x 기준. URP 14 이상이면 그대로 동작하도록 버전에 따라 이름이 갈린 키워드는 양쪽 다 선언.

Shader "RE_AL STEEL/Prop Pixel Lit"
{
    Properties
    {
        [KeywordEnum(UV, Triplanar)] _Mapping ("텍스처 방식   (UV = 모델 UV · Triplanar = 월드 투영)", Float) = 1
        _BaseMap    ("텍스처", 2D) = "white" {}
        _BaseColor  ("색조", Color) = (1, 1, 1, 1)
        _TileSize   ("  타일 크기 (m)   = 텍스처 픽셀 / 32   · Triplanar", Float) = 1
        _BlendSharpness ("  면 경계 날카로움   · Triplanar", Range(1, 64)) = 16
        [Toggle(_OBJECT_SPACE)] _UseObjectSpace ("  오브젝트를 따라다니기   (움직이는 물체 · Triplanar)", Float) = 0
        [Toggle(_NORMALMAP)] _UseNormalMap ("노멀 맵 쓰기   · UV", Float) = 0
        [Normal] _BumpMap ("  노멀 맵", 2D) = "bump" {}
        _BumpScale  ("  노멀 세기", Range(0, 2)) = 1

        [Space(10)]
        [Toggle(_TOP_LAYER)] _UseTop ("윗면 덮개 켜기   (이끼 · 풀 · 눈 · 먼지)", Float) = 0
        _TopMap     ("  덮개 텍스처   (월드 투영)", 2D) = "white" {}
        _TopColor   ("  덮개 색조", Color) = (1, 1, 1, 1)
        _TopTileSize ("  덮개 타일 크기 (m)   = 텍스처 픽셀 / 32", Float) = 1
        _TopAmount  ("  덮는 양   (0 = 없음 · 1 = 옆면까지)", Range(0, 1)) = 0.5
        _TopEdge    ("  경계 들쭉날쭉   (픽셀 디더)", Range(0, 1)) = 0.5

        [Space(10)]
        _PixelDensity ("픽셀 밀도 (1m 당 픽셀)   · 디더 격자", Float) = 32
        _UVPixels   ("UV 픽셀화   (0 = 끔 · 예: 64 = 텍스처를 64칸으로) · UV", Float) = 0
        _ColorSteps ("색 단계   (0 = 끔 · 예: 8)", Range(0, 32)) = 0

        [Space(10)]
        [Toggle(_EMISSION)] _UseEmission ("발광 켜기   (램프 · 네온)", Float) = 0
        [HDR] _EmissionColor ("  발광 색", Color) = (0, 0, 0, 1)
        _EmissionMap ("  발광 마스크   · UV", 2D) = "white" {}

        [Space(10)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("투명 오려내기   (철망 / 잎)", Float) = 0
        _Cutoff     ("  오려낼 기준값", Range(0, 1)) = 0.5
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("그림자 안 받기", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("보이는 면", Float) = 2
        [ToggleUI] _SeeThrough ("시야 가리면 뚫기   (캐릭터 앞을 가릴 때 점무늬 구멍 · RSSeeThrough)", Float) = 1
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

        // ─────────────────────────────────────────────────────────────
        // 공통: 텍스처 읽기 (UV / 트라이플래너) · 윗면 덮개 · 픽셀 맞추기
        // ─────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/RSSeeThrough.hlsl"   // 시야 가림 투명 (RSSeeThrough 컴포넌트)
        #include "Include/RSIndirect.hlsl"     // 간접광 (RS 간접광 컴포넌트)

        TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);
        TEXTURE2D(_TopMap);      SAMPLER(sampler_TopMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        // SRP Batcher 가 먹으려면 머티리얼 프로퍼티 전부가 이 안에 있어야 한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BumpMap_ST;
            float4 _TopMap_ST;
            float4 _EmissionMap_ST;
            half4  _BaseColor;
            half4  _TopColor;
            half4  _EmissionColor;
            float  _Mapping;
            float  _TileSize;
            float  _BlendSharpness;
            float  _UseObjectSpace;
            float  _UseNormalMap;
            float  _BumpScale;
            float  _UseTop;
            float  _TopTileSize;
            float  _TopAmount;
            float  _TopEdge;
            float  _PixelDensity;
            float  _UVPixels;
            float  _ColorSteps;
            float  _UseEmission;
            float  _AlphaClip;
            float  _Cutoff;
            float  _ReceiveShadowsOff;
            float  _Cull;
            float  _SeeThrough;
        CBUFFER_END

        // 투영 좌표: 월드 그대로, 또는 오브젝트 좌표 × 오브젝트 크기 (크기를 곱해야 키워도 픽셀 밀도가 안 변한다)
        void PropProjection(float3 positionOS, float3 normalOS, float3 positionWS, float3 normalWS,
                            out float3 projPos, out float3 projNormal)
        {
        #if defined(_OBJECT_SPACE)
            float4x4 m = GetObjectToWorldMatrix();
            float3 s = float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
            projPos    = positionOS * s;
            projNormal = normalOS;
        #else
            projPos    = positionWS;
            projNormal = normalWS;
        #endif
        }

        // UV 픽셀화: 텍스처를 N 칸으로 뭉갠다 (칸 가운데를 읽음)
        float2 PropUV(float2 uv)
        {
            if (_UVPixels >= 1.0) uv = (floor(uv * _UVPixels) + 0.5) / _UVPixels;
            return uv;
        }

        // 세 축 투영 가중치. 법선이 향한 쪽이 크게 나온다.
        float3 PropTriWeights(float3 n)
        {
            float3 w = pow(max(abs(n), 1e-5), _BlendSharpness);
            return w / max(w.x + w.y + w.z, 1e-5);
        }

        // 트라이플래너 한 장. 부호로 뒤집어 줘야 반대편에서 텍스처가 거울상이 안 된다.
        half4 PropSampleTri(TEXTURE2D_PARAM(tex, smp), float3 p, float3 n, float tile)
        {
            float3 uvw = p / max(tile, 1e-4);
            float3 s   = sign(n);
            float2 uvX = float2(uvw.z * -s.x, uvw.y);
            float2 uvY = float2(uvw.x,        uvw.z * s.y);
            float2 uvZ = float2(uvw.x *  s.z, uvw.y);
            float3 w   = PropTriWeights(n);
            return SAMPLE_TEXTURE2D(tex, smp, uvX) * w.x
                 + SAMPLE_TEXTURE2D(tex, smp, uvY) * w.y
                 + SAMPLE_TEXTURE2D(tex, smp, uvZ) * w.z;
        }

        // 바탕 텍스처 (색조 포함)
        half4 PropBase(float2 uv, float3 projPos, float3 projNormal)
        {
        #if defined(_MAPPING_TRIPLANAR)
            return PropSampleTri(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), projPos, projNormal, _TileSize) * _BaseColor;
        #else
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, PropUV(uv)) * _BaseColor;
        #endif
        }

        float PropHash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        // 윗면 덮개: 위를 보는 정도(월드 법선 y)가 문턱을 넘으면 덮개. 문턱은 픽셀 격자 칸마다 흔들어 도트 경계로
        half3 PropTopLayer(half3 baseRGB, float3 projPos, float3 projNormal, float3 normalWS)
        {
        #if defined(_TOP_LAYER)
            half4 top = PropSampleTri(TEXTURE2D_ARGS(_TopMap, sampler_TopMap), projPos, projNormal, _TopTileSize) * _TopColor;
            float cell = PropHash13(floor(projPos * max(_PixelDensity, 1.0)) + 0.5);
            float th   = lerp(1.05, -0.35, _TopAmount);              // 0 = 아무 데도 · 1 = 옆면 아래까지
            float up   = normalWS.y + (cell - 0.5) * _TopEdge * 0.6;
            float m    = step(th, up) * top.a;
            return lerp(baseRGB, top.rgb, m);
        #else
            return baseRGB;
        #endif
        }

        // 색 단계: 채널마다 N 단계로 끊는다
        half3 PropPosterize(half3 c)
        {
            if (_ColorSteps >= 2.0) c = floor(c * _ColorSteps + 0.5) / _ColorSteps;
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

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   PropVert
            #pragma fragment PropFrag

            #pragma shader_feature_local _MAPPING_UV _MAPPING_TRIPLANAR
            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _TOP_LAYER
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 그림자 (RS Lighting · 라이트 쿠키)

            // Forward+ 키워드는 URP 버전마다 이름이 다르다. 둘 다 선언 (URP 14~16 / URP 17)
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 노멀 맵은 UV 방식에서만 (트라이플래너는 UV 가 없어 접선이 없다)
            #if defined(_NORMALMAP) && !defined(_MAPPING_TRIPLANAR)
                #define PROP_USE_NORMALMAP 1
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float3 projPos     : TEXCOORD3;   // 투영에 쓸 좌표 (월드 or 오브젝트)
                float3 projNormal  : TEXCOORD4;
                half4  fogAndLight : TEXCOORD5;   // x = fog, yzw = 버텍스 라이팅
            #if defined(PROP_USE_NORMALMAP)
                float4 tangentWS   : TEXCOORD6;   // w = 바이탄젠트 부호
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PropVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.uv         = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                PropProjection(IN.positionOS.xyz, IN.normalOS, pos.positionWS, nrm.normalWS, OUT.projPos, OUT.projNormal);

            #if defined(PROP_USE_NORMALMAP)
                real sgn = IN.tangentOS.w * GetOddNegativeScale();
                OUT.tangentWS = float4(nrm.tangentWS, sgn);
            #endif

                OUT.fogAndLight.x   = ComputeFogFactor(pos.positionCS.z);
                OUT.fogAndLight.yzw = VertexLighting(pos.positionWS, nrm.normalWS);
                return OUT;
            }

            half4 PropFrag(Varyings IN, bool front : SV_IsFrontFace) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 geoNormalWS = normalize(IN.normalWS);
                geoNormalWS = front ? geoNormalWS : -geoNormalWS;   // 양면(오려내기) 뒷면도 바르게

                half4 albedo = PropBase(IN.uv, IN.projPos, normalize(IN.projNormal));
            #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
            #endif
                albedo.rgb = PropTopLayer(albedo.rgb, IN.projPos, normalize(IN.projNormal), geoNormalWS);
                albedo.rgb = PropPosterize(albedo.rgb);

                float3 normalWS = geoNormalWS;
            #if defined(PROP_USE_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, PropUV(IN.uv)), _BumpScale);
                float3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                normalWS = TransformTangentToWorld(normalTS, half3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS));
                normalWS = NormalizeNormalPerPixel(normalWS);
                normalWS = front ? normalWS : -normalWS;
            #endif

                InputData inputData = (InputData)0;
                inputData.positionWS      = IN.positionWS;
                inputData.normalWS        = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));

            #if defined(_RECEIVE_SHADOWS_OFF)
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #else
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #endif

                inputData.fogCoord       = IN.fogAndLight.x;
                inputData.vertexLighting = IN.fogAndLight.yzw;
                // 라이트맵은 안 쓴다. 앰비언트(SH) + RS 간접광 (건물 칸)
                inputData.bakedGI        = RSIndirectGI(SampleSH(normalWS), IN.positionWS, normalWS, _RSIndirectReceive.y);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);

            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
            #endif

                half3 emission = half3(0, 0, 0);
            #if defined(_EMISSION)
                emission = _EmissionColor.rgb;
                #if !defined(_MAPPING_TRIPLANAR)
                    emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, PropUV(IN.uv)).rgb;
                #endif
            #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo.rgb;
                surfaceData.alpha      = 1;
                surfaceData.specular   = half3(0, 0, 0);   // 번들거림 원천 차단 (트라이플래너 셰이더와 같은 이유)
                surfaceData.metallic   = 0;
                surfaceData.smoothness = 0;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.emission   = emission;
                surfaceData.occlusion  = 1;

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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   PropShadowVert
            #pragma fragment PropShadowFrag

            #pragma shader_feature_local _MAPPING_UV _MAPPING_TRIPLANAR
            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _ALPHATEST_ON
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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 projPos    : TEXCOORD1;
                float3 projNormal : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            SVaryings PropShadowVert(SAttributes IN)
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
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                PropProjection(IN.positionOS.xyz, IN.normalOS, positionWS, normalWS, OUT.projPos, OUT.projNormal);
                return OUT;
            }

            half4 PropShadowFrag(SVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
            #ifdef _ALPHATEST_ON
                clip(PropBase(IN.uv, IN.projPos, normalize(IN.projNormal)).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthOnly - DOF / 깊이를 쓰는 포스트가 이걸 요구한다
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   PropDepthVert
            #pragma fragment PropDepthFrag

            #pragma shader_feature_local _MAPPING_UV _MAPPING_TRIPLANAR
            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 projPos    : TEXCOORD1;
                float3 projNormal : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings PropDepthVert(DAttributes IN)
            {
                DVaryings OUT = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                PropProjection(IN.positionOS.xyz, IN.normalOS, pos.positionWS, nrm.normalWS, OUT.projPos, OUT.projNormal);
                return OUT;
            }

            half4 PropDepthFrag(DVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                clip(PropBase(IN.uv, IN.projPos, normalize(IN.projNormal)).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // DepthNormals - SSAO 등 노멀 버퍼를 쓰는 기능용
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   PropDNVert
            #pragma fragment PropDNFrag

            #pragma shader_feature_local _MAPPING_UV _MAPPING_TRIPLANAR
            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DNAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 projPos    : TEXCOORD2;
                float3 projNormal : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings PropDNVert(DNAttributes IN)
            {
                DNVaryings OUT = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                PropProjection(IN.positionOS.xyz, IN.normalOS, pos.positionWS, nrm.normalWS, OUT.projPos, OUT.projNormal);
                return OUT;
            }

            half4 PropDNFrag(DNVaryings IN, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                clip(PropBase(IN.uv, IN.projPos, normalize(IN.projNormal)).a - _Cutoff);
            #endif
                float3 n = NormalizeNormalPerPixel(IN.normalWS);
                return half4(front ? n : -n, 0.0);
            }
            ENDHLSL
        }
    }

    // URP 가 아닌 프로젝트에서 열었을 때 최소한 분홍색은 면하게
    FallBack "Universal Render Pipeline/Simple Lit"
}
