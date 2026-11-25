using System.Collections.Generic;
using RealSteel.Boss.Core;
using RealSteel.Boss.FSM;
using RealSteel.Boss.Visual;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>
/// 지역 3 보스 「마에스트로: 월광」 — 1단계 뼈대 (2026-11-19, 기획서 `07.Md/보스3_월광_기획.md`).
///
/// KKH BossControllerBase를 그대로 탄다 — HP·FSM(딜타임 → 패턴 → 딜타임)·그로기/스턴은 KKH 것, 여기는 마에스트로 전용만:
///   · 몸: 나노봇 군집(RSNanoSwarm, KKH 폴더 엔진 — 참조만)의 덩어리. 판정은 루트 BoxCollider2D(덩어리 크기에 맞춤)
///   · 악장(= KKH 페이즈): 악장마다 HP 100%를 새로 가진다. 마지막 악장이 아니면 HP가 0이 되는 대신 1에서 고정 →
///     무너짐 → 다시 쌓기(HP 1 → 100%) → 다음 악장 (기획서 0.4). 진행 악장 수: 헬 3, 나머지 2
///   · HP 문턱으로 페이즈를 넘기는 KKH 규칙(CheckPhaseTransition)은 쓰지 않는다
///   · 전환 진행은 전투 틱(CombatClock)에서 센다. 군집의 그림 시뮬레이션은 엔진이 LateUpdate로 돌린다(그림만)
///
/// 패턴: 자식의 BossPatternGimmickBase를 등록 — 음표 공격(2단계)·달빛 커튼(3단계). 패턴이 쓴 봇의 모래 → 재흡수는 ReleaseToSand(공용).
/// 거대 지휘봉(4단계)은 SetBodyPose로 몸을 키우고 뒤판으로 보낸다 (뒤판이면 본체 판정 꺼짐).
/// 아직 없는 것: 순환 순서, 첫 큐, 헬 2→3악장 가짜 격파 연출(0.5, 지금은 일반 전환과 같다), 그로기 기믹.
/// 플레이어 → 보스 피해는 HitDetection → 허브 ProcessHit(보스 모드)가 깎는다 — 여기서 NotifyDamageFromHub를 부르지 않는다.
/// </summary>
/// <summary>마에스트로 패턴 중 특정 악장부터 나오는 것 (원형 음탄·크레셴도 = 2악장 후보)</summary>
public interface IMaestroMovementGate
{
    int MinMovement { get; }
}

[RequireComponent(typeof(RSNanoSwarm))]
[RequireComponent(typeof(BoxCollider2D))]
public class MaestroBossController : BossControllerBase
{
    private enum TransitionStep { None, Collapse, Rebuild }

    [Tooltip("바닥 높이 y(월드) — 플레이어가 서 있는 높이. 군집 모래도 여기 쌓인다")]
    [SerializeField] private float groundY;

    [Tooltip("외형 (2026-11-24 사용자 참고 그림 → 부위 그림 + 몸짓). 비우면 자식에서 찾고, 없으면 아래 덩어리 몸을 쓴다")]
    [SerializeField] private MaestroLook look;

    [Header("몸 (외형이 없을 때만 — 나노봇 덩어리)")]
    [Tooltip("TODO: 임시값. 덩어리 가로 : 세로 : 깊이 비율 — 실제 크기는 봇 수로 정해진다. 지휘자라 세로로 길게")]
    [SerializeField] private Vector3 blobShape = new Vector3(2.2f, 3.0f, 0.9f);

    [Tooltip("몸 크기 배율 — 봇 수로 정해진 덩어리 크기에 곱한다 (2026-11-19 사용자: 20% 줄임 → 0.8). 맞는 범위도 같이 줄어든다")]
    [Range(0.3f, 1.5f)] [SerializeField] private float bodyScale = 0.8f;

    [Tooltip("TODO: 임시값. 맞는 범위 = 덩어리 크기 × 이 값 (덩어리 가장자리는 성겨서 조금 작게)")]
    [Range(0.3f, 1.2f)] [SerializeField] private float hurtboxScale = 0.8f;

