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

    // 사람이 조작하는지 AI가 조작하는지 몰라도 되도록 인터페이스로만 들고 있는다 (§8)
    private IInputSource inputSource;
    // 이 로봇의 "지금 뭘 하고 있는가" 전체를 담는 상태 객체. Init()에서 idleAction과 함께 생성된다
    // (필드 선언 시점에 바로 new하지 않는 이유: idleAction이 인스펙터 값이라 Init이 불릴 때까진 확정되지 않기 때문)
    private ActionState state;
    // 가드 판정(아래 UpdateGuardState)에 "지금 어느 방향을 보고 있는지"가 필요해서 참조를 들고 있는다
    private RobotMover mover;

    /// <summary>
    /// 가드 여부 — 버튼이 아니라 "상대를 바라보는 방향의 반대쪽을 누르고 있는가"로 매 틱 계산한다 (SF2 방식).
    /// 공격/피격 등 Idle이 아닌 동안은 가드 불가 — ActionState.CanMove 그대로 재사용.
    /// Guard.asset(3구간 액션)은 이 방식에서 쓰지 않는다 — "유지"가 고정 프레임 모델과 안 맞기 때문.
    /// </summary>
    // private set인 이유: 이 값은 오직 UpdateGuardState() 안에서만 정해져야 한다 — 외부에서 직접
    // true/false를 대입할 수 있게 열어두면 "실제로 방향을 누르고 있지 않은데 가드 중"인 거짓 상태가 생길 수 있다
    public bool IsGuarding { get; private set; }

    /// <summary>
    /// 스킬(CSH) 쪽이 이벤트를 구독하려면 이 참조가 필요하다.
    /// 예: robot.GetComponent&lt;ActionExecutor&gt;().State.OnActionActiveStart += ...
    /// </summary>
    public ActionState State => state;

    /// <summary>
    /// CombatDataHub/BattleManager(KKH) API가 조회 키로 쓰는 식별자 ("Player" / "Enemy").
    /// PlayerRobotBootstrap.Awake에서 주입된다. HitDetection 등 다른 3층 시스템도
    /// GetComponent&lt;ActionExecutor&gt;().FighterId로 이 값을 가져다 쓴다.
    /// </summary>
    // 왜 GameObject 참조가 아니라 문자열인가: KKH의 CombatDataHub API 전체가 "Player"/"Enemy" 문자열로
    // 로봇을 구분하도록 이미 만들어져 있어서(§14), NYH 쪽도 같은 키를 그대로 들고 있어야 서로 연결된다
    public string FighterId { get; private set; }

    // 호출: PlayerRobotBootstrap.Awake. 받음: 입력원(사람인지 AI인지 몰라도 되게 인터페이스로 받는다), fighterId(CombatDataHub 조회 키), mover(가드 방향 판정용 facingRight 소스)
    public void Init(IInputSource source, string fighterId, RobotMover mover)
    {
        inputSource = source;
        FighterId = fighterId;
        this.mover = mover;
        // 여기서 처음으로 state를 만든다 — idleAction(인스펙터 값)이 이 시점엔 이미 확정돼 있으므로 안전하게 넘길 수 있다
        state = new ActionState(idleAction);
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 순서: Idle이면 입력 확인 → state.Begin으로 시작 → state.Advance로 프레임 +1
    public void ExecuteTick()
    {
        // Idle일 때만 새 행동을 받아들인다 — 공격 도중(선딜/활성/후딜)에 입력이 들어와도 씹히는 게
        // 정상 동작이고, 그 판단 기준(CanAcceptNewAction)은 ActionState가 Phase로 이미 갖고 있으므로 여기선 그냥 물어보기만 한다
        if (state.CanAcceptNewAction)
        {
            // 입력원한테 "이번 틱에 하고 싶은 행동이 있냐"고 물어본다. 없으면 null이 온다(§8 IInputSource 계약)
            ActionData requested = inputSource?.GetDesiredAction();
            if (requested != null)
            {
                requested = ResolveReplacement(requested); // 최. 추가
                state.Begin(requested);
                // 구간별로 프레임 수를 전부 찍어두면, 나중에 "훅이 정말 선딜 3프레임에 나가는지" 같은
                // 걸 로그만 보고도 검증할 수 있다 (§9 "상태 로그" 규칙)
                Debug.Log($"[{requested.ActionName}] Startup {requested.StartupFrames}f → Active {requested.ActiveFrames}f → Recovery {requested.RecoveryFrames}f");
            }
        }

        // 입력을 받았든 안 받았든 매 틱 반드시 한 번은 프레임을 전진시켜야 한다 (안 그러면 시간이 안 흐름)
        state.Advance();
        // 가드는 ActionState 밖에서 별도로 매 틱 계산한다 — 이유는 아래 UpdateGuardState 주석 참고
        UpdateGuardState();
    }

    // 호출: ExecuteTick(틱마다). 상대를 바라보는 방향의 반대쪽을 누르고 있으면 가드 — 버튼이 아니라 방향으로 판정한다 (SF2 방식)
    // 왜 ActionData/ActionState를 안 쓰는가: 가드는 프레임표상 "선딜 2 / 활성 유지 / 후딜 10"인데,
    // "유지"는 고정된 프레임 수가 아니라 "입력이 지속되는 동안 계속"이라는 뜻이라 3구간 상태기계
    // (정해진 프레임 수만큼 세고 자동으로 다음 Phase로 넘어가는 구조)로는 표현할 방법이 없었다.
    // 그래서 아예 액션으로 취급하지 않고, 매 틱 "지금 뒤로 버티고 있는가"만 독립적으로 계산하기로 했다
    private void UpdateGuardState()
    {
        // 공격 중이거나 피격 경직 중처럼 Idle이 아닌 동안은 방향키를 눌러도 가드가 안 된다 —
        // 실제 격투 게임에서도 공격 모션 중엔 막기로 전환이 안 되는 것과 같은 이치
        if (!state.CanMove)
        {
            IsGuarding = false; // 공격/피격/다운 등 Idle이 아닌 동안은 가드 불가
            return;
        }

        // 지금 좌우 입력이 뭔지 물어본다 (-1=왼쪽, 0=중립, 1=오른쪽)
        float moveInput = inputSource?.GetMoveInput() ?? 0f;
        // "바라보는 방향의 반대쪽"을 누르고 있는지 확인한다: 오른쪽을 보고 있으면 왼쪽(음수) 입력이,
        // 왼쪽을 보고 있으면 오른쪽(양수) 입력이 가드에 해당한다. mover가 아직 없으면(배선 전)
        // 안전하게 false로 처리해서 가드가 잘못 켜지는 일이 없게 한다
        IsGuarding = mover != null && (mover.FacingRight ? moveInput < 0f : moveInput > 0f);
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
