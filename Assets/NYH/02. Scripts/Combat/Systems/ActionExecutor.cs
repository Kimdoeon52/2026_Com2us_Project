using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로봇 하나에 붙는 실행기 (3층 · 시스템). 개수 고정 — 기술이 몇 개든 이 클래스는 하나다.
/// 이 클래스는 지금 진행 중인 게 무슨 기술인지 몰라도 선딜 → 판정 → 후딜을 돌릴 수 있어야 한다 (§1).
///
/// CombatClock.CombatTick()에서 씬에 있는 로봇 수만큼 ExecuteTick()이 호출된다.
/// Update()에서 직접 프레임을 세지 않는다 — 반드시 CombatTick 경유 (§3).
/// </summary>
public class ActionExecutor : MonoBehaviour
{
    // 걷기·대기 중엔 ActionState.CurrentAction이 계속 null이라 판정용 박스를 못 구하는데,
    // 그렇다고 그 동안 맞을 수도 없고 몸통도 없으면 격투 게임으로서 말이 안 되므로, 이 기간에
    // 대신 쓸 허트/푸시박스를 담은 전용 ActionData를 인스펙터에서 미리 꽂아둔다 (ActionState 생성자로 그대로 전달됨)
    [Tooltip("걷기·대기 중(행동 없음) 허트/푸시박스로 쓸 ActionData. Hit박스는 없이 Hurt/Push만 담아야 한다")]
    [SerializeField] private ActionData idleAction;

    // RuntimeRobot에 "이 로봇이 보유한 전체 행동"을 알려주는 임시 자리. RobotAssembler(§11-8, 실제
    // 장착 파츠로 조립)가 생기면 이 필드는 없어지고 그쪽이 채워주는 쪽으로 바뀐다 — 지금은 구조만
    // 먼저 검증하기 위해 인스펙터에서 수동으로 드래그해 넣는다
    [Tooltip("이 로봇이 가진 모든 행동(D/Q/W/E/R 등). RuntimeRobot이 부위 파손 여부로 이 중 못 쓰는 걸 거른다")]
    [SerializeField] private ActionData[] allActions;

    // 2026-10-09 추가: 맞았을 때 경직에 들어가는 규칙(기획서1009 §6-12-10). 비우면 경직 없이 점멸만 한다
    [Tooltip("피격 시 경직 규칙. 비우면 이 로봇은 맞아도 움찔하지 않는다")]
    [SerializeField] private HitReactionData hitReaction;

    // 2026-10-09 추가: 착지 순간 자동으로 실행할 행동(Hit박스 없음). 착지 동안 이동·공격이 멈춰서
    // "무거운 로봇이 쿵 하고 내려앉는" 느낌이 난다. 그림만 바꾸면 착지 모션 중에도 미끄러지듯 걷거나 바로 때릴 수 있다
    [Tooltip("착지 순간 실행할 행동. 비우면 착지하자마자 바로 움직일 수 있다")]
    [SerializeField] private ActionData landAction;

    // 사람이 조작하는지 AI가 조작하는지 몰라도 되도록 인터페이스로만 들고 있는다 (§8)
    private IInputSource inputSource;
    // 이 로봇의 "지금 뭘 하고 있는가" 전체를 담는 상태 객체. Init()에서 idleAction과 함께 생성된다
    // (필드 선언 시점에 바로 new하지 않는 이유: idleAction이 인스펙터 값이라 Init이 불릴 때까진 확정되지 않기 때문)
    private ActionState state;

    /// <summary>HitDetection/RobotMover/PlayerRobotBootstrap이 이 로봇의 행동·프레임 상태를 읽는 유일한 통로.</summary>
    public ActionState State => state;

    /// <summary>지금 쓸 수 있는 행동 목록을 들고 있는 2층 상태. AIInputSource·나중의 UI가 여길 본다 (§5-6-1)</summary>
    public RuntimeRobot Robot { get; private set; }

