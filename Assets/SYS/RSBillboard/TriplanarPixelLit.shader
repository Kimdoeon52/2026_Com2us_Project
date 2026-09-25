// RE:AL STEEL - 트라이플래너 픽셀 라이팅 셰이더 (URP)
//
// UV 를 아예 안 쓴다. 월드(또는 오브젝트) 좌표를 X/Y/Z 세 방향에서 투영해서
// 면의 법선 방향에 따라 섞는다. 그래서
//   · 산 에셋의 UV 가 팔레트든 엉망이든 상관없다
//   · 텍셀 밀도가 모든 면에서 자동으로 같아진다 (_TileSize 하나로 통일)
//   · 윗면 / 옆면 텍스처가 자동으로 갈린다 (잔디 / 절벽)
//
// 라이팅은 BlinnPhong 경로를 쓰되 스페큘러를 0 으로 둔다.
// URP Lit 의 Smoothness 0 은 "반사 없음"이 아니라 "반사를 최대로 흐림"이라
// 면 전체에 넓은 번들거림이 깔린다. 픽셀 텍스처에는 그게 독이다.
// 여기서는 직사광·점광·그림자·앰비언트는 그대로 받고 번들거림만 없다.
//
// Unity 6000.0.x / URP 17.x 기준. URP 14 이상이면 그대로 동작하도록
// 버전에 따라 이름이 갈린 키워드는 양쪽 다 선언해 두었다.

