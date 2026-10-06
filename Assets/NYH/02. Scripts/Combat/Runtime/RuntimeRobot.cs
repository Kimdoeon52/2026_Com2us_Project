using System;
using System.Collections.Generic;

/// <summary>
/// 로봇 1체가 "지금 어떤 행동을 쓸 수 있는가" 목록을 들고 있는 2층 런타임 상태 (§1, 설계 결정서 §6-1).
///
/// 매 프레임 "이 부위 파괴됐나?"를 검사하지 않는다. KKH의 CombatDataHub가 부위 파손 이벤트를
/// 쏴줄 때만 목록을 다시 계산한다 — 그래야 입력원(AIInputSource 등)과 나중에 생길 UI가
/// 전부 이 목록 하나만 보고 "지금 쓸 수 있는 기술이 뭔지"를 판단할 수 있다.
///
/// `ActionExecutor.GateByPartBroken`과 역할이 다르다: 그쪽은 "혹시 몰라 실행 직전에 한 번 더
/// 막는" 최종 안전망이고, 이 클래스는 "애초에 못 쓰는 기술은 후보에서 미리 뺀다"가 목적이다
/// (AI가 봉인된 기술을 계속 고르려 드는 것, 나중에 UI가 버튼을 회색 처리 못 하는 문제를 막는다).
/// 둘 다 있어야 한다 — 하나가 목록을 걸러줘도, 목록을 참조 안 하는 다른 입력원이 생기면
/// 안전망이 없어서 뚫린다.
/// </summary>
public class RuntimeRobot
{
    public string FighterId { get; }

    /// <summary>지금 이 틱에 고를 수 있는 행동 목록. 부위 파손 이벤트가 올 때만 갱신된다</summary>
    public IReadOnlyList<ActionData> AvailableActions => availableActions;

    /// <summary>목록이 바뀔 때마다 알림 — 나중에 UI가 버튼 활성/비활성을 다시 그릴 때 구독하면 된다</summary>
    public event Action OnAvailableActionsChanged;

    // 이 로봇이 "보유한" 전체 행동. 지금은 RobotAssembler가 없어서 생성자로 직접 받지만,
    // 나중에 실제 장착 파츠 기반 조립이 생기면 그쪽이 이 목록을 채워주는 쪽으로 바뀔 자리다 (§11-8)
    private readonly List<ActionData> allActions;
    private readonly List<ActionData> availableActions = new List<ActionData>();

    // CombatDataHub의 Player용/Enemy용 이벤트가 따로 나뉘어 있어서(§14), 내가 어느 쪽을
    // 구독했는지 Dispose 때 기억해둬야 한다 — 반대쪽을 구독 해제하면 아무 효과가 없다
    private readonly bool isPlayer;

    // 실제로 구독에 성공했는지. CombatDataHub(KKH)는 CombatClock과 달리 씬에 없으면 그냥 null을
    // 반환할 뿐 자동 생성되지 않는다(§14) — PlayerRobotBootstrap.Awake(이 클래스가 만들어지는 시점)가
    // DummyBattleBootstrap.Start(CombatDataHub를 실제로 만드는 시점)보다 먼저 돌아서, 생성자 시점엔
    // 아직 CombatDataHub가 없는 게 오히려 정상이다. 그래서 구독을 한 번에 끝내지 않고
    // EnsureSubscribed()를 계속 다시 시도할 수 있게 만들었다
    private bool subscribed;

    public RuntimeRobot(string fighterId, IEnumerable<ActionData> allActions)
    {
        FighterId = fighterId;
        this.allActions = new List<ActionData>(allActions);
        isPlayer = fighterId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;

        EnsureSubscribed();
        RebuildAvailableActions();
    }

    /// <summary>
    /// 호출: ActionExecutor.ExecuteTick(매 틱). 구독이 이미 됐으면 bool 하나 보고 바로 리턴이라 공짜에 가깝다.
    /// CombatDataHub가 생성자 시점엔 없었다가 그 뒤(DummyBattleBootstrap.Start 등)에 생겨도,
    /// 첫 틱이 돌 때쯤이면 씬 초기화가 전부 끝난 뒤이므로 여기서 확실하게 구독이 걸린다
    /// </summary>
    public void EnsureSubscribed()
    {
        if (subscribed) return;
        var hub = CombatDataHub.Instance;
        if (hub == null) return; // 아직도 없으면 다음 틱에 다시 시도

        if (isPlayer) hub.OnPlayerPartBroken += OnPartBroken;
        else hub.OnEnemyPartBroken += OnPartBroken;
        subscribed = true;
    }

    /// <summary>
    /// 호출: ActionExecutor.OnDestroy. 구독 해제 안 하면 파괴된 로봇의 콜백을 CombatDataHub가
    /// 계속 들고 있다가 다음 부위 파손 때 죽은 참조를 불러서 예외가 난다
    /// </summary>
    public void Dispose()
    {
        if (!subscribed) return;
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        if (isPlayer) hub.OnPlayerPartBroken -= OnPartBroken;
        else hub.OnEnemyPartBroken -= OnPartBroken;
        subscribed = false;
    }

    /// <summary>특정 행동 하나가 지금 쓸 수 있는지만 빠르게 묻고 싶을 때 — 매번 목록을 순회할 필요 없이 바로 답할 수 있다</summary>
    public bool IsAvailable(ActionData action) => action != null && availableActions.Contains(action);

    // 호출: CombatDataHub.OnPlayerPartBroken/OnEnemyPartBroken(부위 파손 때 딱 1회).
    // 어느 부위가 깨졌는지는 안 받아도 된다 — 그 부위가 요구하는 행동만 골라 빼는 게 아니라
    // 전체를 다시 계산하는 쪽이 코드가 단순하고, 로봇당 행동 개수가 많아야 십수 개라 비용도 미미하다
    private void OnPartBroken(BodyPart part) => RebuildAvailableActions();

    private void RebuildAvailableActions()
    {
        availableActions.Clear();
        var hub = CombatDataHub.Instance;

        foreach (var action in allActions)
        {
            if (action == null) continue;

            // 코어 고정(D, 회피, 이동)은 부위가 전부 파괴돼도 항상 나가야 한다 (§5 "출처 구분") —
            // 그래야 공격 수단이 0이 되는 상황 자체가 안 생긴다
            bool usable = action.Source == ActionSource.CoreFixed
                || hub == null // CombatDataHub가 아직 씬에 없으면 막을 근거가 없으니 전부 허용
                || !hub.IsPartBroken(FighterId, action.RequiredPart);

            if (usable) availableActions.Add(action);
        }

        OnAvailableActionsChanged?.Invoke();
    }
}