    /// <summary>
    /// CombatDataHub/BattleManager(KKH) API가 조회 키로 쓰는 식별자 ("Player" / "Enemy").
    /// PlayerRobotBootstrap.Awake에서 주입된다. HitDetection 등 다른 3층 시스템도
    /// GetComponent&lt;ActionExecutor&gt;().FighterId로 이 값을 가져다 쓴다.
    /// </summary>
    // 왜 GameObject 참조가 아니라 문자열인가: KKH의 CombatDataHub API 전체가 "Player"/"Enemy" 문자열로
    // 로봇을 구분하도록 이미 만들어져 있어서(§14), NYH 쪽도 같은 키를 그대로 들고 있어야 서로 연결된다
    public string FighterId { get; private set; }

    /// <summary>경직 규칙. HitReactionSystem이 읽는다 (null이면 경직 없음)</summary>
    public HitReactionData HitReaction => hitReaction;

    /// <summary>착지 순간 자동으로 실행되는 행동. AI가 "이건 패턴을 끊는 끼어들기가 아니다"를 구분할 때 본다</summary>
    public ActionData LandAction => landAction;

    /// <summary>같은 오브젝트의 RobotMover. 넉백의 띄우기·경직의 공중 판정·총구 방향이 이걸 본다. 없으면 null</summary>
    public RobotMover Mover { get; private set; }

    /// <summary>
    /// 맞아서 데미지 판정이 끝난 순간(회피 성공 포함). RobotView가 점멸 이펙트를 여기서 건다.
    /// 나중에 데미지 숫자 UI도 이 이벤트나 KKH의 OnHitResolved를 구독하면 된다
    /// </summary>
    public event Action<HitResolutionResult> OnDamaged;

    public event Action<ActionData, int> OnCylinderSpent; // 2026-10-20추가

