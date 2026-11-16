using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>접촉 이펙트가 나온 순간의 정보 — 소리 등을 붙일 때 KyodaiContacts.OnImpact로 받는다</summary>
public struct KyodaiContactHit
{
    public Vector3 point;    // 닿은 자리(표면 위)
    public Vector3 normal;   // 표면에서 바깥쪽
    public Vector3 velocity; // 닿을 때 접촉점 속도(월드/초)
    public float strength;   // 0~1 (최소 속도 → 최대 속도)
}

/// <summary>
/// 쿄다이 몸의 접촉점 — 바닥·물체를 못 뚫게 하고, 닿으면 속도·방향에 맞춰 먼지를 낸다 (2026-11-04).
///
///   · 접촉점: 뼈 + 오프셋(뼈 방향 기준, 월드 단위) + 반지름. 팔이 커지면(Grow) 같이 커진다
///   · 못 뚫게(solid): 애니메이션·IK 뒤에 접촉점이 바닥(경기장 평면 근처의 groundY)이나 물체 충돌체 안이면
///     그 부위 손목을 밀어낸다(바닥은 화면에 보이는 높이 기준) — 팔꿈치·어깨가 대신 꺾인다(KyodaiLimb.PushHand). 모션 창 편집 미리보기도 같은 계산
///   · 이펙트(effects): 처음 닿는 순간 = 충격 먼지(빠를수록 크게) + 화면 흔들림, 닿은 채 미끄러지면 = 끌리는 먼지
///   · 피격 판정은 여기서 하지 않는다 — 타이밍이 매번 같아야 해서 모션 타임라인 이벤트로 한다
/// 이펙트 프리팹 방향: +Y = 표면 바깥쪽, +Z = 미끄러지는 방향(충격이면 들어온 방향의 수평 성분).
/// </summary>
[DefaultExecutionOrder(1000)] // KyodaiRig(애니메이션 → IK)가 자세를 다 잡은 뒤
public class KyodaiContacts : MonoBehaviour
{
    [Serializable]
    public class Point
    {
        [Tooltip("밀어낼 팔(KyodaiLimb 부위 이름). 비우면 밀지 않고 이펙트만")]
        public string partId = "오른팔";
        [Tooltip("붙일 뼈 이름")]
        public string bone = "RightHand";
        [Tooltip("뼈 위치에서 접촉점까지(뼈 방향 기준, 월드 단위). Scene 뷰에서 화살표로 끌어도 됨")]
        public Vector3 offset;
        [Tooltip("접촉 구 반지름(월드 단위, 기본 크기 기준)")]
        [Min(0.01f)] public float radius = 0.5f;
        [Tooltip("바닥·물체를 못 뚫게 (부위 이름이 있어야 함)")]
        public bool solid = true;
        [Tooltip("닿으면 먼지·흔들림")]
        public bool effects = true;
    }

    [SerializeField] private KyodaiBossController3D boss;
    [SerializeField] private List<Point> points = new List<Point>();

    [Header("바닥·물체")]
    [Tooltip("경기장 평면(PlayZ)에서 앞뒤로 이 거리 안에 있을 때만 바닥(groundY)으로 친다 — 뒤에 서 있는 몸은 바닥에 안 걸리게")]
    [Min(0f)] [SerializeField] private float groundDepth = 4f;

    [Tooltip("못 뚫고 먼지가 나는 물체의 레이어 (3D 충돌체 — 박스·구·캡슐·볼록 메쉬만). 비우면 바닥만")]
    [SerializeField] private LayerMask obstacleMask;

    [Tooltip("이만큼 가까우면 닿은 것으로 친다(월드)")]
    [Min(0f)] [SerializeField] private float skin = 0.05f;

    [Header("충격 먼지 (임시값 — 보면서 조절)")]
    [SerializeField] private GameObject impactPrefab;
    [Tooltip("표면으로 들어오는 속도가 이보다 느리면 안 나온다(월드/초)")]
    [Min(0f)] [SerializeField] private float minImpactSpeed = 3f;
    [Tooltip("이 속도에서 가장 크게(월드/초)")]
    [Min(0.01f)] [SerializeField] private float fullImpactSpeed = 25f;
    [Tooltip("이펙트 크기 배율 (가장 약할 때, 가장 셀 때)")]
    [SerializeField] private Vector2 impactScale = new Vector2(0.6f, 2f);
    [Tooltip("가장 셀 때 화면 흔들림(월드). 0이면 안 흔듦")]
    [Min(0f)] [SerializeField] private float maxShake = 0.35f;
    [Min(0f)] [SerializeField] private float shakeSeconds = 0.25f;

    [Header("끌리는 먼지 (임시값)")]
    [SerializeField] private GameObject scrapePrefab;
    [Tooltip("닿은 채 표면을 따라 이보다 빠르면 나온다(월드/초)")]
    [Min(0f)] [SerializeField] private float minScrapeSpeed = 2f;
    [Tooltip("끌리는 먼지 간격(초)")]
    [Min(0.01f)] [SerializeField] private float scrapeInterval = 0.06f;

