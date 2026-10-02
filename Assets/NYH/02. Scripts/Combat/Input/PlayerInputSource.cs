using UnityEngine;

/// <summary>
/// 플레이어 키보드 입력을 ActionData로 변환하는 IInputSource 구현체 (CLAUDE.md §8, §11-9).
/// ActionExecutor는 이 클래스가 무엇인지 몰라도 된다 — IInputSource 인터페이스로만 다룬다.
///
/// 키 배치는 리얼스틸 기획서(1) §6-12 기준 (CLAUDE.md §8):
///   이동 ←→ / 대시 ←←·→→ / 점프 ↑ / D 고정 기본공격 / Q 왼팔 액티브 / W 오른팔 액티브 /
///   E 왼다리 액티브 / R 오른다리 액티브 / 회피 Space
///
/// 2026-10-02: 기획서(1) §6-11(실린더)·§7-1(그래픽 공격계열)에 따라 Q/W(팔 스킬)는
/// "짧게 누르면 일반판정, 길게 누르면 실린더 1발 소모하는 강화판정"으로 갈라진다.
/// 여기서는 짧게/길게만 구분해서 그에 맞는 ActionData를 큐에 넣는다 — 실제 실린더 보유량 확인,
/// "탄 없으면 길게 눌러도 일반판정" 폴백, 차지 포즈 연출은 전부 "연결" 단계(ActionExecutor/
/// CombatDataHub 쪽)에서 처리할 몫이라 여기서는 다루지 않는다 (CLAUDE_1.md §15).
/// 점프·대시도 이번에 "입력 수신"만 추가됐다 — 실제로 RobotMover를 점프/대시시키는 쪽은 아직 없다.
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

    // "길게 눌렀다"로 인정하는 최소 홀드 시간. 기획서엔 "짧게/길게"라고만 적혀 있고 정확한 기준이
    // 없다 — 임시값이니 체감 테스트 후 조정할 것. 지어낸 밸런스 수치이므로 CLAUDE.md §12 원칙상
    // 확정값처럼 쓰면 안 된다
    private const float CHARGE_HOLD_SECONDS = 0.3f;

    // 더블탭을 "대시"로 인정하는 최대 간격. 기획서에 수치 없음 — 마찬가지로 임시값 (CLAUDE_1.md §8)
    private const float DOUBLE_TAP_WINDOW_SECONDS = 0.3f;

    // 인스펙터에서 프레임표와 이름이 같은 .asset을 직접 드래그해서 연결한다 — 코드에 "D면 이 데미지"처럼
    // 하드코딩하지 않고, 어떤 키에 어떤 ActionData를 연결할지 자체를 데이터(인스펙터 값)로 뺀 것
    [Header("행동 에셋 연결 (인스펙터에서 프레임표와 이름이 같은 .asset 드래그)")]
    [Tooltip("D — 파츠 무관 고정 기본공격. 부위가 전부 파괴돼도 항상 사용 가능 (§5 ActionSource.CoreFixed)")]
    [SerializeField] private ActionData basicAttack;

    [Tooltip("Q 짧게 — 왼팔 일반판정. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData leftArmSkill;

    [Tooltip("Q 길게(차지) — 왼팔 강화판정. 실린더 1발 소모 예정(아직 미연결). 수치·에셋 전부 미정")]
    [SerializeField] private ActionData leftArmSkillCharged;

    [Tooltip("W 짧게 — 오른팔 일반판정. 수치·에셋 전부 미정 (CLAUDE.md §15)")]
    [SerializeField] private ActionData rightArmSkill;

    [Tooltip("W 길게(차지) — 오른팔 강화판정. 실린더 1발 소모 예정(아직 미연결). 수치·에셋 전부 미정")]
    [SerializeField] private ActionData rightArmSkillCharged;

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

    // Q/W를 각각 언제 누르기 시작했는지. 뗄 때(GetKeyUp) 이 값과 현재 시간의 차이로 짧게/길게를 가른다.
    // 음수(-1)는 "지금 안 누르고 있음" 상태를 뜻한다
    private float qPressTime = -1f;
    private float wPressTime = -1f;

    // 대시(더블탭) 판정용 — 마지막으로 탭한 시각과 방향을 기억해뒀다가, 같은 방향이 짧은 시간 안에
    // 다시 눌리면 대시로 인정한다
    private float lastTapTime = -999f;
    private int lastTapDirection;
    private bool isDashing;

    // 점프도 공격처럼 "누른 그 틱에 1회성으로 소비돼야 하는" 입력이라 GetDesiredAction과 같은
    // 버퍼링 패턴을 그대로 쓴다 (이동처럼 매 프레임 상태를 묻는 입력이 아니다)
    private bool pendingJump;
    private float pendingJumpTime;

    // 호출: Unity 매 렌더 프레임.
    private void Update()
    {
        UpdateInstantActions();     // D / E / R / Space — 누른 순간 바로 큐에 들어감
        UpdateArmCharge(KeyCode.Q, ref qPressTime, leftArmSkill, leftArmSkillCharged);
        UpdateArmCharge(KeyCode.W, ref wPressTime, rightArmSkill, rightArmSkillCharged);
        UpdateDashState();

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            pendingJump = true;
            pendingJumpTime = Time.time;
        }
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

    // 호출: (아직 없음 — 입력 수신만 먼저 만듦. RobotMover가 실제 점프를 처리하게 되면 그쪽에서 씀)
    public bool GetJumpInput()
    {
        if (!pendingJump) return false;

        pendingJump = false; // 1회성 소비 — GetDesiredAction과 같은 이유
        bool isExpired = Time.time - pendingJumpTime > INPUT_BUFFER_SECONDS;
        return !isExpired;
    }

    // 호출: (아직 없음 — 입력 수신만 먼저 만듦. RobotMover가 이동속도에 반영하게 되면 그쪽에서 씀)
    public bool GetDashInput() => isDashing;

    // 호출: Update. D/E/R/Space는 차지 개념이 없어서 기존처럼 누른 즉시 큐에 넣는다
    private void UpdateInstantActions()
    {
        // 위에서 아래로 순서대로 검사하다가 하나라도 맞으면 즉시 반환 — 여러 키를 동시에 눌러도
        // 코드에 먼저 적힌 키가 우선권을 갖는다(D가 최우선)
        if (Input.GetKeyDown(KeyCode.D)) { QueueAction(basicAttack); return; }
        if (Input.GetKeyDown(KeyCode.E)) { QueueAction(leftLegSkill); return; }
        if (Input.GetKeyDown(KeyCode.R)) { QueueAction(rightLegSkill); return; }
        if (Input.GetKeyDown(KeyCode.Space)) { QueueAction(dodge); return; }
    }

    // 호출: Update(Q/W 각각 한 번씩). 누른 시각을 기록해뒀다가, 뗄 때 길게/짧게를 갈라 큐에 넣는다.
    // charged 에셋을 아직 인스펙터에 안 꽂았으면(null) 그냥 일반판정으로 대체한다 — 이건 "실제 실린더가
    // 없을 때의 폴백"이 아니라 "아직 강화판정 에셋 자체가 없을 때" 안전하게 넘어가기 위한 임시 처리다.
    // 진짜 실린더 보유량 확인은 이 클래스가 아니라 ActionExecutor/CombatDataHub 쪽에서 나중에 해야 한다
    private void UpdateArmCharge(KeyCode key, ref float pressTime, ActionData normal, ActionData charged)
    {
        if (Input.GetKeyDown(key)) pressTime = Time.time;

        if (Input.GetKeyUp(key) && pressTime >= 0f)
        {
            bool isCharged = Time.time - pressTime >= CHARGE_HOLD_SECONDS;
            QueueAction(isCharged && charged != null ? charged : normal);
            pressTime = -1f;
        }
    }

    // 호출: Update. 같은 방향키가 DOUBLE_TAP_WINDOW_SECONDS 안에 다시 눌리면 대시 시작,
    // 그 방향을 더는 안 누르고 있으면(떼거나 반대 방향 입력) 대시 해제
    private void UpdateDashState()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow)) RegisterTap(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) RegisterTap(1);

        if (!isDashing) return;

        bool stillHoldingDashDirection = lastTapDirection < 0
            ? Input.GetKey(KeyCode.LeftArrow)
            : Input.GetKey(KeyCode.RightArrow);
        if (!stillHoldingDashDirection) isDashing = false;
    }

    private void RegisterTap(int direction)
    {
        bool isDoubleTap = direction == lastTapDirection && Time.time - lastTapTime <= DOUBLE_TAP_WINDOW_SECONDS;
        lastTapTime = Time.time;
        lastTapDirection = direction;
        if (isDoubleTap) isDashing = true;
    }

    // 새로 눌린 키가 있으면 이전에 대기 중이던 입력을 덮어쓴다 — 같은 틱 안에서 여러 키를 눌러도
    // 마지막에 누른 것 하나만 실행되게 하기 위한 단순한 정책 (동시입력 우선순위 규칙은 아직 없음)
    // action이 null이면 아무것도 안 한다 — 인스펙터에 에셋을 아직 안 꽂은 슬롯을 큐에 넣지 않기 위함
    private void QueueAction(ActionData action)
    {
        if (action == null) return;

        pendingAction = action;
        pendingTime = Time.time;
    }
}