    [Header("악장 전환 (기획서 0.4 — 시간은 기획 수치 없음)")]
    [Tooltip("TODO: 임시값. ② 무너짐 — 몸이 모래로 무너지는 프레임")]
    [Min(1)] [SerializeField] private int collapseFrames = 90;

    [Tooltip("TODO: 임시값. ③ 다시 쌓기 — 모래가 다시 몸이 되는 동안 HP 게이지가 1 → 100%로 차오르는 프레임")]
    [Min(1)] [SerializeField] private int rebuildFrames = 180;

    [Tooltip("무너질 때 봇이 튀는 속도 범위(월드/초) — 그림만")]
    [SerializeField] private Vector2 collapseKick = new Vector2(1.5f, 4f);

    private const string AnimationHolderName = "KKH 애니메이션 (방향 전환용)";

    private RSNanoSwarm swarm;
    private BoxCollider2D hurtbox;
    private TransitionStep step = TransitionStep.None;
    private int stepFrame;
    private int rebindNext;
    private readonly List<int> debris = new List<int>();
    private readonly List<int> tmp = new List<int>();
    private bool subscribed;
    private bool defeated;

    public RSNanoSwarm Swarm => swarm;
    public override float GroundY => groundY;

    /// <summary>지금 악장 (1부터) = KKH 페이즈</summary>
    public int Movement => Snapshot != null ? Snapshot.currentPhase : 1;

    /// <summary>이 난이도에서 진행하는 악장 수. 기획서 0.4: 아주 쉬움~어려움 1→2, 헬 1→2→3</summary>
    public int MovementCount => Snapshot != null && Snapshot.difficulty == BossDifficulty.Hell ? 3 : 2;

    public bool InTransition => step != TransitionStep.None;

    /// <summary>외형 (부위 그림 + 몸짓). 없으면 null — 덩어리 몸</summary>
    public MaestroLook Look => look;

    /// <summary>몸 중심 (외형이면 몸통 판정 중심, 아니면 덩어리 중심)</summary>
    public Vector3 BodyCenter => look != null && look.Ready ? look.BodyCenter : swarm != null ? swarm.Blob.center : transform.position;

    /// <summary>패턴이 몸에서 손을 뻗을 자리 — 외형이면 지휘봉 끝, 아니면 덩어리 위 앞쪽</summary>
    public Vector3 HandPoint(int side)
    {
        if (look != null && look.Ready) return look.BatonTip;
        if (swarm == null) return transform.position;
        return swarm.Blob.center + new Vector3(side * swarm.Blob.radius.x * 0.8f, swarm.Blob.radius.y * 0.5f, 0f);
    }

    /// <summary>
    /// 패턴이 빌려 쓸 수 있는 봇 수. 외형이면 남는 봇(몸에 겹쳐 숨긴 봇)에서 10%만 남기고, 덩어리 몸이면 몸이 전체의 keepRatio 아래로 줄지 않게
    /// </summary>
    public int SpareBots(float keepRatio)
    {
        if (swarm == null) return 0;
        if (look != null && look.Ready) return BodyBotCount - Mathf.CeilToInt(look.ReserveCount * 0.1f);
        return BodyBotCount - Mathf.CeilToInt(swarm.Count * keepRatio);
    }

    // ───────────────────────── 초기화 ─────────────────────────

    // KKH FacePlayer는 애니메이션 컨트롤러가 붙은 오브젝트의 스케일 x를 뒤집는다. 루트에 붙으면 군집까지 거울로 뒤집히므로
    // 쿄다이처럼 그림 없는 빈 자식에 붙인다 (나노봇 몸은 좌우를 리그가 따로 정한다)
    protected override void SetupAnimationController()
    {
        if (animationController != null && animationController.transform != transform) return;
        Transform holder = transform.Find(AnimationHolderName);
        if (holder == null)
        {
            holder = new GameObject(AnimationHolderName).transform;
            holder.SetParent(transform, false);
        }
        var controller = holder.GetComponent<BossAnimationController>();
        if (controller == null) controller = holder.gameObject.AddComponent<BossAnimationController>();
        animationController = controller;
    }