    // 호출: PlayerRobotBootstrap.Awake. 받음: 입력원(사람인지 AI인지 몰라도 되게 인터페이스로 받는다), fighterId(CombatDataHub 조회 키)
    // (2026-09-29: 가드가 기동 행동에서 삭제되면서 방향 기반 가드 판정용으로 받던 mover 파라미터는 더 이상 필요 없어 제거함 — CLAUDE_1.md §5·§15)
    public void Init(IInputSource source, string fighterId)
    {
        inputSource = source;
        FighterId = fighterId;
        // 파라미터로 받지 않고 직접 찾는 이유: 여기서 하는 일은 "같은 몸에 붙은 다리가 있으면 쓴다" 수준이고,
        // Init 시그니처를 바꾸면 CSH가 병합한 코드·부트스트랩 호출부까지 같이 고쳐야 해서 영향 범위만 커진다
        Mover = GetComponent<RobotMover>();
        // 여기서 처음으로 state를 만든다 — idleAction(인스펙터 값)이 이 시점엔 이미 확정돼 있으므로 안전하게 넘길 수 있다
        state = new ActionState(idleAction);
        // RuntimeRobot 생성자가 CombatDataHub의 부위 파손 이벤트를 구독한다 — PlayerRobotBootstrap.Awake가
        // BattleManager.InitializeBattle보다 먼저 돌 수도 있어서, 생성 시점에 CombatDataHub가 아직
        // 없을 수도 있다. RuntimeRobot 쪽에서 null 체크를 해두므로 여기선 그냥 넘겨도 안전하다
        Robot = new RuntimeRobot(fighterId, allActions ?? Array.Empty<ActionData>());

        // 최. 추가 — 슬롯 입력을 주는 입력원(플레이어)이면 스킬 드라이버 경로를 쓴다. 적/보스는 null이라 기존 경로 그대로
        skillDriver = source is ISkillInputSource skillInput
            ? new SkillDriver(this, skillInput, GetComponent<ISkillLoadout>())
            : null;
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 순서: Idle이면 입력 확인 → state.Begin으로 시작 → state.Advance로 프레임 +1
    public void ExecuteTick()
    {
        // CombatDataHub가 생성자 시점엔 없었을 수 있어서(초기화 순서, RuntimeRobot.cs 주석 참고)
        // 매 틱 가볍게 재시도한다 — 이미 구독됐으면 bool 체크 한 줄로 끝난다
        Robot?.EnsureSubscribed();

        // 죽었으면 더 할 일이 없다 — 입력도 프레임 전진도 안 한다 (사망 연출은 RobotView가 OnDead로 따로 돌림)
        CheckDeath();
        if (state.IsDead) return;

        // 쿨타임은 입력을 받기 "전에" 줄인다 — 그래야 cooldownFrames가 N이면 시작한 틱으로부터 정확히 N틱 뒤에 다시 쓸 수 있다
        TickCooldowns();

        CheckLanding();

        // Idle일 때만 새 행동을 받아들인다 — 공격 도중(선딜/활성/후딜)에 입력이 들어와도 씹히는 게
        // 정상 동작이고, 그 판단 기준(CanAcceptNewAction)은 ActionState가 Phase로 이미 갖고 있으므로 여기선 그냥 물어보기만 한다
        if (skillDriver != null)
        {
            // 최. 추가 — 플레이어: 슬롯 입력 해석·시작·스킬 러너 진행을 SkillDriver가 맡는다.
            // 일반 ActionData(D, 회피)는 SkillDriver가 아래 TryBeginRequested를 그대로 불러서 기존과 똑같이 시작된다
            skillDriver.Tick();
        }
        else if (state.CanAcceptNewAction)
        {
            // 입력원한테 "이번 틱에 하고 싶은 행동이 있냐"고 물어본다. 없으면 null이 온다(§8 IInputSource 계약)
            ActionData requested = inputSource?.GetDesiredAction();
            // 최. 수정: 게이트 → Begin → 쿨/탄 블록을 TryBeginRequested로 옮김 (SkillDriver와 같이 쓰려고). 내용·순서는 그대로
            if (requested != null)
                TryBeginRequested(requested, inputSource as IActionAdjustmentSource);
        }

        // 입력을 받았든 안 받았든 매 틱 반드시 한 번은 프레임을 전진시켜야 한다 (안 그러면 시간이 안 흐름)
        state.Advance();

        // 프레임을 전진시킨 "뒤"에 쏘는 이유: Startup → Active로 넘어가는 바로 그 틱에 첫 발이 나가야
        // 박스 앵커(Active 구간 시작 = 첫 히트 가능 프레임)와 타이밍이 같아진다
        EmitProjectiles();

        ApplyAdvance();
        ApplyLeap();
    }

    // 2026-10-27 추가: ActionData.LeapVelocity가 있는 행동이면 활성 구간에 들어서는 틱에 한 번 뛴다 (긴급 이탈 백점프 등).
    // 선딜 = 땅에서 웅크리는 예비동작, 활성 시작 = 이륙. EmitProjectiles와 같은 이유로 Advance 뒤에 본다
    private void ApplyLeap()
    {
        if (state.IsExternallyDriven || Mover == null) return;

        ActionData action = state.CurrentAction;
        if (action == null || action.LeapVelocity == Vector2.zero) return;
        if (state.Phase != ActionPhase.Active || state.FrameInPhase != 0) return;

        float direction = Mover.FacingRight ? 1f : -1f;
        Mover.Leap(direction * action.LeapVelocity.x, action.LeapVelocity.y);
    }

    // 2026-10-11 추가: ActionData.AdvanceDistance를 선딜~활성 프레임 수로 나눠 틱마다 조금씩 앞으로 민다.
    // RobotMover.Tick(입력 이동)이 아니라 여기서 하는 이유: 거리가 "몇 프레임 동안"에 묶인 값이라 틱에서 세야
    // 주사율과 무관하게 같은 거리를 간다(§3). 화면 밖 보정·Push박스 밀어내기는 RobotMover.Tick이 그대로 처리한다
    private void ApplyAdvance()
    {
        // 최. 추가 — 스킬 러너 구동 중이면 러너가 Timeline 기준으로 직접 전진시킨다 (홀드 시간이 끼면 GlobalFrame 기준이 어긋남)
        if (state.IsExternallyDriven) return;

        ActionData action = state.CurrentAction;
        if (action == null || action.AdvanceDistance == 0f) return;

        // GlobalFrame은 Begin 후 1부터 1씩 오르므로 1 ~ (선딜+활성) 구간에 정확히 그 프레임 수만큼 불린다.
        // 활성 전용(돌진)이면 (선딜+1) ~ (선딜+활성) — 선딜 동안은 제자리에서 예비동작
        int firstFrame = action.AdvanceDuringActiveOnly ? state.Timeline.StartupFrames + 1 : 1;
        int lastFrame = state.Timeline.StartupFrames + state.Timeline.ActiveFrames;
        int advanceFrames = lastFrame - firstFrame + 1;
        if (advanceFrames <= 0 || state.GlobalFrame < firstFrame || state.GlobalFrame > lastFrame) return;

        float direction = Mover == null || Mover.FacingRight ? 1f : -1f;
        Vector3 pos = transform.position;
        pos.x += direction * action.AdvanceDistance / advanceFrames;
        transform.position = pos;
    }

    /// <summary>
    /// 판정이 끝난 뒤 HitDetection이 맞은 쪽에 알려주는 통로. 이벤트를 밖에서 직접 Invoke할 수 없어서 함수로 연다
    /// </summary>
    // 호출: HitDetection(근접·투사체 공통). 전달: OnDamaged → RobotView 점멸
    public void NotifyDamaged(HitResolutionResult result) => OnDamaged?.Invoke(result);

    // 코어 HP가 0이 됐는지 매 틱 확인해서 Dead로 보낸다. 피격 지점(HitDetection)에서만 확인하지 않는 이유:
    // 데미지가 들어오는 길이 근접·투사체·(나중의) 기믹·지속 피해 등 여러 갈래라, HP를 직접 보는 쪽이 빠뜨릴 일이 없다.
    // KKH의 BattleManager.OnFighterKilled도 같은 순간을 알지만 그쪽은 "전투 종료 처리"라 역할이 다르다
    private void CheckDeath()
    {
        if (state.IsDead) return;
        var hub = CombatDataHub.Instance;
        var snapshot = hub != null ? hub.GetSnapshot(FighterId) : null;
        if (snapshot != null && !snapshot.IsAlive) state.Kill();
    }

    // 지난 틱엔 공중이었는데 이번 틱에 땅이면 = 방금 착지. 그때 아무것도 안 하고 있으면 착지 행동을 시작한다.
    // 공중 공격 중에 착지했으면(행동 중) 건너뛴다 — 하던 행동이 우선이다.
    // RobotMover의 착지는 Update에서 일어나지만, 행동 시작은 반드시 틱 안에서 해야 프레임이 정확히 세어진다(§3)
    private bool wasGrounded = true;

    private void CheckLanding()
    {
        bool grounded = Mover == null || Mover.IsGrounded;
        bool justLanded = grounded && !wasGrounded;
        wasGrounded = grounded;

        if (justLanded && landAction != null && state.CanAcceptNewAction)
            state.Begin(landAction);
    }

    // 활성 구간 동안 ActionData.ProjectileIntervalFrames마다 한 발씩 쏜다 (§11-15).
    // 기술 이름을 보지 않고 "이 행동에 투사체가 달려 있는가"만 본다 — 총을 쏘는 기술이 몇 개로 늘어도 이 코드는 그대로다
    private void EmitProjectiles()
    {
        ActionData action = state.CurrentAction;
        if (action == null || action.Projectile == null || state.Phase != ActionPhase.Active) return;

        // Active 구간에서 FrameInPhase는 0부터 센다(진입 틱 = 0) — 0, interval, 2×interval ... 에 발사
        if (state.FrameInPhase % action.ProjectileIntervalFrames != 0) return;

        bool facingRight = Mover == null || Mover.FacingRight;
        Vector3 muzzle = BoxResolver.ToWorldPoint(transform.position, action.ProjectileMuzzle, facingRight);
        Projectile.Spawn(action.Projectile, action, this, muzzle, facingRight);
    }

    // 호출: ExecuteTick(새 행동을 받아들이는 자리). 부위가 파괴된 상태에서 그 부위 소속 기술을 내면
    // CombatDataHub.CanExecuteAction이 false를 돌려준다 — 여기서 걸러서 null로 바꾸면 ExecuteTick
    // 입장에서는 "이번 틱엔 아무 입력도 없었다"와 똑같이 취급되어 그냥 Idle을 유지한다.
    // ResolveReplacement는 "대체된 행동"에 한해서만 같은 체크를 이미 하고 있었는데(CSH 삽입 코드),
    // 대체되지 않은 일반적인 경우(대부분의 입력)는 이 체크를 한 번도 안 거쳤었다 — 그 구멍을 여기서 막는다.
    // CombatDataHub가 아직 씬에 없으면(테스트 초기 등) 막을 근거가 없으니 그냥 통과시킨다
    private ActionData GateByPartBroken(ActionData action)
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return action;

        return hub.CanExecuteAction(FighterId, action) ? action : null;
    }

