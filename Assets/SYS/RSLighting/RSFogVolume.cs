// RE:AL STEEL - 볼류메트릭 안개 상자 (RSFogVolume)
//
// 이 오브젝트 둘레 상자 안에 빛을 받는 안개를 채운다. 햇빛이 그림자 사이로 빛줄기가 되고,
// 밤에는 캐릭터 빛 · 가로등 둘레가 뿌옇게 번진다. 셰이더: RE_AL STEEL/Fog Volume.
//
// 상자 메시 · 머티리얼은 자동으로 만들고 저장하지 않는다. 설정은 이 컴포넌트에서만 바꾼다.
// 필요: URP 에셋 Depth Texture (물 설치 메뉴가 이미 켬). 무게: 상자가 화면을 덮는 넓이 × 걸음 수.
//
// 포인트 · 스폿 라이트는 걸음마다 더하지 않고 광선 위 적분을 식으로 푼다 (디더가 안 보이고 가볍다).
// 이 컴포넌트가 상자에 닿는 라이트를 골라(최대 8개, 밝고 범위 큰 순) 셰이더에 넘긴다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("볼류메트릭 안개", "상자 안에 빛을 받는 안개를 채운다.\n" +
        "· 햇빛이 나무 · 건물 그림자 사이로 빛줄기가 되고, 밤엔 달빛 · 캐릭터 빛 · 가로등 둘레가 뿌옇게\n" +
        "· 바닥에 깔리고(높이 감쇠) 바람에 흐른다. 벽 뒤로는 새지 않는다 (깊이 텍스처)\n" +
        "· 시간대 안개색을 따르게 하면 새벽 · 노을 · 밤 색이 자동으로 바뀐다\n" +
        "· 캐릭터 빛 · 가로등 번짐은 식으로 계산해서 걸음 수와 상관없이 매끈하다 (상자에 닿는 라이트 최대 8개)\n" +
        "· 일부 구역만 진하게 · 옅게 · 없게: 아래 '안개 구역' 에 네모 구역 추가 (상자를 겹치지 않는다)\n" +
        "· 무거우면: 걸음 간격을 늘리거나 상자를 필요한 곳만 덮게")]
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("RE_AL STEEL/Lighting/볼류메트릭 안개 상자")]
    public partial class RSFogVolume : MonoBehaviour
    {
        [RSGroup("범위")]
        [RSHelp("상자 크기 (m). 이 오브젝트 위치가 상자 가운데. 스테이지를 덮는 큰 상자 하나를 두고, 실내 · 골목 · 물가처럼 일부만 다르게 할 곳은 상자를 겹치지 말고 아래 '안개 구역' 에 구역을 추가한다.")]
        [Tooltip("상자 크기 X, Y(높이), Z (m)")]
        [RSKey]
        public Vector3 size = new Vector3(40f, 8f, 40f);
        [Tooltip("상자 가장자리에서 안개가 옅어지는 폭 (m)")]
        public float edgeFade = 3f;

        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("안개")]
            [Tooltip("안개 색 (빛을 받기 전의 색)")]
            [RSKey]
            public Color color = new Color(0.78f, 0.82f, 0.9f);
            [Tooltip("시간대(RS 시간대)가 정한 안개색을 곱한다 — 새벽 · 노을 · 밤 색이 자동으로")]
            public bool followTimeOfDay = true;
            [Range(0f, 1f), Tooltip("밀도 (1/m). 0.05 옅은 연무 · 0.15 짙은 안개")]
            [RSKey]
            public float density = 0.08f;
            [Range(0f, 2f), Tooltip("높이 감쇠. 클수록 바닥에 낮게 깔린다")]
            public float heightFalloff = 0.35f;
            [Tooltip("노이즈 덩어리 크기 (작을수록 큰 덩어리)")]
            public float noiseScale = 0.12f;
            [Range(0f, 1f), Tooltip("노이즈 세기 (0 = 고른 안개)")]
            public float noiseStrength = 0.6f;
            [Range(0f, 1f), Tooltip("뒤를 가리는 정도. 1 = 짙은 안개처럼 뒤가 흐려짐 / 0.3 = 실내 먼지처럼 빛기둥만 보이고 방 안은 또렷")]
            public float occlusion = 1f;
            [Tooltip("바람 (m/s) — 안개가 흐르는 방향과 빠르기")]
            public Vector3 wind = new Vector3(0.6f, 0f, 0.2f);
            [RSGroup("빛")]
            [RSHelp("안개가 받는 빛. 빛줄기는 '그림자 세기'와 '앞쪽 산란'이 만든다 — 해를 향해 볼 때 가장 밝다.")]
            [Range(0f, 4f), Tooltip("햇빛 · 달빛 세기")]
            public float sunStrength = 1.2f;
            [Range(0f, 1f), Tooltip("그림자 세기. 1 = 그림자 속은 어둡게 → 빛줄기가 뚜렷")]
            public float shadowStrength = 1f;
            [Range(0f, 0.9f), Tooltip("빛 앞쪽 산란. 0 = 고르게 / 0.8 = 해를 볼 때만 강하게 빛남")]
            public float anisotropy = 0.55f;
            [Range(0f, 2f), Tooltip("주변광(하늘빛) 세기")]
            public float ambientStrength = 0.6f;
            [Tooltip("포인트 · 스폿 라이트도 안개를 밝힌다 (캐릭터 빛 · 가로등)")]
            public bool pointLights = true;
            [Range(0f, 4f), Tooltip("포인트 · 스폿 라이트 세기")]
            public float pointStrength = 1.5f;
            [Range(0.05f, 2f), Tooltip("라이트 중심 부드러움 (m). 작을수록 라이트 바로 둘레가 눈부시게 뭉친다")]
            public float pointCore = 0.35f;

            [RSGroup("갓레이 (안개 속 빛줄기)")]
            [RSHelp("햇빛이 건물 · 나무 그림자와 구름 · 나뭇잎 그림자(RS 구름 그림자 쿠키) 사이로 비쳐 안개 속에 빛기둥이 선다.\n" +
                "맵에 가릴 물체가 없어도 구름 쿠키만으로 얼룩진 빛기둥이 생긴다. 화면에서 해를 향한 빛살은 '갓레이 (화면)' 컴포넌트.")]
            [Range(0f, 8f), Tooltip("빛줄기 강조. 햇빛이 닿는 곳만 더 밝힌다 (그늘은 그대로) → 빛기둥이 선다")]
            public float shaftBoost = 2f;
            [Range(0f, 1f), Tooltip("빛줄기 대비. 그늘 쪽 뿌연 주변광을 뺀다. 올릴수록 빛기둥 사이가 어둡다")]
            public float shaftContrast = 0.4f;
            [Range(0f, 1f), Tooltip("빛줄기 무늬. 해 방향으로 늘어진 기둥 무늬를 섞는다 — 맵에 가릴 물체가 없어도 빛기둥이 선다. 0 = 그림자 · 구름으로만")]
            public float shaftPattern = 0.6f;
            [Range(0.02f, 2f), Tooltip("빛줄기 무늬 촘촘함 (1m 에 몇 번). 작을수록 굵은 기둥")]
            public float shaftPatternScale = 0.6f;
            [Range(0f, 1f), Tooltip("보는 방향 고르게. 빛기둥은 기둥 방향으로 볼 때 진하고 옆에서 보면 흐리다(실제 물리). 올리면 옆에서도 보이고 정면은 덜 과하다. 0 = 물리 그대로")]
            public float shaftEven = 0f;
            [Range(0.5f, 4f), Tooltip("빛줄기 또렷함. 클수록 빛기둥 경계가 날카롭다")]
            public float shaftSharpness = 1.5f;
            [Range(0f, 1f), Tooltip("구름 · 나뭇잎 쿠키 반영. 0 = 건물 · 나무 그림자만 / 1 = 구름 얼룩까지 빛기둥으로")]
            public float cookieShafts = 0.8f;
            [Tooltip("빛줄기 강조를 시간대 '햇살 빛줄기 (시각별)' 커브에 따라 (아침 · 노을 강하게, 한낮 약하게). 밤은 아래 '달빛 빛줄기'")]
            public bool shaftsFollowTime = true;
            [Range(0f, 2f), Tooltip("밤(달빛)일 때 빛줄기 강조 배율. 0 = 밤엔 빛줄기 없음")]
            public float moonShafts = 0.6f;
        }

        [SerializeField, RSLook] Look look = new Look();
        [RSGroup("스테이지 룩"), Tooltip("켜면 스테이지 룩 프로필을 따르지 않고 이 상자 값만 쓴다 (안개 상자가 여러 개인데 한 곳만 다르게 할 때)")]
        public bool ownLook = false;

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값(이 상자만 따로가 아니면), 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null && !ownLook ? p.fog : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null && !ownLook; } }



        [RSGroup("품질")]
        [Range(4, 48), Tooltip("최대 걸음 수 (햇빛 · 그림자 빛줄기용). 라이트 번짐은 걸음과 상관없다")]
        public int steps = 16;
        [Range(0.25f, 8f), Tooltip("걸음 간격 (m). 광선 길이 ÷ 간격만큼 걷는다 (최대 걸음 수까지). 클수록 가볍고 빛줄기가 거칠다")]
        public float stepLength = 1.5f;
        [Tooltip("카메라에서 이 거리까지만 계산 (m)")]
        public float maxDistance = 60f;
        [Range(1, 8), Tooltip("디더 칸 크기 (화면 픽셀). 도트 느낌은 2")]
        public int ditherPixel = 2;
        public enum DitherStyle { 고운결, 도트격자, 무작위알갱이 }
        [Tooltip("디더 무늬. 고운결 = 가장 고르지만 가까이서 빗살 무늬 / 도트격자 = 픽셀 아트 느낌 / 무작위알갱이 = 필름 노이즈")]
        public DitherStyle ditherStyle = DitherStyle.고운결;
        [HideInInspector, Tooltip("조절용: 안개 대신 빛줄기(강조 부분)만 크게 보여 준다. 인스펙터 맨 위 버튼으로 켜고 끈다")]
        public bool debugShafts = false;

        public enum ShaftPreset { 약하게, 보통, 강하게 }

        /// <summary>빛줄기 세기 프리셋 — 밀도 · 햇빛 세기 · 빛줄기 강조 · 대비만 바꾼다</summary>
        public void ApplyShaftPreset(ShaftPreset p)
        {
            var l = L;
            switch (p)
            {
                case ShaftPreset.약하게: l.density = 0.06f; l.sunStrength = 1.0f; l.shaftBoost = 1.5f; l.shaftContrast = 0.7f; break;
                case ShaftPreset.보통:   l.density = 0.10f; l.sunStrength = 1.3f; l.shaftBoost = 2.5f; l.shaftContrast = 0.85f; break;
                case ShaftPreset.강하게: l.density = 0.14f; l.sunStrength = 1.6f; l.shaftBoost = 3.0f; l.shaftContrast = 0.85f; break;   // 시험방 첫 값
            }
            Apply();
        }

        // 라이트 목록 · 안개 구역은 배열이라 머티리얼 상수 버퍼(UnityPerMaterial)에 못 넣는다.
        // 머티리얼에 넣으면 SRP Batcher 가 안개 상자들끼리 값을 섞어 쓴다(그리는 순서 = 카메라 위치에 따라 바뀜)
        // → 렌더러마다 따로인 프로퍼티 블록으로 넘긴다
        MaterialPropertyBlock mpb;
        MaterialPropertyBlock Mpb { get { if (mpb == null) mpb = new MaterialPropertyBlock(); return mpb; } }

        // ─────────────────────────────────────────────────────────────
        // 안개 구역: 이 상자 안 네모 구역마다 밀도 · 빛줄기 · 색을 바꾼다 (목록 순서대로 적용, 최대 8개)
        // 계산은 이 상자 하나가 하고 구역은 값만 → 상자를 겹치지 않아도 실내 · 골목 · 늪을 따로 맞춘다
        // (Volume 기반 Modifier 스택: 기본값 → 구역 1 → 구역 2 …)
        // ─────────────────────────────────────────────────────────────
        public enum ZoneMode { 곱하기, 더하기, 덮어쓰기 }

        [System.Serializable]
        public class Zone
        {
            [Tooltip("구분용 이름 (예: 방 안, 골목, 늪)")]
            public string name = "구역";
            [Tooltip("끄면 적용 안 함")]
            public bool enabled = true;
            [Tooltip("이 오브젝트를 따라 움직인다 (위치 · 회전). 비우면 이 안개 상자 기준")]
            public Transform follow;
            [Tooltip("가운데 위치 (m) — 따라가는 오브젝트(없으면 이 안개 상자) 기준")]
            public Vector3 center = Vector3.zero;
            [Tooltip("크기 X, Y(높이), Z (m)")]
            public Vector3 size = new Vector3(10f, 4f, 10f);
            [Min(0f), Tooltip("경계에서 서서히 바뀌는 폭 (m). 안쪽으로 이만큼 들어가야 값이 다 적용된다")]
            public float edgeSoftness = 0.5f;
            [Tooltip("곱하기 = 원래 밀도 × 값 / 더하기 = 원래 밀도 + 값 / 덮어쓰기 = 원래 값 무시하고 이 값")]
            public ZoneMode mode = ZoneMode.곱하기;
            [Min(0f), Tooltip("곱하기면 배율 (3 = 세 배, 0 = 없앰). 더하기 · 덮어쓰기면 밀도 (1/m, 0.05 옅음 · 0.15 짙음)")]
            public float density = 2f;
            [Range(0f, 4f), Tooltip("이 안에서 빛줄기(햇빛이 닿는 곳 강조) 배율. 1 = 그대로")]
            public float shaftMultiplier = 1f;
            [Tooltip("이 안의 안개 색에 곱하는 색. 흰색 = 그대로")]
            public Color tint = Color.white;

            /// <summary>구역 로컬(m, 가운데 0) → 월드</summary>
            public Matrix4x4 ToWorld(Transform owner)
            {
                var a = follow != null ? follow : owner;
                return Matrix4x4.TRS(a.position, a.rotation, Vector3.one) * Matrix4x4.Translate(center);
            }

            /// <summary>월드 축 정렬 경계</summary>
            public Bounds WorldBounds(Transform owner)
            {
                var m = ToWorld(owner);
                Vector3 h = size * 0.5f;
                Vector3 e = new Vector3(
                    Mathf.Abs(m.m00) * h.x + Mathf.Abs(m.m01) * h.y + Mathf.Abs(m.m02) * h.z,
                    Mathf.Abs(m.m10) * h.x + Mathf.Abs(m.m11) * h.y + Mathf.Abs(m.m12) * h.z,
                    Mathf.Abs(m.m20) * h.x + Mathf.Abs(m.m21) * h.y + Mathf.Abs(m.m22) * h.z);
                return new Bounds(m.MultiplyPoint3x4(Vector3.zero), e * 2f);
            }

            public string Summary
            {
                get
                {
                    return mode == ZoneMode.곱하기 ? "×" + density.ToString("0.##")
                         : mode == ZoneMode.더하기 ? "+" + density.ToString("0.###") : "=" + density.ToString("0.###");
                }
            }
        }

        [RSGroup("안개 구역")]
        [Tooltip("이 상자 안 일부만 안개를 진하게 · 옅게 · 없게. 인스펙터의 '안개 구역' 칸에서 추가 · 편집 (씬 뷰 손잡이)")]
        public List<Zone> zones = new List<Zone>();

        public const int MaxZones = 8;
        static readonly int IdZoneCount = Shader.PropertyToID("_FogZoneCount");
        static readonly int IdZone = Shader.PropertyToID("_FogZone");
        static readonly int IdZoneHalf = Shader.PropertyToID("_FogZoneHalf");
        static readonly int IdZoneParam = Shader.PropertyToID("_FogZoneParam");
        static readonly int IdZoneColor = Shader.PropertyToID("_FogZoneColor");
        static readonly Matrix4x4[] zoneM = new Matrix4x4[MaxZones];
        static readonly Vector4[] zoneHalf = new Vector4[MaxZones], zoneParam = new Vector4[MaxZones], zoneColor = new Vector4[MaxZones];

        /// <summary>이 상자의 월드 경계</summary>
        public Bounds WorldBounds
        {
            get
            {
                var m = transform.localToWorldMatrix;
                Vector3 h = size * 0.5f;
                Vector3 e = new Vector3(
                    Mathf.Abs(m.m00) * h.x + Mathf.Abs(m.m01) * h.y + Mathf.Abs(m.m02) * h.z,
                    Mathf.Abs(m.m10) * h.x + Mathf.Abs(m.m11) * h.y + Mathf.Abs(m.m12) * h.z,
                    Mathf.Abs(m.m20) * h.x + Mathf.Abs(m.m21) * h.y + Mathf.Abs(m.m22) * h.z);
                return new Bounds(transform.position, e * 2f);
            }
        }

        void SetZones()
        {
            int n = 0;
            if (zones != null)
                foreach (var z in zones)
                {
                    if (z == null || !z.enabled || n >= MaxZones) continue;
                    Vector3 h = z.size * 0.5f;
                    zoneM[n] = z.ToWorld(transform).inverse;
                    zoneHalf[n] = new Vector4(h.x, h.y, h.z, Mathf.Max(0f, z.edgeSoftness));
                    zoneParam[n] = new Vector4((float)z.mode, Mathf.Max(0f, z.density), z.shaftMultiplier, 0f);
                    Color t = z.tint.linear;
                    zoneColor[n] = new Vector4(t.r, t.g, t.b, 1f);
                    n++;
                }
            Mpb.SetInteger(IdZoneCount, n);
            Mpb.SetMatrixArray(IdZone, zoneM);
            Mpb.SetVectorArray(IdZoneHalf, zoneHalf);
            Mpb.SetVectorArray(IdZoneParam, zoneParam);
            Mpb.SetVectorArray(IdZoneColor, zoneColor);
        }

        void OnDrawGizmos()
        {
            if (zones == null) return;
            foreach (var z in zones)
            {
                if (z == null) continue;
                Gizmos.matrix = z.ToWorld(transform);
                Gizmos.color = z.enabled ? ZoneColor(z) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
                Gizmos.DrawWireCube(Vector3.zero, z.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }

        public static Color ZoneColor(Zone z)
        {
            switch (z.mode)
            {
                case ZoneMode.곱하기: return z.density < 1f ? new Color(0.4f, 0.75f, 1f, 0.8f) : new Color(0.35f, 1f, 0.55f, 0.8f);
                case ZoneMode.더하기: return new Color(1f, 0.8f, 0.3f, 0.8f);
                default: return new Color(1f, 0.45f, 0.85f, 0.8f);
            }
        }

        [SerializeField, HideInInspector] Shader shader;   // 빌드에 셰이더가 빠지지 않게 참조를 들고 있는다

        const string ShaderName = "RE_AL STEEL/Fog Volume";
        Mesh mesh;
        Material mat;
        MeshFilter mf;
        MeshRenderer mr;
        Vector3 builtSize;

        void Reset() { shader = Shader.Find(ShaderName); }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            if (shader == null) shader = Shader.Find(ShaderName);
            mf = GetComponent<MeshFilter>();
            mr = GetComponent<MeshRenderer>();
            mf.hideFlags = HideFlags.HideInInspector;
            mr.hideFlags = HideFlags.HideInInspector;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            builtSize = Vector3.zero;
            Apply();
        }

        /// <summary>스테이지 룩 프로필이 바뀌거나 값이 바뀌면 (인스펙터에서 프로필을 고친 경우 이 컴포넌트의 OnValidate 는 안 불린다)</summary>

        [System.NonSerialized] int lookSeen;
        [System.NonSerialized] RSStageLook lookFrom;
        void OnStageLookChanged()

        {

            if (this == null || !isActiveAndEnabled) return;
            // 다른 묶음(예: 젖은 바닥 슬라이더)만 바뀐 거면 건너뛴다 — 무거운 재계산을 피하려고
            int h = JsonUtility.ToJson(L).GetHashCode();
            if (h == lookSeen && RSStageLook.Current == lookFrom) return;
            lookSeen = h; lookFrom = RSStageLook.Current;

            Apply();

        }


        void OnDisable()
        {



            RSStageLook.Changed -= OnStageLookChanged;
            if (mr != null) { mr.sharedMaterial = null; mr.SetPropertyBlock(null); }
            if (mf != null && mf.sharedMesh == mesh) mf.sharedMesh = null;
            Kill(mesh); Kill(mat);
            mesh = null; mat = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnValidate()
        {

            MigrateLegacy();
            size = new Vector3(Mathf.Max(0.5f, size.x), Mathf.Max(0.5f, size.y), Mathf.Max(0.5f, size.z));
            if (shader == null) shader = Shader.Find(ShaderName);
        }

        // 머티리얼은 저장 안 하는 런타임 것이라 매 프레임 값을 써도 씬이 수정됨으로 표시되지 않는다
        void Update() { Apply(); }

        void Apply()
        {
            if (shader == null) return;
            if (mat == null)
            {
                mat = new Material(shader) { name = "RS_FogVolume (자동)", hideFlags = HideFlags.DontSave };
                mr.sharedMaterial = mat;
            }
            if (mesh == null || builtSize != size) BuildMesh();

            Color c = L.color;
            if (L.followTimeOfDay)
            {
                // 시간대 안개색의 '색조'만 따른다 (밝기는 햇빛 · 달빛이 정한다)
                Color f = RenderSettings.fogColor;
                c *= f / Mathf.Max(0.05f, f.maxColorComponent);
                c.a = 1f;
            }
            mat.SetVector("_BoxHalf", size * 0.5f);
            mat.SetColor("_FogColor", c);
            mat.SetFloat("_Density", L.density);
            mat.SetFloat("_HeightFalloff", L.heightFalloff);
            mat.SetFloat("_EdgeFade", edgeFade);
            mat.SetFloat("_NoiseScale", L.noiseScale);
            mat.SetFloat("_NoiseStrength", L.noiseStrength);
            mat.SetVector("_Wind", L.wind);
            mat.SetFloat("_Steps", steps);
            mat.SetFloat("_StepLength", stepLength);
            mat.SetFloat("_PointCore", L.pointCore);
            float shaftK = 1f;
            if (L.shaftsFollowTime)
            {
                if (tod == null && (Time.frameCount >= todSearchFrame || Time.frameCount < todSearchFrame - 200)) { tod = FindAnyObjectByType<RSTimeOfDay>(); todSearchFrame = Time.frameCount + 120; }
                if (tod != null) shaftK = tod.IsMoon ? L.moonShafts : tod.ShaftFactor;
            }
            mat.SetFloat("_ShaftBoost", L.shaftBoost * shaftK);
            mat.SetFloat("_ShaftContrast", L.shaftContrast);
            mat.SetFloat("_ShaftSharpness", L.shaftSharpness);
            mat.SetFloat("_CookieShafts", L.cookieShafts);
            mat.SetFloat("_ShaftPattern", L.shaftPattern);
            mat.SetFloat("_ShaftPatternScale", L.shaftPatternScale);
            mat.SetFloat("_ShaftEven", L.shaftEven);
            mat.SetFloat("_ShaftDebug", debugShafts ? 1f : 0f);
            mat.SetTexture(IdNoise, NoiseTexture);
            SetLights(L.pointLights);
            SetZones();
            if (mr != null) mr.SetPropertyBlock(Mpb);
            mat.SetFloat("_MaxDistance", maxDistance);
            mat.SetFloat("_Anisotropy", L.anisotropy);
            mat.SetFloat("_SunStrength", L.sunStrength);
            mat.SetFloat("_ShadowStrength", L.shadowStrength);
            mat.SetFloat("_AmbientStrength", L.ambientStrength);
            mat.SetFloat("_PointStrength", L.pointStrength);
            mat.SetFloat("_DitherPixel", ditherPixel);
            mat.SetFloat("_DitherStyle", (float)ditherStyle);
            mat.SetFloat("_Occlusion", L.occlusion);
        }

        // ─────────────────────────────────────────────────────────────
        // 포인트 · 스폿 라이트 목록
        // ─────────────────────────────────────────────────────────────

        RSTimeOfDay tod;
        int todSearchFrame = -1;

        const int MaxLights = 8;
        static readonly int IdNoise = Shader.PropertyToID("_FogNoise");
        static readonly int IdCount = Shader.PropertyToID("_FogLightCount");
        static readonly int IdPos = Shader.PropertyToID("_FogLightPos");
        static readonly int IdCol = Shader.PropertyToID("_FogLightColor");
        static readonly int IdSpot = Shader.PropertyToID("_FogLightSpot");
        static readonly Vector4[] lpos = new Vector4[MaxLights], lcol = new Vector4[MaxLights], lspot = new Vector4[MaxLights];
        static readonly List<(float score, Light l)> picked = new List<(float, Light)>();
        static Light[] sceneLights = new Light[0];
        static float lightsScannedAt = -10f;

        /// <summary>라이트 목록을 다음 프레임에 다시 찾는다 (라이트를 새로 만든 직후 등)</summary>
        public static void RescanLights() { lightsScannedAt = -10f; }

        void SetLights(bool on)
        {
            int n = 0;
            if (on && mr != null)
            {
                // 씬 라이트 찾기는 1초에 한 번 (매 프레임 찾으면 무겁다)
                float now = Time.realtimeSinceStartup;
                if (now - lightsScannedAt > 1f || now < lightsScannedAt)
                {
                    sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                    lightsScannedAt = now;
                }
                Bounds b = mr.bounds;
                picked.Clear();
                foreach (var l in sceneLights)
                {
                    if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) continue;
                    if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                    Vector3 p = l.transform.position;
                    if (b.SqrDistance(p) > l.range * l.range) continue;   // 상자에 안 닿음
                    float score = l.intensity * l.color.maxColorComponent * l.range;
                    picked.Add((score, l));
                }
                picked.Sort((x, y) => y.score.CompareTo(x.score));
                bool temp = GraphicsSettings.lightsUseColorTemperature;
                for (int i = 0; i < picked.Count && n < MaxLights; i++, n++)
                {
                    var l = picked[i].l;
                    Vector3 p = l.transform.position;
                    Color c = l.color.linear * l.intensity;
                    if (temp && l.useColorTemperature) c *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature);
                    float spotInv = 0f, spotAdd = 1f;
                    Vector3 dir = l.transform.forward;
                    if (l.type == LightType.Spot)
                    {
                        // URP 와 같은 원뿔 감쇠 식
                        float cosOut = Mathf.Cos(Mathf.Deg2Rad * l.spotAngle * 0.5f);
                        float cosIn = Mathf.Cos(Mathf.Deg2Rad * l.innerSpotAngle * 0.5f);
                        spotInv = 1f / Mathf.Max(0.001f, cosIn - cosOut);
                        spotAdd = -cosOut * spotInv;
                    }
                    lpos[n] = new Vector4(p.x, p.y, p.z, l.range);
                    lcol[n] = new Vector4(c.r, c.g, c.b, spotAdd);
                    lspot[n] = new Vector4(dir.x, dir.y, dir.z, spotInv);
                }
            }
            Mpb.SetInteger(IdCount, n);
            Mpb.SetVectorArray(IdPos, lpos);
            Mpb.SetVectorArray(IdCol, lcol);
            Mpb.SetVectorArray(IdSpot, lspot);
        }

        // ─────────────────────────────────────────────────────────────
        // 노이즈 텍스처 (32³, 반복, 2 옥타브 값 노이즈를 미리 구움 — 셰이더가 걸음마다 한 번만 읽는다)
        // ─────────────────────────────────────────────────────────────

        static Texture3D noiseTex;

        static Texture3D NoiseTexture
        {
            get
            {
                if (noiseTex != null) return noiseTex;
                const int N = 32;
                var data = new byte[N * N * N];
                for (int z = 0; z < N; z++)
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            float a = ValueNoise(x, y, z, N, 4, 17);   // 큰 덩어리 (텍스처 한 장에 4칸)
                            float c = ValueNoise(x, y, z, N, 9, 71);   // 잔 결 (9칸 ≈ 2.25배)
                            data[(z * N + y) * N + x] = (byte)Mathf.Clamp(Mathf.RoundToInt((a * 0.65f + c * 0.35f) * 255f), 0, 255);
                        }
                noiseTex = new Texture3D(N, N, N, TextureFormat.R8, false)
                {
                    name = "RS_FogNoise (자동)", hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                };
                noiseTex.SetPixelData(data, 0);
                noiseTex.Apply(false, true);
                return noiseTex;
            }
        }

        // 반복되는 값 노이즈: period 칸짜리 격자, 부드러운 보간
        static float ValueNoise(int x, int y, int z, int n, int period, int seed)
        {
            float fx = (float)x * period / n, fy = (float)y * period / n, fz = (float)z * period / n;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy), iz = Mathf.FloorToInt(fz);
            float ux = S(fx - ix), uy = S(fy - iy), uz = S(fz - iz);
            float v000 = H(ix, iy, iz, period, seed), v100 = H(ix + 1, iy, iz, period, seed);
            float v010 = H(ix, iy + 1, iz, period, seed), v110 = H(ix + 1, iy + 1, iz, period, seed);
            float v001 = H(ix, iy, iz + 1, period, seed), v101 = H(ix + 1, iy, iz + 1, period, seed);
            float v011 = H(ix, iy + 1, iz + 1, period, seed), v111 = H(ix + 1, iy + 1, iz + 1, period, seed);
            float a = Mathf.Lerp(Mathf.Lerp(v000, v100, ux), Mathf.Lerp(v010, v110, ux), uy);
            float b = Mathf.Lerp(Mathf.Lerp(v001, v101, ux), Mathf.Lerp(v011, v111, ux), uy);
            return Mathf.Lerp(a, b, uz);
        }
        static float S(float t) { return t * t * (3f - 2f * t); }
        static float H(int x, int y, int z, int period, int seed)
        {
            x = ((x % period) + period) % period; y = ((y % period) + period) % period; z = ((z % period) + period) % period;
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791) ^ (uint)(seed * 2654435761u);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFF) / 65535f;
        }

        void BuildMesh()
        {
            if (mesh == null) mesh = new Mesh { name = "RS_FogBox", hideFlags = HideFlags.DontSave };
            Vector3 h = size * 0.5f;
            var v = new[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y,  h.z), new Vector3(h.x, -h.y,  h.z), new Vector3(h.x, h.y,  h.z), new Vector3(-h.x, h.y,  h.z),
            };
            // 바깥을 보는 면 (셰이더는 Cull Front 로 안쪽 면을 그린다)
            var t = new[]
            {
                0, 2, 1, 0, 3, 2,   // -Z
                4, 5, 6, 4, 6, 7,   // +Z
                0, 1, 5, 0, 5, 4,   // -Y
                3, 7, 6, 3, 6, 2,   // +Y
                0, 4, 7, 0, 7, 3,   // -X
                1, 2, 6, 1, 6, 5,   // +X
            };
            mesh.Clear();
            mesh.vertices = v;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            builtSize = size;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
