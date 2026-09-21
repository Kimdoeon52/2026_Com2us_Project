using UnityEngine;

/// <summary>
/// [임시/테스트용] 플레이어 오브젝트에 붙은 컴포넌트들을 서로 연결하고,
/// ActionExecutor.ExecuteTick()을 CombatClock의 틱에 구독시키는 자리.
///
/// 정식 조립기(RobotAssembler, §11-8)가 생기면 이 와이어링은 거기로 흡수될 수 있다.
/// </summary>
public class PlayerRobotBootstrap : MonoBehaviour
{
    [Tooltip("CombatDataHub/BattleManager(KKH)가 조회 키로 쓰는 식별자. \"Player\" 또는 \"Enemy\"")]
    [SerializeField] private string fighterId = "Player";

    private ActionExecutor executor;

    // 호출: Unity(오브젝트 생성 시). 같은 오브젝트의 부품들을 찾아 서로 연결한다 — 입력(IInputSource)과 상태(ActionState)를 여기서만 나눠준다
    private void Awake()
    {
        var input = GetComponent<PlayerInputSource>();
        executor = GetComponent<ActionExecutor>();

        executor.Init(input, fighterId);
        var mover = GetComponent<RobotMover>();
        mover.Init(input, executor.State);
        GetComponent<RobotView>()?.Init(executor.State, input);
        GetComponent<BoxDrawer>()?.Init(executor.State, mover);
    }

    // 호출: Unity(활성화 시). CombatClock의 틱에 ExecuteTick을 등록한다 — 전투 로직이 1/60초마다 도는 연결 고리
    private void OnEnable()
    {
        CombatClock.Instance.OnCombatTick += executor.ExecuteTick;
    }

    // 호출: Unity(비활성화/파괴 시). 등록했던 ExecuteTick을 해제한다 (안 하면 파괴된 오브젝트를 계속 호출함)
    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= executor.ExecuteTick;
    }
}
