using UnityEngine;

/// <summary>
/// 플레이어 키보드 입력을 ActionData로 변환하는 IInputSource 구현체 (CLAUDE.md §8, §11-9).
/// ActionExecutor는 이 클래스가 무엇인지 몰라도 된다 — IInputSource 인터페이스로만 다룬다.
///
/// 키 배치는 0929 기획서 682~696행 기준 (CLAUDE.md §8, 2026-09-29 전면 교체):
///   이동 ←→ / D 고정 기본공격 / Q 왼팔 액티브 / W 오른팔 액티브 / E 왼다리 액티브 / R 오른다리 액티브 / 회피 Space
/// 잽/스트레이트/훅/어퍼컷/백스핀 엘보우/가드 키 배치는 전부 폐기됐다 — 이 파일이 그 교체본이다.
/// 점프(↑)·대시(←←/→→)는 아직 설계가 안 끝나서(CLAUDE.md §15) 여기서 다루지 않는다.
/// </summary>
public class PlayerInputSource : MonoBehaviour, IInputSource
{
    // GetKeyDown은 누른 렌더 프레임에서만 true라, 그 프레임에 전투 틱이 없으면 입력이 유실된다.
    // 그래서 Update에서 잡아두고 다음 틱에서 소비한다. 2틱이 지나면 만료 — 후딜 중 재입력은 무시되어야 한다 (§11-4)
    //
    // "왜 하필 2틱인가": 렌더링이 전투 틱(1/60초)보다 느린 순간(프레임 드랍 등)에도 최소 한 번은
    // 그 입력을 소비할 틱이 돌아오게 하기 위한 여유값이다. 1틱만 주면 타이밍이 딱 맞아떨어져야만
    // 입력이 살아남고, 너무 길게 주면(예: 10틱) 이미 지나간 오래된 입력이 뒤늦게 실행되는 이상한 느낌이 든다
    private const float INPUT_BUFFER_SECONDS = CombatClock.TICK * 2f;

    // 인스펙터에서 프레임표와 이름이 같은 .asset을 직접 드래그해서 연결한다 — 코드에 "D면 이 데미지"처럼
    // 하드코딩하지 않고, 어떤 키에 어떤 ActionData를 연결할지 자체를 데이터(인스펙터 값)로 뺀 것
    [Header("행동 에셋 연결 (인스펙터에서 프레임표와 이름이 같은 .asset 드래그)")]
    [Tooltip("D — 파츠 무관 고정 기본공격. 부위가 전부 파괴돼도 항상 사용 가능 (§5 ActionSource.CoreFixed)")]
    [SerializeField] private ActionData basicAttack;

    [Tooltip("Q — 왼팔 액티브. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData leftArmSkill;

    [Tooltip("W — 오른팔 액티브. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData rightArmSkill;

    [Tooltip("E — 왼다리 액티브. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData leftLegSkill;

    [Tooltip("R — 오른다리 액티브. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData rightLegSkill;

    [Tooltip("Space — 회피(구 위닝). 무적 판정은 ActionData.IsInvincibleDuringActive로 처리한다 (§5)")]
    [SerializeField] private ActionData dodge;

    // 키를 누른 "그 렌더 프레임"과, ActionExecutor가 실제로 그 값을 "가져가는 틱"의 타이밍이 다를 수 있어서
    // 즉시 반환하지 않고 일단 여기 잡아둔다 (자세한 이유는 위 INPUT_BUFFER_SECONDS 설명 참고)
    private ActionData pendingAction;
    private float pendingTime;

    // 호출: Unity 매 렌더 프레임. GetKeyDown은 누른 프레임에서만 true라, 틱이 없는 프레임의 입력이 사라지지 않게 pendingAction에 잡아 둔다
    private void Update()
    {
        ActionData pressed = ReadKeyDown();
        if (pressed == null) return; // 이번 프레임에 아무 공격 키도 안 눌렸으면 할 일 없음

        // 새로 눌린 키가 있으면 이전에 대기 중이던 입력을 덮어쓴다 — 같은 틱 안에서 여러 키를 눌러도
        // 마지막에 누른 것 하나만 실행되게 하기 위한 단순한 정책 (동시입력 우선순위 규칙은 아직 없음)
        pendingAction = pressed;
        pendingTime = Time.time;
    }

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 반환: 잡아 둔 행동을 1회 돌려주고 비운다. 2틱이 지나 만료됐으면 null
    public ActionData GetDesiredAction()
    {
        // 주의 — 여기서 "지금 이 부위가 파괴됐는지" 같은 판정은 하지 않는다.
        // 그건 §6-1 규칙대로 RuntimeRobot.availableActions가 판단할 몫이다.
        // (RuntimeRobot 완성 전까지는 그냥 눌린 키 → ActionData 매핑만 한다)

        if (pendingAction == null) return null; // 대기 중인 입력이 없으면 그냥 null — "이번 틱엔 하고 싶은 게 없다"는 뜻

        // 꺼내주기 전에 미리 비워둔다("1회성 소비") — 안 비우면 다음 틱에도 같은 행동이 계속 나가버려서
        // 한 번 키를 눌렀는데 같은 기술이 연속으로 계속 나가는 버그가 생긴다
        ActionData action = pendingAction;
        pendingAction = null;

        // 너무 오래된 입력(2틱 이상 지남)이면 실제로는 버려야 한다 — 예를 들어 긴 후딜 중에 눌러둔 입력이
        // 한참 뒤 Idle로 돌아왔을 때 뒤늦게 튀어나오면 플레이어가 예상 못 한 타이밍에 기술이 나가버린다
        bool isExpired = Time.time - pendingTime > INPUT_BUFFER_SECONDS;
        return isExpired ? null : action;
    }

    // 호출: RobotMover.Update(이동), RobotView.OnActionEnd(Walk/Idle 선택). 반환: -1(왼쪽) ~ 1(오른쪽)
    public float GetMoveInput()
    {
        // 이동은 공격 입력과 달리 버퍼링이 필요 없다 — "지금 이 순간 눌려있는지"만 알면 되고,
        // 키를 뗀 순간 즉시 멈춰야 자연스럽기 때문에 GetKey(누르고 있는 동안 계속 true)를 그대로 쓴다
        if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
        if (Input.GetKey(KeyCode.RightArrow)) return 1f;

        return 0f; // 둘 다 안 눌렸거나 둘 다 눌렸으면(상쇄) 중립
    }

    // 호출: Update. 이번 프레임에 눌린 키를 인스펙터에 연결된 ActionData로 바꿔서 돌려준다
    private ActionData ReadKeyDown()
    {
        // GetKeyDown(누른 그 순간 한 프레임만 true)을 쓰는 이유: 공격 키는 GetKey를 쓰면 누르고 있는
        // 내내 계속 true라서, "누른 순간 한 번만" 기술이 나가게 하려면 Down이 맞다.
        // 위에서 아래로 순서대로 검사하다가 하나라도 맞으면 즉시 반환 — 여러 키를 동시에 눌러도
        // 코드에 먼저 적힌 키가 우선권을 갖는다(D가 최우선)
        if (Input.GetKeyDown(KeyCode.D)) return basicAttack;
        if (Input.GetKeyDown(KeyCode.Q)) return leftArmSkill;
        if (Input.GetKeyDown(KeyCode.W)) return rightArmSkill;
        if (Input.GetKeyDown(KeyCode.E)) return leftLegSkill;
        if (Input.GetKeyDown(KeyCode.R)) return rightLegSkill;
        if (Input.GetKeyDown(KeyCode.Space)) return dodge;

        return null; // 아무 공격 키도 안 눌림
    }
}