Shader "RE_AL STEEL/Triplanar Pixel Lit"
{
    Properties
    {
        _SideMap    ("옆면  - 벽 / 절벽에 붙는다", 2D) = "white" {}
        _TopMap     ("윗면  - 바닥 / 지붕에 붙는다", 2D) = "white" {}
        _SideColor  ("옆면 색조", Color) = (1, 1, 1, 1)
        _TopColor   ("윗면 색조", Color) = (1, 1, 1, 1)

        _TileSize   ("타일 크기 (유닛)   = 텍스처 픽셀 / PPU", Float) = 2.0
        [Toggle(_OBJECT_SPACE)] _UseObjectSpace ("오브젝트를 따라다니기 (움직이는 물체용)", Float) = 0

        _BlendSharpness ("경계 날카로움   (높을수록 좁게 섞임)", Range(1, 64)) = 12
        [Toggle(_HARD_BLEND)] _HardBlend ("안 섞기   (평평한 로우폴리용)", Float) = 0
        _TopBias    ("윗면 넓히기   (+ 경사면까지 / - 위쪽만)", Range(-1, 1)) = 0

        [Toggle(_ALPHATEST_ON)] _AlphaClip ("투명 오려내기   (철망 / 난간)", Float) = 0
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
        // 공통: 트라이플래너 샘플링
        // ─────────────────────────────────────────────────────────────
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "RSSeeThrough.hlsl"   // 시야 가림 투명 (RSSeeThrough 컴포넌트)

        TEXTURE2D(_SideMap);    SAMPLER(sampler_SideMap);
        TEXTURE2D(_TopMap);     SAMPLER(sampler_TopMap);

        // SRP Batcher 가 먹으려면 머티리얼 프로퍼티 전부가 이 안에 있어야 한다.
        CBUFFER_START(UnityPerMaterial)
            float4 _SideMap_ST;
            float4 _TopMap_ST;
            half4  _SideColor;
            half4  _TopColor;
            float  _TileSize;
            float  _BlendSharpness;
            float  _TopBias;
            float  _Cutoff;
            float  _Cull;
            float  _HardBlend;
            float  _UseObjectSpace;
            float  _AlphaClip;
            float  _ReceiveShadowsOff;
            float  _SeeThrough;
        CBUFFER_END

        // 세 축 투영 가중치. 법선이 향한 쪽이 크게 나온다.
        float3 TriplanarWeights(float3 normalWS)
        {
            float3 n = abs(normalWS);

            // 윗면 범위 조절: 양수면 잔디가 경사면을 더 타고 내려온다
            n.y = saturate(n.y + _TopBias);

            float3 w = pow(max(n, 1e-5), _BlendSharpness);

        #ifdef _HARD_BLEND
            // 승자 독식 - 경계가 한 픽셀에서 딱 갈린다. 면이 평평한 로우폴리에 맞다.
            float m = max(max(w.x, w.y), w.z);
            w = step(m - 1e-6, w);
        #endif

            return w / max(w.x + w.y + w.z, 1e-5);
        }

        // 축마다 UV 를 만든다. 부호로 뒤집어 줘야 반대편에서 텍스처가 거울상이 안 된다.
        half4 SampleTriplanar(float3 p, float3 normalWS)
        {
            float3 uvw = p / max(_TileSize, 1e-4);
            float3 s   = sign(normalWS);

            float2 uvX = float2(uvw.z * -s.x, uvw.y);
            float2 uvY = float2(uvw.x,        uvw.z * s.y);
            float2 uvZ = float2(uvw.x *  s.z, uvw.y);

            float3 w = TriplanarWeights(normalWS);

            half4 cX = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uvX) * _SideColor;
            half4 cZ = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uvZ) * _SideColor;

            // 위를 보는 면만 윗면 텍스처. 아래를 보는 면은 옆면 텍스처를 그대로 쓴다.
            half4 cTop  = SAMPLE_TEXTURE2D(_TopMap,  sampler_TopMap,  uvY) * _TopColor;
            half4 cDown = SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uvY) * _SideColor;
            half4 cY    = normalWS.y > 0.0 ? cTop : cDown;

            return cX * w.x + cY * w.y + cZ * w.z;
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
            #pragma vertex   TriVert
            #pragma fragment TriFrag

            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _HARD_BLEND
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES   // 구름 그림자 (RS Lighting · 라이트 쿠키)

            // Forward+ 키워드는 URP 버전마다 이름이 다르다. 둘 다 선언해 두면
            // 어느 버전에서든 해당하는 쪽이 켜진다. (URP 14~16 / URP 17)
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 projPos     : TEXCOORD2;   // 투영에 쓸 좌표 (월드 or 오브젝트)
                float3 projNormal  : TEXCOORD3;
                half4  fogAndLight : TEXCOORD4;   // x = fog, yzw = 버텍스 라이팅
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TriVert(Attributes IN)
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

            #ifdef _OBJECT_SPACE
                // 움직이거나 회전하는 물체용. 텍스처가 물체를 따라다닌다.
                OUT.projPos    = IN.positionOS.xyz;
                OUT.projNormal = IN.normalOS;
            #else
                OUT.projPos    = pos.positionWS;
                OUT.projNormal = nrm.normalWS;
            #endif

                OUT.fogAndLight.x   = ComputeFogFactor(pos.positionCS.z);
                OUT.fogAndLight.yzw = VertexLighting(pos.positionWS, nrm.normalWS);

                return OUT;
            }

            half4 TriFrag(Varyings IN) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normalWS = normalize(IN.normalWS);
                half4  albedo   = SampleTriplanar(IN.projPos, normalize(IN.projNormal));

            #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
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
                // 라이트맵은 안 쓴다. 앰비언트(SH)만 받는다 —
                // 지역 분위기색은 RenderSettings 의 앰비언트에서 잡는 게 원칙이라 이걸로 충분하다.
                inputData.bakedGI        = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);

            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
            #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = albedo.rgb;
                surfaceData.alpha       = albedo.a;
                surfaceData.specular    = half3(0, 0, 0);   // ← 번들거림의 원인을 원천 차단
                surfaceData.metallic    = 0;
                surfaceData.smoothness  = 0;
                surfaceData.normalTS    = half3(0, 0, 1);
                surfaceData.emission     = half3(0, 0, 0);
                surfaceData.occlusion    = 1;

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // 그림자 캐스터 - 이게 없으면 그림자를 못 드리운다
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
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _HARD_BLEND
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 projPos    : TEXCOORD0;
                float3 projNormal : TEXCOORD1;
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

            #ifdef _OBJECT_SPACE
                OUT.projPos    = IN.positionOS.xyz;
                OUT.projNormal = IN.normalOS;
            #else
                OUT.projPos    = positionWS;
                OUT.projNormal = normalWS;
            #endif

                return OUT;
            }

            half4 ShadowFrag(SVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
            #ifdef _ALPHATEST_ON
                half4 c = SampleTriplanar(IN.projPos, normalize(IN.projNormal));
                clip(c.a - _Cutoff);
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
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _HARD_BLEND
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 projPos    : TEXCOORD0;
                float3 projNormal : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DVaryings DepthVert(DAttributes IN)
            {
                DVaryings OUT = (DVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;

            #ifdef _OBJECT_SPACE
                OUT.projPos    = IN.positionOS.xyz;
                OUT.projNormal = IN.normalOS;
            #else
                OUT.projPos    = pos.positionWS;
                OUT.projNormal = nrm.normalWS;
            #endif
                return OUT;
            }

            half4 DepthFrag(DVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                half4 c = SampleTriplanar(IN.projPos, normalize(IN.projNormal));
                clip(c.a - _Cutoff);
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
            #pragma vertex   DNVert
            #pragma fragment DNFrag

            #pragma shader_feature_local _OBJECT_SPACE
            #pragma shader_feature_local_fragment _HARD_BLEND
            #pragma shader_feature_local_fragment _ALPHATEST_ON
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
                float3 projPos    : TEXCOORD1;
                float3 projNormal : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DNVert(DNAttributes IN)
            {
                DNVaryings OUT = (DNVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.normalWS   = nrm.normalWS;
                OUT.positionWS = pos.positionWS;

            #ifdef _OBJECT_SPACE
                OUT.projPos    = IN.positionOS.xyz;
                OUT.projNormal = IN.normalOS;
            #else
                OUT.projPos    = pos.positionWS;
                OUT.projNormal = nrm.normalWS;
            #endif
                return OUT;
            }

            half4 DNFrag(DNVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                half4 c = SampleTriplanar(IN.projPos, normalize(IN.projNormal));
                clip(c.a - _Cutoff);
            #endif
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    // URP 가 아닌 프로젝트에서 열었을 때 최소한 분홍색은 면하게
    FallBack "Universal Render Pipeline/Simple Lit"

    // 전용 인스펙터. 이 클래스를 못 찾으면 유니티가 기본 인스펙터로 알아서 돌아간다.
    CustomEditor "RealSteel.EditorTools.TriplanarShaderGUI"
}