    // 호출: ExecuteTick(새 행동을 받아들이는 자리). ActionData.GroundOnly인 행동을 공중에서 요청하면 null.
    // RobotMover가 없으면(배선 전) 땅에 있다고 본다 — CheckLanding과 같은 안전 기본값
    private ActionData GateByGround(ActionData action)
    {
        if (action == null || !action.GroundOnly) return action;

        bool grounded = Mover == null || Mover.IsGrounded;
        return grounded ? action : null;
    }

    #region 최.추가
    private EquipmentEffectSet effects;

    public void SetEffects(EquipmentEffectSet set)
    {
        if (state == null)
        {
            Debug.LogWarning("[ActionExecutor] SetEffects 호출 시점에 state가 아직 없음 - Init(PlayerRobotBootstrap.Awake) 이후에 호출해야 함");
            return;
        }

        effects?.Unequip();

        effects = set;
        state.SetModifierResolver(set != null ? (Action<ActionData, ResolvedModifiers>)ResolveModifiers : null);

        effects?.Equip();
    }

    private void OnDestroy()
    {
        effects?.Unequip();
        effects = null;
        Robot?.Dispose(); // RuntimeRobot의 부위 파손 이벤트 구독 해제 (NYH 추가)
    }

    private void ResolveModifiers(ActionData action, ResolvedModifiers into)
    {
        effects?.Resolve(action, IsPartBroken, into);
    }
    private ActionData ResolveReplacement(ActionData requested)
    {
        if (effects == null) return requested;

        ActionData replaced = effects.ResolveReplacement(requested, IsPartBroken);
        if (replaced == requested) return requested;

        var hub = CombatDataHub.Instance;
        if (hub != null && !hub.CanExecuteAction(FighterId, replaced)) return requested;

        return replaced;
    }

