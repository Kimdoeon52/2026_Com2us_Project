using UnityEngine;

/// <summary>
/// 로봇의 몸 이동을 맡는 시스템 — 이동 입력(IInputSource)·점프·도약·띄우기를 실제 위치로 옮기고, 화면 끝 벽과
/// 상대 Push박스에 막히게 한다. 바라보는 방향(FacingRight)의 단일 기준이기도 하다.
///
/// 처음엔 "키 누르면 움직인다"를 눈으로 확인하는 임시 스캐폴드라 View 폴더에 있었는데, 틱 이관(아래) 뒤로는
/// 판정 위치를 정하는 전투 로직이 돼서 2026-10-28에 Systems 폴더로 옮겼다(GUID 유지라 씬 연결은 그대로).
///
/// 2026-10-27: 이동·점프·Push 보정을 Update()에서 CombatClock 틱(Tick)으로 옮겼다 (§3).
/// 전엔 Time.deltaTime으로 움직여서 판정(틱)과 위치(렌더 프레임)가 서로 다른 시계로 돌았고,
/// F9로 멈춘 뒤 Keypad8로 한 프레임씩 넘겨도 캐릭터는 안 움직였다. 이제 위치도 1/60초마다 정확히 한 번 바뀐다.
/// 렌더 프레임 사이 보간은 아직 없다 — 144Hz 모니터에서 움직임이 60Hz 계단처럼 보이면 그때 보간을 추가한다.
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

    // 2026-10-11 추가: 예전엔 원본 그림이 왼쪽을 보고 있어서 씬에서 로봇을 Y축 -180도 돌려 오른쪽을 보게 맞췄다.
    // 회전으로 좌우를 맞추면 회전을 복사하는 이펙트(총알·화염·잔상)까지 전부 그 사정을 알아야 해서, 회전은 0으로 두고
    // "원본 그림이 어느 쪽을 보는지"를 여기 한 곳에서 알려주는 방식으로 바꿨다. 지금 BoxRobot·플레이어 시트는 둘 다 왼쪽
    [Tooltip("원본 스프라이트(시트 그림)가 오른쪽을 보고 있으면 체크. 지금 BoxRobot·플레이어 그림은 왼쪽이라 끔")]
    [SerializeField] private bool spriteFacesRight;

    // 상대 로봇의 Transform. 방향 전환에는 더 이상 안 쓴다(아래 참고) — Push박스 겹침 계산(ResolvePushOverlap)에만 쓰인다
    [SerializeField] private Transform opponent;
    [SerializeField] private CombatCamera combatCamera; // 비우면 씬에서 자동 검색

    private IInputSource inputSource;
    private ActionState actionState;
    // 히트스톱 중인지 묻는 용도(2026-10-28). 같은 몸에 붙은 실행기 — 없으면 히트스톱 없이 그냥 움직인다
    private ActionExecutor executor;
    // 지금 오른쪽을 보고 있는지. Tick()에서 매 틱 갱신되고, 다른 시스템(BoxDrawer, HitDetection)은
    // 이 필드를 직접 못 건드리고 아래 FacingRight 프로퍼티로 읽기만 한다
    private bool facingRight = true;

    // 점프 물리 상태. groundY는 Init 시점의 발밑 높이를 바닥으로 고정한다 — 지형 높낮이가 없는 평지 전제
    private float groundY;
    private float verticalVelocity;
    private bool isGrounded = true;

    // 현재 수평 이동 속도(부호 있음). verticalVelocity와 같은 이유로 필드로 들고 있는다 — 공중에서
    // 공격(canMove=false) 중에도 이 값을 그대로 유지해야 "점프 중 공격하면 관성이 뚝 끊기는" 문제가
    // 안 생긴다. 지상에서는 매 프레임 입력으로 덮어쓰므로 평소엔 그냥 "지금 입력" 그대로다
    private float moveVelocity;

    // 2026-10-27 추가: Leap(스스로 뛰는 기술)로 떴는지. 켜져 있는 동안은 공중에서 입력을 안 읽어서 궤적·방향이 고정되고,
    // 착지하면 꺼진다. 일반 점프(↑)는 이걸 안 켜므로 예전처럼 공중에서도 입력으로 움직일 수 있다
    private bool airLocked;

    /// <summary>현재 바라보는 방향. BoxDrawer/HitDetection이 좌우 반전 판정의 단일 기준으로 참조한다</summary>
    public bool FacingRight => facingRight;

    /// <summary>원본 그림이 오른쪽을 보는지. 같은 시트에서 나온 이펙트(총알 등)도 이 규칙으로 뒤집는다</summary>
    public bool SpriteFacesRight => spriteFacesRight;

    /// <summary>
    /// facingRight 쪽을 보게 하려면 localScale.x에 곱할 부호. 원본 그림 방향이 같으면 +1, 반대면 -1.
    /// 좌우 반전 규칙은 이 함수 하나로 모은다 — 로봇·총알이 각자 따로 계산하면 한쪽만 바꾸고 다른 쪽을 빠뜨린다
    /// </summary>
    public float ScaleSignFor(bool facingRight) => facingRight == spriteFacesRight ? 1f : -1f;

    /// <summary>땅에 있는지. RobotView(점프/착지 그림), AI(상대가 공중인지), HitReactionSystem(공중 경직 여부)이 읽는다</summary>
    public bool IsGrounded => isGrounded;

    /// <summary>현재 위아래 속도. 양수면 올라가는 중</summary>
    public float VerticalVelocity => verticalVelocity;

    /// <summary>
    /// 맞아서 위로 뜨게 만든다 (어퍼컷 등 ActionData.LaunchVelocity). 점프와 같은 물리를 그대로 쓴다 —
    /// 행동 중이든 아니든 무조건 뜬다. 이미 떠 있으면 위쪽 속도만 새로 덮어쓴다(공중 추가타)
    /// </summary>
    // 호출: KnockbackSystem.Apply. 받음: 위쪽 초기 속도
    public void Launch(float upwardVelocity)
    {
        if (upwardVelocity <= 0f) return;
        verticalVelocity = upwardVelocity;
        isGrounded = false;
    }

    /// <summary>
    /// 스스로 뛴다 (긴급 이탈 백점프 등 ActionData.LeapVelocity). 수평 속도까지 정해서 뛰고, 착지할 때까지
    /// 입력을 안 읽어서 궤적과 바라보는 방향이 고정된다 — 뒤로 뛰면서도 상대를 계속 바라본다
    /// </summary>
    // 호출: ActionExecutor.ApplyLeap. 받음: 월드 기준 수평 속도(부호 = 방향), 위쪽 초기 속도
    public void Leap(float horizontalVelocity, float upwardVelocity)
    {
        moveVelocity = horizontalVelocity;
        if (upwardVelocity > 0f)
        {
            verticalVelocity = upwardVelocity;
            isGrounded = false;
        }
        airLocked = !isGrounded;
    }

    /// <summary>RobotMover와 RobotView를 분리하고 다른 시스템에 흡수는 수 있음 —
    /// 지금은 "키 누르면 움직인다"를 눈으로 확인하기 위한 최소 스캐폴드</summary>
    // 호출: PlayerRobotBootstrap.Awake. 받음: source(이동 키 읽기), state(공격 중 이동 잠금 판단)
    public void Init(IInputSource source, ActionState state)
    {
        inputSource = source;
        actionState = state;
        executor = GetComponent<ActionExecutor>();
        groundY = transform.position.y;
        // 인스펙터에서 안 넣어줬으면 씬에서 직접 찾는다 — 카메라는 씬에 보통 하나뿐이라 자동 검색이 안전함
        if (combatCamera == null) combatCamera = FindFirstObjectByType<CombatCamera>();

        // 예전 방식(Y축 -180 회전)이 씬에 남아 있으면 그림이 두 번 뒤집혀 뒤를 보고 걷는다 — 조용히 틀어지지 않게 알려준다
        if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 1f)
            Debug.LogWarning($"[RobotMover] {name}: 회전이 {transform.eulerAngles}로 남아 있음 — 좌우는 이제 spriteFacesRight로 맞추므로 Rotation을 0,0,0으로 바꿀 것");
    }

    // 호출: CombatClock.OnCombatTick (PlayerRobotBootstrap이 ActionExecutor.ExecuteTick 다음에 등록).
    // 순서: 좌우 이동(대시 배율 포함) → 점프(Y축) → 화면 밖 보정 → 입력 방향으로 좌우 반전 → Push박스 겹침 보정
    public void Tick()
    {
        // 히트스톱: 맞은 쪽(배율 0)은 공중에 떠 있어도 그 자리에 멈춘다(중력도 멈춤) — 어퍼컷 띄우기 속도도 보류됐다가
        // 정지가 풀리는 틱부터 날아가서 "멈췄다가 튕겨 나가는" 모양이 된다. 때린 쪽은 평소의 몇 %(HitStopTimeScale)로
        // 계속 움직인다 — 공중에서 때렸으면 아주 느리게 떨어진다. 정지 중엔 둘 다 입력을 안 읽어서 방향이 안 바뀐다
        float dt = CombatClock.TICK;
        bool slowedByHitStop = false;
        if (executor != null && executor.FrozenThisTick)
        {
            if (executor.HitStopTimeScale <= 0f) return;
            dt *= executor.HitStopTimeScale;
            slowedByHitStop = true;
        }

        // 공격·경직 등 Idle이 아닌 동안은 이동 입력을 무시한다
        // actionState가 null이어도 이동은 허용해야(테스트 시 ActionState 없이 이동만 확인하고 싶을 때 등) canMove를 true로 둔다
        bool canMove = !slowedByHitStop && (actionState == null || actionState.CanMove);
        float moveInput = 0f;

        // 도약(Leap) 중이면 입력을 아예 안 읽는다 — 수평 속도와 방향이 착지까지 그대로 간다
        bool leaping = airLocked && !isGrounded;

        if (canMove && !leaping)
        {
            moveInput = inputSource?.GetMoveInput() ?? 0f;
            // 대시(더블탭) 중이면 moveSpeed에 배율을 곱한다 — 기획서 §6-12 "←←/→→ 달리기"
            bool isDashing = inputSource?.GetDashInput() ?? false;
            float speedMultiplier = isDashing ? dashSpeedMultiplier : 1f;
            moveVelocity = moveInput * moveSpeed * speedMultiplier;
        }
        else if (isGrounded && !slowedByHitStop)
        {
            // 지상에서 행동(공격 등) 중이면 그 자리에 멈춘다 — 기존 동작과 동일.
            // 히트스톱으로 느려진 틱은 속도를 지우지 않는다 — 정지가 풀리면 원래 속도 그대로 이어서 움직여야 한다
            moveVelocity = 0f;
        }
        // 공중에서 행동 중이면(canMove=false && !isGrounded) moveVelocity를 아무도 안 건드린다 —
        // 점프 직전/직후 마지막으로 읽은 속도가 그대로 유지되어 수평 관성이 끊기지 않는다.
        // 공중에서 입력으로 궤적을 바꾸고 싶어지면(에어 컨트롤) 이 분기를 다시 설계해야 함 — 지금은
        // "공중 상태를 어떻게 다룰지"가 §15 미정 항목이라 최소한의 수정만 했다

        // 틱 하나 = 정확히 1/60초라 TICK을 곱하면 "초당 moveVelocity만큼"이 된다 — 주사율과 무관 (§3).
        // 히트스톱으로 느려진 틱이면 dt가 그 비율만큼 작다
        transform.Translate(Vector3.right * moveVelocity * dt, Space.World);

        UpdateJump(canMove, dt, consumeInput: !slowedByHitStop);

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
        float sign = ScaleSignFor(facingRight);
        transform.localScale = new Vector3(sign * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    // 호출: Tick. 단순 포물선 점프 — 지상에서만 발동, 체공 중엔 중력만 적용하다 바닥(groundY)에 닿으면 착지.
    // GetJumpInput()은 조건과 무관하게 매 틱 반드시 한 번 호출해서 소비해야 한다 — 안 그러면
    // 입력원의 버퍼링 플래그가 안 비워져서 나중에 엉뚱한 타이밍에 점프가 터질 수 있다.
    // 입력원들은 ↑를 Update에서 받아 틱이 읽을 때까지 들고 있으므로(래치), 틱에서 읽어도 입력이 유실되지 않는다
    // dt: 이번 틱의 시간(평소 TICK, 히트스톱으로 느려지면 더 작음). consumeInput: false면 점프 입력을 안 꺼낸다 —
    // 히트스톱 중 누른 ↑는 입력원에 남겨뒀다가 정지가 풀린 뒤 처리한다
    private void UpdateJump(bool canMove, float dt, bool consumeInput)
    {
        bool jumpPressed = consumeInput && (inputSource?.GetJumpInput() ?? false);

        if (jumpPressed && canMove && isGrounded)
        {
            verticalVelocity = jumpVelocity;
            isGrounded = false;
        }

        if (isGrounded) return;

        verticalVelocity += gravity * dt;

        Vector3 pos = transform.position;
        pos.y += verticalVelocity * dt;

        if (pos.y <= groundY)
        {
            pos.y = groundY;
            verticalVelocity = 0f;
            isGrounded = true;
            airLocked = false; // 도약 궤적 고정은 착지와 함께 끝난다
        }

        transform.position = pos;
    }

    // 호출: Tick(이동·방향 계산 끝난 뒤). 내 Push박스가 상대 Push박스와 겹치면, 겹친 만큼 나만 뒤로 물러난다.
    // "나만" 미는 이유: 상대도 똑같이 자기 Tick()에서 이 함수를 돌려서 스스로 물러날 것이므로,
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