    protected override void RegisterGimmicks()
    {
        // 꺼둔 패턴(오브젝트 비활성·컴포넌트 체크 해제)은 등록하지 않는다
        foreach (var gimmick in GetComponentsInChildren<BossGimmickBase>(false))
            if (gimmick.enabled) RegisterGimmick(gimmick);
        if (activePatternPool.Count == 0)
            Debug.Log("[Maestro] 패턴 기믹이 아직 없음 — 딜타임만 반복한다 (1단계 뼈대)");
    }

    protected override void OnBossInitialized()
    {
        defeated = false;
        step = TransitionStep.None;
        swarm = GetComponent<RSNanoSwarm>();
        if (look == null) look = GetComponentInChildren<MaestroLook>(true);
        hurtbox = GetComponent<BoxCollider2D>();
        hurtbox.isTrigger = true;
        BuildBody();
        Debug.Log($"[Maestro] 시작 — 난이도 {SelectedDifficulty}, 악장 {MovementCount}개, 악장 HP {Snapshot?.maxHp}");
    }

    // 덩어리 몸을 새로 만든다. 크기는 봇 수 × 격자 간격으로 정해진다
    private void BuildBody()
    {
        if (swarm == null) return;
        if (swarm.shader == null) swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
        swarm.floorFromTransform = false;
        swarm.floorY = groundY;
        if (look != null)
        {
            // 외형: 그림 부위 + 발밑 웅덩이. 처음엔 플레이어를 본다
            look.standalone = false;
            ResetBodyPose();
            Transform player = PlayerTransform;
            float face = player != null ? Mathf.Sign(player.position.x - transform.position.x) : -1f;
            look.Init(new Vector3(transform.position.x, groundY, transform.position.z), face);
            look.SetMovement(Movement, instant: true);
            SyncHurtbox();
            return;
        }
        swarm.Blob.aspect = blobShape.x / Mathf.Max(0.01f, blobShape.y);
        baseRadius = swarm.BlobSize(swarm.botCount, blobShape) * bodyScale;
        ResetBodyPose();
        swarm.Blob.radius = baseRadius;
        swarm.Blob.center = BlobCenterAt(transform.position.x);
        swarm.Build(swarm.Blob.center);
        swarm.ResetBotLooks();
        SyncHurtbox();
    }

    private Vector3 BlobCenterAt(float x) => new Vector3(x, groundY + swarm.Blob.radius.y, transform.position.z);

    // ───────────────────────── 몸 자세 (패턴이 바꾼다 — 거대 지휘봉 등) ─────────────────────────

    private Vector3 baseRadius;
    private Vector3 poseOffset;
    private float poseGrow = 1f;
    private bool poseBack;

    /// <summary>몸 자세: 원래 자리에서 offset(월드, x·y) 만큼, 크기 grow 배, back이면 뒤판(배경)에 그린다. 봇은 스프링으로 흘러간다</summary>
    public void SetBodyPose(Vector2 offset, float grow, bool back)
    {
        poseOffset = offset;
        poseGrow = Mathf.Max(0.1f, grow);
        poseBack = back;
        if (look != null) look.SetPose(offset, poseGrow, back);
    }

    public void ResetBodyPose() => SetBodyPose(Vector2.zero, 1f, false);

    /// <summary>지금 몸 자세 (모션 편집기가 보스 이동 미리보기 뒤 되돌릴 때)</summary>
    public void GetBodyPose(out Vector2 offset, out float grow, out bool back)
    {
        offset = poseOffset; grow = poseGrow; back = poseBack;
    }

    /// <summary>몸 깊이(z): 0 = 앞판, 1 = 뒤판, 음수 = 원래대로 — 판정은 SetBodyPose의 back으로 따로 켜고 끈다</summary>
    public void SetBodyDepth(float depth) { if (look != null) look.SetDepth(depth); }

