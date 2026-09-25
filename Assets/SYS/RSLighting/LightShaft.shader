// RE:AL STEEL - 햇살 빛줄기 셰이더 (RSLightShafts 전용)
//
// 빛 기둥: 축(빛 방향)을 중심으로 카메라를 향하게 세운 넓은 판. 가장자리 없이 가우스로 퍼지고,
//          안에서 결(값 노이즈)이 천천히 흐르고, 카메라 가까이서는 흐려진다.
// 먼지:    기둥 안에 떠 있는 작은 빛 점. 둥실거리며 반짝인다.
// 가산 합성 — 밝히기만 한다. 안개 속에선 옅어진다.
// 세부 값은 컴포넌트(RSLightShafts)가 MaterialPropertyBlock 으로 넣는다 (머티리얼 값은 기본값).
Shader "RE_AL STEEL/Light Shaft"
{
    Properties
    {
        [HDR] _Color ("색 · 세기(a) — 스크립트가 덮어씀", Color) = (1, 0.95, 0.82, 0.35)
        _Shape ("모양: 가장자리 부드러움, 위 흐려짐, 아래 흐려짐, 계단", Vector) = (1, 0.6, 0.35, 0)
        _Noise ("결: 세기, 크기, 속도, 가까이 흐려짐(m)", Vector) = (0.55, 1.2, 0.06, 6)
        _Mote ("먼지: 밝기, 둥실 폭", Vector) = (2.5, 0.35, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Shape;
                float4 _Noise;
                float4 _Mote;
            CBUFFER_END

            float _RSShaftClock;   // RSLightShafts 가 넣는 전역 시계

            struct Attributes
            {
                float3 center : POSITION;    // 빛 기둥 가운데 (오브젝트 공간)
                float3 axis   : NORMAL;      // 빛이 가는 방향 (아래쪽)
                float4 size   : TANGENT;     // x 반길이, y 반폭, z 흐름 속도(m/s), w 나타남·사라짐 시간
                float2 corner : TEXCOORD0;   // x -1..1 가로, y -1 위(하늘) .. 1 아래(지면)
                float2 life   : TEXCOORD1;   // x 이번 등장 시작 시각, y 머무는 시간
                float4 extra  : TEXCOORD2;   // 기둥: (난수, 0, 세기배율, -)  먼지: (난수, 1+크기, 세로위치 0..1, 가로 -1..1)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner     : TEXCOORD0;
                float4 data       : TEXCOORD1;   // x 알파, y 난수, z 종류(0 기둥 · 1 먼지), w 카메라 거리
                float  fogCoord   : TEXCOORD2;
            };

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

            Varyings vert(Attributes IN)
            {
                float3 ax = normalize(TransformObjectToWorldDir(IN.axis));
                bool isMote = IN.extra.y >= 0.5;

                // 나타남 → 머묾 → 사라짐 (시계로 계산)
                float local = _RSShaftClock - IN.life.x;
                float fade = max(0.05, IN.size.w);
                float a = saturate(local / fade) * saturate((IN.life.y - local) / fade);
                a = (local < 0.0 || local > IN.life.y) ? 0.0 : a * a * (3.0 - 2.0 * a);

                // 해 방향 쪽으로 천천히 흐름
                float3 flat = float3(ax.x, 0, ax.z);
                flat = dot(flat, flat) > 1e-6 ? normalize(flat) : float3(1, 0, 0);
                float3 c = TransformObjectToWorld(IN.center) + flat * (IN.size.z * max(local, 0.0));

                float3 w;
                if (!isMote)
                {
                    float3 vd = GetWorldSpaceNormalizeViewDir(c);
                    float3 side = cross(ax, vd);
                    float sl = length(side);
                    side = sl > 1e-4 ? side / sl : float3(1, 0, 0);
                    w = c + ax * (IN.corner.y * IN.size.x) + side * (IN.corner.x * IN.size.y);
                    a *= IN.extra.z;
                }
                else
                {
                    // 기둥 안의 한 점 (가로 방향은 카메라와 무관한 수평 방향)
                    float3 side0 = cross(ax, float3(0, 1, 0));
                    side0 = dot(side0, side0) > 1e-6 ? normalize(side0) : float3(1, 0, 0);
                    float3 p = c + ax * ((IN.extra.z * 2.0 - 1.0) * IN.size.x) + side0 * (IN.extra.w * IN.size.y);

                    // 둥실둥실
                    float t = _RSShaftClock;
                    float s = IN.extra.x * 6.2831;
                    p += float3(sin(t * 0.37 + s), sin(t * 0.53 + s * 1.7) * 0.7, cos(t * 0.29 + s * 2.3)) * _Mote.y;

                    // 반짝임 + 기둥 위아래 흐려짐을 따라감
                    float tw = 0.35 + 0.65 * saturate(0.5 + 0.5 * sin(t * (1.3 + IN.extra.x * 2.0) + s * 3.0));
                    float v = IN.extra.z;
                    a *= tw * smoothstep(0.0, _Shape.y, v) * (1.0 - smoothstep(1.0 - _Shape.z, 1.0, v));
                    a *= 1.0 - smoothstep(0.35, 0.75, abs(IN.extra.w));   // 가장자리 먼지는 옅게

                    float sz = IN.extra.y - 1.0;
                    float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                    w = p + (right * IN.corner.x + up * IN.corner.y) * sz;
                }

                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(w);
                if (a <= 0.0) OUT.positionCS = float4(2, 2, 2, 1);   // 안 보이면 화면 밖으로 (그리지 않음)
                OUT.corner = IN.corner;
                OUT.data = float4(a, IN.extra.x, isMote ? 1.0 : 0.0, distance(w, GetCameraPositionWS()));
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = IN.corner.x;
                float v = IN.corner.y * 0.5 + 0.5;          // 0 = 위, 1 = 아래
                half a;

                if (IN.data.z < 0.5)
                {
                    // 가로: 가우스로 퍼지고 판 끝에서 0 — 가장자리 선이 안 보인다
                    float k = lerp(9.0, 2.2, _Shape.x);
                    half across = exp(-u * u * k) * saturate(1.0 - u * u);
                    // 세로: 하늘에서 서서히 나타나 지면 근처에서 사라짐
                    half along = smoothstep(0.0, _Shape.y, v) * (1.0 - smoothstep(1.0 - _Shape.z, 1.0, v));

                    // 결: 느리게 흐르는 얼룩 (세로로 길쭉)
                    float sd = IN.data.y * 17.0;
                    float t = _RSShaftClock * _Noise.z;
                    float2 q = float2(u * 1.6 * _Noise.y + sd, v * 3.0 * _Noise.y - t);
                    float n = VNoise(q) * 0.65 + VNoise(q * 2.3 + 11.0 + float2(t * 0.7, 0)) * 0.35;
                    half density = lerp(1.0, saturate(n * 1.8 - 0.2), _Noise.x);

                    // 카메라 가까이서 흐려짐
                    half nearF = _Noise.w > 0.001 ? saturate((IN.data.w - _Noise.w * 0.3) / _Noise.w) : 1.0;

                    a = across * along * density * nearF * IN.data.x;
                    if (_Shape.w >= 1.0) a = floor(a * _Shape.w + 0.5) / _Shape.w;
                }
                else
                {
                    // 먼지: 둥근 빛 점
                    float r = length(IN.corner);
                    half dot1 = saturate(1.0 - r);
                    a = dot1 * dot1 * _Mote.x * IN.data.x;
                }

                half3 col = _Color.rgb * (_Color.a * a);
                col = MixFogColor(col, half3(0, 0, 0), IN.fogCoord);
                return half4(col, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