    private bool IsPartBroken(BodyPart part)
    {
        var hub = CombatDataHub.Instance;
        return hub != null && hub.GetSnapshot(FighterId) != null && hub.IsPartBroken(FighterId, part);
    }

    // ---- 행동 시작 공통 (기존 ExecuteTick 블록을 그대로 옮김) ----

    /// <summary>
    /// 장비 대체 → 부위 파손 → 실린더(부족하면 폴백) → 쿨타임 게이트를 거쳐 Begin하고, 쿨타임·탄을 확정한다.
    /// 적/보스(GetDesiredAction 경로)와 플레이어의 일반 ActionData(SkillDriver 경로)가 같이 쓴다.
    /// 시작했으면 true
    /// </summary>
    internal bool TryBeginRequested(ActionData requested, IActionAdjustmentSource adjuster)
    {
        if (requested == null || !state.CanAcceptNewAction) return false;

        requested = ResolveReplacement(requested); // 최. 추가
        requested = GateByPartBroken(requested); // 부위 파손 시 기술 시전 차단 (§11-4, §14)
        requested = GateByGround(requested); // 지상 전용 행동(회피 등)을 공중에서 누르면 무시. 쿨타임·탄보다 먼저 — 막히면 아무것도 소모하지 않는다
        // 탄이 모자라면 일반판정(fallbackAction)으로 바꾼다. 쿨타임 검사보다 먼저여야 한다 —
        // 바뀐 뒤의 행동이 쿨타임 중인지를 봐야 하기 때문 (없으면 null)
        requested = GateByCylinder(requested, out int cylinderCost);
        requested = GateByCooldown(requested); // 쿨타임 중이면 입력 무시. 막히면 탄도 소모하지 않는다
        if (requested == null) return false;

        // 입력원이 이번 실행분 보정을 줄 수 있으면 받는다 (AI 패턴 속도 등 — IActionAdjustmentSource 참고)
        FrameAdjustment extra = adjuster != null ? adjuster.GetAdjustment(requested) : FrameAdjustment.None;
        state.Begin(requested, extra);
        // Begin 직후에만 쿨타임을 걸고 탄을 깎는다 — state.Resolved(장비 보정)가 이때 확정되기 때문이고,
        // 게이트를 통과했어도 Begin이 안 됐다면 쿨타임/탄이 새면 안 되기 때문이다
        StartCooldown(requested);
        SpendCylinder(requested, cylinderCost);
        // 구간별로 프레임 수를 전부 찍어두면, 나중에 "훅이 정말 선딜 3프레임에 나가는지" 같은
        // 걸 로그만 보고도 검증할 수 있다 (§9 "상태 로그" 규칙).
        // 원본이 아니라 확정값(Timeline)을 찍는다 — 보정이 걸린 뒤엔 실제로 도는 값이 이쪽이다
        ResolvedAction timeline = state.Timeline;
        Debug.Log($"[{requested.ActionName}] Startup {timeline.StartupFrames}f → Active {timeline.ActiveFrames}f → Recovery {timeline.RecoveryFrames}f");
        return true;
    }

