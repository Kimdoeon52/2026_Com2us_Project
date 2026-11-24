using System;
using System.Collections.Generic;
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
/// 마에스트로 외형 = 나노봇 퍼펫 (2026-11-24 개편). 참고 그림 픽셀 하나 = 봇 하나를 이 스크립트가 직접 놓는다(엔진 리그 안 씀).
///   · 뼈: 상체·머리·달, 지휘팔(위팔·아래팔·손·지휘봉·손가락), 반대팔(위팔·아래팔·손·손가락 3개). 관절 각도는 2차 스프링(넘쳤다 돌아옴) +
///     팔로스루(부모가 빨리 돌면 자식이 늦게) → 어깨 → 팔꿈치 → 손목 → 지휘봉 순서로 채찍처럼 휜다
///   · 천: 끈·소매·자락 픽셀은 천 뼈대 선에 붙어 있고, 뼈대는 물리(원래 모양 복원력 + 중력 + 바람 + 관성)로 매 프레임 계산 → 가만히 있어도 펄럭이고 팔을 따라 휘날림
///   · 몸짓: 예비 동작 → 본동작 → 여운 키 포즈 (Swing / RaiseBoth / ConductFast) + Flick, 손가락도 몸짓마다 쥐고·펴고·떨림. 평소엔 4박 지휘(변주) + 숨쉬기·8자 떠다니기·노이즈
///   · 아랫단 조각은 계속 흘러내리고, 반짝이는 떠다닌다
///   · 남는 봇(패턴이 빌려 쓰는 봇)은 하체 그림 실루엣(군집 덩어리 가이드)으로 하체에 겹쳐 숨긴다 — 하체는 돌리지 않는다
///   · 봇을 잃으면(무너짐·패턴) 덩어리에서 다시 가져와 아래부터 채운다 (흘러와서 자리 잡음)
/// 그림 전용(Time.deltaTime) — 판정과 무관. 판정 범위(HurtRect)는 컨트롤러가 읽는다.
/// </summary>
public class MaestroLook : MonoBehaviour
{
    [Header("연결 (빌더가 채움)")]
    public RSNanoSwarm swarm;
    public MaestroPuppetData data;
    [Tooltip("하체 그림 실루엣 — 남는 봇을 여기 겹쳐 숨긴다")]
    public RSNanoSpriteShape reserveShape;

    [Tooltip("시안 단독 실행 — 보스 컨트롤러 없이 시작할 때 군집을 켠다")]
    public bool standalone;

    [Header("그림")]
    [Tooltip("판정 범위 (그림 픽셀, 그림 아래 끝 가운데 기준, 위가 +): x, y = 왼쪽 아래, z, w = 너비·높이. TODO 임시값 — 몸통·머리. " +
             "아래 끝을 바닥 근처까지 내려 플레이어 기본 공격이 닿게 (11-24 사용자)")]
    public Vector4 hurtRectPx = new Vector4(-12f, 16f, 30f, 96f);
    [Tooltip("그림을 바닥 아래로 내리는 정도 (그림 픽셀) — 아래로 흩어지는 꼬리 끝이 바닥에 묻히고 몸통이 바닥에 가까워진다")]
    public float sinkPx = 14f;
    [Tooltip("그림이 원래 바라보는 쪽 (-1 = 왼쪽 — 지휘봉 든 팔이 화면 왼쪽)")]
    public float artFacing = -1f;
    [Tooltip("켜면 플레이어 쪽으로 돌아선다 (좌우가 뒤집힘). 그림이 정면이라 기본 꺼짐")]
    public bool turnToPlayer;
    [Tooltip("지휘봉 끝 (그림 픽셀)")]
    public Vector2 batonTipPx = new Vector2(31f, 3f);

    [Header("크기 맞추기 (시작할 때)")]
    public bool autoFit = true;
    [Tooltip("보스 키 = 플레이어 키 × 이 값 (TODO 임시값)")]
    [Min(0.5f)] public float heightPerPlayer = 2.0f;
    [Tooltip("보스 키가 화면에 보이는 높이의 이 비율을 넘지 않게")]
    [Range(0.2f, 0.9f)] public float maxScreenRatio = 0.5f;
    [Min(0.5f)] public float fallbackHeight = 3f;

    [Header("평소")]
    [Tooltip("평소 지휘 빠르기·크기는 아래 '악장별 연출'이 정한다")]
    [HideInInspector] public float bpm = 54f;
    [Tooltip("평소 지휘 크기 (도)")]
    [HideInInspector] public float idleBeat = 16f;
    [Tooltip("떠다니기 크기 (키 대비) — x 좌우, y 위아래. 주기가 달라 8자로 돈다")]
    public Vector2 floatRatio = new Vector2(0.008f, 0.014f);
    public Vector2 floatPeriods = new Vector2(4.3f, 2.7f);
    [Tooltip("손가락이 오므라드는 최대 각도 (도)")]
    public float fingerCurlDeg = 65f;

    [Header("아랫단 조각 · 반짝이")]
    [Tooltip("조각이 흘러내리는 거리 (그림 픽셀)")]
    public float dripRangePx = 22f;
    [Tooltip("흘러내리는 빠르기 (그림 픽셀/초)")]
    public float dripSpeedPx = 7f;

    [Serializable]
    public class MovementLook
    {
        [Tooltip("평소 지휘 빠르기 (분당 박)")] [Min(10f)] public float bpm = 54f;
        [Tooltip("평소 지휘 크기 (도)")] [Min(0f)] public float idleBeat = 16f;
        [Tooltip("끈·소매 바람 배율")] [Min(0f)] public float windScale = 1f;
        [Tooltip("아랫단 조각 흘러내림 빠르기 배율")] [Min(0f)] public float dripScale = 1f;
        [Tooltip("떠다니기 크기 배율")] [Min(0f)] public float floatScale = 1f;
        [Min(0f)] public float sparkleGlow = 2.2f;
        [Min(0f)] public float moonGlow = 0.6f;
        [Tooltip("몸 분해 — 초당 떨어져 나갔다 돌아오는 입자 수 (시트 3악장 '신체 분해')")] [Min(0f)] public float shedPerSecond;
        [Tooltip("주변을 떠도는 음표 수 (시트 2·3악장)")] [Min(0)] public int ambientNotes;
        [Tooltip("주변에서 깜박이는 빛기둥 수 (시트 3악장)")] [Min(0)] public int beams;

        public void LerpTo(MovementLook b, float k)
        {
            bpm = Mathf.Lerp(bpm, b.bpm, k); idleBeat = Mathf.Lerp(idleBeat, b.idleBeat, k);
            windScale = Mathf.Lerp(windScale, b.windScale, k); dripScale = Mathf.Lerp(dripScale, b.dripScale, k);
            floatScale = Mathf.Lerp(floatScale, b.floatScale, k);
            sparkleGlow = Mathf.Lerp(sparkleGlow, b.sparkleGlow, k); moonGlow = Mathf.Lerp(moonGlow, b.moonGlow, k);
            shedPerSecond = Mathf.Lerp(shedPerSecond, b.shedPerSecond, k);
            ambientNotes = b.ambientNotes; beams = b.beams;
        }
    }

    [Header("악장별 연출 (시트 '페이즈별 변화' — 그림만, 패턴 수치는 안 바꿈)")]
    [Tooltip("1악장 고요 · 2악장 경쾌 · 3악장 격렬. 악장이 바뀌면 1.5초에 걸쳐 넘어간다. 전부 시안값(TODO)")]
    public MovementLook[] movementLooks =
    {
        new MovementLook { bpm = 54f, idleBeat = 16f },
        new MovementLook { bpm = 76f, idleBeat = 22f, windScale = 1.4f, dripScale = 1.4f, floatScale = 1.2f, sparkleGlow = 2.8f, moonGlow = 0.9f, ambientNotes = 6 },
        new MovementLook { bpm = 100f, idleBeat = 28f, windScale = 2.2f, dripScale = 2.5f, floatScale = 1.5f, sparkleGlow = 3.5f, moonGlow = 1.4f, shedPerSecond = 24f, ambientNotes = 10, beams = 6 },
    };