    /// <summary>뒤판(배경)에 있어 공격을 안 맞는 중</summary>
    public bool IsBodyBack => poseBack;

    /// <summary>지금 자세 기준 몸 중심 목표 (그림)</summary>
    private Vector3 PosedCenter()
    {
        Vector3 c = BlobCenterAt(transform.position.x) + poseOffset;
        if (poseBack) c.z = swarm.BackZ;
        return c;
    }

    private void OnEnable() => Subscribe();

    private void Start() => Subscribe(); // Awake 순서상 OnEnable 때 CombatClock이 늦게 생겨도 한 번 더

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null && subscribed) clock.OnCombatTick -= CombatTick;
        subscribed = false;
    }

    private void Subscribe()
    {
        if (subscribed || !Application.isPlaying) return;
        CombatClock.Instance.OnCombatTick += CombatTick;
        subscribed = true;
    }

    // ───────────────────────── 틱·그림 ─────────────────────────

    private void CombatTick()
    {
        TickSand();
        TickTransition();
    }

    // ───────────────────────── 패턴 봇 → 모래 → 재흡수 (패턴 공용) ─────────────────────────

    private struct PendingSand { public int bot, frames; }
    private readonly List<PendingSand> pendingSand = new List<PendingSand>(4096);

    /// <summary>
    /// 패턴이 쓰던 봇(날던 봇)을 모래로 떨어뜨리고 delayFrames 틱 뒤 몸(덩어리)으로 재흡수한다 (나노봇 원칙: 붕괴 → 흐름).
    /// 악장 전환 중이거나 격파 뒤엔 재흡수하지 않는다 — 전환이 모래 전부를 다시 쌓는다
    /// </summary>
    public void ReleaseToSand(int bot, Vector3 vel, int delayFrames)
    {
        if (swarm == null || bot < 0 || bot >= swarm.Count) return;
        var mode = swarm.Bots[bot].mode;
        if (mode == BotMode.Free || mode == BotMode.Bound) swarm.Drop(bot, vel); // 날던 봇 · 패턴이 다른 가이드(지휘봉 줄기 등)에 붙인 봇
        swarm.SetGlow(bot, 0f);
        if (!InTransition && !defeated) pendingSand.Add(new PendingSand { bot = bot, frames = delayFrames });
    }

    /// <summary>몸(덩어리)에 붙어 있는 봇 수 — 패턴이 몸을 너무 깎지 않게 (달빛 커튼 등)</summary>
    public int BodyBotCount => swarm != null ? swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob).Count : 0;

    private void TickSand()
    {
        if (pendingSand.Count == 0) return;
        if (swarm == null || InTransition || defeated) { pendingSand.Clear(); return; }
        for (int k = pendingSand.Count - 1; k >= 0; k--)
        {
            var s = pendingSand[k];
            if (--s.frames > 0) { pendingSand[k] = s; continue; }
            if (s.bot < swarm.Count && swarm.Bots[s.bot].mode == BotMode.Debris) swarm.Bind(s.bot, RSNanoSwarm.G_Blob);
            pendingSand.RemoveAt(k);
        }
    }

    // KKH가 루트를 뒤집어도(음수 스케일) 되돌리고, 몸·판정을 루트 x에 맞춘다 (그림 — 판정 콜라이더는 다음 틱에 이 자리로 쓰인다)
    protected override void LateUpdate()
    {
        base.LateUpdate();
        Vector3 s = transform.localScale;
        if (s.x < 0f) transform.localScale = new Vector3(-s.x, s.y, s.z);
        if (swarm == null) return;
        if (look != null && look.Ready)
        {
            // 외형: 발밑을 루트에 맞추고, 전환 중이 아니면 플레이어 쪽을 본다 (돌아설 때 부드럽게 뒤집힘)
            look.SetFeet(new Vector3(transform.position.x, groundY, transform.position.z));
            Transform player = PlayerTransform;
            if (!InTransition && !defeated && player != null) look.SetFacing(player.position.x - transform.position.x);
        }
        else if (!InTransition)
        {
            swarm.Blob.radius = baseRadius * poseGrow;
            swarm.Blob.center = PosedCenter();
        }
        SyncHurtbox();
    }

    // 맞는 범위 = 외형이면 몸통·머리 판정 사각형, 아니면 덩어리 크기 (루트 스케일 1 기준 로컬)
    private void SyncHurtbox()
    {
        if (hurtbox == null || swarm == null) return;
        if (look != null && look.Ready)
        {
            Rect r = look.HurtRect;
            hurtbox.offset = r.center - (Vector2)transform.position;
            hurtbox.size = r.size * (hurtboxScale / 0.8f); // 0.8 = 기본값 → 판정 사각형 그대로
            hurtbox.enabled = !poseBack;
            return;
        }
        Vector3 local = swarm.Blob.center - transform.position;
        hurtbox.offset = new Vector2(local.x, local.y);
        hurtbox.size = new Vector2(swarm.Blob.radius.x, swarm.Blob.radius.y) * (2f * hurtboxScale);
        hurtbox.enabled = !poseBack; // 배경(뒤판)에 있는 동안은 아예 안 맞는다 (거대 지휘봉 ②~④ — 때리는 건 지휘봉)
    }

    public override float GetBossVisualHalfWidth()
    {
        float half = look != null && look.Ready ? look.ArtHalfWidth
                   : swarm != null ? swarm.Blob.radius.x * hurtboxScale : 1f;
        return half + cameraEdgePadding;
    }

    // ───────────────────────── 피해·악장 HP 고정 ─────────────────────────

    // 전환 중(무적)에는 직접 호출 피해도 막는다 — 허브 경유 피해는 허브가 isInvincible을 보고 막는다
    public override void TakeDamage(int rawDamage, bool isAlreadyCalculated = false)
    {
        if (Snapshot != null && Snapshot.isInvincible) return;
        base.TakeDamage(rawDamage, isAlreadyCalculated);
    }

    // 허브 경유(HitDetection)든 TakeDamage든 피해 뒤에는 여기를 지난다.
    // 허브는 이 알림(OnBossDamaged) 뒤에 사망 검사를 하므로, 여기서 HP를 1로 되돌리면 허브도 격파로 치지 않는다
    protected override void ProcessPostDamageReactions(int finalDamage)
    {
        if (!defeated && Snapshot != null && Snapshot.currentHp <= 0 && Movement < MovementCount)
        {
            BeginMovementTransition();
            return;
        }
        // 피격 반응 (그림만) — 큰 피해일수록 크게 (최대 HP의 2%면 최대)
        if (look != null && look.Ready && finalDamage > 0 && Snapshot != null)
            look.Hit(Mathf.Clamp(finalDamage / Mathf.Max(1f, Snapshot.maxHp * 0.02f), 0.35f, 1f));
        base.ProcessPostDamageReactions(finalDamage);
    }

    // 악장은 HP 문턱이 아니라 "HP 1 고정 → 전환"으로만 넘어간다
    protected override void CheckPhaseTransition() { }

    // 패턴 고르기 — 지금 악장에 나올 수 있는 것만, 이번 악장에 새로 나오는 패턴을 앞에 (KKH는 쉬운 난이도에서 앞의 N개만 쓴다).
    // 그 안에서는 순서대로 돌린다. 기획서 1.1의 순환 순서(첫 큐 → 음표 → 커튼 → 음표 → 지휘봉)는 아직 — 5단계
    private readonly List<BossPatternGimmickBase> patternChoices = new List<BossPatternGimmickBase>();
    private int patternCursor;

    // ── 디버그 패널용 (MaestroDebugPanel) ──
    private BossPatternGimmickBase forcedPattern;
    /// <summary>켜면 자동으로 패턴을 고르지 않는다 (디버그 — 몸짓·연출만 볼 때)</summary>
    [System.NonSerialized] public bool debugPauseAuto;
    public BossPatternGimmickBase LastPattern { get; private set; }
    public IReadOnlyList<BossPatternGimmickBase> DebugPatterns => activePatternPool;

    /// <summary>디버그: 진행 중이던 패턴을 끊고 이 패턴을 바로 실행 (악장 전환·격파 중엔 안 함)</summary>
    public void DebugRunPattern(BossPatternGimmickBase pattern)
    {
        if (pattern == null || InTransition || defeated || Snapshot == null || !Snapshot.IsAlive) return;
        foreach (var p in activePatternPool) if (p != null) p.InterruptPattern(InterruptReason.Manual);
        forcedPattern = pattern;
        StateMachine?.ChangeState(new BossIdleState(this, RenewStateCancellationToken(), 0.05f));
    }

    /// <summary>디버그: 이번 악장 HP를 0으로 (악장 전환·격파 확인)</summary>
    public void DebugFinishMovement()
    {
        if (!Application.isPlaying || Snapshot == null || InTransition || defeated) return;
        TakeDamage(Snapshot.currentHp, true);
    }

    public override BossPatternGimmickBase GetNextPatternGimmick()
    {
        if (forcedPattern != null) { var f = forcedPattern; forcedPattern = null; LastPattern = f; return f; }
        if (debugPauseAuto) return null;
        patternChoices.Clear();
        int m = Movement;
        for (int pass = 0; pass < 2; pass++)
            foreach (var p in activePatternPool)
            {
                if (p == null) continue;
                int min = p is IMaestroMovementGate g ? g.MinMovement : 1;
                if (min > m) continue;                       // 아직 안 나오는 악장
                bool fresh = min == m && m > 1;               // 이번 악장에 새로 나오는 패턴
                if ((pass == 0) != fresh) continue;
                if (Snapshot != null && Snapshot.IsSkillSealed(p.GimmickID)) continue;
                patternChoices.Add(p);
            }
        if (patternChoices.Count == 0) return null;
        int allowed = Snapshot != null ? Mathf.Clamp(Snapshot.maxAllowedPatterns, 1, patternChoices.Count) : patternChoices.Count;
        var pick = patternChoices[patternCursor % allowed];
        patternCursor++;
        LastPattern = pick;
        return pick;
    }

    // ① HP 1 고정 + 무적 + 진행 중 패턴 중단 → ② 무너짐 시작
    private void BeginMovementTransition()
    {
        Snapshot.currentHp = 1;
        Snapshot.isInvincible = true;
        // 패턴이 바꾼 자세(거대화·뒤판)는 원래대로 — 무너짐·다시 쌓기는 제자리에서
        ResetBodyPose();
        if (swarm != null && look == null)
        {
            swarm.Blob.radius = baseRadius;
            swarm.Blob.center = PosedCenter();
        }
        if (look != null) look.Stop();
        RefreshHud();

        foreach (var pattern in activePatternPool)
            if (pattern != null) pattern.InterruptPattern(InterruptReason.PhaseTransition);
        StateMachine?.ChangeState(new MaestroTransitionState(this, RenewStateCancellationToken()));

        DropBody();

        pendingSand.Clear();
        step = TransitionStep.Collapse;
        stepFrame = 0;
        Debug.Log($"[Maestro] {Movement}악장 종료 — HP 1 고정, 무너짐");
    }

    private void TickTransition()
    {
        if (step == TransitionStep.None || Snapshot == null) return;
        stepFrame++;

        if (step == TransitionStep.Collapse)
        {
            if (stepFrame < collapseFrames) return;
            // ③ 다시 쌓기 시작 — 떨어진 봇(모래)을 차례로 몸에 되붙인다
            step = TransitionStep.Rebuild;
            stepFrame = 0;
            rebindNext = 0;
            debris.Clear();
            if (swarm != null) swarm.AllIn(BotMode.Debris, debris);
            Debug.Log($"[Maestro] 다시 쌓기 ({debris.Count}봇)");
            return;
        }

        // Rebuild: 쌓아 올리기와 HP 게이지 회복을 같은 진행률로
        float t = Mathf.Clamp01(stepFrame / (float)rebuildFrames);
        Snapshot.currentHp = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, Snapshot.maxHp, t)));
        RefreshHud();

        if (swarm != null)
        {
            int upto = Mathf.CeilToInt(debris.Count * t);
            for (; rebindNext < upto; rebindNext++)
                swarm.Bind(debris[rebindNext], RSNanoSwarm.G_Blob);
        }

        if (stepFrame < rebuildFrames) return;

        // ④ 다음 악장 — 무적 해제, 딜타임부터 다시
        step = TransitionStep.None;
        Snapshot.currentHp = Snapshot.maxHp;
        Snapshot.isInvincible = false;
        TransitionPhase(Movement + 1);
        RefreshHud();
        float postDelay = MasterData != null ? MasterData.patternPostDelay : 1f;
        StateMachine?.ChangeState(new BossIdleState(this, RenewStateCancellationToken(), postDelay));
    }

    // HP 게이지 갱신 — 허브는 HP를 0으로 깎은 값을 이미 방송했으므로 고친 값을 다시 방송한다
    private void RefreshHud()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;
        hub.SyncBossToEnemyProxy();
        hub.BroadcastFullState();
    }

    protected override void OnPhaseChanged(int prevPhase, int newPhase)
    {
        Debug.Log($"[Maestro] {prevPhase}악장 → {newPhase}악장 (HP {Snapshot?.currentHp}/{Snapshot?.maxHp})");
        patternCursor = 0; // 새 악장은 그 악장 첫 패턴부터
        if (look != null && look.Ready) look.SetMovement(newPhase); // 시트 '페이즈별 변화' — 그림 연출만
    }