    // ---- 스킬 베이스(SkillDriver / SkillContext) 연결 ----
    // 스킬 러너는 비용 확정 시점이 제각각(홀드형은 뗄 때)이라 위 블록을 한 번에 못 쓰고, 단계별로 나눠 부른다

    private SkillDriver skillDriver;

    /// <summary>플레이어 스킬 드라이버. 적/보스는 null. UI가 CurrentRunner(차지 게이지 등)를 읽는다</summary>
    public SkillDriver SkillDriver => skillDriver;

    /// <summary>슬롯 행동에 장비 대체(ActionReplaceEffect)를 적용한다. 시작 판정 전에 부른다</summary>
    internal ActionData SkillResolveSlotAction(ActionData bound) => ResolveReplacement(bound);

    /// <summary>스킬 시작 게이트: 부위 파손 → 지상 전용 → 쿨타임. 실린더는 러너가 비용 확정할 때 따로 본다</summary>
    internal ActionData SkillGateStart(ActionData action) => GateByCooldown(GateByGround(GateByPartBroken(action)));

    internal ActionData SkillGateCylinder(ActionData action, out int cost) => GateByCylinder(action, out cost);

    /// <summary>폴백 없이 action 자체의 탄이 충분한가. CombatDataHub가 없으면 막을 근거가 없으니 true</summary>
    internal bool SkillCanAffordCylinder(ActionData action)
    {
        if (action == null) return false;
        var hub = CombatDataHub.Instance;
        if (hub == null) return true;

        int needed = ResolveCylinderCost(action);
        return needed <= 0 || hub.CanSpendCylinder(FighterId, needed);
    }

    /// <summary>비용 확정: 쿨타임 시작 + 탄 소모. state.Resolved가 action 기준이어야 한다(러너가 SwapTo 뒤에 부름)</summary>
    internal void SkillApplyCost(ActionData action, int cost)
    {
        StartCooldown(action);
        SpendCylinder(action, cost);
    }

    internal void SkillStartCooldown(ActionData action, float scale) => StartCooldown(action, scale);


    #endregion

    #region 쿨타임 / 실린더 (CSH)

    // ---- 쿨타임 ----
    // 쿨타임은 "행동"이 아니라 ActionData.CooldownKey 단위로 센다 — 일반/강화판정이 쿨을 공유하려면 같은 키를 가리켜야 한다.
    // ActionData는 Player와 Enemy가 같이 쓰는 불변 에셋이라 남은 시간은 에셋이 아니라 이 로봇(ActionExecutor)이 들고 있다 (§2)
    private struct CooldownSlot
    {
        public ActionData Key;
        public int Remaining; // 남은 틱
        public int Total;     // 시작할 때의 틱 (UI 비율 계산용)
    }

