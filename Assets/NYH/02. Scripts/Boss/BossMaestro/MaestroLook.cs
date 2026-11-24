using System;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>패턴이 거는 몸짓 (MaestroLook.Play). 새 값은 맨 뒤에만 추가할 것</summary>
public enum MaestroGesture
{
    None,        // 평소 지휘 (4박)
    Swing,       // 크게 한 번 휘두르기 — 음표 공격
    RaiseBoth,   // 두 팔을 들어 올림 — 달빛 커튼, 거대 지휘봉 ①
    ConductFast, // 몸 전체로 격렬하게 지휘 — 거대 지휘봉 ④ (배경에서)
}

/// <summary>
/// 마에스트로 외형 + 몸짓 (2026-11-24). 사용자 참고 그림(`03. Image/Boss/Maestro/마에스트로_*.png`, 원본 시트에서 칸 단위로 뽑은 93×150px)을
/// 부위 6장(몸·머리·초승달·지휘팔·반대팔·반짝이)으로 나눠 리그 그림 부위로 굽고(MaestroLookBuilder), 이 스크립트가 부위 트랜스폼을 움직인다.
/// 리그가 매 프레임 부위 자리를 읽어 봇을 보낸다 → 봇이 스프링으로 따라와 움직임이 부드럽다(부위 '느낌' 값 + 여기의 각도 스프링).
///   · 몸짓: 평소 4박 지휘 / Swing / RaiseBoth / ConductFast + Flick(음 하나마다 지휘팔 튕김). 몸짓 사이는 섞여서 넘어간다
///   · 이동: 발 위치·크기·뒤판을 부드럽게 따라가고, 움직이는 방향으로 몸이 기운다. 방향 전환은 좌우가 뒤집히며 돈다
///   · 남는 봇(패턴이 빌려 쓰는 봇)은 발밑 모래 웅덩이(군집 덩어리 가이드)에 모아 둔다
/// 그림 전용(Time.deltaTime) — 판정과 무관. 판정 범위(HurtRect)는 컨트롤러가 읽는다.
/// </summary>
public class MaestroLook : MonoBehaviour
{
    [Header("연결 (빌더가 채움)")]
    public RSNanoSwarm swarm;
    public RSNanoRig rig;
    public Transform body, head, moon, batonArm, otherArm, sparkle;
    public RSNanoLimb[] limbs = Array.Empty<RSNanoLimb>();

    [Tooltip("시안 단독 실행 — 보스 컨트롤러 없이 시작할 때 군집·리그를 켠다")]
    public bool standalone;

    [Header("그림")]
    [Tooltip("그림이 원래 바라보는 쪽 (-1 = 왼쪽 — 지휘봉 든 팔이 화면 왼쪽)")]
    public float artFacing = -1f;
    [Tooltip("판정 범위 (그림 픽셀, 발밑 가운데 기준): x, y = 왼쪽 아래, z, w = 너비·높이. TODO 임시값 — 몸통·머리")]
    public Vector4 hurtRectPx = new Vector4(-12f, 30f, 30f, 82f);
    [Tooltip("그림 반폭 (픽셀)")]
    public float artHalfWidthPx = 46f;

    [Header("발밑 모래 웅덩이 (패턴이 빌려 쓰는 봇)")]
    [Tooltip("웅덩이 반지름 (m) — 가로, 높이")]
    public Vector2 poolRadius = new Vector2(1.4f, 0.16f);

    [Header("평소 지휘")]
    [Min(10f)] public float bpm = 54f;
    [Tooltip("평소 지휘 크기 (도)")]
    [Min(0f)] public float idleBeat = 14f;
    [Min(0f)] public float floatAmplitude = 0.08f;
    [Min(0f)] public float swayDegrees = 2f;

    [Header("부드러움")]
    [Tooltip("관절 각도가 목표를 따라가는 시간 (초) — 작을수록 날카롭다")]
    [Min(0.01f)] public float armSmooth = 0.09f;
    [Min(0.01f)] public float softSmooth = 0.18f;
    [Tooltip("발 위치를 따라가는 시간 (초)")]
    [Min(0.01f)] public float moveSmooth = 0.25f;
    [Tooltip("움직이는 속도(m/s)당 몸 기울기 (도)")]
    public float leanPerSpeed = 4f;
    [Tooltip("돌아서는 시간 (초)")]
    [Min(0.01f)] public float turnSmooth = 0.2f;

    [Header("몸짓 각도 (도, 그림 기준 + = 앞쪽 아래로)")]
    public Vector2 swingAngles = new Vector2(-35f, 100f);   // 휘두르기: 뒤로 들었다 → 앞 아래로
    public Vector2 raiseAngles = new Vector2(-25f, 100f);   // 들기: 지휘팔, 반대팔
    public Vector2 fastRange = new Vector2(-25f, 60f);      // 격렬한 지휘 폭
    public float flickAngle = 24f;