#if UNITY_EDITOR
    // 시험용: 악장 연출만 바로 바꿔 본다 (HP·패턴은 그대로) — 플레이 중 컴포넌트 ⋮ 메뉴
    [ContextMenu("시험: 연출만 1악장")] private void DebugLook1() { if (look != null) look.SetMovement(1); }
    [ContextMenu("시험: 연출만 2악장")] private void DebugLook2() { if (look != null) look.SetMovement(2); }
    [ContextMenu("시험: 연출만 3악장")] private void DebugLook3() { if (look != null) look.SetMovement(3); }
#endif

    protected override void OnBossDefeated()
    {
        defeated = true;
        step = TransitionStep.None;
        Debug.Log($"[Maestro] 격파 ({Movement}악장)");
        if (look != null) look.refillEnabled = false; // 격파 뒤엔 다시 쌓지 않는다
        DropBody();
    }

    // 몸 전체를 모래로 — 덩어리(외형이면 하체에 숨긴 남는 봇)와 외형 그림 봇(퍼펫) 모두.
    // 다시 쌓기는 모래를 덩어리에 되붙이고, 외형이면 퍼펫이 거기서 봇을 가져가 그림이 아래부터 다시 채워진다
    private void DropBody()
    {
        if (swarm == null) return;
        if (look != null && look.Ready)
        {
            // 외형: 위에서부터 차례로 부드럽게 부스러진다 (무너짐 시간 안에 끝나게). 되돌리기는 전환의 다시 쌓기가 맡는다
            look.DropAll(Mathf.Min(0.8f, collapseFrames * CombatClock.TICK * 0.6f));
            return;
        }
        var bound = swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob);
        foreach (int i in bound)
            swarm.Drop(i, new Vector3(Random.Range(-collapseKick.y, collapseKick.y) * 0.5f,
                                      Random.Range(collapseKick.x, collapseKick.y), 0f));
    }

#if UNITY_EDITOR
    // 시험용 (2026-11-23): 남은 HP만큼 실제 피해 경로(TakeDamage)로 넣어 악장 끝을 바로 본다 — 플레이 중 컴포넌트 ⋮ 메뉴
    [ContextMenu("시험: 이번 악장 HP 0으로 (악장 전환·격파 확인)")]
    private void DebugFinishMovementMenu() => DebugFinishMovement();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(p.x - 4f, groundY), new Vector3(p.x + 4f, groundY));
    }
#endif
}