    [Header("이동")]
    [Min(0.01f)] public float moveSmooth = 0.3f;
    [Tooltip("움직이는 속도(m/s)당 상체 기울기 (도)")]
    public float leanPerSpeed = 8f;
    [Header("피격 반응")]
    [Tooltip("한 번 맞을 때 튀어 나가는 입자 수")]
    [Min(0)] public int hitBurst = 14;
    [Tooltip("움찔하는 크기 (도)")]
    [Min(0f)] public float hitFlinch = 10f;
    [Min(0.01f)] public float turnSmooth = 0.2f;

    [Header("봇 채우기 · 흩어지기")]
    [Tooltip("잃은 봇을 프레임마다 몇 개까지 다시 가져올지 (다시 쌓이는 빠르기)")]
    [Min(1)] public int refillPerFrame = 90;
    [Tooltip("봇 하나가 제자리로 흘러오는 시간 (초) — 최소·최대. 휘어진 궤적으로 천천히 출발해 천천히 자리 잡는다")]
    public Vector2 gatherSeconds = new Vector2(0.45f, 0.9f);
    [Tooltip("모여 올 때 궤적이 휘는 정도 (키 대비)")]
    [Min(0f)] public float gatherCurve = 0.12f;
    [Tooltip("흩어질 때 봇이 바깥으로 퍼지는 속도 (m/s)")]
    [Min(0f)] public float scatterSpeed = 0.8f;
    [Tooltip("끄면 잃은 봇을 다시 채우지 않는다 (격파 뒤)")]
    public bool refillEnabled = true;

    // ── 몸짓 채널 ──
    private enum Ch { Chest, Head, BShoulder, BElbow, BWrist, Baton, BGrip, OShoulder, OElbow, OWrist, OCurl, Count }

    private struct Key
    {
        public float t;
        public float[] v;
        public Key(float t, params float[] v) { this.t = t; this.v = v; }
    }