    // ── 상태 ──
    private Quaternion bodyRot0, headRot0, moonRot0, batonRot0, otherRot0, sparkleRot0;
    private Vector3 bodyPos0, headPos0, moonPos0, sparklePos0;
    private bool cached, started;
    private float batonAng, batonVel, otherAng, otherVel, headAng, headVel, leanAng, leanVel;
    private float flick;
    private MaestroGesture gesture = MaestroGesture.None;
    private float gestureTime, gestureLength, gestureWeight, gestureWeightVel;
    private Vector3 feetTarget, feet, feetVel;
    private Vector2 poseOffset;
    private float growTarget = 1f, grow = 1f, growVel;
    private float backTarget, back, backVel;
    private float faceTarget = -1f, face = -1f, faceVel;
    private Vector3 lastFeet;

    public bool Ready => started;
    public float Cell => swarm != null ? swarm.Cell : 0.03125f;
    public float Facing => Mathf.Sign(face == 0f ? faceTarget : face);
    public int PoolCount { get; private set; }

    /// <summary>판정 범위 (월드) — 지금 위치·크기·방향 기준</summary>
    public Rect HurtRect
    {
        get
        {
            float c = Cell * grow;
            Vector3 f = RigFeet;
            float x0 = hurtRectPx.x * c, x1 = (hurtRectPx.x + hurtRectPx.z) * c;
            if (face * artFacing < 0f) { float t = x0; x0 = -x1; x1 = -t; } // 그림이 뒤집혔으면 좌우 반대
            return new Rect(f.x + x0, f.y + hurtRectPx.y * c, x1 - x0, hurtRectPx.w * c);
        }
    }

    public Vector3 BodyCenter { get { var r = HurtRect; return new Vector3(r.center.x, r.center.y, RigFeet.z); } }

    /// <summary>그림 전체 반폭 (m) — 카메라가 보스를 화면 안에 둘 때</summary>
    public float ArtHalfWidth => artHalfWidthPx * Cell * grow;

    /// <summary>지휘봉 끝 (월드) — 거대 지휘봉이 여기서 자라난다</summary>
    public Vector3 BatonTip
    {
        get
        {
            // 그림에서 지휘봉 끝 = 지휘팔 관절(41,102)에서 (-10, +44)px 쯤 (원본 시트 기준)
            if (batonArm == null) return BodyCenter;
            return batonArm.TransformPoint(new Vector3(-10f, 44f, 0f) * Cell);
        }
    }

    private Vector3 RigFeet => feet + (Vector3)poseOffset;

    // ───────────────────────── 시작 ─────────────────────────

    /// <summary>군집을 세우고 리그를 켠다. feetPos = 발밑(바닥), 바닥 높이 = feetPos.y</summary>
    public void Init(Vector3 feetPos, float facing)
    {
        Cache();
        if (swarm == null || rig == null) return;
        swarm.floorFromTransform = false;
        swarm.floorY = feetPos.y;
        feet = feetTarget = lastFeet = feetPos;
        face = faceTarget = facing >= 0f ? 1f : -1f;

        // 그림 부위가 가져가고 남는 봇 = 발밑 모래 웅덩이
        int need = 0;
        foreach (var l in limbs) if (l != null) need += l.SlotTotal > 0 ? l.SlotTotal : l.bots;
        PoolCount = Mathf.Max(0, swarm.botCount - need);
        swarm.Blob.aspect = poolRadius.x / Mathf.Max(0.01f, poolRadius.y);
        swarm.Blob.radius = new Vector3(poolRadius.x, poolRadius.y, 0.3f);
        swarm.Blob.center = PoolCenter();
        swarm.Build(swarm.Blob.center);
        swarm.ResetBotLooks();

        rig.Begin(RSNanoSwarm.G_Blob, () => feetPos + Vector3.right * face * 6f, 1f, RigFeet);
        ApplyRigTransform();
        started = true;
    }

    private void Start()
    {
        if (standalone && !started) Init(transform.position, artFacing);
    }

    private void OnDisable()
    {
        if (rig != null && rig.Active) rig.End();
        started = false;
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (body) { bodyRot0 = body.localRotation; bodyPos0 = body.localPosition; }
        if (head) { headRot0 = head.localRotation; headPos0 = head.localPosition; }
        if (moon) { moonRot0 = moon.localRotation; moonPos0 = moon.localPosition; }
        if (batonArm) batonRot0 = batonArm.localRotation;
        if (otherArm) otherRot0 = otherArm.localRotation;
        if (sparkle) { sparkleRot0 = sparkle.localRotation; sparklePos0 = sparkle.localPosition; }
    }

