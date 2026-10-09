using System;
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

    /// <summary>같은 오브젝트의 RobotMover. 넉백의 띄우기·경직의 공중 판정·총구 방향이 이걸 본다. 없으면 null</summary>
    public RobotMover Mover { get; private set; }

    /// <summary>
    /// 맞아서 데미지 판정이 끝난 순간(회피 성공 포함). RobotView가 점멸 이펙트를 여기서 건다.
    /// 나중에 데미지 숫자 UI도 이 이벤트나 KKH의 OnHitResolved를 구독하면 된다
    /// </summary>
    public event Action<HitResolutionResult> OnDamaged;

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

        CheckLanding();

        // Idle일 때만 새 행동을 받아들인다 — 공격 도중(선딜/활성/후딜)에 입력이 들어와도 씹히는 게
        // 정상 동작이고, 그 판단 기준(CanAcceptNewAction)은 ActionState가 Phase로 이미 갖고 있으므로 여기선 그냥 물어보기만 한다
        if (state.CanAcceptNewAction)
        {
            // 입력원한테 "이번 틱에 하고 싶은 행동이 있냐"고 물어본다. 없으면 null이 온다(§8 IInputSource 계약)
            ActionData requested = inputSource?.GetDesiredAction();
            if (requested != null)
            {
                requested = ResolveReplacement(requested); // 최. 추가
                requested = GateByPartBroken(requested); // 부위 파손 시 기술 시전 차단 (§11-4, §14)
            }

            if (requested != null)
            {
                state.Begin(requested);
                // 구간별로 프레임 수를 전부 찍어두면, 나중에 "훅이 정말 선딜 3프레임에 나가는지" 같은
                // 걸 로그만 보고도 검증할 수 있다 (§9 "상태 로그" 규칙).
                // 원본이 아니라 확정값(Timeline)을 찍는다 — 보정이 걸린 뒤엔 실제로 도는 값이 이쪽이다
                ResolvedAction timeline = state.Timeline;
                Debug.Log($"[{requested.ActionName}] Startup {timeline.StartupFrames}f → Active {timeline.ActiveFrames}f → Recovery {timeline.RecoveryFrames}f");
            }
        }

        // 입력을 받았든 안 받았든 매 틱 반드시 한 번은 프레임을 전진시켜야 한다 (안 그러면 시간이 안 흐름)
        state.Advance();

        // 프레임을 전진시킨 "뒤"에 쏘는 이유: Startup → Active로 넘어가는 바로 그 틱에 첫 발이 나가야
        // 박스 앵커(Active 구간 시작 = 첫 히트 가능 프레임)와 타이밍이 같아진다
        EmitProjectiles();
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
        state.SetModifierResolver(set != null ? (Func<ActionData, ResolvedModifiers>)ResolveModifiers : null);

        effects?.Equip();
    }

    private void OnDestroy()
    {
        effects?.Unequip();
        effects = null;
        Robot?.Dispose(); // RuntimeRobot의 부위 파손 이벤트 구독 해제 (NYH 추가)
    }

    private ResolvedModifiers ResolveModifiers(ActionData action)
    {
        return effects != null ? effects.Resolve(action, IsPartBroken) : ResolvedModifiers.Identity;
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


    #endregion
}