    //                         상체  머리 지휘어깨 팔꿈치 손목 지휘봉 쥠  반대어깨 팔꿈치 손목 손가락
    private static readonly Key[] SwingKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.15f, -4f,  -3f,  -15f,  25f, -20f,  -5f, 0.8f,   15f,  10f,   0f,  0.6f), // 예비: 뒤로 당김
        new Key(0.35f, -6f,  -5f,  -45f,  35f, -35f, -10f, 1f,     30f,  20f,  -5f, -0.2f), // 크게 젖힘
        new Key(0.65f,  8f,   6f,   90f, -10f,  40f,  20f, 1f,     40f, -10f,  20f, -0.6f), // 휘두름
        new Key(0.85f,  4f,   3f,   75f,   5f,  15f,   8f, 0.9f,   25f,   0f,  10f, -0.3f), // 지나침
        new Key(1f,     2f,   1f,   55f,   5f,   5f,   0f, 0.7f,   15f,   0f,   0f,  0f),   // 여운
    };

    private static readonly Key[] RaiseKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.12f,  6f,   4f,   20f,  30f,  10f,   0f, 0.9f,  -20f,  25f,   0f,  0.9f), // 예비: 웅크림, 주먹
        new Key(0.4f,  -5f,  -6f,  -35f, -15f, -25f, -10f, 1f,    110f, -10f, -20f, -0.7f), // 들어 올림, 손가락 쫙
        new Key(1f,    -4f,  -5f,  -32f, -12f, -20f,  -8f, 1f,    105f,  -8f, -15f, -0.6f), // 유지
    };

    // ── 상태 ──
    private bool started;
    private float cell = 0.03125f;
    private Vector2[] restOffset = Array.Empty<Vector2>();   // 뼈: 부모 관절 → 이 관절 (그림 공간 m)
    private float[] ang = Array.Empty<float>(), angVel = Array.Empty<float>(), angPrevTarget = Array.Empty<float>();
    private Vector2[] bonePos = Array.Empty<Vector2>();
    private float[] boneAng = Array.Empty<float>();
    private Vector2[] pixLocal = Array.Empty<Vector2>();     // 뼈 픽셀: 관절 기준, 하체: 발밑 기준 (그림 공간 m)
    private int[] clothSeg = Array.Empty<int>();
    private float[] clothT = Array.Empty<float>(), clothD = Array.Empty<float>();
    private float[] pixHash = Array.Empty<float>();
    private int[] slotBot = Array.Empty<int>();
    private bool[] slotArriving = Array.Empty<bool>();
    private int[] fillOrder = Array.Empty<int>();
    private readonly List<int> tmp = new List<int>(512);

    private class ClothSim
    {
        public Vector2[] restLocal;   // 붙은 뼈 기준 (그림 공간 m)
        public float[] restLen;       // 그림 공간 m
        public Vector2[] pos, prev;   // 월드 2D
        public bool init;
    }
    private ClothSim[] cloth = Array.Empty<ClothSim>();

    private int iChest, iHead, iMoon, iBS, iBE, iBW, iBaton, iBGrip, iOS, iOE, iOW, iF1, iF2, iF3;
    private MaestroGesture gesture = MaestroGesture.None;
    private float gestureTime, gestureLength, gestureWeight, gestureWeightVel, flick;
    private Vector3 feetTarget, feet, feetVel;
    private Vector2 poseOffset, rootArt, lastRootWorld;
    private float growTarget = 1f, grow = 1f, growVel;
    private float backTarget, back, backVel;
    private float faceTarget = -1f, face = -1f, faceVel;
    private float leanAng, leanVel;
    private float batonForm = 1f, batonFormDur = 0.5f;
    private float[] batonU = Array.Empty<float>();      // 지휘봉 픽셀: 손잡이 0 → 끝 1
    private float hitPulse, nextHitTime;

    // 악장별 연출
    private MovementLook cur = new MovementLook();
    private int movement = 1;
    private float beatPhase, dripPhase, shedAcc, appliedSparkle = -1f, appliedMoon = -1f;

    /// <summary>지금 악장 (1~3) — 그림 연출만 바뀐다 (1.5초에 걸쳐)</summary>
    public void SetMovement(int m, bool instant = false)
    {
        movement = Mathf.Clamp(m, 1, Mathf.Max(1, movementLooks.Length));
        if (instant && movementLooks.Length > 0) cur.LerpTo(movementLooks[movement - 1], 1f);
    }

    // 악장 프로필 쪽으로 서서히 + 박·조각 위상 누적 + 몸 분해 + 발광 다시 칠하기
    private void TickMovement(float dt)
    {
        if (movementLooks.Length > 0) cur.LerpTo(movementLooks[Mathf.Clamp(movement - 1, 0, movementLooks.Length - 1)], Mathf.Clamp01(dt / 1.5f) * 3f);
        beatPhase += dt * cur.bpm / 60f;
        dripPhase += dt * dripSpeedPx * cur.dripScale;

        if (cur.shedPerSecond > 0.5f && refillEnabled && dissolving.Count == 0)
        {
            shedAcc += cur.shedPerSecond * dt;
            int n = Mathf.FloorToInt(shedAcc);
            if (n > 0) { shedAcc -= n; BurstParticles(n, soft: true); }
        }

        if (Mathf.Abs(appliedSparkle - cur.sparkleGlow) > 0.05f || Mathf.Abs(appliedMoon - cur.moonGlow) > 0.05f)
        {
            appliedSparkle = cur.sparkleGlow;
            appliedMoon = cur.moonGlow;
            for (int k = 0; k < slotBot.Length; k++)
            {
                if (slotBot[k] < 0) continue;
                byte kind = data.pixKind[k];
                if (kind == (byte)MaestroPuppetData.PixelKind.Sparkle) swarm.SetGlow(slotBot[k], cur.sparkleGlow);
                else if (kind == (byte)MaestroPuppetData.PixelKind.Glow) swarm.SetGlow(slotBot[k], cur.moonGlow);
            }
        }
    }
    private int pixelCount;

    public bool Ready => started;
    public float Cell => cell;
    public float Facing => Mathf.Sign(face == 0f ? faceTarget : face);
    public int ReserveCount { get; private set; }
    public float Height => (data != null ? data.artHeightPx : 150f) * cell * grow;
    public float ArtHalfWidth => (data != null ? data.artWidthPx * 0.5f : 46f) * cell * grow;

    /// <summary>판정 범위 (월드) — 지금 위치·크기·방향 기준</summary>
    public Rect HurtRect
    {
        get
        {
            float c = cell * grow;
            Vector2 f = RootWorld();
            float x0 = hurtRectPx.x * c, x1 = (hurtRectPx.x + hurtRectPx.z) * c;
            if (FlipX < 0f) { float t = x0; x0 = -x1; x1 = -t; }
            return new Rect(f.x + x0, f.y + hurtRectPx.y * c, x1 - x0, hurtRectPx.w * c);
        }
    }

    public Vector3 BodyCenter { get { var r = HurtRect; return new Vector3(r.center.x, r.center.y, PlaneZ); } }

    /// <summary>지휘봉 끝 (월드) — 거대 지휘봉이 여기서 자라난다</summary>
    public Vector3 BatonTip
    {
        get
        {
            if (!started || iBaton < 0) return BodyCenter;
            Vector2 local = ArtPx(batonTipPx) - ArtPx(data.bones[iBaton].pivotPx);
            Vector2 art = bonePos[iBaton] + Rot(local, boneAng[iBaton]);
            Vector2 w = ArtToWorld(art);
            return new Vector3(w.x, w.y, PlaneZ);
        }
    }

    private float FlipX => Mathf.Abs(face * artFacing) < 0.02f ? 0.02f * Mathf.Sign(faceTarget * artFacing) : face * artFacing;
    private float PlaneZ => swarm != null ? (back > 0.5f ? swarm.BackZ : swarm.FrontZ) : transform.position.z;

    // ───────────────────────── 시작 ─────────────────────────

    /// <summary>크기를 맞추고 군집을 세우고 그림대로 봇을 놓는다. feetPos = 발밑(바닥)</summary>
    public void Init(Vector3 feetPos, float facing)
    {
        if (swarm == null || data == null || data.PixelCount == 0) { Debug.LogError("[Maestro] 외형 데이터가 없음 — 빌더(마에스트로 만들기·갱신)를 다시 실행"); return; }
        swarm.floorFromTransform = false;
        swarm.floorY = feetPos.y;
        feet = feetTarget = feetPos;
        face = faceTarget = turnToPlayer && Mathf.Abs(facing) > 0.01f ? Mathf.Sign(facing) : artFacing;

        float height = FitHeight(feetPos);
        swarm.botWorldSize = height / data.artHeightPx * 8f / Mathf.Max(1, swarm.grain);
        cell = swarm.Cell;

        BuildRig();
        pixelCount = data.PixelCount;
        ReserveCount = Mathf.Max(0, swarm.botCount - pixelCount);

        // 남는 봇 = 하체 실루엣으로 하체에 겹쳐 숨김
        swarm.Blob.shape = reserveShape;
        swarm.Blob.shapeScale = 1f;
        swarm.Blob.shapeArtColor = true;
        swarm.Blob.noise = 0f;
        swarm.Blob.wobble = 0f;
        swarm.Blob.flow = 0f;
        swarm.Blob.center = new Vector3(feetPos.x, feetPos.y, swarm.FrontZ);
        swarm.Build(swarm.Blob.center);
        swarm.ResetBotLooks();

        // 그림 자리에 바로 놓는다 (처음엔 흘러오지 않음)
        Pose(Time.time, 0f);
        SimulateCloth(Time.time, 0f); // 천 뼈대를 쉬는 모양으로 먼저 세운다
        swarm.Collect(RSNanoSwarm.G_Blob, swarm.Blob.center, pixelCount, tmp);
        for (int k = 0; k < pixelCount && k < tmp.Count; k++) Assign(k, tmp[k], snap: true);
        started = true;
        Debug.Log($"[Maestro] 외형 시작 — 키 {height:0.00}m (칸 {cell * 100f:0.00}cm), 그림 봇 {pixelCount}, 남는 봇 {ReserveCount}");
    }

    private void BuildRig()
    {
        int nb = data.bones.Length;
        restOffset = new Vector2[nb];
        ang = new float[nb]; angVel = new float[nb]; angPrevTarget = new float[nb];
        bonePos = new Vector2[nb]; boneAng = new float[nb];
        for (int b = 0; b < nb; b++)
        {
            var bone = data.bones[b];
            Vector2 parentPivot = bone.parent >= 0 ? ArtPx(data.bones[bone.parent].pivotPx) : Vector2.zero;
            restOffset[b] = ArtPx(bone.pivotPx) - parentPivot;
        }
        iChest = data.BoneIndex("상체"); iHead = data.BoneIndex("머리"); iMoon = data.BoneIndex("달");
        iBS = data.BoneIndex("지휘_위팔"); iBE = data.BoneIndex("지휘_아래팔"); iBW = data.BoneIndex("지휘_손");
        iBaton = data.BoneIndex("지휘봉"); iBGrip = data.BoneIndex("지휘_손가락");
        iOS = data.BoneIndex("반대_위팔"); iOE = data.BoneIndex("반대_아래팔"); iOW = data.BoneIndex("반대_손");
        iF1 = data.BoneIndex("손가락1"); iF2 = data.BoneIndex("손가락2"); iF3 = data.BoneIndex("손가락3");

        // 천: 뼈대 쉬는 모양(붙은 뼈 기준)
        cloth = new ClothSim[data.cloths.Length];
        for (int c = 0; c < cloth.Length; c++)
        {
            var def = data.cloths[c];
            int n = def.spinePx.Length;
            var sim = new ClothSim { restLocal = new Vector2[n], restLen = new float[Mathf.Max(0, n - 1)], pos = new Vector2[n], prev = new Vector2[n] };
            Vector2 pivot = def.parentBone >= 0 ? ArtPx(data.bones[def.parentBone].pivotPx) : Vector2.zero;
            for (int i = 0; i < n; i++) sim.restLocal[i] = ArtPx(def.spinePx[i]) - pivot;
            for (int i = 0; i < n - 1; i++) sim.restLen[i] = Vector2.Distance(ArtPx(def.spinePx[i]), ArtPx(def.spinePx[i + 1]));
            cloth[c] = sim;
        }

        // 픽셀: 뼈 기준 자리 / 천 뼈대 묶기
        int np = data.PixelCount;
        pixLocal = new Vector2[np];
        clothSeg = new int[np]; clothT = new float[np]; clothD = new float[np];
        pixHash = new float[np];
        slotBot = new int[np];
        slotArriving = new bool[np];
        arriveFrom = new Vector3[np];
        arriveT0 = new float[np];
        arriveDur = new float[np];
        for (int k = 0; k < np; k++)
        {
            slotBot[k] = -1;
            pixHash[k] = Mathf.Repeat(Mathf.Sin(k * 12.9898f) * 43758.5453f, 1f);
            Vector2 p = ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f));
            int owner = data.pixOwner[k];
            if (owner >= 0) pixLocal[k] = p - ArtPx(data.bones[owner].pivotPx);
            else if (owner == -1) pixLocal[k] = p;
            else BindCloth(k, -2 - owner, p);
        }

        // 지휘봉 픽셀: 손잡이(관절)에서 끝까지 비율 — 지휘봉이 손에서부터 생기게
        batonU = new float[np];
        if (iBaton >= 0)
        {
            Vector2 grip = ArtPx(data.bones[iBaton].pivotPx), tip = ArtPx(batonTipPx);
            Vector2 axis = tip - grip;
            float len2 = Mathf.Max(1e-8f, axis.sqrMagnitude);
            for (int k = 0; k < np; k++)
                if (data.pixOwner[k] == iBaton)
                    batonU[k] = Mathf.Clamp01(Vector2.Dot(ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f)) - grip, axis) / len2);
        }

        // 다시 채우는 순서 = 아래 픽셀부터 (모래성 쌓기)
        fillOrder = new int[np];
        for (int k = 0; k < np; k++) fillOrder[k] = k;
        Array.Sort(fillOrder, (a, b) => data.pixPos[b].y.CompareTo(data.pixPos[a].y));
    }

    // 천 픽셀 → 가장 가까운 뼈대 마디 (마디 번호, 마디 위 비율, 마디 옆 거리)
    private void BindCloth(int k, int c, Vector2 p)
    {
        var def = data.cloths[c];
        float best = float.MaxValue;
        for (int i = 0; i < def.spinePx.Length - 1; i++)
        {
            Vector2 a = ArtPx(def.spinePx[i]), b = ArtPx(def.spinePx[i + 1]);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-8f, ab.sqrMagnitude);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 q = a + ab * t;
            float d2 = (p - q).sqrMagnitude;
            if (d2 >= best) continue;
            best = d2;
            Vector2 dir = ab / Mathf.Sqrt(len2);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            clothSeg[k] = i;
            clothT[k] = t;
            clothD[k] = Vector2.Dot(p - q, perp);
        }
        pixLocal[k] = new Vector2(c, 0f); // 천 번호를 기억 (x)
    }

    // 보스 키 = min(플레이어 키 × heightPerPlayer, 화면 높이 × maxScreenRatio)
    private float FitHeight(Vector3 feetPos)
    {
        if (!autoFit) return fallbackHeight;
        float h = fallbackHeight;
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            bool any = false;
            Bounds b = default;
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.3f && b.size.y < 6f) h = b.size.y * heightPerPlayer;
        }
        var cam = Camera.main;
        if (cam != null && PlaneY(cam, 0f, feetPos.z, out float y0) && PlaneY(cam, 1f, feetPos.z, out float y1))
            h = Mathf.Min(h, Mathf.Abs(y1 - y0) * maxScreenRatio);
        return Mathf.Max(0.8f, h);
    }

    private static bool PlaneY(Camera cam, float vy, float z, out float y)
    {
        y = 0f;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, vy, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        y = ray.origin.y + ray.direction.y * t;
        return true;
    }

    private void Start()
    {
        if (standalone && !started) Init(transform.position, artFacing);
    }

    private void OnDisable() => started = false;

    // ───────────────────────── 바깥에서 거는 것 ─────────────────────────

    public void SetFeet(Vector3 worldFeet) => feetTarget = worldFeet;

    public void SetFacing(float dir) { if (turnToPlayer && Mathf.Abs(dir) > 0.01f) faceTarget = Mathf.Sign(dir); }

    /// <summary>자세: 발밑에서 offset(m) 만큼, 크기 grow 배, back이면 뒤판(배경). 부드럽게 넘어간다</summary>
    public void SetPose(Vector2 offset, float grow, bool back)
    {
        poseOffset = offset;
        growTarget = Mathf.Max(0.1f, grow);
        backTarget = back ? 1f : 0f;
    }

    /// <summary>몸짓을 seconds 동안. 끝나면 평소 지휘로 섞여 돌아간다</summary>
    public void Play(MaestroGesture g, float seconds)
    {
        gesture = g;
        gestureTime = 0f;
        gestureLength = seconds;
        // 시트 '지휘봉 준비': 팔이 오르며 지휘봉이 입자로 모여 생긴다
        if (g == MaestroGesture.Swing || g == MaestroGesture.RaiseBoth) FormBaton(Mathf.Clamp(seconds * 0.35f, 0.25f, 0.6f));
    }

    public void Stop() => gesture = MaestroGesture.None;

    /// <summary>손목 스냅 + 지휘봉 튕김 + 반대손 펴기 (음 하나마다)</summary>
    public void Flick() => flick = 1f;

    /// <summary>지휘봉이 손에서부터 입자로 모여 생긴다 (seconds 동안). 아직 안 생긴 부분은 손 주위를 맴돈다</summary>
    public void FormBaton(float seconds)
    {
        batonForm = 0f;
        batonFormDur = Mathf.Max(0.05f, seconds);
    }

    /// <summary>
    /// 피격 반응 (시트 '피격/반응') — 몸이 뒤로 움찔, 손가락이 펴지고, 가장자리 입자 몇 개가 튀어 나갔다가 다시 흘러 들어온다.
    /// strength 0~1. 너무 잦으면 겹치지 않게 짧은 간격을 둔다
    /// </summary>
    public void Hit(float strength = 1f)
    {
        if (!started || Time.time < nextHitTime) return;
        nextHitTime = Time.time + 0.12f;
        hitPulse = Mathf.Max(hitPulse, Mathf.Clamp01(strength));
        BurstParticles(Mathf.RoundToInt(hitBurst * Mathf.Clamp01(strength)));
    }

    // 그림 봇 n개를 바깥으로 튕겨 내고 잠시 뒤 덩어리로 되돌린다 (빈자리는 남는 봇이 부드럽게 흘러와 채움)
    private void BurstParticles(int n, bool soft = false)
    {
        if (swarm == null || n <= 0 || slotBot.Length == 0) return;
        var bots = swarm.Bots;
        Vector2 center = BodyCenter;
        float now = Time.time;
        for (int tries = 0; tries < n * 4 && n > 0; tries++)
        {
            int k = UnityEngine.Random.Range(0, slotBot.Length);
            int i = slotBot[k];
            if (i < 0 || i >= bots.Length || bots[i].mode != BotMode.Free) continue;
            Vector2 away = (Vector2)bots[i].pos - center;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
            slotBot[k] = -1;
            // soft = 몸 분해(3악장): 위로 천천히 떠오르듯 떨어져 나가 조금 더 오래 머문다
            var v = soft ? new Vector3(away.x * 0.4f, 0.6f + 0.6f * UnityEngine.Random.value, 0f) * UnityEngine.Random.Range(0.6f, 1.2f)
                         : new Vector3(away.x, away.y + 0.5f, 0f) * UnityEngine.Random.Range(1f, 2.5f);
            swarm.Drop(i, v);
            swarm.SetGlow(i, soft ? cur.sparkleGlow * 0.5f : 0f);
            reabsorb.Add(new Pending { bot = i, at = now + (soft ? UnityEngine.Random.Range(0.7f, 1.4f) : UnityEngine.Random.Range(0.4f, 0.9f)) });
            n--;
        }
    }

    /// <summary>
    /// 몸을 모래로 부드럽게 무너뜨린다 — duration초에 걸쳐 위에서부터(약간 무작위) 차례로 부스러져 바깥으로 살짝 퍼지며 내려앉는다.
    /// includeReserve면 하체에 숨긴 남는 봇도. reabsorbAfter ≥ 0이면 떨어진 뒤 그만큼(+ 0~reabsorbSpread초) 지나 덩어리로 되돌려 다시 쌓이게 한다
    /// (악장 전환처럼 다른 곳이 되돌리면 -1). 무너지는 동안은 다시 채우지 않는다
    /// </summary>
    public void DropAll(float duration, float reabsorbAfter = -1f, float reabsorbSpread = 0f, bool includeReserve = true)
    {
        if (swarm == null) return;
        var bots = swarm.Bots;
        float now = Time.time;
        float top = (data != null ? data.artHeightPx : 150f) * cell * grow;
        float baseY = FeetWorld().y;
        void Queue(int i)
        {
            if (i < 0 || i >= bots.Length) return;
            float h01 = Mathf.Clamp01((bots[i].pos.y - baseY) / Mathf.Max(0.01f, top));
            float at = now + duration * ((1f - h01) * 0.75f + UnityEngine.Random.value * 0.25f); // 위에서부터
            float returnAfter = reabsorbAfter < 0f ? -1f : reabsorbAfter + UnityEngine.Random.value * reabsorbSpread;
            dissolving.Add(new Pending { bot = i, at = at, extra = returnAfter });
        }
        for (int k = 0; k < slotBot.Length; k++)
        {
            int i = slotBot[k];
            slotBot[k] = -1; // 자리에서 놓아 준다 — 떨어질 때까지 그 자리에 머문다(엔진이 마지막 자리를 붙잡음)
            if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Free) Queue(i);
        }
        if (includeReserve)
            foreach (int i in swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob)) Queue(i);
    }

    private struct Pending { public int bot; public float at, extra; }
    private readonly List<Pending> dissolving = new List<Pending>(4096);
    private readonly List<Pending> reabsorb = new List<Pending>(4096);
    private Vector3[] arriveFrom = Array.Empty<Vector3>();
    private float[] arriveT0 = Array.Empty<float>(), arriveDur = Array.Empty<float>();

    // 무너지는 봇을 시간이 되면 떨어뜨리고, 되돌릴 시간이 된 모래는 덩어리로
    private void TickDissolve(float now)
    {
        if (dissolving.Count > 0)
        {
            var bots = swarm.Bots;
            Vector2 center = BodyCenter;
            for (int n = dissolving.Count - 1; n >= 0; n--)
            {
                var d = dissolving[n];
                if (now < d.at) continue;
                dissolving.RemoveAt(n);
                int i = d.bot;
                if (i < 0 || i >= bots.Length || bots[i].mode == BotMode.Debris || bots[i].mode == BotMode.Dead) continue;
                Vector2 away = (Vector2)bots[i].pos - center;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                var v = new Vector3(away.x * scatterSpeed * UnityEngine.Random.Range(0.4f, 1f),
                                    UnityEngine.Random.Range(0.1f, 0.9f) * scatterSpeed, 0f);
                swarm.Drop(i, v);
                swarm.SetGlow(i, 0f);
                if (d.extra >= 0f) reabsorb.Add(new Pending { bot = i, at = now + d.extra });
            }
        }
        if (reabsorb.Count > 0)
        {
            var bots = swarm.Bots;
            for (int n = reabsorb.Count - 1; n >= 0; n--)
            {
                if (now < reabsorb[n].at) continue;
                int i = reabsorb[n].bot;
                reabsorb.RemoveAt(n);
                if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Debris) swarm.Bind(i, RSNanoSwarm.G_Blob);
            }
        }
    }

    // ───────────────────────── 매 프레임 ─────────────────────────

    private void LateUpdate()
    {
        if (!started || swarm == null) return;
        float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.05f);
        float t = Time.time;

        feet = Vector3.SmoothDamp(feet, feetTarget, ref feetVel, moveSmooth, Mathf.Infinity, dt);
        grow = Mathf.SmoothDamp(grow, growTarget, ref growVel, 0.35f, Mathf.Infinity, dt);
        back = Mathf.SmoothDamp(back, backTarget, ref backVel, 0.3f, Mathf.Infinity, dt);
        face = Mathf.SmoothDamp(face, faceTarget, ref faceVel, turnSmooth, Mathf.Infinity, dt);

        if (gesture != MaestroGesture.None)
        {
            gestureTime += dt;
            if (gestureLength > 0f && gestureTime >= gestureLength) gesture = MaestroGesture.None;
        }
        gestureWeight = Mathf.SmoothDamp(gestureWeight, gesture == MaestroGesture.None ? 0f : 1f, ref gestureWeightVel, 0.12f, Mathf.Infinity, dt);
        flick = Mathf.MoveTowards(flick, 0f, dt * 3.5f);
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 4f);
        batonForm = Mathf.MoveTowards(batonForm, 1f, dt / Mathf.Max(0.05f, batonFormDur));
        TickMovement(dt);

        Pose(t, dt);
        SimulateCloth(t, dt);
        TickDissolve(t);
        PlaceBots(t);

        swarm.Blob.center = new Vector3(RootWorld().x, RootWorld().y, PlaneZ);
        swarm.Blob.shapeScale = grow;
        UpdateFx(t);
    }

    // ── 이펙트 (시트: 바닥 그림자 타원·초승달 번짐·지휘봉 끝 발광) ──
    private MaestroFx fx;
    private SpriteRenderer fxShadow, fxFloorRing, fxMoonGlow, fxTipGlow;

    private void UpdateFx(float t)
    {
        if (fx == null)
        {
            fx = MaestroFx.Get(this);
            fxShadow = fx.Acquire(MaestroFx.Disc);
            fxFloorRing = fx.Acquire(MaestroFx.RingTex);
            fxMoonGlow = fx.Acquire(MaestroFx.Disc);
            fxTipGlow = fx.Acquire(MaestroFx.Disc);
        }
        bool visible = slotBot.Length > 0 && dissolving.Count == 0 && refillEnabled;
        float alive = visible ? 1f : 0f;
        float h = Height;
        float frontness = 1f - back;

        // 바닥 그림자 — 발밑(바닥 높이)에 납작한 어두운 타원 + 옅은 보라 고리
        var ground = new Vector3(feet.x + poseOffset.x, feet.y, PlaneZ);
        fx.Place(fxShadow, ground, new Vector2(ArtHalfWidth * 1.6f, h * 0.07f), new Color(0.05f, 0.04f, 0.1f, 0.55f * alive * frontness));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.3f);
        fx.Place(fxFloorRing, ground, new Vector2(ArtHalfWidth * 1.9f, h * 0.09f), new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, (0.12f + 0.08f * pulse) * alive * frontness));

        // 초승달 뒤 은은한 번짐
        if (iMoon >= 0)
        {
            Vector2 m = ArtToWorld(bonePos[iMoon] + new Vector2(-2f, 6f) * cell);
            fx.Place(fxMoonGlow, new Vector3(m.x, m.y, PlaneZ), Vector2.one * h * 0.22f, new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, (0.18f + 0.06f * pulse) * alive));
        }

        // 지휘봉 끝 — 몸짓 중·튕길 때 밝게
        float tipA = (0.15f + 0.45f * gestureWeight + 0.6f * Mathf.Sin(Mathf.Clamp01(flick) * Mathf.PI)) * batonForm * alive;
        fx.Place(fxTipGlow, BatonTip, Vector2.one * h * (0.06f + 0.05f * gestureWeight), new Color(MaestroFx.MoonWhite.r, MaestroFx.MoonWhite.g, MaestroFx.MoonWhite.b, tipA));

        UpdateAmbient(t, alive * frontness);
    }

    // ── 주변 음표(2·3악장) · 빛기둥(3악장) ──
    private readonly List<SpriteRenderer> ambientNotes = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> ambientBeams = new List<SpriteRenderer>();
    private float ambientFade;

    private void UpdateAmbient(float t, float visible)
    {
        int wantNotes = cur.ambientNotes, wantBeams = cur.beams;
        while (ambientNotes.Count < wantNotes) ambientNotes.Add(fx.Acquire(MaestroFx.Note));
        while (ambientBeams.Count < wantBeams) ambientBeams.Add(fx.Acquire(MaestroFx.Beam));
        ambientFade = Mathf.MoveTowards(ambientFade, visible, Time.deltaTime);
        float h = Height;
        Vector3 c = BodyCenter;

        // 음표: 몸 둘레 기운 타원을 천천히 돌며 위아래로 떠다님, 각자 반짝
        for (int n = 0; n < ambientNotes.Count; n++)
        {
            var r = ambientNotes[n];
            if (n >= wantNotes) { r.color = Color.clear; continue; }
            float ph = n / (float)Mathf.Max(1, wantNotes) * Mathf.PI * 2f + t * (0.35f + 0.05f * (n % 3)) * (cur.bpm / 54f);
            var p = c + new Vector3(Mathf.Cos(ph) * ArtHalfWidth * 1.5f, Mathf.Sin(ph) * h * 0.18f + Mathf.Sin(t * 1.7f + n) * h * 0.04f + h * 0.08f, 0f);
            float tw = 0.55f + 0.45f * Mathf.Sin(t * 3f + n * 1.9f);
            Color col = n % 2 == 0 ? MaestroFx.MoonWhite : MaestroFx.Lavender;
            col.a = tw * ambientFade * 0.9f;
            fx.Place(r, p, Vector2.one * h * 0.075f, col, 8f * Mathf.Sin(t * 1.3f + n));
        }

        // 빛기둥: 몸 양옆에서 솟았다 사라지는 세로 빛 (시트 3악장 광폭화)
        for (int n = 0; n < ambientBeams.Count; n++)
        {
            var r = ambientBeams[n];
            if (n >= wantBeams) { r.color = Color.clear; continue; }
            float cycle = 1.6f + 0.4f * (n % 3);
            float u = Mathf.Repeat(t / cycle + n * 0.37f, 1f);
            float side = (n % 2 == 0 ? -1f : 1f) * (0.5f + 0.9f * Mathf.Repeat(n * 0.618f, 1f));
            var p = new Vector3(c.x + side * ArtHalfWidth * 1.3f, feet.y + h * 0.6f, c.z);
            Color col = MaestroFx.Violet;
            col.a = Mathf.Sin(u * Mathf.PI) * 0.45f * ambientFade;
            fx.Place(r, p, new Vector2(h * 0.08f, h * (1.1f + 0.3f * u)), col);
        }
    }

    // 뼈 각도 (몸짓 목표 → 2차 스프링) → 그림 공간 관절 자리·각도
    private void Pose(float t, float dt)
    {
        int nb = data.bones.Length;
        float h = data.artHeightPx * cell;

        // 몸 전체 떠다니기 — 주기가 다른 두 흔들림(8자) + 느린 노이즈 (쿄다이 떠다니기와 같은 원리)
        float nx = Mathf.PerlinNoise(t * 0.3f, 0.17f) - 0.5f, ny = Mathf.PerlinNoise(0.63f, t * 0.3f) - 0.5f;
        rootArt = new Vector2(floatRatio.x * cur.floatScale * h * (Mathf.Sin(t * 2f * Mathf.PI / floatPeriods.x) + nx),
                              floatRatio.y * cur.floatScale * h * (Mathf.Sin(t * 2f * Mathf.PI / floatPeriods.y) + ny));

        Vector2 rootW = RootWorld();
        float speedX = dt > 0f ? (rootW.x - lastRootWorld.x) / dt : 0f;
        lastRootWorld = rootW;
        leanAng = Mathf.SmoothDamp(leanAng, Mathf.Clamp(-speedX * leanPerSpeed * Mathf.Sign(FlipX), -12f, 12f), ref leanVel, 0.25f, Mathf.Infinity, dt);

        var target = new float[nb];
        Targets(t, target);

        for (int b = 0; b < nb; b++)
        {
            var bone = data.bones[b];
            float x = target[b];
            // 팔로스루: 부모가 빨리 돌면 자식이 반대로 늦게
            if (bone.parent >= 0 && bone.followThrough > 0f) x -= angVel[bone.parent] * bone.followThrough;
            // 지휘봉은 단단한 막대 — 손에 그대로 붙인다 (스프링·팔로스루 없음. 11-24 사용자: 고무처럼 휜다)
            if (b == iBaton) { ang[b] = 0f; angVel[b] = 0f; angPrevTarget[b] = 0f; }
            else if (dt <= 0f) { ang[b] = x; angVel[b] = 0f; angPrevTarget[b] = x; }
            else SecondOrder(ref ang[b], ref angVel[b], ref angPrevTarget[b], x, dt, bone.frequency, bone.damping, bone.response);

            float parentAng = bone.parent >= 0 ? boneAng[bone.parent] : 0f;
            Vector2 parentPos = bone.parent >= 0 ? bonePos[bone.parent] : rootArt;
            bonePos[b] = parentPos + Rot(restOffset[b], parentAng);
            boneAng[b] = parentAng + ang[b] * Mathf.Deg2Rad;
        }
    }

    // 2차 동역학 (진동수 f, 감쇠 z, 반응 r) — 넘쳤다 돌아오고, r > 1이면 예비 동작
    private static void SecondOrder(ref float y, ref float yd, ref float xp, float x, float dt, float f, float z, float r)
    {
        float k1 = z / (Mathf.PI * f);
        float k2 = 1f / ((2f * Mathf.PI * f) * (2f * Mathf.PI * f));
        float k3 = r * z / (2f * Mathf.PI * f);
        float xd = (x - xp) / dt;
        xp = x;
        float k2s = Mathf.Max(k2, Mathf.Max(dt * dt / 2f + dt * k1 / 2f, dt * k1));
        y += dt * yd;
        yd += dt * (x + k3 * xd - y - k1 * yd) / k2s;
    }

    // 뼈마다 목표 각도 (도, 그림 기준 + = 반시계)
    private void Targets(float t, float[] target)
    {
        var idle = new float[(int)Ch.Count];
        IdleChannels(t, idle);
        var g = new float[(int)Ch.Count];
        GestureChannels(t, g, idle);
        var c = new float[(int)Ch.Count];
        for (int i = 0; i < c.Length; i++) c[i] = Mathf.Lerp(idle[i], g[i], gestureWeight);

        // Flick: 손목 스냅 + 지휘봉 + 반대손 펴기
        float fk = Mathf.Sin(Mathf.Clamp01(flick) * Mathf.PI);
        c[(int)Ch.BWrist] += 40f * fk;
        c[(int)Ch.Baton] += 15f * fk;
        c[(int)Ch.OCurl] -= 0.5f * fk;
        c[(int)Ch.Chest] += 2f * fk;

        // 피격: 상체·머리가 뒤로 젖혀지고(그림 기준 앞 = +), 팔이 움찔, 손가락이 펴진다 — 스프링이 넘쳤다 돌아오며 복귀
        float hp = hitPulse;
        c[(int)Ch.Chest] -= hitFlinch * hp;
        c[(int)Ch.Head] -= hitFlinch * 1.3f * hp;
        c[(int)Ch.BShoulder] -= hitFlinch * 1.2f * hp;
        c[(int)Ch.BElbow] += hitFlinch * 0.8f * hp;
        c[(int)Ch.OShoulder] += hitFlinch * hp;
        c[(int)Ch.OCurl] -= 0.6f * hp;

        // 지휘봉이 생기는 동안: 손이 살짝 위로 들리고 손가락이 그 주위를 감싸듯
        float forming = 1f - batonForm;
        c[(int)Ch.BWrist] -= 15f * forming;
        c[(int)Ch.BGrip] += 0.4f * forming;

        Set(target, iChest, c[(int)Ch.Chest] + leanAng);
        Set(target, iHead, c[(int)Ch.Head]);
        Set(target, iMoon, 4f * Mathf.Sin(t * 0.5f) - c[(int)Ch.Head] * 0.5f);
        Set(target, iBS, c[(int)Ch.BShoulder]);
        Set(target, iBE, c[(int)Ch.BElbow]);
        Set(target, iBW, c[(int)Ch.BWrist]);
        Set(target, iBaton, c[(int)Ch.Baton]);
        Set(target, iBGrip, -30f * (c[(int)Ch.BGrip] - 0.5f)); // 그림 = 반쯤 쥔 손 (0.5)
        Set(target, iOS, c[(int)Ch.OShoulder]);
        Set(target, iOE, c[(int)Ch.OElbow]);
        Set(target, iOW, c[(int)Ch.OWrist]);
        // 손가락: 손가락마다 조금씩 늦게·다르게 (오므림 = 시계방향)
        float curl = c[(int)Ch.OCurl] - 0.25f; // 그림 = 살짝 편 손 (0.25)
        Set(target, iF1, -fingerCurlDeg * curl + 6f * Mathf.Sin(t * 1.3f));
        Set(target, iF2, -fingerCurlDeg * curl * 1.1f + 6f * Mathf.Sin(t * 1.3f - 0.6f));
        Set(target, iF3, -fingerCurlDeg * curl * 1.2f + 6f * Mathf.Sin(t * 1.3f - 1.2f));
    }

    private static void Set(float[] a, int i, float v) { if (i >= 0) a[i] = v; }

    // 평소: 4박 지휘(마디마다 변주) + 숨쉬기 + 노이즈
    private void IdleChannels(float t, float[] c)
    {
        float beat = beatPhase; // 박 위상은 누적 — 악장이 바뀌어 빠르기가 변해도 튀지 않게
        int bar = Mathf.FloorToInt(beat / 4f);
        int b = Mathf.FloorToInt(beat) & 3;
        float f = beat - Mathf.Floor(beat);
        // 마디마다 크기·강세가 조금씩 다르다 (같은 반복으로 안 보이게)
        float barAmp = 0.8f + 0.4f * Mathf.PerlinNoise(bar * 0.71f, 0.3f);
        bool flourish = Mathf.PerlinNoise(bar * 0.37f, 5.1f) > 0.68f; // 가끔 크게 한 번
        float amp = cur.idleBeat * barAmp * (flourish && b == 0 ? 1.8f : 1f);
        float beatCurve = CatmullRom(BeatAngles[(b + 3) & 3], BeatAngles[b], BeatAngles[(b + 1) & 3], BeatAngles[(b + 2) & 3], Ease(f));
        float n1 = Mathf.PerlinNoise(t * 0.4f, 1.7f) - 0.5f, n2 = Mathf.PerlinNoise(t * 0.5f, 7.3f) - 0.5f;

        c[(int)Ch.Chest] = 1.5f * Mathf.Sin(t * 1.15f) + 3f * n1;
        c[(int)Ch.Head] = (b == 0 ? 4f * Mathf.Sin(f * Mathf.PI) : 0f) + 5f * n2;
        c[(int)Ch.BShoulder] = amp * beatCurve;
        c[(int)Ch.BElbow] = amp * 0.6f * beatCurve;
        c[(int)Ch.BWrist] = 10f * Mathf.Sin((beat + 0.25f) * Mathf.PI * 2f) * barAmp;
        c[(int)Ch.Baton] = 4f * Mathf.Sin((beat + 0.4f) * Mathf.PI * 2f);
        c[(int)Ch.BGrip] = 0.5f + 0.1f * Mathf.Sin(t * 0.8f);
        c[(int)Ch.OShoulder] = 7f * Mathf.Sin(t * 0.55f) + 4f * n2;
        c[(int)Ch.OElbow] = 10f * Mathf.Sin(t * 0.55f + 0.8f);
        c[(int)Ch.OWrist] = 12f * Mathf.Sin(t * 0.7f + 1.6f);
        c[(int)Ch.OCurl] = 0.25f + 0.2f * Mathf.Sin(t * 0.9f);
    }

    private void GestureChannels(float t, float[] c, float[] idle)
    {
        float u = gestureLength > 0f ? Mathf.Clamp01(gestureTime / gestureLength) : 0f;
        switch (gesture)
        {
            case MaestroGesture.Swing:
                EvalKeys(SwingKeys, u, c);
                return;
            case MaestroGesture.RaiseBoth:
                EvalKeys(RaiseKeys, Mathf.Clamp01(gestureTime / Mathf.Min(1.2f, Mathf.Max(0.3f, gestureLength))), c);
                // 들고 있는 동안 떨림 (힘이 들어간 손가락·팔)
                c[(int)Ch.OShoulder] += 2.5f * Mathf.Sin(t * 9f);
                c[(int)Ch.BShoulder] += 2f * Mathf.Sin(t * 8f + 1f);
                c[(int)Ch.OCurl] += 0.08f * Mathf.Sin(t * 13f);
                return;
            case MaestroGesture.ConductFast:
            {
                float ph = gestureTime * Mathf.PI * 2f * 1.6f;
                float s = Mathf.Sin(ph);
                c[(int)Ch.Chest] = 6f * s;
                c[(int)Ch.Head] = 5f * Mathf.Sin(ph - 0.5f);
                c[(int)Ch.BShoulder] = Mathf.Lerp(-30f, 70f, 0.5f + 0.5f * s);
                c[(int)Ch.BElbow] = 30f * Mathf.Sin(ph - 0.9f);
                c[(int)Ch.BWrist] = 30f * Mathf.Sin(ph - 1.6f);
                c[(int)Ch.Baton] = 10f * Mathf.Sin(ph - 2f);
                c[(int)Ch.BGrip] = 1f;
                c[(int)Ch.OShoulder] = Mathf.Lerp(70f, -10f, 0.5f + 0.5f * s);
                c[(int)Ch.OElbow] = 25f * Mathf.Sin(ph + 0.7f);
                c[(int)Ch.OWrist] = 15f * Mathf.Sin(ph + 0.2f);
                c[(int)Ch.OCurl] = 0.8f + 0.2f * s; // 주먹을 쥐고 박자마다 꽉
                return;
            }
            default:
                Array.Copy(idle, c, c.Length);
                return;
        }
    }

    // 키 사이를 가속·감속 곡선으로
    private static void EvalKeys(Key[] keys, float u, float[] c)
    {
        int k = 0;
        while (k < keys.Length - 2 && u > keys[k + 1].t) k++;
        float a = keys[k].t, b = keys[k + 1].t;
        float s = b > a ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - a) / (b - a))) : 1f;
        for (int i = 0; i < c.Length && i < keys[k].v.Length; i++) c[i] = Mathf.Lerp(keys[k].v[i], keys[k + 1].v[i], s);
    }

    // 천: 붙은 뼈 기준 원래 모양으로 끌림(뿌리 강·끝 약) + 중력 + 바람 + 관성(베를레) + 길이 유지
    private void SimulateCloth(float t, float dt)
    {
        float s = grow;
        for (int c = 0; c < cloth.Length; c++)
        {
            var def = data.cloths[c];
            var sim = cloth[c];
            int n = sim.pos.Length;
            if (n < 2) continue;
            float pAng = def.parentBone >= 0 ? boneAng[def.parentBone] : 0f;
            Vector2 pPos = def.parentBone >= 0 ? bonePos[def.parentBone] : rootArt;

            if (!sim.init)
            {
                for (int i = 0; i < n; i++) sim.pos[i] = sim.prev[i] = ArtToWorld(pPos + Rot(sim.restLocal[i], pAng));
                sim.init = true;
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 rest = ArtToWorld(pPos + Rot(sim.restLocal[i], pAng));
                if (i == 0) { sim.prev[0] = sim.pos[0]; sim.pos[0] = rest; continue; } // 뿌리 고정
                float u = i / (float)(n - 1);
                float k = Mathf.Lerp(def.stiffnessRoot, def.stiffnessTip, u);
                float wph = t * def.windSpeed + c * 3.1f + i * 0.35f;
                Vector2 wind = new Vector2(Mathf.PerlinNoise(wph, c * 1.7f) - 0.5f, Mathf.PerlinNoise(c * 2.3f, wph) - 0.5f) * (2f * def.wind * cur.windScale * u);
                Vector2 acc = (rest - sim.pos[i]) * k + new Vector2(0f, -def.gravity * u) + wind * s;
                Vector2 vel = (sim.pos[i] - sim.prev[i]) * (1f - def.drag);
                sim.prev[i] = sim.pos[i];
                sim.pos[i] += vel + acc * dt * dt;
            }
            // 길이 유지 (뿌리부터)
            for (int it = 0; it < 3; it++)
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 d = sim.pos[i + 1] - sim.pos[i];
                    float len = d.magnitude;
                    if (len < 1e-6f) continue;
                    float want = sim.restLen[i] * s;
                    Vector2 fix = d * ((len - want) / len);
                    if (i == 0) sim.pos[i + 1] -= fix;
                    else { sim.pos[i] += fix * 0.5f; sim.pos[i + 1] -= fix * 0.5f; }
                }
        }
    }

    // 픽셀 자리(월드) → 봇. 잃은 자리는 덩어리에서 다시 가져와 아래부터 채운다
    private void PlaceBots(float t)
    {
        var bots = swarm.Bots;
        float z = PlaneZ;
        int missing = 0;
        for (int k = 0; k < slotBot.Length; k++)
        {
            int i = slotBot[k];
            if (i >= 0 && (i >= bots.Length || bots[i].mode != BotMode.Free)) { slotBot[k] = -1; i = -1; }
            if (i < 0) { missing++; continue; }
            Vector2 w = PixelWorld(k, t);
            var p = new Vector3(w.x, w.y, z);
            bots[i].hold = p;
            bots[i].wait = 9999f;
            if (slotArriving[k])
            {
                // 흘러오는 중 — 휘어진 궤적(옆으로 살짝 + 위로 볼록)을 천천히 출발해 천천히 도착 (엔진 스프링에 맡기면 순간이동처럼 빠르다)
                float u = Mathf.Clamp01((t - arriveT0[k]) / Mathf.Max(0.05f, arriveDur[k]));
                if (u >= 1f) slotArriving[k] = false;
                else
                {
                    float e = u * u * (3f - 2f * u);
                    Vector3 a = arriveFrom[k];
                    Vector3 mid = (a + p) * 0.5f;
                    Vector3 d = p - a;
                    float bend = gatherCurve * Height * (pixHash[k] - 0.5f) * 2f;
                    Vector3 ctrl = mid + new Vector3(-d.y, d.x, 0f).normalized * bend + Vector3.up * Mathf.Abs(bend) * 0.5f;
                    Vector3 q = Vector3.Lerp(Vector3.Lerp(a, ctrl, e), Vector3.Lerp(ctrl, p, e), e);
                    bots[i].pos = q;
                    bots[i].hold = q;
                    bots[i].vel = Vector3.zero;
                    continue;
                }
            }
            bots[i].pos = p;
            bots[i].vel = Vector3.zero;
        }

        if (missing == 0 || !refillEnabled || dissolving.Count > 0) return; // 무너지는 동안은 다시 채우지 않는다
        swarm.Collect(RSNanoSwarm.G_Blob, new Vector3(RootWorld().x, RootWorld().y, z), Mathf.Min(missing, refillPerFrame), tmp);
        int next = 0;
        for (int o = 0; o < fillOrder.Length && next < tmp.Count; o++)
        {
            int k = fillOrder[o];
            if (slotBot[k] >= 0) continue;
            Assign(k, tmp[next++], snap: false);
        }
    }

    private void Assign(int k, int bot, bool snap)
    {
        Vector2 w = PixelWorld(k, Time.time);
        var p = new Vector3(w.x, w.y, PlaneZ);
        swarm.Fire(bot, p, 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, snap: snap, clump: 1);
        swarm.SetTint(bot, data.pixColor[k]);
        byte kind = data.pixKind[k];
        swarm.SetGlow(bot, kind == (byte)MaestroPuppetData.PixelKind.Sparkle ? cur.sparkleGlow : kind == (byte)MaestroPuppetData.PixelKind.Glow ? cur.moonGlow : 0f);
        slotBot[k] = bot;
        slotArriving[k] = !snap;
        if (!snap)
        {
            arriveFrom[k] = swarm.Bots[bot].pos;
            arriveT0[k] = Time.time;
            arriveDur[k] = Mathf.Lerp(gatherSeconds.x, gatherSeconds.y, pixHash[k]);
        }
    }

    private Vector2 PixelWorld(int k, float t)
    {
        int owner = data.pixOwner[k];
        if (owner >= 0)
        {
            Vector2 art = bonePos[owner] + Rot(pixLocal[k], boneAng[owner]);
            if (owner == iBaton && batonForm < 1f)
            {
                // 아직 안 생긴 부분은 손잡이 주위를 맴돌다가, 생기는 앞자리가 지나가면 제자리로 날아간다
                float front = batonForm * 1.25f;
                float w = Mathf.Clamp01((front - batonU[k]) / 0.25f);
                float ph = pixHash[k];
                float orbit = t * (6f + 4f * ph) + ph * 6.283f;
                Vector2 swirl = bonePos[owner] + new Vector2(Mathf.Cos(orbit), Mathf.Sin(orbit)) * (3f + 3f * ph) * cell;
                art = Vector2.Lerp(swirl, art, w * w * (3f - 2f * w));
            }
            return ArtToWorld(art);
        }
        if (owner == -1)
        {
            Vector2 p = rootArt + pixLocal[k];
            byte kind = data.pixKind[k];
            float h = pixHash[k];
            if (kind == (byte)MaestroPuppetData.PixelKind.Drip)
            {
                // 아랫단 조각: 계속 흘러내리고 다시 위에서
                float fall = Mathf.Repeat(dripPhase * (0.6f + 0.8f * h) + h * dripRangePx, dripRangePx);
                p += new Vector2(Mathf.Sin(t * 1.7f + h * 20f) * 0.8f * cell, -fall * cell);
            }
            else if (kind == (byte)MaestroPuppetData.PixelKind.Sparkle)
            {
                p += new Vector2(Mathf.Sin(t * (0.6f + h) + h * 30f) * 2f, Mathf.Cos(t * (0.5f + 0.7f * h) + h * 17f) * 3f) * cell;
            }
            return ArtToWorld(p);
        }
        // 천
        var sim = cloth[(int)pixLocal[k].x];
        int j = Mathf.Clamp(clothSeg[k], 0, sim.pos.Length - 2);
        Vector2 a = sim.pos[j], b = sim.pos[j + 1];
        Vector2 dir = b - a;
        float len = dir.magnitude;
        dir = len > 1e-6f ? dir / len : Vector2.down;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        return Vector2.Lerp(a, b, clothT[k]) + perp * (clothD[k] * grow * Mathf.Sign(FlipX));
    }

    // ── 좌표 ──
    private Vector2 ArtPx(Vector2 px) => new Vector2((px.x - data.feetXPx) * cell, (data.artHeightPx - px.y) * cell);
    private static Vector2 Rot(Vector2 v, float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y); }
    private Vector2 FeetWorld() => new Vector2(feet.x + poseOffset.x, feet.y + poseOffset.y - sinkPx * cell * grow);
    private Vector2 RootWorld() => ArtToWorld(rootArt);
    private Vector2 ArtToWorld(Vector2 art) { Vector2 f = FeetWorld(); return new Vector2(f.x + art.x * FlipX * grow, f.y + art.y * grow); }

    // 4박 지휘 각도 (평소 크기 1 기준) — 1 아래(+) · 2 안 · 3 밖 · 4 위(-)
    private static readonly float[] BeatAngles = { 1.2f, -0.3f, 0.4f, -1f };

    private static float Ease(float f) => f < 0.7f ? Mathf.SmoothStep(0f, 0.55f, f / 0.7f) : Mathf.Lerp(0.55f, 1f, Mathf.Pow((f - 0.7f) / 0.3f, 0.6f));

    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
