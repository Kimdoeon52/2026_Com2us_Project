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
    [Tooltip("걷기·대기 중(행동 없음) 허트/푸시박스로 쓸 ActionData. Hit박스는 없이 Hurt/Push만 담아야 한다")]
    [SerializeField] private ActionData idleAction;

    private IInputSource inputSource;
    private ActionState state;

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
    public string FighterId { get; private set; }

    // 호출: PlayerRobotBootstrap.Awake. 받음: 입력원(사람인지 AI인지 몰라도 되게 인터페이스로 받는다), fighterId(CombatDataHub 조회 키)
    public void Init(IInputSource source, string fighterId)
    {
        inputSource = source;
        FighterId = fighterId;
        state = new ActionState(idleAction);
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). 순서: Idle이면 입력 확인 → state.Begin으로 시작 → state.Advance로 프레임 +1
    public void ExecuteTick()
    {
        if (state.CanAcceptNewAction)
        {
            ActionData requested = inputSource?.GetDesiredAction();
            if (requested != null)
            {
                state.Begin(requested);
                Debug.Log($"[{requested.ActionName}] Startup {requested.StartupFrames}f → Active {requested.ActiveFrames}f → Recovery {requested.RecoveryFrames}f");
            }
        }

        state.Advance();
    }
}