    // ───────────────────────── 바깥에서 거는 것 ─────────────────────────

    /// <summary>발밑 목표 (월드). 부드럽게 따라간다</summary>
    public void SetFeet(Vector3 worldFeet) => feetTarget = worldFeet;

    /// <summary>바라볼 쪽 (+1 오른쪽, -1 왼쪽). 좌우가 뒤집히며 돈다</summary>
    public void SetFacing(float dir) { if (Mathf.Abs(dir) > 0.01f) faceTarget = Mathf.Sign(dir); }

    /// <summary>자세: 발밑에서 offset(m) 만큼, 크기 grow 배, back이면 뒤판(배경). 부드럽게 넘어간다</summary>
    public void SetPose(Vector2 offset, float grow, bool back)
    {
        poseOffset = offset;
        growTarget = Mathf.Max(0.1f, grow);
        backTarget = back ? 1f : 0f;
    }

    /// <summary>몸짓을 seconds 동안 (0 이하면 그만둘 때까지). 끝나면 평소 지휘로 섞여 돌아간다</summary>
    public void Play(MaestroGesture g, float seconds)
    {
        gesture = g;
        gestureTime = 0f;
        gestureLength = seconds;
    }

    public void Stop() => gesture = MaestroGesture.None;

    /// <summary>지휘팔을 앞으로 짧게 튕긴다 (음 하나마다)</summary>
    public void Flick() => flick = 1f;

    /// <summary>발밑 모래 웅덩이 중심 (월드)</summary>
    public Vector3 PoolCenter()
    {
        Vector3 f = RigFeet;
        return new Vector3(f.x, (swarm != null ? swarm.floorY : f.y) + poolRadius.y, back > 0.5f && swarm != null ? swarm.BackZ : f.z);
    }

    // ───────────────────────── 매 프레임 ─────────────────────────

