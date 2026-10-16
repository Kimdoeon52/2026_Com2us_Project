// RE:AL STEEL - 햇살 빛줄기 (옥토패스식 빛 내림 · 연무 · 떠다니는 먼지)
//
// 옥토패스의 햇살은 가는 광선이 아니라 넓고 흐릿한 "빛 연무" 다: 가장자리가 없고, 안에서 결이 천천히 흐르고,
// 반짝이는 먼지가 떠다닌다. 이 컴포넌트는 그런 빛 기둥 몇 개를 영역 안 여기저기에 띄웠다가
// 스르르 사라지게 하고, 다른 자리에 다시 띄운다.
//
// · 이 오브젝트 위치 = 영역 중심 · 지면 높이
// · 방향은 태양을 따르고(각도 조금씩 흩어짐), 세기 · 색은 시간대(RSTimeOfDay)의 커브 · 그라데이션이 정한다
// · 전부 합쳐 메시 하나, 드로우콜 1. 나타남/사라짐 · 흐름 · 먼지 움직임은 셰이더가 계산한다
//   (메시는 빛줄기가 새 자리로 옮겨갈 때만 다시 만든다)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("햇살 빛줄기", "하늘에서 비스듬히 내려오는 넓고 흐릿한 빛 연무 기둥. 영역 안 여기저기에 생겼다가 사라지고 다른 자리에 다시 생긴다. 기둥 안에 먼지가 떠다닌다.\n· 이 오브젝트 위치 = 영역 가운데 · 지면 높이\n· 방향은 태양을 따르고, 세기 · 색은 시간대의 햇살 커브 · 그라데이션이 정한다 (밤엔 없음)\n· 전부 합쳐 드로우콜 1")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/햇살 빛줄기")]
    public partial class RSLightShafts : MonoBehaviour, IRSEditorAnimated
    {
        const string ChildName = "__RS_LightShafts (자동 생성 · 저장 안 됨)";

        [Tooltip("방향을 따라갈 태양. 비우면 씬의 기본 태양 (RenderSettings.sun)")]
        public Light sun;
        [Tooltip("RE_AL STEEL/Light Shaft 셰이더 머티리얼 (설치 메뉴가 MAT_RS_LightShaft 를 넣어 준다)")]
        public Material material;

        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("배치")]
            [RSHelp("어디에 몇 개. 옥토패스처럼 넓고 적게(3 ~ 5개) 두는 게 자연스럽다.")]
            [Range(0, 16), Tooltip("동시에 있는 빛줄기 수")]
            [RSKey]
            public int count = 4;
            [Tooltip("빛줄기 길이 범위 (m, 최소 ~ 최대)")]
            public Vector2 length = new Vector2(16f, 26f);
            [Tooltip("빛줄기 폭 범위 (m). 넓을수록 연무처럼 보인다 (4 ~ 9)")]
            public Vector2 width = new Vector2(4f, 9f);
            [Range(20f, 90f), Tooltip("해가 낮아도 이 각도(도)보다 눕지 않는다 — 너무 길게 눕는 것 방지")]
            public float minSteepness = 45f;
            [RSGroup("세기 · 색")]
            [RSHelp("최종 세기 = Intensity × 시간대의 햇살 커브. 색 = Tint × 시간대의 햇살 색.")]
            [Range(0f, 3f), Tooltip("기본 세기. 0.2 ~ 0.5")]
            [RSKey]
            public float intensity = 0.35f;
            [Range(0f, 1f), Tooltip("빛줄기마다 세기를 다르게 (0 = 모두 같게)")]
            public float intensityVariation = 0.4f;
            [Tooltip("빛줄기 색 (시간대 햇살 색에 곱해진다)")]
            [RSKey]
            public Color tint = new Color(1f, 0.96f, 0.86f, 1f);
            [RSGroup("모양")]
            [RSHelp("기둥 하나의 모양. 옥토패스 햇살은 가장자리가 없고 안에서 얼룩이 천천히 흐른다.")]
            [Range(0.05f, 1f), Tooltip("가장자리 부드러움 (1 = 가장자리 없이 연무처럼)")]
            public float edgeSoftness = 1f;
            [Range(0.01f, 1f), Tooltip("하늘 쪽에서 서서히 나타나는 구간 (길이 대비)")]
            public float topFade = 0.6f;
            [Range(0.01f, 1f), Tooltip("지면 쪽에서 사라지는 구간 (길이 대비)")]
            public float bottomFade = 0.35f;
            [Range(0f, 1f), Tooltip("빛 안의 결 (흐르는 얼룩) 세기. 0 = 매끈한 기둥")]
            public float noiseAmount = 0.55f;
            [Tooltip("결 크기 (클수록 잘다)")]
            public float noiseScale = 1.2f;
            [Tooltip("결이 흐르는 속도")]
            public float noiseSpeed = 0.06f;
            [Tooltip("카메라에서 이 거리(m) 안이면 흐려진다 — 가까이서 뿌연 덩어리가 되는 것 방지")]
            public float nearFade = 6f;
            [Range(0, 12), Tooltip("밝기 계단 (0 = 부드럽게, 4 ~ 6 = 픽셀아트식)")]
            public int steps = 0;
            [RSGroup("떠다니는 먼지")]
            [RSHelp("빛 속을 떠다니며 반짝이는 먼지.")]
            [Range(0, 24), Tooltip("빛줄기 하나당 먼지 수 (0 = 끔)")]
            public int motesPerShaft = 8;
            [Tooltip("먼지 크기 범위 (m)")]
            public Vector2 moteSize = new Vector2(0.05f, 0.11f);
            [Range(0f, 8f), Tooltip("먼지 밝기 (빛줄기 세기에 곱해진다)")]
            public float moteBrightness = 2.5f;
            [Tooltip("먼지가 둥실거리는 폭 (m)")]
            public float moteDrift = 0.35f;
            [RSGroup("나타남 · 사라짐")]
            [RSHelp("빛줄기가 생겼다 사라지는 리듬.")]
            [Tooltip("한 빛줄기가 머무는 시간 범위 (초)")]
            public Vector2 lifetime = new Vector2(10f, 18f);
            [Tooltip("나타나고 사라지는 데 걸리는 시간 (초)")]
            public float fadeTime = 3.5f;
            [Tooltip("빛줄기가 해 방향으로 천천히 흐르는 속도 (m/s)")]
            public float drift = 0.1f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.shafts : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }

        [RSGroup("배치")]
        [Tooltip("빛줄기가 생기는 영역 (이 오브젝트 기준 X, Z, m)")]
        public Vector2 area = new Vector2(34f, 34f);
        [Range(0f, 20f), Tooltip("빛줄기마다 방향이 조금씩 흩어지는 각도 (도). 0 = 전부 평행 (인위적)")]
        public float spread = 6f;
        [Tooltip("지면 아래로 파고드는 깊이 (m). 아래쪽이 흐려지며 끝나서 땅에 닿는 선이 안 보이게")]
        public float sinkIntoGround = 1.5f;
        [Tooltip("무작위 시드. 바꾸면 위치 · 크기 순서가 새로")]
        public int seed = 3;





        [RSGroup("에디터")]
        [Tooltip("플레이하지 않아도 에디터에서 움직임을 본다")]
        public bool animateInEditMode = true;

        /// <summary>시간대가 넣는 값</summary>
        [System.NonSerialized] public float intensityScale = 1f;
        [System.NonSerialized] public Color sunColor = Color.white;

        GameObject child;
        Mesh mesh;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        int builtKey, mpbKey;

        readonly List<Vector3> vCenter = new List<Vector3>();
        readonly List<Vector3> vAxis = new List<Vector3>();
        readonly List<Vector4> vSize = new List<Vector4>();
        readonly List<Vector2> vCorner = new List<Vector2>();
        readonly List<Vector2> vLife = new List<Vector2>();
        readonly List<Vector4> vExtra = new List<Vector4>();
        readonly List<int> tris = new List<int>();

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int NoiseId = Shader.PropertyToID("_Noise");
        static readonly int MoteId = Shader.PropertyToID("_Mote");
        static readonly int ClockId = Shader.PropertyToID("_RSShaftClock");

        public bool WantsEditorAnimation { get { return isActiveAndEnabled && animateInEditMode; } }

        /// <summary>빛줄기 시계. 셰이더도 같은 값(_RSShaftClock)으로 움직임을 계산한다</summary>
        static float Now
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return (float)(UnityEditor.EditorApplication.timeSinceStartup % 100000.0);
#endif
                return Time.time;
            }
        }

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            RSLightingClock.Register(this);
            builtKey = 0; mpbKey = 0;
            Refresh();
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

            OnValidate(); Refresh();

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            RSLightingClock.Unregister(this);
            // 오브젝트가 꺼지는 중에는 자식을 바로 지울 수 없다 (유니티 제약) — 그땐 참조만 놓고 다음에 DestroyStale 이 정리
            if (Application.isPlaying || gameObject.activeInHierarchy)
            {
                if (child != null) { if (Application.isPlaying) Destroy(child); else DestroyImmediate(child); }
            }
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            child = null; mesh = null; mr = null;
        }

        void OnValidate()
        {

            MigrateLegacy();
            L.count = Mathf.Max(0, L.count);
            L.fadeTime = Mathf.Max(0.05f, L.fadeTime);
            L.lifetime.x = Mathf.Max(L.fadeTime * 2f + 0.1f, L.lifetime.x);
            L.lifetime.y = Mathf.Max(L.lifetime.x, L.lifetime.y);
            L.nearFade = Mathf.Max(0f, L.nearFade);
            builtKey = 0; mpbKey = 0;
        }

        void Update() { Refresh(); }
        public void EditorTick(float dt) { Refresh(); }

        /// <summary>옥토패스 참고 권장값으로</summary>
        public void ResetLook()
        {
            L.count = 4; area = new Vector2(34f, 34f); L.length = new Vector2(16f, 26f); L.width = new Vector2(4f, 9f);
            L.minSteepness = 45f; spread = 6f; sinkIntoGround = 1.5f;
            L.intensity = 0.35f; L.intensityVariation = 0.4f; L.tint = new Color(1f, 0.96f, 0.86f, 1f);
            L.edgeSoftness = 1f; L.topFade = 0.6f; L.bottomFade = 0.35f; L.noiseAmount = 0.55f; L.noiseScale = 1.2f; L.noiseSpeed = 0.06f;
            L.nearFade = 6f; L.steps = 0;
            L.motesPerShaft = 8; L.moteSize = new Vector2(0.05f, 0.11f); L.moteBrightness = 2.5f; L.moteDrift = 0.35f;
            L.lifetime = new Vector2(10f, 18f); L.fadeTime = 3.5f; L.drift = 0.1f;
            builtKey = 0; mpbKey = 0;
            Refresh();
        }

        // ─────────────────────────────────────────────────────────────

        void EnsureChild()
        {
            if (child != null) return;
            DestroyStale();
            child = new GameObject(ChildName);
            child.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>();
            mr = child.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mesh = new Mesh { name = "RS_LightShafts", hideFlags = HideFlags.DontSave };
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            builtKey = 0; mpbKey = 0;
        }

        void DestroyStale()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name != ChildName) continue;
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }
        }

        public void Refresh()
        {
            EnsureChild();
            if (mr == null) mr = child.GetComponent<MeshRenderer>();

            // 바뀐 것만 쓴다 — 에디터에서 매 프레임 오브젝트를 건드리면 인스펙터가 계속 다시 만들어져 버튼이 안 눌린다
            if (mr.sharedMaterial != material) mr.sharedMaterial = material;
            var ct = child.transform;
            if (ct.localPosition != Vector3.zero) ct.localPosition = Vector3.zero;
            if (ct.localRotation != Quaternion.identity) ct.localRotation = Quaternion.identity;
            if (ct.localScale != Vector3.one) ct.localScale = Vector3.one;

            Color c = L.tint * sunColor;
            c.a = L.intensity * Mathf.Max(0f, intensityScale);
            bool on = c.a > 0.001f && L.count > 0 && material != null;
            if (mr.enabled != on) mr.enabled = on;
            if (!on) return;

            UpdateBlock(c);

            float now = Now;
            Shader.SetGlobalFloat(ClockId, now);
            BuildMesh(now);
        }

        void UpdateBlock(Color c)
        {
            int key = 17;
            unchecked
            {
                key = key * 31 + Mathf.RoundToInt(c.r * 255f); key = key * 31 + Mathf.RoundToInt(c.g * 255f);
                key = key * 31 + Mathf.RoundToInt(c.b * 255f); key = key * 31 + Mathf.RoundToInt(c.a * 1000f);
                key = key * 31 + (L.edgeSoftness.GetHashCode() ^ L.topFade.GetHashCode() * 3 ^ L.bottomFade.GetHashCode() * 5 ^ L.steps * 7);
                key = key * 31 + (L.noiseAmount.GetHashCode() ^ L.noiseScale.GetHashCode() * 3 ^ L.noiseSpeed.GetHashCode() * 5 ^ L.nearFade.GetHashCode() * 7);
                key = key * 31 + (L.moteBrightness.GetHashCode() ^ L.moteDrift.GetHashCode() * 3);
                if (key == 0) key = 1;
            }
            if (key == mpbKey) return;
            mpbKey = key;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(ColorId, c);
            mpb.SetVector(ShapeId, new Vector4(L.edgeSoftness, L.topFade, L.bottomFade, L.steps));
            mpb.SetVector(NoiseId, new Vector4(L.noiseAmount, L.noiseScale, L.noiseSpeed, L.nearFade));
            mpb.SetVector(MoteId, new Vector4(L.moteBrightness, L.moteDrift, 0f, 0f));
            mr.SetPropertyBlock(mpb);
        }

        Vector3 BaseAxis()
        {
            var s = sun != null ? sun : RenderSettings.sun;
            Vector3 axis = s != null ? s.transform.forward : new Vector3(0.3f, -0.9f, 0.25f).normalized;
            if (axis.y > -0.05f) axis.y = -0.05f;
            return Steepen(axis.normalized);
        }

        Vector3 Steepen(Vector3 axis)
        {
            float minY = Mathf.Sin(L.minSteepness * Mathf.Deg2Rad);
            if (-axis.y >= minY) return axis;
            Vector3 flat = new Vector3(axis.x, 0f, axis.z);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
            flat.Normalize();
            return (flat * Mathf.Cos(L.minSteepness * Mathf.Deg2Rad) + Vector3.down * minY).normalized;
        }

        void Cycle(int i, float now, out int cycle, out float start, out float period)
        {
            period = Mathf.Lerp(L.lifetime.x, L.lifetime.y, Hash01(i, 0, 1));
            float span = period * 1.3f;                                    // 사라진 뒤 조금 쉰다
            float phase = Hash01(i, 0, 2) * span;
            cycle = Mathf.FloorToInt((now + phase) / span);
            start = cycle * span - phase;
        }

        void BuildMesh(float now)
        {
            Vector3 baseAxis = BaseAxis();

            // 바뀐 게 없으면 그대로 둔다
            int key = 17;
            unchecked
            {
                for (int i = 0; i < L.count; i++) { Cycle(i, now, out int cy, out _, out _); key = key * 31 + cy; }
                key = key * 31 + Mathf.RoundToInt(baseAxis.x * 200f);
                key = key * 31 + Mathf.RoundToInt(baseAxis.y * 200f);
                key = key * 31 + Mathf.RoundToInt(baseAxis.z * 200f);
                var p = transform.position;
                key = key * 31 + Mathf.RoundToInt(p.x * 100f) * 7 + Mathf.RoundToInt(p.y * 100f) * 13 + Mathf.RoundToInt(p.z * 100f);
                key = key * 31 + L.count * 3 + seed * 5 + L.motesPerShaft * 7;
                key = key * 31 + (area.GetHashCode() ^ L.length.GetHashCode() * 3 ^ L.width.GetHashCode() * 5 ^ L.lifetime.GetHashCode() * 7);
                key = key * 31 + (L.fadeTime.GetHashCode() ^ L.drift.GetHashCode() * 3 ^ sinkIntoGround.GetHashCode() * 5 ^ spread.GetHashCode() * 7);
                key = key * 31 + (L.intensityVariation.GetHashCode() ^ L.moteSize.GetHashCode() * 3 ^ L.minSteepness.GetHashCode() * 5);
                if (key == 0) key = 1;
            }
            if (key == builtKey && mesh.vertexCount > 0) return;
            builtKey = key;

            vCenter.Clear(); vAxis.Clear(); vSize.Clear(); vCorner.Clear(); vLife.Clear(); vExtra.Clear(); tris.Clear();
            var ct = child.transform;

            for (int i = 0; i < L.count; i++)
            {
                Cycle(i, now, out int cycle, out float start, out float period);

                float gx = (Hash01(i, cycle, 3) - 0.5f) * area.x;
                float gz = (Hash01(i, cycle, 4) - 0.5f) * area.y;
                float len = Mathf.Lerp(L.length.x, L.length.y, Hash01(i, cycle, 5));
                float wid = Mathf.Lerp(L.width.x, L.width.y, Hash01(i, cycle, 6));
                float sd = Hash01(i, cycle, 7);
                float gain = 1f - L.intensityVariation * Hash01(i, cycle, 8);

                // 방향을 조금씩 흩는다
                Vector3 axis = baseAxis;
                if (spread > 0f)
                {
                    var q = Quaternion.Euler((Hash01(i, cycle, 9) - 0.5f) * 2f * spread, (Hash01(i, cycle, 10) - 0.5f) * 2f * spread, 0f);
                    axis = Steepen((q * axis).normalized);
                }

                Vector3 ground = transform.position + new Vector3(gx, 0f, gz);
                Vector3 bottom = ground + axis * sinkIntoGround;
                Vector3 center = bottom - axis * (len * 0.5f);
                Vector3 cLocal = ct.InverseTransformPoint(center);
                Vector3 aLocal = ct.InverseTransformDirection(axis);

                // 빛 기둥
                AddQuad(cLocal, aLocal, new Vector4(len * 0.5f, wid * 0.5f, L.drift, L.fadeTime),
                        new Vector2(start, period), new Vector4(sd, 0f, gain, 0f));

                // 떠다니는 먼지: 기둥 안 아래쪽 2/3 에 흩뿌림
                for (int m = 0; m < L.motesPerShaft; m++)
                {
                    float along = Mathf.Lerp(0.35f, 0.95f, Hash01(i * 97 + m, cycle, 11));   // 0 위 .. 1 아래
                    float across = (Hash01(i * 97 + m, cycle, 12) - 0.5f) * 1.4f;             // -0.7 .. 0.7
                    float msz = Mathf.Lerp(L.moteSize.x, L.moteSize.y, Hash01(i * 97 + m, cycle, 13));
                    float ms = Hash01(i * 97 + m, cycle, 14);
                    AddQuad(cLocal, aLocal, new Vector4(len * 0.5f, wid * 0.5f, L.drift, L.fadeTime),
                            new Vector2(start, period), new Vector4(ms, 1f + msz, along, across));
                }
            }

            mesh.Clear();
            mesh.SetVertices(vCenter);
            mesh.SetNormals(vAxis);
            mesh.SetTangents(vSize);
            mesh.SetUVs(0, vCorner);
            mesh.SetUVs(1, vLife);
            mesh.SetUVs(2, vExtra);
            mesh.SetTriangles(tris, 0);

            // 셰이더가 정점을 옮기므로 경계는 넉넉히
            float r = Mathf.Max(area.x, area.y) * 0.5f + L.length.y + L.width.y + L.drift * L.lifetime.y + L.moteDrift;
            mesh.bounds = new Bounds(ct.InverseTransformPoint(transform.position), Vector3.one * r * 2f);
        }

        void AddQuad(Vector3 c, Vector3 a, Vector4 size, Vector2 life, Vector4 extra)
        {
            int b = vCenter.Count;
            for (int k = 0; k < 4; k++)
            {
                vCenter.Add(c); vAxis.Add(a); vSize.Add(size); vLife.Add(life); vExtra.Add(extra);
            }
            vCorner.Add(new Vector2(-1f, -1f));   // 위 (하늘 쪽)
            vCorner.Add(new Vector2( 1f, -1f));
            vCorner.Add(new Vector2( 1f,  1f));   // 아래 (지면 쪽)
            vCorner.Add(new Vector2(-1f,  1f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        float Hash01(int i, int cycle, int ch)
        {
            unchecked
            {
                uint h = (uint)i * 73856093u ^ (uint)cycle * 19349663u ^ (uint)ch * 83492791u ^ (uint)seed * 2654435761u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