    private readonly List<CooldownSlot> cooldowns = new List<CooldownSlot>(4);

    /// <summary>쿨타임이 시작된 순간. 인자: 쿨타임 기준 행동, 총 프레임. HUD가 구독해서 게이지를 채운다</summary>
    public event Action<ActionData, int> OnCooldownStarted;

    /// <summary>action의 남은 쿨타임(프레임). 0이면 사용 가능. null이거나 쿨타임이 없는 행동이어도 0</summary>
    public int GetCooldownRemaining(ActionData action)
    {
        if (action == null) return 0;
        int index = FindCooldown(action.CooldownKey);
        return index < 0 ? 0 : cooldowns[index].Remaining;
    }

    /// <summary>남은 비율 (1 = 방금 시작, 0 = 사용 가능). 게이지 UI용</summary>
    public float GetCooldownRatio(ActionData action)
    {
        if (action == null) return 0f;
        int index = FindCooldown(action.CooldownKey);
        if (index < 0) return 0f;

        CooldownSlot slot = cooldowns[index];
        return slot.Total > 0 ? (float)slot.Remaining / slot.Total : 0f;
    }

    public bool IsOnCooldown(ActionData action) => GetCooldownRemaining(action) > 0;

    /// <summary>쿨타임을 즉시 끝낸다 (머리 패시브 "쿨 초기화" 등이 부른다)</summary>
    public void ResetCooldown(ActionData action)
    {
        if (action == null) return;
        int index = FindCooldown(action.CooldownKey);
        if (index >= 0) cooldowns.RemoveAt(index);
    }

    private int FindCooldown(ActionData key)
    {
        for (int i = 0; i < cooldowns.Count; i++)
        {
            if (cooldowns[i].Key == key) return i;
        }
        return -1;
    }

    // 호출: ExecuteTick (틱마다 1회, 입력을 받기 전). 뒤에서부터 도는 이유: 다 끝난 칸을 지우면서 돌기 때문
    private void TickCooldowns()
    {
        for (int i = cooldowns.Count - 1; i >= 0; i--)
        {
            CooldownSlot slot = cooldowns[i];
            slot.Remaining--;
            if (slot.Remaining <= 0) cooldowns.RemoveAt(i);
            else cooldowns[i] = slot;
        }
    }

    // 쿨타임 중이면 입력을 무시한다 (null 반환). 플레이어 입력은 이미 소비된 뒤라 버퍼에 남지 않는다
    private ActionData GateByCooldown(ActionData action)
    {
        if (action == null) return null;

        int remaining = GetCooldownRemaining(action);
        if (remaining <= 0) return action;

        Debug.Log($"[ActionExecutor] {FighterId}: '{action.ActionName}' 쿨타임 중 — 남은 {remaining}f ({remaining * CombatClock.TICK:F2}초)");
        return null;
    }

    // 호출: ExecuteTick, state.Begin 직후. 장비 보정(SkillParamId.Cooldown)은 기획서 단위인 "초"로 적용한다 —
    // "쿨타임 -1초" 같은 효과를 그대로 쓸 수 있게. state.Resolved는 Begin에서 확정되므로 Begin 뒤에 불러야 한다
    // scale: 스킬 러너의 조기 취소·끊김용 배율 (레이저 충전 전 취소 = 0.5). 기존 호출은 전부 1
    private void StartCooldown(ActionData action, float scale = 1f)
    {
        if (action == null || action.CooldownFrames <= 0 || scale <= 0f) return;

        float baseSeconds = action.CooldownFrames * CombatClock.TICK;
        float seconds = Mathf.Max(0f, state.Resolved.Apply(SkillParamId.Cooldown, baseSeconds)) * scale;
        int frames = Mathf.RoundToInt(seconds / CombatClock.TICK);
        if (frames <= 0) return; // 보정으로 쿨타임이 사라졌다

        ActionData key = action.CooldownKey;
        var slot = new CooldownSlot { Key = key, Remaining = frames, Total = frames };

        int index = FindCooldown(key);
        if (index >= 0) cooldowns[index] = slot;
        else cooldowns.Add(slot);

        OnCooldownStarted?.Invoke(key, frames);
    }

