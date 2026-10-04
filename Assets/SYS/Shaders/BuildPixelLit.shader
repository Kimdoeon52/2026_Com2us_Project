// RE:AL STEEL - 조립 픽셀 라이팅 셰이더 (URP)
//
// 조립 도구(RS 조립) · 재질(RSSurface)이 쓰는 UV 셰이더. 트라이플래너와 같은 조명(번들거림 없는 BlinnPhong,
// 그림자 · 점광 · 구름 쿠키 · 간접광 · 시야 가림)을 받고, 텍스처는 메시 UV 그대로 읽는다.
//  · UV 는 조립 도구가 1m = 32 픽셀로 계산해서 넣는다 (반복 · 도안 칸 · 늘리기)
//  · 정점 색을 곱한다 (부품마다 밝기 흔들기)
//  · 오려내기(_ALPHATEST_ON): 울타리 살 · 철망 · 잎
Shader "RE_AL STEEL/Build Pixel Lit"
{
    Properties
    {
        _BaseMap    ("텍스처", 2D) = "white" {}
        _BaseColor  ("색조", Color) = (1, 1, 1, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("투명 오려내기   (울타리 살 / 철망)", Float) = 0
        _Cutoff     ("  오려낼 기준값", Range(0, 1)) = 0.5
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("그림자 안 받기", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("보이는 면", Float) = 2
        [ToggleUI] _SeeThrough ("시야 가리면 뚫기   (RSSeeThrough)", Float) = 1
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
        #include "Include/RSSeeThrough.hlsl"
        #include "Include/RSIndirect.hlsl"

        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float  _Cutoff;
            float  _Cull;
            float  _AlphaClip;
            float  _ReceiveShadowsOff;
            float  _SeeThrough;
        CBUFFER_END

        half4 SampleBase(float2 uv, half4 vcol)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * vcol;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   BVert
            #pragma fragment BFrag

            #pragma multi_compile_local_fragment _ _ALPHATEST_ON   // 실행 중에 만든 머티리얼도 빌드에서 오려내기
            #pragma shader_feature_local_fragment _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                half4  color       : TEXCOORD3;
                half4  fogAndLight : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BVert(Attributes IN)
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
                OUT.uv         = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                OUT.color      = IN.color;
                OUT.fogAndLight.x   = ComputeFogFactor(pos.positionCS.z);
                OUT.fogAndLight.yzw = VertexLighting(pos.positionWS, nrm.normalWS);
                return OUT;
            }

            half4 BFrag(Varyings IN, bool front : SV_IsFrontFace) : SV_Target
            {
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 normalWS = normalize(IN.normalWS);
                normalWS = front ? normalWS : -normalWS;   // 양면(오려내기) 뒷면도 바르게
                half4 albedo = SampleBase(IN.uv, IN.color);
            #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
            #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS   = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
            #if defined(_RECEIVE_SHADOWS_OFF)
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #else
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #endif
                inputData.fogCoord       = IN.fogAndLight.x;
                inputData.vertexLighting = IN.fogAndLight.yzw;
                inputData.bakedGI        = RSIndirectGI(SampleSH(normalWS), IN.positionWS, normalWS, _RSIndirectReceive.y);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                inputData.bakedGI *= aoFactor.indirectAmbientOcclusion;
            #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo     = albedo.rgb;
                surfaceData.alpha      = 1;
                surfaceData.specular   = half3(0, 0, 0);
                surfaceData.smoothness = 0;
                surfaceData.normalTS   = half3(0, 0, 1);
                surfaceData.occlusion  = 1;

                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

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
            #pragma vertex   SVert
            #pragma fragment SFrag
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON   // 실행 중에 만든 머티리얼도 빌드에서 오려내기
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SA { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SV { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            SV SVert(SA IN)
            {
                SV OUT = (SV)0;
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
                OUT.color = IN.color;
                return OUT;
            }

            half4 SFrag(SV IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
            #ifdef _ALPHATEST_ON
                clip(SampleBase(IN.uv, IN.color).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DVert
            #pragma fragment DFrag
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON   // 실행 중에 만든 머티리얼도 빌드에서 오려내기
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct DA { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DV { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : TEXCOORD1; float3 positionWS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            DV DVert(DA IN)
            {
                DV OUT = (DV)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                OUT.color = IN.color;
                return OUT;
            }

            half4 DFrag(DV IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                clip(SampleBase(IN.uv, IN.color).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   NVert
            #pragma fragment NFrag
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON   // 실행 중에 만든 머티리얼도 빌드에서 오려내기
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct NA { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : TEXCOORD2; float3 positionWS : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            NV NVert(NA IN)
            {
                NV OUT = (NV)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                OUT.color = IN.color;
                return OUT;
            }

            half4 NFrag(NV IN, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                RSSeeThroughClip(IN.positionWS, IN.positionCS, _SeeThrough);
            #ifdef _ALPHATEST_ON
                clip(SampleBase(IN.uv, IN.color).a - _Cutoff);
            #endif
                float3 n = NormalizeNormalPerPixel(IN.normalWS);
                return half4(front ? n : -n, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
