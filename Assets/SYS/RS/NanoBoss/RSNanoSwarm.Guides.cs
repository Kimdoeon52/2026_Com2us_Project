// RE:AL STEEL - 나노봇 군집 가이드 (절차적 애니메이션의 틀)
//
// 보스에는 고정된 몸이 없다. 대신 '가이드' — 봇들이 붙을 자리를 정하는 보이지 않는 틀 — 를 코드로 움직인다.
// 봇은 맡은 가이드의 자리를 스프링으로 쫓아가고, 패턴은 봇을 가이드 사이로 옮겨 담는다.
// 봇이 덩어리에서 줄기로 '흘러 들어가는' 모습이 곧 공격 예고가 된다.
//
//   Blob    덩어리 — 출렁이는 타원체. 대기 · 이동 · 모든 패턴의 출발점 (1 · 2페)
//   Tube    줄기 — 곡선 + 두께. 가시 찌르기 / 사슬 물리로 휘두르기
//   Ring    포신 — 회전하는 고리. 빔 모으기
//   Knight  기사 — 3페의 압축된 검은 실루엣 (캡슐 조각 + 포즈)
//   Halo    후광 — 3페 기사 뒤를 도는 탄약 무리
//
// 좌표: 횡스크롤 평면 = XY (z = 0 이 싸우는 면, +z = 화면 안쪽)
//
// 10-28: 가이드는 '값' 만 갖는다. 봇의 목표 자리 계산은 Burst 잡(RSNanoSim.NanoSimJob)이 이 값을 받아서 한다.
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public abstract class SwarmGuide
    {
        public string name;
        public float stiffness = 60f;
        public float damping = 10f;
        public float maxSpeed = 25f;
        public float noise = 0.03f;       // 살아 있는 떨림 (m)
        public float noiseSpeed = 2f;
        public float glow;                // 이 가이드에 붙은 봇의 추가 발광
        public Color32 tint = new Color32(255, 255, 255, 255);
        public bool danger;               // 붙은 봇이 플레이어를 해치는가
        [Range(0f, 1f)]
        public float bind = 1f;           // 결속: 1 = 기계 격자 / 0 = 유체 (떨림 · 끈적임 · 늘이기)
    }

    // ─────────────────────────────────────────────────────────────
    // 덩어리
    // ─────────────────────────────────────────────────────────────
    public sealed class BlobGuide : SwarmGuide
    {
        public Vector3 center;
        public Vector3 radius = new Vector3(3.2f, 2.6f, 0.9f);
        public float wobble = 0.08f;          // 윤곽 출렁임
        public float wobbleSpeed = 1.6f;
        public float flow = 0.22f;            // 안쪽 흐름 (봇이 덩어리 안을 떠돈다)
        public float flowSpeed = 0.8f;
        public Vector3 bulgeDir = Vector3.right;
        public float bulge;                   // 한쪽으로 쏠림 (예고)
        public float floorY;

        public float aspect = 1.26f;          // 가로 ÷ 세로 (격자 자리표를 이 비율로 만든다)

        public BlobGuide() { name = "덩어리"; stiffness = 26f; damping = 6.5f; maxSpeed = 18f; noise = 0.05f; bind = 1f; }
    }

    // ─────────────────────────────────────────────────────────────
    // 줄기 (가시 · 채찍)
    // ─────────────────────────────────────────────────────────────
    public sealed class TubeGuide : SwarmGuide
    {
        public const int K = 16;
        public readonly Vector3[] nodes = new Vector3[K];
        public float r0 = 0.5f, r1 = 0.05f;   // 뿌리 · 끝 두께

        // 사슬 (휘두르기) — 마디 각도가 앞 마디를 늦게 따라가며 채찍처럼 휜다
        readonly float[] ang = new float[K - 1];
        readonly float[] angVel = new float[K - 1];

        public TubeGuide(string n) { name = n; stiffness = 420f; damping = 28f; maxSpeed = 70f; noise = 0.02f; }

        /// <summary>a → b 직선. ext (0~1) 만큼만 뻗는다 (0 이면 전부 a 에 모임)</summary>
        public void SetLine(Vector3 a, Vector3 b, float ext = 1f)
        {
            Vector3 e = a + (b - a) * ext;
            for (int i = 0; i < K; i++) nodes[i] = Vector3.LerpUnclamped(a, e, i / (float)(K - 1));
        }

        /// <summary>a → c → b 로 꺾인 경로 (바닥을 기다가 솟기 등)</summary>
        public void SetPath(Vector3 a, Vector3 c, Vector3 b)
        {
            float l1 = Vector3.Distance(a, c), l2 = Vector3.Distance(c, b), L = Mathf.Max(1e-4f, l1 + l2);
            for (int i = 0; i < K; i++)
            {
                float s = i / (float)(K - 1) * L;
                nodes[i] = s <= l1 ? Vector3.Lerp(a, c, l1 > 0f ? s / l1 : 0f) : Vector3.Lerp(c, b, l2 > 0f ? (s - l1) / l2 : 0f);
            }
        }

        public void ResetChain(float angleDeg)
        {
            for (int i = 0; i < K - 1; i++) { ang[i] = angleDeg * Mathf.Deg2Rad; angVel[i] = 0f; }
        }

        /// <summary>
        /// 사슬 한 단계. 첫 마디는 baseAngle 을 강하게, 나머지는 앞 마디를 점점 약하게 따라간다 → 끝이 늦게 휘감긴다
        /// </summary>
        public void ChainStep(Vector3 root, float baseAngleDeg, float length, float dt, float stiff = 260f)
        {
            float target = baseAngleDeg * Mathf.Deg2Rad;
            for (int i = 0; i < K - 1; i++)
            {
                float goal = i == 0 ? target : ang[i - 1];
                float k = i == 0 ? stiff * 1.6f : stiff * (1f - 0.75f * i / (K - 2f));
                float c = 2f * Mathf.Sqrt(k) * 0.55f;
                float d = Mathf.DeltaAngle(ang[i] * Mathf.Rad2Deg, goal * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                angVel[i] += (d * k - angVel[i] * c) * dt;
                ang[i] += angVel[i] * dt;
            }
            float seg = length / (K - 1);
            nodes[0] = root;
            for (int i = 0; i < K - 1; i++)
                nodes[i + 1] = nodes[i] + new Vector3(Mathf.Cos(ang[i]), Mathf.Sin(ang[i]), 0f) * seg;
        }

        public Vector3 Tip => nodes[K - 1];
    }

    // ─────────────────────────────────────────────────────────────
    // 포신 (빔)
    // ─────────────────────────────────────────────────────────────
    public sealed class RingGuide : SwarmGuide
    {
        public Vector3 center;
        public Vector3 axis = Vector3.right;
        public float radius = 1.4f, length = 0.9f, angle;

        public RingGuide() { name = "포신"; stiffness = 300f; damping = 22f; maxSpeed = 60f; noise = 0.01f; bind = 0.5f; }
    }

    // ─────────────────────────────────────────────────────────────
    // 기사 (3페)
    // ─────────────────────────────────────────────────────────────
    public sealed class KnightGuide : SwarmGuide
    {
        enum Bone { Body, Arm, Blade }

        struct Seg
        {
            public Vector2 a, b;
            public float ra, rb;
            public Bone bone;
            public Seg(float ax, float ay, float bx, float by, float ra, float rb, Bone bone)
            { a = new Vector2(ax, ay); b = new Vector2(bx, by); this.ra = ra; this.rb = rb; this.bone = bone; }
        }

        // 오른쪽을 보는 자세, 원점 = 떠 있는 발밑. 키 약 5m
        static readonly Seg[] Segs =
        {
            new Seg( 0.00f, 0.25f,  0.00f, 2.30f, 0.95f, 0.55f, Bone.Body),   // 0 망토
            new Seg( 0.00f, 0.30f, -0.55f,-0.35f, 0.45f, 0.00f, Bone.Body),   // 1 망토 자락 (뾰족)
            new Seg( 0.00f, 2.20f,  0.05f, 3.50f, 0.60f, 0.75f, Bone.Body),   // 2 몸통
            new Seg(-0.95f, 3.45f,  0.95f, 3.45f, 0.32f, 0.32f, Bone.Body),   // 3 어깨
            new Seg(-0.95f, 3.55f, -1.50f, 4.30f, 0.24f, 0.00f, Bone.Body),   // 4 어깨 가시 (뒤)
            new Seg( 0.95f, 3.55f,  1.45f, 4.25f, 0.22f, 0.00f, Bone.Body),   // 5 어깨 가시 (앞)
            new Seg( 0.05f, 3.75f,  0.08f, 4.35f, 0.30f, 0.26f, Bone.Body),   // 6 머리
            new Seg( 0.05f, 4.30f, -0.40f, 5.05f, 0.15f, 0.00f, Bone.Body),   // 7 뿔
            new Seg(-0.95f, 3.40f, -1.15f, 2.10f, 0.18f, 0.10f, Bone.Body),   // 8 반대 팔
            new Seg( 0.95f, 3.40f,  1.25f, 2.55f, 0.20f, 0.17f, Bone.Arm),    // 9 칼 팔 위
            new Seg( 1.25f, 2.55f,  1.55f, 1.85f, 0.17f, 0.15f, Bone.Arm),    // 10 칼 팔 아래
            new Seg( 0.00f, 0.00f,  0.00f, 0.00f, 0.15f, 0.00f, Bone.Blade),  // 11 칼날 (손에서 뻗음)
        };
        public const int BladeSeg = 11;
        const float BladeLength = 3.6f;
        static readonly Vector2 Pivot = new Vector2(0f, 1.6f);
        static readonly Vector2 Shoulder = new Vector2(0.95f, 3.4f);
        static readonly Vector2 Hand = new Vector2(1.55f, 1.85f);
        static readonly Vector2 HeadCenter = new Vector2(0.08f, 4.05f);

        public Vector3 origin;
        public float facing = 1f;        // +1 오른쪽, -1 왼쪽
        public float lean;               // 몸 기울기 (도, + = 뒤로 젖힘)
        public float armAngle;           // 칼 팔 회전 (도, + = 들어 올림)
        public float bladeAngle;         // 칼날 꺾임 (도)
        public Vector3 lookAt;
        public bool bladeDanger;

        readonly Vector2[] wa = new Vector2[Segs.Length], wb = new Vector2[Segs.Length];
        Vector2 head;

        // 조각별 면적 누적 비율 — 봇을 면적에 비례해 나눠 준다
        static readonly float[] Cum = BuildCum();
        static float[] BuildCum()
        {
            var cum = new float[Segs.Length];
            float total = 0f;
            for (int i = 0; i < Segs.Length; i++)
            {
                float len = i == BladeSeg ? BladeLength : Vector2.Distance(Segs[i].a, Segs[i].b);
                total += len * (Segs[i].ra + Segs[i].rb);
                cum[i] = total;
            }
            for (int i = 0; i < cum.Length; i++) cum[i] /= total;
            return cum;
        }

        public KnightGuide()
        {
            name = "기사"; stiffness = 900f; damping = 55f; maxSpeed = 95f; noise = 0.004f; bind = 1f;
            UpdatePose();
        }

        static Vector2 Rot(Vector2 p, Vector2 c, float deg)
        {
            float r = deg * Mathf.Deg2Rad, s = Mathf.Sin(r), co = Mathf.Cos(r);
            Vector2 d = p - c;
            return c + new Vector2(d.x * co - d.y * s, d.x * s + d.y * co);
        }

        /// <summary>포즈 값을 조각 끝점에 반영한다. 매 프레임 한 번</summary>
        public void UpdatePose()
        {
            Vector2 sh = Rot(Shoulder, Pivot, lean);
            for (int i = 0; i < Segs.Length; i++)
            {
                var s = Segs[i];
                if (s.bone == Bone.Body) { wa[i] = Rot(s.a, Pivot, lean); wb[i] = Rot(s.b, Pivot, lean); }
                else if (s.bone == Bone.Arm)
                {
                    wa[i] = sh + (Vector2)(Quaternion.Euler(0, 0, lean + armAngle) * (s.a - Shoulder));
                    wb[i] = sh + (Vector2)(Quaternion.Euler(0, 0, lean + armAngle) * (s.b - Shoulder));
                }
            }
            Vector2 hand = sh + (Vector2)(Quaternion.Euler(0, 0, lean + armAngle) * (Hand - Shoulder));
            Vector2 fore = (hand - wa[10]).normalized;      // 팔꿈치 → 손
            Vector2 dir = Quaternion.Euler(0, 0, bladeAngle) * fore;
            wa[BladeSeg] = hand;
            wb[BladeSeg] = hand + dir * BladeLength;
            head = Rot(HeadCenter, Pivot, lean);
        }

        Vector3 ToWorld(Vector2 p, float z) => origin + new Vector3(p.x * facing, p.y, z);

        public Vector3 HeadWorld => ToWorld(head, 0f);
        public Vector3 ChestWorld => ToWorld(Rot(new Vector2(0.05f, 3.0f), Pivot, lean), 0f);
        public Vector3 BladeTipWorld => ToWorld(wb[BladeSeg], 0f);
        public Vector3 HandWorld => ToWorld(wa[BladeSeg], 0f);

        /// <summary>봇에게 기사 몸의 자리를 배정한다 (면적에 비례해 고르게)</summary>
        public static void Sample(ref NanoBot b, float r1, float r2, float r3, float r4)
        {
            int seg = 0;
            while (seg < Segs.Length - 1 && r1 > Cum[seg]) seg++;
            b.seg = seg;
            b.segU = r2;
            float ang = r3 * 6.2832f, rad = Mathf.Sqrt(r4);
            b.segV = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
        }

        // ── 잡으로 넘기는 값 ──
        public int SegCount => Segs.Length;
        /// <summary>조각 끝점 (기사 좌표, 오른쪽을 볼 때): a.xy, b.xy</summary>
        public Vector4 SegWorld(int i) => new Vector4(wa[i].x, wa[i].y, wb[i].x, wb[i].y);
        public static Vector2 SegRadius(int i) => new Vector2(Segs[i].ra, Segs[i].rb);

        /// <summary>몸 면적 (m²) — 격자 간격으로 기사 봇 수를 정한다</summary>
        public static float BodyArea
        {
            get
            {
                float a = 0f;
                for (int i = 0; i < Segs.Length; i++)
                {
                    float len = i == BladeSeg ? BladeLength : Vector2.Distance(Segs[i].a, Segs[i].b);
                    a += len * (Segs[i].ra + Segs[i].rb);
                }
                return a;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 후광 (3페 탄약)
    // ─────────────────────────────────────────────────────────────
    public sealed class HaloGuide : SwarmGuide
    {
        public Vector3 center;
        public float radius = 2.8f;
        public float spin = 0.35f;

        public HaloGuide() { name = "후광"; stiffness = 40f; damping = 8f; maxSpeed = 40f; noise = 0.06f; bind = 0.2f; }
    }
}