    private void LateUpdate()
    {
        if (!started) return;
        float dt = Mathf.Max(1e-4f, Time.deltaTime);
        float t = Time.time;

        // 이동·크기·뒤판·방향 — 부드럽게
        feet = Vector3.SmoothDamp(feet, feetTarget, ref feetVel, moveSmooth, Mathf.Infinity, dt);
        grow = Mathf.SmoothDamp(grow, growTarget, ref growVel, 0.35f, Mathf.Infinity, dt);
        back = Mathf.SmoothDamp(back, backTarget, ref backVel, 0.3f, Mathf.Infinity, dt);
        face = Mathf.SmoothDamp(face, faceTarget, ref faceVel, turnSmooth, Mathf.Infinity, dt);
        float speedX = (RigFeet.x - lastFeet.x) / dt;
        lastFeet = RigFeet;
        leanAng = Mathf.SmoothDamp(leanAng, Mathf.Clamp(-speedX * leanPerSpeed * Facing * -artFacing, -14f, 14f), ref leanVel, 0.2f, Mathf.Infinity, dt);
        ApplyRigTransform();
        foreach (var l in limbs) if (l != null) l.back = back;
        if (swarm != null) swarm.Blob.center = PoolCenter();

        // 몸짓 섞기
        if (gesture != MaestroGesture.None)
        {
            gestureTime += dt;
            if (gestureLength > 0f && gestureTime >= gestureLength) gesture = MaestroGesture.None;
        }
        gestureWeight = Mathf.SmoothDamp(gestureWeight, gesture == MaestroGesture.None ? 0f : 1f, ref gestureWeightVel, 0.15f, Mathf.Infinity, dt);

        float beat = t * bpm / 60f;
        int b = Mathf.FloorToInt(beat) & 3;
        float f = beat - Mathf.Floor(beat);

        // 평소 지휘 (4박: 1 아래 → 2 안 → 3 밖 → 4 위)
        float idleBaton = idleBeat * CatmullRom(BeatAngles[(b + 3) & 3], BeatAngles[b], BeatAngles[(b + 1) & 3], BeatAngles[(b + 2) & 3], Ease(f));
        float idleOther = 8f * Mathf.Sin(beat * Mathf.PI / 4f);

        GestureAngles(out float gBaton, out float gOther);
        flick = Mathf.MoveTowards(flick, 0f, dt * 4f);
        float flickAdd = flickAngle * Mathf.Sin(flick * Mathf.PI);

        float targetBaton = Mathf.Lerp(idleBaton, gBaton, gestureWeight) + flickAdd;
        float targetOther = Mathf.Lerp(idleOther, gOther, gestureWeight);
        batonAng = Mathf.SmoothDampAngle(batonAng, targetBaton, ref batonVel, armSmooth, Mathf.Infinity, dt);
        otherAng = Mathf.SmoothDampAngle(otherAng, targetOther, ref otherVel, softSmooth, Mathf.Infinity, dt);
        // + = 앞쪽 아래. 그림이 왼쪽을 보므로 앞 아래 = 반시계(+z). 반대팔은 그림 오른쪽이라 '들기' = 반시계
        if (batonArm) batonArm.localRotation = batonRot0 * Quaternion.Euler(0f, 0f, batonAng);
        if (otherArm) otherArm.localRotation = otherRot0 * Quaternion.Euler(0f, 0f, otherAng);

        // 머리 — 1박 끄덕 + 지휘팔 쪽으로 살짝
        float nod = b == 0 ? Mathf.Sin(f * Mathf.PI) * 4f : 0f;
        headAng = Mathf.SmoothDampAngle(headAng, nod + batonAng * 0.08f, ref headVel, softSmooth, Mathf.Infinity, dt);
        if (head) head.localRotation = headRot0 * Quaternion.Euler(0f, 0f, headAng);

        // 몸 — 떠다니기 · 박자 기울기 · 이동 기울기 · 격렬할 땐 더 크게
        float energy = gesture == MaestroGesture.ConductFast ? gestureWeight : 0f;
        if (body)
        {
            body.localPosition = bodyPos0 + Vector3.up * (floatAmplitude * Mathf.Sin(t * 0.9f));
            float sway = swayDegrees * (1f + 2f * energy) * Mathf.Sin(beat * Mathf.PI * (0.5f + energy));
            body.localRotation = bodyRot0 * Quaternion.Euler(0f, 0f, sway + leanAng);
        }

        // 초승달 · 반짝이 — 천천히 떠다님
        if (moon)
        {
            moon.localPosition = moonPos0 + new Vector3(0f, 0.05f * Mathf.Sin(t * 0.8f), 0f);
            moon.localRotation = moonRot0 * Quaternion.Euler(0f, 0f, 5f * Mathf.Sin(t * 0.5f) + 20f * energy * Mathf.Sin(t * 3f));
        }
        if (sparkle)
        {
            sparkle.localPosition = sparklePos0 + new Vector3(0.05f * Mathf.Sin(t * 0.6f), 0.08f * Mathf.Sin(t * 0.7f + 1f), 0f);
            sparkle.localRotation = sparkleRot0 * Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(t * 0.35f));
        }
    }

    // 몸짓별 목표 각도 (지휘팔, 반대팔)
    private void GestureAngles(out float baton, out float other)
    {
        float u = gestureLength > 0f ? Mathf.Clamp01(gestureTime / gestureLength) : 0f;
        switch (gesture)
        {
            case MaestroGesture.Swing:
                // 처음 25%는 천천히 뒤로 들고, 나머지에 크게 앞 아래로
                baton = u < 0.25f ? Mathf.Lerp(0f, swingAngles.x, Mathf.SmoothStep(0f, 1f, u / 0.25f))
                                  : Mathf.Lerp(swingAngles.x, swingAngles.y, Mathf.SmoothStep(0f, 1f, (u - 0.25f) / 0.75f));
                other = 25f;
                return;
            case MaestroGesture.RaiseBoth:
                baton = raiseAngles.x + 3f * Mathf.Sin(gestureTime * 9f);
                other = raiseAngles.y + 3f * Mathf.Sin(gestureTime * 8f + 1f);
                return;
            case MaestroGesture.ConductFast:
            {
                float s = Mathf.Sin(gestureTime * Mathf.PI * 2f * 1.6f);
                baton = Mathf.Lerp(fastRange.x, fastRange.y, 0.5f + 0.5f * s);
                other = Mathf.Lerp(60f, -10f, 0.5f + 0.5f * s);
                return;
            }
            default:
                baton = 0f;
                other = 0f;
                return;
        }
    }

    // 리그 원점 = 발밑, 크기 = grow, 좌우 = 바라보는 쪽 (그림 방향 기준)
    private void ApplyRigTransform()
    {
        if (rig == null) return;
        Vector3 p = RigFeet;
        rig.transform.position = p;
        float sx = face * -artFacing; // 그림이 왼쪽을 보는데 오른쪽을 봐야 하면 뒤집는다
        if (Mathf.Abs(sx) < 0.02f) sx = 0.02f * Mathf.Sign(faceTarget * -artFacing);
        rig.transform.localScale = new Vector3(sx * grow, grow, 1f);
    }

    // 4박 지휘 각도 (평소 크기 1 기준) — 1 아래(+) · 2 안 · 3 밖 · 4 위(-)
    private static readonly float[] BeatAngles = { 1.2f, -0.3f, 0.4f, -1f };

    private static float Ease(float f) => f < 0.7f ? Mathf.SmoothStep(0f, 0.55f, f / 0.7f) : Mathf.Lerp(0.55f, 1f, Mathf.Pow((f - 0.7f) / 0.3f, 0.6f));

    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
