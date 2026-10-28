// RE:AL STEEL - 나노봇 리그 부위
//
// 리그(RSNanoRig) 아래 자식 오브젝트에 붙인다. 봇이 이 부위의 '격자 자리' 를 채운다.
// 아래 값은 전부 애니메이션 창에서 키를 잡을 수 있다 (양 · 결속 · 자라기 · 판정 · 발광 · 요동 …).
//
//   줄기     관절 체인 (이 오브젝트 → 첫째 자식 → 그 첫째 자식 …). 관절 사이는 부드러운 곡선.
//            관절마다 '나노봇 리그 관절' 컴포넌트로 두께 배율. '자라기' 0→1 = 뿌리부터 뻗어 나옴
//            '바닥까지 채움' = 파도처럼 관절선 아래를 바닥까지 채움
//   덩어리   타원 (크기 = size × 트랜스폼 스케일). 주먹 · 몸통 · 혹
//   웅덩이   바닥에 깔리는 층 (너비 × 안쪽 깊이). 물결 + '요동 지점' 에서 솟아오름
//   그림     픽셀아트 스프라이트 시트를 따라간다 (어두운 픽셀 = 봇 자리, 흰색 · 투명은 무시). 프레임이 바뀌면 봇이 흘러간다
//
// 양(amount) = 자리를 앞에서부터(뿌리 · 가운데부터) 채운 비율. 올리면 가까운 봇부터 흘러들고, 내리면 끝부터 풀려난다.
// 결속(bind) 1 = 촘촘한 기계 격자 / 0 = 유체. 0 → 1 로 올리는 순간 뿌리부터 '착착' 잠긴다.
//
// 느낌은 프레임(1/60초) 단위
//   반응      자리에 거의 도착하는 데 걸리는 프레임 (작을수록 날카롭다)
//   출렁임    0 = 딱 멈춤, 1 = 크게 튕겼다 자리 잡음
//   끝 늦음   끝이 몇 프레임 늦게 따라오나 (팔로스루 · 잔상). 줄기는 뿌리 0 → 끝 최대
//   모이는 시간  멀리서 이 부위로 흘러들어 올 때의 반응
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace RealSteel.NanoBoss
{
    public enum LimbType
    {
        [InspectorName("줄기 (관절 체인)")] Chain,
        [InspectorName("덩어리 (타원)")] Blob,
        [InspectorName("웅덩이 (바닥 층)")] Puddle,
        [InspectorName("그림 (스프라이트 시트 따라가기)")] Sprite,
    }

    public enum GatherOrder
    {
        [InspectorName("가까운 봇부터 바로")] Nearest,
        [InspectorName("뿌리부터 차례로")] Root,
        [InspectorName("무작위로 흩어져")] Random,
    }

    public struct NanoFeelPreset
    {
        public string name, tip;
        public float response, bounce, tipLag, gather;
        public NanoFeelPreset(string n, string t, float r, float b, float l, float g) { name = n; tip = t; response = r; bounce = b; tipLag = l; gather = g; }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/나노봇 리그 부위")]
    public class RSNanoLimb : MonoBehaviour
    {
        public static readonly NanoFeelPreset[] Presets =
        {
            new NanoFeelPreset("물렁", "웅덩이 · 촉수 · 파도. 늦게 따라오고 크게 출렁인다", 22f, 0.55f, 8f, 30f),
            new NanoFeelPreset("보통", "팔 · 덩어리. 무난하게 따라온다", 12f, 0.3f, 3f, 22f),
            new NanoFeelPreset("단단", "주먹 · 갑옷. 빠르고 거의 안 출렁인다", 6f, 0.12f, 0f, 16f),
            new NanoFeelPreset("칼같이", "가시 · 칼날. 즉시 자리 잡는다", 3f, 0f, 0f, 10f),
        };

        public const int SpineSamples = NanoSimJob.S;

        public LimbType type = LimbType.Chain;

        [Header("봇")]
        [Tooltip("이 부위의 격자 자리 수 = 양이 1 일 때 쓰는 봇 수. 인스펙터의 '크기에 맞게' 로 정하면 빈틈 없이 맞물린다")]
        [Min(1)] public int bots = 200;
        [Tooltip("자리를 앞(뿌리 · 가운데)에서부터 채운 비율 (키를 잡는다). 0 이면 봇을 다 풀어 준다")]
        [Range(0f, 1f)] public float amount = 1f;
        [Tooltip("모자란 봇을 이 부위에서 먼저 가져온다 — 가까운 것부터 (예: 웅덩이에서 솟는 가시). 가져간 만큼 그 부위는 줄어든다")]
        public RSNanoLimb feedFrom;

        [Header("판정 · 모습")]
        [Tooltip("켜져 있는 동안 이 부위 봇이 플레이어를 해친다 (키를 잡는다)")]
        public bool danger;
        [Tooltip("발광 (색은 tint). 예고에 쓴다")]
        [Min(0f)] public float glow;
        public Color tint = Color.white;

        [Header("결속")]
        [Tooltip("1 = 촘촘한 기계 격자 (판이 맞물림) · 0 = 유체 (흐르고 출렁임). 0 → 1 로 올리면 뿌리부터 착착 잠긴다 (키를 잡는다)")]
        [Range(0f, 1f)] public float bind = 1f;

        [Header("느낌 (프레임 = 1/60초)")]
        [Tooltip("반응: 자리에 거의 도착하는 데 걸리는 프레임. 작을수록 날카롭다")]
        [Range(1f, 90f)] public float response = 10f;
        [Tooltip("출렁임: 0 = 딱 멈춤, 1 = 크게 튕겼다 자리 잡음")]
        [Range(0f, 1f)] public float bounce = 0.25f;
        [Tooltip("끝 늦음: 끝이 몇 프레임 늦게 따라오나 (팔로스루 · 잔상). 줄기는 뿌리 0 → 끝 최대")]
        [Range(0f, 36f)] public float tipLag;
        [Tooltip("모이는 시간: 멀리서 이 부위로 흘러들어 올 때의 반응 (프레임)")]
        [Range(1f, 90f)] public float gather = 20f;
        [Tooltip("새로 모일 때 순서")]
        public GatherOrder gatherOrder = GatherOrder.Nearest;
        [Tooltip("흐름: 유체일 때 덩어리째 일렁이는 폭 (m). 기계(결속 1)일 땐 0")]
        [FormerlySerializedAs("noise")]
        [Range(0f, 0.6f)] public float flow = 0.15f;

        [Header("줄기")]
        [Tooltip("두께 (m). 관절마다 '나노봇 리그 관절' 의 두께 배율이 곱해진다")]
        public float radius = 0.45f;
        [Tooltip("뿌리부터 얼마나 뻗었나 (키를 잡는다)")]
        [Range(0f, 1f)] public float grow = 1f;
        [Tooltip("관절선 아래를 바닥까지 채운다 (파도)")]
        public bool fillToFloor;
        [Tooltip("앞뒤(z) 두께 배율")]
        public float depth = 0.6f;
        [Tooltip("관절 사이를 부드러운 곡선으로 잇는다 (끄면 직선)")]
        public bool smooth = true;

        [Header("덩어리")]
        [Tooltip("반지름 (m). 트랜스폼 스케일이 곱해진다")]
        public Vector3 size = new Vector3(1f, 1f, 0.6f);
        [Tooltip("출렁임")]
        [Range(0f, 0.4f)] public float wobble = 0.08f;

        [Header("웅덩이")]
        [Tooltip("x = 너비, y = 안쪽(z) 깊이 (m)")]
        public Vector2 extent = new Vector2(8f, 2.5f);
        [Tooltip("층 두께 (m)")]
        public float layer = 0.15f;
        [Tooltip("물결 높이 (m)")]
        public float ripple = 0.08f;
        public float rippleSpeed = 4f;
        [Tooltip("요동치다 솟는 지점 (보통 가시 뿌리)")]
        public Transform bumpAt;
        [Tooltip("솟는 높이 (m, 키를 잡는다)")]
        public float bump;
        [Tooltip("솟는 범위 반경 (m)")]
        public float bumpRadius = 1.2f;
        [Tooltip("요동 세기 (0~1)")]
        [Range(0f, 1f)] public float bumpShake = 0.5f;

        [Header("그림")]
        [Tooltip("구운 그림 모양 (부위 인스펙터에 스프라이트 시트를 넣으면 만들어진다)")]
        public RSNanoSpriteShape picture;
        [Tooltip("지금 프레임 (키를 잡는다). '초당 프레임' 이 0 일 때 쓴다")]
        [Min(0f)] public float frame;
        [Tooltip("초당 프레임. 0 보다 크면 패턴 시작부터 저절로 넘긴다 (픽셀 애니는 보통 8~12)")]
        [Min(0f)] public float fps = 12f;
        [Tooltip("끝 프레임 다음에 처음으로")]
        public bool loop = true;
        [Tooltip("그림 픽셀 하나의 크기 (m). 보통 군집 격자 간격과 같게")]
        [Min(0.01f)] public float pixelSize = 0.1f;

        // ── 예전 값 (10-27 판). 불러올 때 프레임 값으로 한 번 바꾼다 ─────────
        [SerializeField, HideInInspector] int feelVersion;
        [HideInInspector] public float stiffness = -1f, damping = -1f, maxSpeed = -1f, lag = -1f;
        [HideInInspector, Tooltip("관절에 '나노봇 리그 관절' 이 없을 때만 쓴다")]
        public AnimationCurve thickness = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);

        // ── 런타임 ───────────────────────────────────────────────────
        [NonSerialized] public int assigned;
        [NonSerialized] public int index;
        readonly List<Transform> joints = new List<Transform>();
        readonly List<RSNanoJoint> jointComp = new List<RSNanoJoint>();
        Vector3[] jp = Array.Empty<Vector3>();
        float[] jt = Array.Empty<float>();

        public float CurScale { get; private set; } = 1f;
        public int Desired => Mathf.RoundToInt(SlotTotal * Mathf.Clamp01(amount) * (isActiveAndEnabled ? 1f : 0f));
        /// <summary>자리 수 — 그림은 구운 봇 수, 나머지는 '자리 수 (봇)'</summary>
        public int SlotTotal => type == LimbType.Sprite ? (picture && picture.Ready ? picture.botCount : 0) : bots;
        public IReadOnlyList<Transform> Joints => joints;
        public int LayoutKey => (int)type * 7919 + SlotTotal * 31 + (fillToFloor ? 1 : 0) + joints.Count * 131 +
                                (type == LimbType.Sprite && picture ? picture.GetInstanceID() * 17 + (picture.bakedKey != null ? picture.bakedKey.GetHashCode() : 0) : 0);

        /// <summary>그림: 시각 clock 의 프레임 번호</summary>
        public int SpriteFrame(float clock)
        {
            if (!picture || !picture.Ready) return 0;
            int n = picture.frameCount;
            int f = fps > 0f ? Mathf.FloorToInt(clock * fps + 1e-4f) : Mathf.FloorToInt(frame + 1e-4f);
            return loop ? ((f % n) + n) % n : Mathf.Clamp(f, 0, n - 1);
        }

        /// <summary>그림: 프레임 f 의 자리 (부위 좌표, m) 를 dst[offset..] 의 xyz 에 쓴다 (w = 채움 순서는 그대로)</summary>
        public void WriteSpriteSlots(float4[] dst, int offset, int f)
        {
            if (!picture || !picture.Ready) return;
            int n = picture.botCount;
            for (int k = 0; k < n; k++)
            {
                Vector3 p = picture.Local(f, k, pixelSize);
                dst[offset + k] = new float4(p.x, p.y, p.z, dst[offset + k].w);
            }
        }

        void Reset() { feelVersion = 2; }
        void Awake() { Migrate(); }
        void OnValidate() { Migrate(); }

        /// <summary>예전 값(스프링 세기 · 감쇠 · 잔상 초)을 프레임 값으로 한 번 바꾼다</summary>
        public void Migrate()
        {
            if (feelVersion >= 2) return;
            if (stiffness > 0f && damping >= 0f)
            {
                NanoSpring.FromStiffness(stiffness, damping, out float om, out float ze);
                response = Mathf.Clamp(NanoSpring.SettleK / om * 60f, 1f, 90f);
                bounce = Mathf.Clamp01((1f - ze) / 0.8f);
            }
            if (lag >= 0f) tipLag = Mathf.Clamp(lag * 60f, 0f, 36f);
            feelVersion = 2;
        }

        public void ApplyPreset(NanoFeelPreset p)
        {
            response = p.response; bounce = p.bounce; tipLag = p.tipLag; gather = p.gather;
        }

        /// <summary>잡에 넘길 느낌 (고유진동수 · 감쇠비 · 모일 때 진동수 · 끝 늦음 초)</summary>
        public void GetFeel(out float omega, out float zeta, out float gatherOmega, out float lagSec)
        {
            Migrate();
            NanoSpring.FromFrames(response, bounce, out omega, out zeta);
            NanoSpring.FromFrames(gather, 0f, out gatherOmega, out _);
            lagSec = tipLag / 60f;
        }

        // ─────────────────────────────────────────────────────────────
        // 관절 · 관절선
        // ─────────────────────────────────────────────────────────────
        public void RefreshJoints()
        {
            joints.Clear();
            jointComp.Clear();
            joints.Add(transform);
            jointComp.Add(GetComponent<RSNanoJoint>());
            if (type != LimbType.Chain) return;
            var t = transform;
            int guard = 0;
            while (t.childCount > 0 && guard++ < 32)
            {
                Transform next = null;
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (!c.GetComponent<RSNanoLimb>() && !c.GetComponent<RSNanoEmitter>()) { next = c; break; }   // 다른 부위는 관절이 아니다
                }
                if (!next) break;
                joints.Add(next);
                jointComp.Add(next.GetComponent<RSNanoJoint>());
                t = next;
            }
        }

        /// <summary>지금 관절 위치 · 두께를 읽어 둔다 (매 프레임)</summary>
        public void CachePose()
        {
            if (joints.Count == 0) RefreshJoints();
            int n = joints.Count;
            if (jp.Length != n) { jp = new Vector3[n]; jt = new float[n]; }
            for (int j = 0; j < n; j++)
            {
                jp[j] = joints[j] ? joints[j].position : transform.position;
                float u = n > 1 ? j / (float)(n - 1) : 0f;
                jt[j] = jointComp[j] ? Mathf.Max(0f, jointComp[j].thickness) : Mathf.Max(0f, thickness.Evaluate(u));
            }
            Vector3 ls = transform.lossyScale;
            CurScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
        }

        public float JointThickness(int j) => j >= 0 && j < jt.Length ? jt[j] : 1f;
        public bool HasJointComponent(int j) => j >= 0 && j < jointComp.Count && jointComp[j];

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>관절선 위 u (0 뿌리 ~ 1 끝) 의 위치와 반지름 (m). CachePose 뒤에 부른다</summary>
        public void Sample(float u, out Vector3 p, out float r)
        {
            int n = jp.Length;
            if (n == 0) { p = transform.position; r = radius * CurScale; return; }
            if (n == 1) { p = jp[0]; r = radius * CurScale * jt[0]; return; }
            float f = Mathf.Clamp01(u) * (n - 1);
            int i = Mathf.Min((int)f, n - 2);
            float t = f - i;
            Vector3 p1 = jp[i], p2 = jp[i + 1];
            if (smooth && n > 2)
            {
                Vector3 p0 = i > 0 ? jp[i - 1] : 2f * p1 - p2;
                Vector3 p3 = i + 2 < n ? jp[i + 2] : 2f * p2 - p1;
                p = CatmullRom(p0, p1, p2, p3, t);
            }
            else p = Vector3.LerpUnclamped(p1, p2, t);
            r = radius * CurScale * Mathf.Lerp(jt[i], jt[i + 1], t);
        }

        /// <summary>관절선 표본 (위치 + 반지름) SpineSamples 개를 dst[offset..] 에 쓴다. 덩어리 · 웅덩이는 원점</summary>
        public void WriteSpine(float4[] dst, int offset)
        {
            if (type != LimbType.Chain)
            {
                Vector3 o = transform.position;
                for (int s = 0; s < SpineSamples; s++) dst[offset + s] = new float4(o.x, o.y, o.z, 0f);
                return;
            }
            for (int s = 0; s < SpineSamples; s++)
            {
                Sample(s / (float)(SpineSamples - 1), out Vector3 p, out float r);
                dst[offset + s] = new float4(p.x, p.y, p.z, r);
            }
        }

        /// <summary>관절선 길이 (m)</summary>
        public float ChainLength()
        {
            float len = 0f;
            Sample(0f, out Vector3 prev, out _);
            for (int s = 1; s <= 24; s++)
            {
                Sample(s / 24f, out Vector3 p, out _);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            return len;
        }

        // ─────────────────────────────────────────────────────────────
        // 격자 자리
        // ─────────────────────────────────────────────────────────────

        /// <summary>격자 자리표 (채우는 순서대로). space = 자리 간격 (m)</summary>
        public Vector4[] BuildSlots(float floorY, out float space)
        {
            CachePose();
            int n = Mathf.Max(1, bots);
            switch (type)
            {
                case LimbType.Chain:
                {
                    var w = new float[16];
                    for (int k = 0; k < w.Length; k++)
                    {
                        Sample(k / (w.Length - 1f), out Vector3 p, out float r);
                        w[k] = fillToFloor ? Mathf.Max(2f * r, p.y + r - floorY) : 2f * r;
                    }
                    return NanoLattice.Chain(n, ChainLength(), w, fillToFloor, out space);
                }
                case LimbType.Blob:
                {
                    Vector3 ls = transform.lossyScale;
                    return NanoLattice.Ellipse(n, new Vector2(size.x * ls.x, size.y * ls.y), out space);
                }
                case LimbType.Sprite:
                {
                    int m = Mathf.Max(1, SlotTotal);
                    var res = new Vector4[m];
                    for (int k = 0; k < m; k++)
                    {
                        Vector3 p = picture && picture.Ready ? picture.Local(0, k, pixelSize) : Vector3.zero;
                        res[k] = new Vector4(p.x, p.y, p.z, k / (float)m);
                    }
                    space = pixelSize * CurScale;
                    return res;
                }
                default:
                    return NanoLattice.Puddle(n, extent, out space);
            }
        }

        /// <summary>격자 간격 spacing 으로 이 부위를 빈틈 없이 채우는 봇 수 (지금 모양 기준)</summary>
        public int EstimateBots(float spacing, float floorY)
        {
            if (type == LimbType.Sprite) return Mathf.Max(1, SlotTotal);   // 그림: 칠한 픽셀 수
            CachePose();
            float cell = 0.866f * spacing * spacing;
            float area;
            switch (type)
            {
                case LimbType.Chain:
                {
                    area = 0f;
                    Sample(0f, out Vector3 prev, out float r0);
                    float wPrev = fillToFloor ? Mathf.Max(2f * r0, prev.y + r0 - floorY) : 2f * r0;
                    for (int s = 1; s <= 24; s++)
                    {
                        Sample(s / 24f, out Vector3 p, out float r);
                        float w = fillToFloor ? Mathf.Max(2f * r, p.y + r - floorY) : 2f * r;
                        area += Vector3.Distance(prev, p) * (w + wPrev) * 0.5f;
                        prev = p; wPrev = w;
                    }
                    break;
                }
                case LimbType.Blob:
                {
                    Vector3 ls = transform.lossyScale;
                    area = Mathf.PI * Mathf.Abs(size.x * ls.x * size.y * ls.y);
                    break;
                }
                default:
                    area = extent.x * extent.y * 0.35f;   // 안쪽(z)은 덜 촘촘하게
                    break;
            }
            return Mathf.Clamp(Mathf.RoundToInt(area / Mathf.Max(1e-5f, cell)), 4, 8000);
        }
    }
}