    // ---- 실린더 ----
    // 호출: ExecuteTick. 강화판정은 탄이 있어야 나가고, 탄이 없으면 폴백(일반판정)으로 대체한다 (기획서 "실린더 시스템").
    // 순서: 대체(ResolveReplacement) → 부위 파손 게이트 → 여기. 대체된 행동의 비용을 봐야 하고,
    // 팔이 파손됐다면 폴백(같은 팔 스킬)도 어차피 막히므로 부위 게이트가 먼저 와야 한다.
    // 폴백도 부위 게이트를 다시 거치고 비용을 확인한다. 폴백의 폴백은 따라가지 않는다.
    // CombatDataHub가 없으면 막을 근거가 없으니 그대로 통과 (GateByPartBroken과 같은 정책)
    private ActionData GateByCylinder(ActionData requested, out int cost)
    {
        cost = 0;
        if (requested == null) return null;

        var hub = CombatDataHub.Instance;
        if (hub == null) return requested;

        int needed = ResolveCylinderCost(requested);
        if (needed <= 0 || hub.CanSpendCylinder(FighterId, needed))
        {
            cost = needed;
            return requested;
        }

        // 탄 부족 — 같은 스킬의 일반판정으로 대체한다
        int have = hub.GetSnapshot(FighterId)?.currentCylinder ?? 0;
        ActionData fallback = GateByPartBroken(requested.FallbackAction);
        if (fallback == null)
        {
            Debug.Log($"[ActionExecutor] {FighterId}: '{requested.ActionName}'는 탄 {needed}발 필요(보유 {have}발), 대체 행동이 없어 입력 무시");
            return null;
        }

        int fallbackNeeded = ResolveCylinderCost(fallback);
        if (fallbackNeeded > 0 && !hub.CanSpendCylinder(FighterId, fallbackNeeded)) return null;

        cost = fallbackNeeded;
        Debug.Log($"[ActionExecutor] {FighterId}: '{requested.ActionName}'는 탄 {needed}발 필요(보유 {have}발) → '{fallback.ActionName}'로 대체");
        return fallback;
    }

    // 장비 보정(SkillParamId.CylinderCost)이 반영된 비용. 기본 비용이 0인 행동은 보정으로 비용이 생기지 않게 0으로 고정한다.
    // 게이트 시점에는 아직 state.Begin 전이라 state.Resolved를 쓸 수 없어서 별도 scratch에 한 번 계산한다
    // (Contribute를 한 번 더 부르게 되므로 EffectInstance.Contribute는 상태를 안 바꾸는 순수 계산이어야 한다)
    private readonly ResolvedModifiers costScratch = new ResolvedModifiers();

    private int ResolveCylinderCost(ActionData action)
    {
        if (action == null || action.CylinderCost <= 0) return 0;

        costScratch.Reset();
        effects?.Resolve(action, IsPartBroken, costScratch);
        return Mathf.Max(0, Mathf.RoundToInt(costScratch.Apply(SkillParamId.CylinderCost, action.CylinderCost)));
    }

    // 행동 시작 직후 탄을 깎는다. 게이트에서 이미 충분한지 확인했으므로 실패는 정상 상황에선 나오지 않는다
    private void SpendCylinder(ActionData action, int cost)
    {
        if (cost <= 0)
        {
            Debug.Log($"[ActionExecutor] {FighterId}: '{action.ActionName}' 실린더 소모 없음");
            return;
        }
        OnCylinderSpent?.Invoke(action, cost);   // 2026-10-20추가

        var hub = CombatDataHub.Instance;
        if (hub == null || !hub.TrySpendCylinder(FighterId, cost))
            Debug.LogWarning($"[ActionExecutor] {FighterId}: '{action.ActionName}' 실린더 {cost}발 소비 실패 — 게이트와 소비 사이에 탄이 줄었을 수 있음");
    }

    #endregion
}