    [Tooltip("이펙트 오브젝트를 지우기까지(초)")]
    [Min(0.1f)] [SerializeField] private float effectLifetime = 2f;

    [SerializeField] private bool showGizmos = true;

    public event Action<KyodaiContactHit> OnImpact;

    public IReadOnlyList<Point> Points => points;
    public float GroundDepth => groundDepth;
    public LayerMask ObstacleMask => obstacleMask;
    public KyodaiBossController3D Boss => boss != null ? boss : boss = GetComponentInParent<KyodaiBossController3D>();

    private readonly Dictionary<string, Transform> boneCache = new Dictionary<string, Transform>();
    private Transform[] pointBones;
    private float[] restScale;
    private Vector3[] prev;
    private bool[] touching;
    private float[] scrapeTimer;
    private bool hasPrev;

    /// <summary>쿄다이 밑에서 이름으로 뼈 찾기 (편집 중에도 됨)</summary>
    public Transform FindBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName) || Boss == null) return null;
        if (boneCache.TryGetValue(boneName, out var t) && t != null) return t;
        boneCache.Clear();
        foreach (var c in Boss.GetComponentsInChildren<Transform>(true))
            if (!boneCache.ContainsKey(c.name)) boneCache[c.name] = c;
        return boneCache.TryGetValue(boneName, out t) ? t : null;
    }

    /// <summary>접촉점 월드 위치. scale = 기본 크기 대비 지금 크기(팔이 커지면 같이)</summary>
    public static Vector3 PointWorld(Transform bone, Vector3 offset, float scale) => bone.position + bone.rotation * (offset * scale);

    /// <summary>
    /// 화면 기준 바닥: 3D 점 p가 경기장 평면에 겹쳐 보이는 자리(seen)와, 그 깊이에서 경기장 평면 1만큼이 실제로 몇인지(ratio).
    /// 경기장 바닥은 2D라서 뒤에 있는 손도 "보이는 높이"로 바닥에 닿아야 한다 (KyodaiBossController3D.ToPlayPlane과 같은 기준)
    /// </summary>
    public static Vector2 SeenOnPlane(KyodaiBossController3D boss, Vector3 p, out float ratio)
    {
        Vector2 seen = boss.ToPlayPlane(p);
        Camera cam = boss.TargetCamera;
        ratio = 1f;
        if (cam != null && !cam.orthographic)
        {
            Vector3 eye = cam.transform.position;
            float planeDist = (new Vector3(seen.x, seen.y, boss.PlayZ) - eye).magnitude;
            if (planeDist > 1e-4f) ratio = (p - eye).magnitude / planeDist;
        }
        return seen;
    }

    /// <summary>
    /// 구(p, r)를 바닥·물체 밖으로 꺼낼 이동량. 안 겹치면 0.
    /// 바닥 = 화면에서 groundY 선 — 경기장 평면(PlayZ) ± depth 안에 있는 점만 친다(뒤에 서 있는 몸은 안 걸리게).
    /// 물체 = mask 레이어의 3D 충돌체
    /// </summary>
    public static Vector3 Penetration(KyodaiBossController3D boss, Vector3 p, float r, float depth, int mask)
    {
        Vector3 push = Vector3.zero;
        if (Mathf.Abs(p.z - boss.PlayZ) <= depth)
        {
            Vector2 seen = SeenOnPlane(boss, p, out float ratio);
            float d = boss.GroundY - (seen.y - r / ratio); // 화면 기준 파고든 깊이
            if (d > 0f) push.y = d * ratio;                // 그 깊이(실제)만큼 올린다
        }
        if (mask != 0)
        {
            Vector3 q = p + push;
            foreach (var col in Physics.OverlapSphere(q, r, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 cp = col.ClosestPoint(q);
                Vector3 away = q - cp;
                float dist = away.magnitude;
                if (dist < 1e-5f || dist >= r) continue; // 중심이 안에 들어갔으면 방향을 몰라서 넘긴다
                Vector3 step = away / dist * (r - dist);
                push += step;
                q += step;
            }
        }
        return push;
    }

    /// <summary>구(p, r)가 표면에 닿아 있나 (skin만큼 여유). 닿은 자리·바깥쪽 방향. 바닥이면 닿은 자리 = 화면에 보이는 바닥 위(경기장 평면)</summary>
    public bool Touching(Vector3 p, float r, out Vector3 point, out Vector3 normal)
    {
        var b = Boss;
        if (b != null && Mathf.Abs(p.z - b.PlayZ) <= groundDepth)
        {
            Vector2 seen = SeenOnPlane(b, p, out float ratio);
            if (seen.y - r / ratio <= b.GroundY + skin)
            {
                point = new Vector3(seen.x, b.GroundY, b.PlayZ);
                normal = Vector3.up;
                return true;
            }
        }
        if (obstacleMask.value != 0)
        {
            foreach (var col in Physics.OverlapSphere(p, r + skin, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 cp = col.ClosestPoint(p);
                Vector3 away = p - cp;
                point = cp;
                normal = away.sqrMagnitude > 1e-10f ? away.normalized : Vector3.up;
                return true;
            }
        }
        point = p;
        normal = Vector3.up;
        return false;
    }

    private void Start()
    {
        if (Boss == null) { Debug.LogError("[KyodaiContacts] KyodaiBossController3D 밑에 있어야 함"); enabled = false; return; }
        pointBones = new Transform[points.Count];
        restScale = new float[points.Count];
        prev = new Vector3[points.Count];
        touching = new bool[points.Count];
        scrapeTimer = new float[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            pointBones[i] = FindBone(points[i].bone);
            if (pointBones[i] == null) Debug.LogWarning($"[KyodaiContacts] 뼈 '{points[i].bone}'를 못 찾음 — 이 접촉점은 꺼짐");
            else restScale[i] = Mathf.Max(pointBones[i].lossyScale.x, 1e-6f);
        }
    }

    private float ScaleOf(int i) => pointBones[i].lossyScale.x / restScale[i];

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || pointBones == null) return;

        // 1) 못 뚫게 — 손목을 밀어내고 팔을 다시 꺾는다
        for (int i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            if (!pt.solid || pointBones[i] == null || string.IsNullOrEmpty(pt.partId)) continue;
            KyodaiLimb limb = boss.FindLimb(pt.partId);
            if (limb == null || !limb.Ready) continue;
            float s = ScaleOf(i);
            Vector3 push = Penetration(boss, PointWorld(pointBones[i], pt.offset, s), pt.radius * s, groundDepth, obstacleMask.value);
            limb.PushHand(push);
        }

        // 2) 이펙트 — 밀어낸 뒤의 자리로
        for (int i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            if (pointBones[i] == null) continue;
            float s = ScaleOf(i);
            Vector3 p = PointWorld(pointBones[i], pt.offset, s);
            if (!hasPrev || !pt.effects) { prev[i] = p; touching[i] = false; continue; }

            Vector3 velocity = (p - prev[i]) / dt;
            prev[i] = p;
            bool now = Touching(p, pt.radius * s, out Vector3 point, out Vector3 normal);
            Vector3 tangent = velocity - normal * Vector3.Dot(velocity, normal);

            if (now && !touching[i])
            {
                float into = -Vector3.Dot(velocity, normal);
                if (into >= minImpactSpeed) Impact(point, normal, velocity, tangent, into);
                scrapeTimer[i] = scrapeInterval;
            }
            else if (now && tangent.magnitude >= minScrapeSpeed)
            {
                scrapeTimer[i] -= dt;
                if (scrapeTimer[i] <= 0f)
                {
                    scrapeTimer[i] = scrapeInterval;
                    Spawn(scrapePrefab, point, normal, tangent, 1f);
                }
            }
            touching[i] = now;
        }
        hasPrev = true;
    }

    private void Impact(Vector3 point, Vector3 normal, Vector3 velocity, Vector3 tangent, float into)
    {
        float strength = Mathf.InverseLerp(minImpactSpeed, fullImpactSpeed, into);
        Spawn(impactPrefab, point, normal, tangent, Mathf.Lerp(impactScale.x, impactScale.y, strength));
        if (maxShake > 0f) KyodaiCameraShake.Shake(maxShake * strength, shakeSeconds);
        OnImpact?.Invoke(new KyodaiContactHit { point = point, normal = normal, velocity = velocity, strength = strength });
    }

    private void Spawn(GameObject prefab, Vector3 point, Vector3 normal, Vector3 direction, float scale)
    {
        if (prefab == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(direction, normal);
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.right, normal);
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        var go = Instantiate(prefab, point, Quaternion.LookRotation(forward, normal));
        go.transform.localScale *= scale;
        Destroy(go, effectLifetime);
    }

#if UNITY_EDITOR
    // 편집 중에도(애니메이션 창 녹화 중 포함) 접촉점을 보여준다 — 바닥·물체를 뚫으면 빨간색
    private void OnDrawGizmos()
    {
        if (!showGizmos || Boss == null) return;
        bool playing = Application.isPlaying && pointBones != null;
        for (int i = 0; i < points.Count; i++)
        {
            var pt = points[i];
            Transform bone = playing ? pointBones[i] : FindBone(pt.bone);
            if (bone == null) continue;
            float s = playing ? ScaleOf(i) : 1f;
            Vector3 p = PointWorld(bone, pt.offset, s);
            float r = pt.radius * s;
            bool inside = Penetration(Boss, p, r, groundDepth, obstacleMask.value).sqrMagnitude > 1e-6f;
            Gizmos.color = inside ? new Color(1f, 0.2f, 0.2f, 0.9f) : pt.solid ? new Color(0.3f, 1f, 0.5f, 0.8f) : new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(p, r);
        }
    }
#endif
}
