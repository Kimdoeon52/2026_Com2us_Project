using UnityEngine;

/// <summary>
/// 일반 적(몹)·보스가 공용으로 쓸 수 있는 가장 단순한 AI 입력원 (§8, §11-9).
/// 규칙은 딱 하나뿐이다 — "사거리 밖이면 다가간다, 사거리 안이면 공격한다."
/// 보스 기믹처럼 복잡한 패턴은 여기 넣지 않는다. 그건 이 클래스가 다룰 일이 아니라,
/// 나중에 패턴 자체를 데이터(또는 별도 전략 클래스)로 뽑아서 올릴 자리다 — 지금은
/// "적도 플레이어와 같은 IInputSource 하나로 움직인다"는 구조만 검증하는 첫 단계.
///
/// DummyJabInputSource와 달리 이동(GetMoveInput)도 실제로 반환한다 — 그게 이 클래스가
/// 생긴 이유다. ActionExecutor/RobotMover 쪽은 한 줄도 안 바뀐다(§8 "입력만 바꿔 끼운다").
/// </summary>
public class AIInputSource : MonoBehaviour, IInputSource
{
    [Tooltip("쫓아가고 때릴 대상. 보통 Player의 Transform")]
    [SerializeField] private Transform opponent;

    [Tooltip("이 거리 안으로 들어오면 이동을 멈추고 공격한다. ActionData.사거리와 맞출 값 — 지금은 임시값")]
    [SerializeField] private float attackRange = 1.2f; // TODO: 임시값. D 사거리(기획서 1.2)와 일단 맞춰둠

    [Tooltip("사거리 안에 들어왔을 때 쓸 공격. 테스트 중엔 Enemy_Attack_1a 같은 더미 에셋을 꽂아두면 됨")]
    [SerializeField] private ActionData attackAction;

    // 같은 오브젝트의 ActionExecutor.Robot(RuntimeRobot)을 보려고 들고 있는다 — 공격 하나뿐인
    // 지금은 사실상 항상 통과하지만(테스트 에셋이 CoreFixed라 부위 파손과 무관하게 항상 사용 가능),
    // 나중에 attackAction이 여러 개로 늘어나도 "봉인된 기술은 고르지 않는다"가 자동으로 지켜지도록
    // 지금부터 이 경로로 물어보게 만들어둔다
    private ActionExecutor executor;

    private void Awake()
    {
        executor = GetComponent<ActionExecutor>();
    }

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 사거리 안이면 공격을 돌려주고, 밖이면 null(=이동만 계속)
    public ActionData GetDesiredAction()
    {
        if (opponent == null || attackAction == null) return null;
        if (DistanceToOpponent() > attackRange) return null;

        // Robot이 아직 없거나(배선 전) 이 공격이 지금 봉인 목록에 없으면 내지 않는다 (§5-6-1)
        var robot = executor != null ? executor.Robot : null;
        if (robot != null && !robot.IsAvailable(attackAction)) return null;

        return attackAction;
    }

    // 호출: RobotMover.Update(이동), RobotView(걷기 애니메이션 선택). 사거리 밖일 때만 상대 쪽으로 이동 입력을 낸다.
    // 사거리 안에서 0을 반환하는 이유: 계속 다가가게 두면 Push박스에 막혀 밀착한 채로 미세하게 떨림 —
    // "공격 가능 거리"에 들어오면 그 자리에서 공격만 하는 게 지금 단계에서는 더 안정적이다
    public float GetMoveInput()
    {
        if (opponent == null) return 0f;
        if (DistanceToOpponent() <= attackRange) return 0f;

        return opponent.position.x > transform.position.x ? 1f : -1f;
    }

    // 이번 버전은 점프·대시를 쓰지 않는다 — 보스 패턴에서 필요해지면 그때 이 자리에서 조건을 추가한다
    public bool GetJumpInput() => false;
    public bool GetDashInput() => false;

    private float DistanceToOpponent() => Mathf.Abs(opponent.position.x - transform.position.x);
}
