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
/// 아직 없는 것: 거대 지휘봉·순환 순서, 첫 큐, 헬 2→3악장 가짜 격파 연출(0.5, 지금은 일반 전환과 같다), 그로기 기믹.
/// 플레이어 → 보스 피해는 HitDetection → 허브 ProcessHit(보스 모드)가 깎는다 — 여기서 NotifyDamageFromHub를 부르지 않는다.
/// </summary>
[RequireComponent(typeof(RSNanoSwarm))]
[RequireComponent(typeof(BoxCollider2D))]
public class MaestroBossController : BossControllerBase
{
    private enum TransitionStep { None, Collapse, Rebuild }

    [Tooltip("바닥 높이 y(월드) — 플레이어가 서 있는 높이. 군집 모래도 여기 쌓인다")]
    [SerializeField] private float groundY;

    [Header("몸 (나노봇 덩어리)")]
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

    /// <summary>몸 중심 (덩어리 중심)</summary>
    public Vector3 BodyCenter => swarm != null ? swarm.Blob.center : transform.position;

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
        swarm.Blob.aspect = blobShape.x / Mathf.Max(0.01f, blobShape.y);
        swarm.Blob.radius = swarm.BlobSize(swarm.botCount, blobShape) * bodyScale;
        swarm.Blob.center = BlobCenterAt(transform.position.x);
        swarm.Build(swarm.Blob.center);
        swarm.ResetBotLooks();
        SyncHurtbox();
    }

    private Vector3 BlobCenterAt(float x) => new Vector3(x, groundY + swarm.Blob.radius.y, transform.position.z);

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
        if (swarm.Bots[bot].mode == BotMode.Free) swarm.Drop(bot, vel);
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
        if (!InTransition) swarm.Blob.center = BlobCenterAt(transform.position.x);
        SyncHurtbox();
    }

    // 맞는 범위 = 덩어리 크기 (루트 스케일 1 기준 로컬)
    private void SyncHurtbox()
    {
        if (hurtbox == null || swarm == null) return;
        Vector3 local = swarm.Blob.center - transform.position;
        hurtbox.offset = new Vector2(local.x, local.y);
        hurtbox.size = new Vector2(swarm.Blob.radius.x, swarm.Blob.radius.y) * (2f * hurtboxScale);
    }

    public override float GetBossVisualHalfWidth()
    {
        float half = swarm != null ? swarm.Blob.radius.x * hurtboxScale : 1f;
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
        base.ProcessPostDamageReactions(finalDamage);
    }

    // 악장은 HP 문턱이 아니라 "HP 1 고정 → 전환"으로만 넘어간다
    protected override void CheckPhaseTransition() { }

    // ① HP 1 고정 + 무적 + 진행 중 패턴 중단 → ② 무너짐 시작
    private void BeginMovementTransition()
    {
        Snapshot.currentHp = 1;
        Snapshot.isInvincible = true;
        RefreshHud();

        foreach (var pattern in activePatternPool)
            if (pattern != null) pattern.InterruptPattern(InterruptReason.PhaseTransition);
        StateMachine?.ChangeState(new MaestroTransitionState(this, RenewStateCancellationToken()));

        if (swarm != null)
        {
            var bound = swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob);
            foreach (int i in bound)
                swarm.Drop(i, new Vector3(Random.Range(-collapseKick.y, collapseKick.y) * 0.5f,
                                          Random.Range(collapseKick.x, collapseKick.y), 0f));
        }

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
    }

    protected override void OnBossDefeated()
    {
        defeated = true;
        step = TransitionStep.None;
        Debug.Log($"[Maestro] 격파 ({Movement}악장)");
        if (swarm == null) return;
        var bound = swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob);
        foreach (int i in bound)
            swarm.Drop(i, new Vector3(Random.Range(-collapseKick.y, collapseKick.y) * 0.5f,
                                      Random.Range(collapseKick.x, collapseKick.y), 0f));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        Gizmos.DrawLine(new Vector3(p.x - 4f, groundY), new Vector3(p.x + 4f, groundY));
    }
#endif
}
