using UnityEngine;

/// <summary>
/// [임시/테스트용] 플레이어 오브젝트에 붙은 컴포넌트들을 서로 연결하고,
/// ActionExecutor.ExecuteTick()을 CombatClock의 틱에 구독시키는 자리.
///
/// 정식 조립기(RobotAssembler, §11-8)가 생기면 이 와이어링은 거기로 흡수될 수 있다.
///
/// 왜 이런 "조립 전용" 클래스가 따로 필요한가: ActionExecutor, RobotMover, RobotView, BoxDrawer는
/// 서로 직접 GetComponent로 상대를 찾지 않는다(강하게 결합되면 나중에 하나만 떼어내 재사용하기
/// 어려워짐). 대신 전부 Init(...)으로 필요한 걸 "받기만" 하는 형태로 만들어뒀고, 이 클래스가
/// 그 배선을 한 곳에서 몰아서 해준다 — 누가 누구를 참조하는지 여기 한 파일만 보면 전부 알 수 있다
/// </summary>
public class PlayerRobotBootstrap : MonoBehaviour
{
    // CombatDataHub(KKH)가 "이 로봇이 누구인지" 구분하는 유일한 방법이 이 문자열이다(§14) —
    // GameObject 참조가 아니라 문자열인 이유는 KKH 쪽 API 전체가 이미 이렇게 만들어져 있기 때문
    [Tooltip("CombatDataHub/BattleManager(KKH)가 조회 키로 쓰는 식별자. \"Player\" 또는 \"Enemy\"")]
    [SerializeField] private string fighterId = "Player";

    // OnEnable/OnDisable에서 구독/해제할 때 다시 써야 해서 필드로 들고 있는다
    private ActionExecutor executor;

    // 호출: Unity(오브젝트 생성 시). 같은 오브젝트의 부품들을 찾아 서로 연결한다 — 입력(IInputSource)과 상태(ActionState)를 여기서만 나눠준다
    private void Awake()
    {
        // 구체 타입(PlayerInputSource)이 아니라 인터페이스로 찾는다 — 키보드든 AI든 테스트용 허수아비든
        // 오브젝트에 붙어 있는 입력원 하나를 그대로 쓰게 하기 위함 (§8 "입력만 바꿔 끼운다")
        var input = GetComponent<IInputSource>();
        executor = GetComponent<ActionExecutor>();
        // mover.Init에는 executor.State가 필요하다 — 그래서 executor.Init을 먼저 불러서 State가
        // 만들어지게 한 다음에 mover.Init을 부르는 순서가 되어야 한다 (아래 두 줄의 순서가 중요함)
        // (2026-09-29: 가드가 기동 행동에서 삭제되면서 executor.Init에는 더 이상 mover를 안 넘긴다 — §5·§15)
        var mover = GetComponent<RobotMover>();

        executor.Init(input, fighterId);
        // executor.Init이 먼저 끝나서 State가 이미 만들어져 있어야 여기서 null이 안 넘어간다.
        // mover가 빠져 있으면 경고만 남기고 넘어간다 — 여기서 예외가 나면 아래 RobotView/BoxDrawer 배선까지 전부 막힌다
        if (mover != null) mover.Init(input, executor.State);
        else Debug.LogWarning($"[PlayerRobotBootstrap] {name}에 RobotMover가 없음 — 바라보는 방향을 몰라서 박스 좌우 반전이 틀어짐");
        // ?. 를 쓴 이유: RobotView/BoxDrawer는 디버그·연출용이라 아직 오브젝트에 안 붙어있을 수도 있다.
        // 필수 컴포넌트(executor, mover)가 아니라서 없어도 전투 로직 자체는 돌아가야 하므로, 없으면 그냥 건너뛴다
        GetComponent<RobotView>()?.Init(executor.State, input, mover);
        GetComponent<BoxDrawer>()?.Init(executor.State, mover);
    }

    // 호출: Unity(활성화 시). CombatClock의 틱에 ExecuteTick을 등록한다 — 전투 로직이 1/60초마다 도는 연결 고리
    // Awake가 아니라 OnEnable에서 구독하는 이유: 오브젝트가 비활성화됐다가 다시 활성화되는 경우
    // (예: 풀링, 일시적으로 꺼놨다 켜는 연출 등)에도 구독이 제대로 다시 걸리게 하기 위함.
    // Awake는 오브젝트 생성 시 딱 한 번만 불리지만 OnEnable/OnDisable은 활성화될 때마다 짝을 맞춰 불린다
    private void OnEnable()
    {
        CombatClock.Instance.OnCombatTick += executor.ExecuteTick;
    }

    // 호출: Unity(비활성화/파괴 시). 등록했던 ExecuteTick을 해제한다 (안 하면 파괴된 오브젝트를 계속 호출함)
    // Instance가 아니라 Existing을 쓰는 이유: 씬이 종료되는 중에는 CombatClock이 이미 파괴됐을 수도 있는데,
    // 여기서 Instance를 부르면 "없으니 새로 만들어야지"하고 사라지는 씬에 쓸모없는 오브젝트를 새로 만들어버린다.
    // Existing은 "있으면 있는 거, 없으면 null"만 반환하므로 그런 부작용이 없다
    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= executor.ExecuteTick;
    }
}
