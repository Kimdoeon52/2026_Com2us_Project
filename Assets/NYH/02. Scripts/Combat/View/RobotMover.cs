using UnityEngine;

/// <summary>
/// [임시/테스트용] IInputSource의 이동 입력을 실제 transform 이동으로 옮기는 자리.
/// 3층 구조 목록(CLAUDE.md §1)에 정식으로 없는 클래스라 나중에 팀 구조에 맞춰
/// 위치가 바뀌거나 다른 시스템에 흡수될 수 있음 — 지금은 "키 누르면 움직인다"를
/// 눈으로 확인하기 위한 최소 스캐폴드.
///
/// CombatClock이 아직 없어서 임시로 Update()에서 직접 프레임을 센다.
/// CombatClock 완성되면 이 이동 로직도 CombatTick()로 옮겨야 한다 (§3 원칙).
/// (참고: CombatClock 자체는 이제 있음. 이동 로직을 CombatTick으로 옮기는 작업은 아직 안 함 —
///  이동은 판정에 직접 쓰이는 값이 아니라서 우선순위가 낮았을 뿐, 위 원칙 자체는 여전히 유효)
/// </summary>
public class RobotMover : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f; // TODO: 임시값. 프레임표/기획 확정 전

    [Header("대시 — 수치 임시 (기획서에 배율 없음, CLAUDE.md §12)")]
    [Tooltip("←←/→→ 더블탭(IInputSource.GetDashInput()) 중일 때 moveSpeed에 곱하는 배율")]
    [SerializeField] private float dashSpeedMultiplier = 1.8f; // TODO: 임시값

    [Header("점프 — 수치 임시 (기획서에 물리값 없음, 애니메이션 묘사만 있음)")]
    [SerializeField] private float jumpVelocity = 6f;  // TODO: 임시값
    [SerializeField] private float gravity = -20f;      // TODO: 임시값

    // 상대 로봇의 Transform. 방향 전환에는 더 이상 안 쓴다(아래 참고) — Push박스 겹침 계산(ResolvePushOverlap)에만 쓰인다
    [SerializeField] private Transform opponent;
    [SerializeField] private CombatCamera combatCamera; // 비우면 씬에서 자동 검색

    private IInputSource inputSource;
    private ActionState actionState;
    // 지금 오른쪽을 보고 있는지. Update()에서 매 프레임 갱신되고, 다른 시스템(BoxDrawer, HitDetection)은
    // 이 필드를 직접 못 건드리고 아래 FacingRight 프로퍼티로 읽기만 한다
    private bool facingRight = true;

    // 점프 물리 상태. groundY는 Init 시점의 발밑 높이를 바닥으로 고정한다 — 지형 높낮이가 없는 평지 전제
    private float groundY;
    private float verticalVelocity;
    private bool isGrounded = true;

    /// <summary>현재 바라보는 방향. BoxDrawer/HitDetection이 좌우 반전 판정의 단일 기준으로 참조한다</summary>
    public bool FacingRight => facingRight;

    /// <summary>RobotMover와 RobotView를 분리하고 다른 시스템에 흡수는 수 있음 —
    /// 지금은 "키 누르면 움직인다"를 눈으로 확인하기 위한 최소 스캐폴드</summary>
    // 호출: PlayerRobotBootstrap.Awake. 받음: source(이동 키 읽기), state(공격 중 이동 잠금 판단)
    public void Init(IInputSource source, ActionState state)
    {
        inputSource = source;
        actionState = state;
        groundY = transform.position.y;
        // 인스펙터에서 안 넣어줬으면 씬에서 직접 찾는다 — 카메라는 씬에 보통 하나뿐이라 자동 검색이 안전함
        if (combatCamera == null) combatCamera = FindFirstObjectByType<CombatCamera>();
    }

    // 호출: Unity 매 렌더 프레임. 순서: 좌우 이동(대시 배율 포함) → 점프(Y축) → 화면 밖 보정 → 입력 방향으로 좌우 반전 → Push박스 겹침 보정
    private void Update()
    {
        // 공격·경직 등 Idle이 아닌 동안은 이동 입력을 무시한다
        // actionState가 null이어도 이동은 허용해야(테스트 시 ActionState 없이 이동만 확인하고 싶을 때 등) canMove를 true로 둔다
        bool canMove = actionState == null || actionState.CanMove;
        float moveInput = canMove ? (inputSource?.GetMoveInput() ?? 0f) : 0f;

        // 대시(더블탭) 중이면 moveSpeed에 배율을 곱한다 — 기획서 §6-12 "←←/→→ 달리기"
        bool isDashing = canMove && (inputSource?.GetDashInput() ?? false);
        float speedMultiplier = isDashing ? dashSpeedMultiplier : 1f;

        // Time.deltaTime을 곱해서 프레임 속도와 무관하게 "초당 moveSpeed만큼" 이동하게 한다.
        // (이동 자체는 아직 CombatTick으로 안 옮겨서, 클래스 주석대로 §3 원칙을 완전히 지키진 못한 임시 상태)
        transform.Translate(Vector3.right * moveInput * moveSpeed * speedMultiplier * Time.deltaTime, Space.World);

        UpdateJump(canMove);

        // 화면 밖으로 못 나가게 스테이지 벽 / 최대 거리로 보정
        if (combatCamera != null)
        {
            Vector3 pos = transform.position;
            pos.x = combatCamera.ClampFighterX(pos.x); // 방금 이동한 값을 그대로 화면 경계 안으로 다시 잘라낸다 — 먼저 이동시키고 나중에 보정하는 순서라 "밀어붙여도 벽을 못 뚫는" 느낌이 남
            transform.position = pos;
        }

        // 2026-10-02: 방향 전환 기준을 "상대 위치"에서 "이동 입력 방향"으로 교체했다.
        // 리얼스틸 기획서(1) 그래픽 §7-1 "전투 캐릭터 동작"의 "방향 전환 | 반대 방향 입력"이 근거 —
        // 할로우나이트/스컬류 보스전은 미러매치 대전격투가 아니라서, 뒷걸음질쳐도 상대를 계속
        // 바라보는 옛 SF2식 가정이 더 이상 안 맞는다. 입력이 없으면(제자리 정지) 마지막 방향을 유지한다
        if (Mathf.Abs(moveInput) > 0.01f) facingRight = moveInput > 0f;

        // 상대의 Push박스와 겹치면 그만큼 뒤로 밀어낸다 — 이게 없으면 Push박스는 그냥 Scene 뷰에
        // 그려지기만 할 뿐 아무 역할도 안 해서, 서로 그냥 뚫고 지나가 버린다 (§4 "몸끼리 겹쳐 지나가지 못하게")
        // TODO: 지금은 X축만 보고 미는데, 점프로 Y가 달라지는 상황(보스 위로 뛰어넘기 등)에서
        // 공중에 떠 있어도 수평으로 막히는 게 맞는지는 실제 플레이로 확인 필요 — 보스 크기 확정 전까지 보류
        ResolvePushOverlap();

        // localScale.x의 부호만 뒤집어서 스프라이트를 좌우 반전시킨다. Mathf.Abs로 감싼 이유는
        // 이미 한 번 뒤집힌 상태(x가 음수)에서 다시 뒤집으려 할 때 부호가 꼬이지 않게, 항상 "크기"만
        // 가져와서 새로 부호를 매기기 위함 — 그냥 sign을 곱하기만 하면 반복 실행 시 계속 반전되는 버그가 생김
        float sign = facingRight ? 1f : -1f;
        transform.localScale = new Vector3(sign * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    // 호출: Update. 단순 포물선 점프 — 지상에서만 발동, 체공 중엔 중력만 적용하다 바닥(groundY)에 닿으면 착지.
    // GetJumpInput()은 조건과 무관하게 매 프레임 반드시 한 번 호출해서 소비해야 한다 — 안 그러면
    // PlayerInputSource의 버퍼링 플래그가 안 비워져서 나중에 엉뚱한 타이밍에 점프가 터질 수 있다
    private void UpdateJump(bool canMove)
    {
        bool jumpPressed = inputSource?.GetJumpInput() ?? false;

        if (jumpPressed && canMove && isGrounded)
        {
            verticalVelocity = jumpVelocity;
            isGrounded = false;
        }

        if (isGrounded) return;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 pos = transform.position;
        pos.y += verticalVelocity * Time.deltaTime;

        if (pos.y <= groundY)
        {
            pos.y = groundY;
            verticalVelocity = 0f;
            isGrounded = true;
        }

        transform.position = pos;
    }

    // 호출: Update(이동·방향 계산 끝난 뒤). 내 Push박스가 상대 Push박스와 겹치면, 겹친 만큼 나만 뒤로 물러난다.
    // "나만" 미는 이유: 상대도 똑같이 자기 Update()에서 이 함수를 돌려서 스스로 물러날 것이므로,
    // 각자 자기 위치만 책임지면 결과적으로 서로 밀어내는 것과 같은 효과가 난다 — 굳이 상대의 transform까지
    // 건드리는 중앙 조정자를 따로 안 둬도 되는 단순한 방식
    private void ResolvePushOverlap()
    {
        if (opponent == null || actionState == null) return;

        // 상대의 ActionExecutor/RobotMover가 있어야 상대의 박스 데이터와 방향을 읽을 수 있다.
        // 없으면(아직 배선 전 등) 그냥 아무 보정도 안 하고 넘어간다
        var opponentExecutor = opponent.GetComponent<ActionExecutor>();
        if (opponentExecutor == null) return;
        var opponentMover = opponent.GetComponent<RobotMover>();
        bool opponentFacingRight = opponentMover == null || opponentMover.FacingRight;

        // 양쪽 다 "지금 활성화된 박스 중 Push 타입"을 찾는다. 공격 중인 액션에 아직 Push박스를
        // 안 채워뒀으면 null이 나올 수 있는데, 그럴 땐 보정할 기준이 없으니 그냥 넘어간다
        // (지금은 Idle.asset에만 Push박스가 채워져 있어서, 서로 걷기/대기 중일 때만 실제로 막힌다)
        Rect? myPush = FindPushWorldRect(actionState, transform.position, facingRight);
        Rect? theirPush = FindPushWorldRect(opponentExecutor.State, opponent.position, opponentFacingRight);
        if (myPush == null || theirPush == null) return;

        Rect a = myPush.Value;
        Rect b = theirPush.Value;

        // x축으로 얼마나 겹쳤는지 계산 — 두 구간 [min,max]의 교집합 길이. 이 게임은 좌우로만 싸우므로
        // y축 겹침은 따로 안 본다(어차피 둘 다 같은 바닥 높이에 서 있어서 y는 항상 겹치는 게 정상)
        float overlap = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        if (overlap <= 0f) return; // 안 겹치면 할 일 없음

        // 내가 상대보다 왼쪽에 있으면 나를 더 왼쪽으로, 오른쪽에 있으면 더 오른쪽으로 겹친 만큼 밀어낸다.
        // 이러면 내 Push박스의 경계가 상대 Push박스의 경계에 딱 맞닿는 위치까지만 물러나게 된다
        float pushDir = a.center.x <= b.center.x ? -1f : 1f;
        Vector3 pos = transform.position;
        pos.x += pushDir * overlap;
        transform.position = pos;
    }

    // state에 등록된 박스 중 Push 타입 하나를 찾아 월드 좌표로 변환해서 반환한다. 없으면 null.
    // (§4 "Push는 액션당 1개"라 보통 하나만 있지만, 혹시 여러 개 등록돼 있어도 여기선 첫 번째 것만 쓴다)
    private static Rect? FindPushWorldRect(ActionState state, Vector3 position, bool facingRight)
    {
        foreach (var box in state.GetActiveBoxes())
        {
            if (box.type == BoxType.Push)
                return BoxResolver.ToWorldRect(position, box.rect, facingRight);
        }
        return null;
    }
}
