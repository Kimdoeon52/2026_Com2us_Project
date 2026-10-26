using UnityEngine;

/// <summary>
/// 플레이어 입력원 (레거시 Input 사용). 기존 PlayerInputSource를 대체한다.
///
/// 기존과 다른 점
/// - 행동(ActionData)을 모른다. 키 → 슬롯 버튼 상태(SkillInputFrame)만 만든다.
///   탭/길게/홀드 해석은 슬롯에 꽂힌 스킬이 한다 (SkillData.EvaluateTrigger, SkillRunner).
/// - 행동 시간 판정을 Time.time이 아니라 CombatTick 단위(HoldTicks)로 센다.
///
/// 이동·점프·대시는 기존 PlayerInputSource와 동작을 똑같이 유지한다 (RobotMover/RobotView가 IInputSource로 읽음).
///
/// 사용법: 플레이어 오브젝트의 PlayerInputSource를 빼고 이 컴포넌트 + PlayerSkillLoadout을 붙인다.
/// PlayerRobotBootstrap이 GetComponent&lt;IInputSource&gt;()로 찾으므로 IInputSource 구현체는 하나만 있어야 한다.
/// </summary>
public class PlayerSkillInputSource : MonoBehaviour, IInputSource, ISkillInputSource
{
    // 기존 PlayerInputSource와 같은 값 — 점프 버퍼, 대시 더블탭 판정
    private const float JUMP_BUFFER_SECONDS = CombatClock.TICK * 2f;
    private const float DOUBLE_TAP_WINDOW_SECONDS = 0.3f;

    [Header("슬롯 키 (레거시 Input)")]
    [SerializeField] private KeyCode basicKey = KeyCode.D;
    [SerializeField] private KeyCode leftArmKey = KeyCode.Q;
    [SerializeField] private KeyCode rightArmKey = KeyCode.W;
    [SerializeField] private KeyCode leftLegKey = KeyCode.E;
    [SerializeField] private KeyCode rightLegKey = KeyCode.R;
    [SerializeField] private KeyCode dodgeKey = KeyCode.Space;

    [Header("버퍼")]
    [Tooltip("행동 중(선딜/활성/후딜)에 들어온 시작 입력을 몇 틱 동안 들고 있을지. 기존 PlayerInputSource = 2틱")]
    [Min(0)][SerializeField] private int inputBufferTicks = 2;

    public int InputBufferTicks => inputBufferTicks;

    // ---- 슬롯 버튼 추적 ----
    // Update(렌더 프레임)와 CombatTick(60fps 고정)의 주기가 달라서, 눌림/뗌은 Update에서 래치해 두고 샘플할 때 소비한다.
    // 이렇게 안 하면 한 렌더 프레임 안에 틱이 여러 번 돌 때 GetKeyDown이 두 번 읽히거나, 틱 사이의 짧은 탭을 놓친다.
    private sealed class SlotTracker
    {
        public KeyCode Key;
        private bool latchedPressed;
        private bool latchedReleased;
        private int holdTicks;

        public void Poll()
        {
            if (Input.GetKeyDown(Key)) latchedPressed = true;
            if (Input.GetKeyUp(Key)) latchedReleased = true;
        }

        public SlotState Sample()
        {
            bool pressed = latchedPressed;
            bool released = latchedReleased;
            bool held = Input.GetKey(Key);
            latchedPressed = false;
            latchedReleased = false;

            if (pressed) holdTicks = 0;                     // 새로 눌렀으면 처음부터 센다
            if (pressed || held || released) holdTicks++;  // 누른 틱 = 1, 뗀 틱에도 1 더해서 마지막 길이를 넘긴다

            var state = new SlotState(pressed, held, released, holdTicks);
            if (!held) holdTicks = 0;                       // 떼고 난 뒤에는 0
            return state;
        }
    }

    private readonly SlotTracker basic = new SlotTracker();
    private readonly SlotTracker leftArm = new SlotTracker();
    private readonly SlotTracker rightArm = new SlotTracker();
    private readonly SlotTracker leftLeg = new SlotTracker();
    private readonly SlotTracker rightLeg = new SlotTracker();
    private readonly SlotTracker dodge = new SlotTracker();

    // 한 렌더 프레임에 Poll이 두 번 돌지 않게 하는 가드 (Update와 Sample 양쪽에서 부를 수 있으므로)
    private int lastPolledFrame = -1;

    // ---- 이동 계열 (기존 PlayerInputSource 그대로) ----
    private float lastTapTime = -999f;
    private int lastTapDirection;
    private bool isDashing;

    private bool pendingJump;
    private float pendingJumpTime;

    private void Awake() => ApplyKeys();

    private void OnValidate() => ApplyKeys();

    private void ApplyKeys()
    {
        basic.Key = basicKey;
        leftArm.Key = leftArmKey;
        rightArm.Key = rightArmKey;
        leftLeg.Key = leftLegKey;
        rightLeg.Key = rightLegKey;
        dodge.Key = dodgeKey;
    }

    private void Update()
    {
        PollSlots();
        UpdateDashState();

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            pendingJump = true;
            pendingJumpTime = Time.time;
        }
    }

    // CombatClock.Update가 이 컴포넌트의 Update보다 먼저 돌 수도 있어서, 샘플 직전에도 한 번 Poll한다.
    // 같은 렌더 프레임 안에서는 한 번만 돈다
    private void PollSlots()
    {
        if (lastPolledFrame == Time.frameCount) return;
        lastPolledFrame = Time.frameCount;

        basic.Poll();
        leftArm.Poll();
        rightArm.Poll();
        leftLeg.Poll();
        rightLeg.Poll();
        dodge.Poll();
    }

    // ---- ISkillInputSource ----
    public SkillInputFrame SampleSkillInput()
    {
        PollSlots();
        return new SkillInputFrame(
            basic.Sample(), leftArm.Sample(), rightArm.Sample(),
            leftLeg.Sample(), rightLeg.Sample(), dodge.Sample());
    }

    // ---- IInputSource ----
    // 스킬은 전부 SkillDriver 경로로 가므로 기존 경로에는 아무것도 주지 않는다.
    // (ActionExecutor는 ISkillInputSource를 구현한 입력원이면 이 함수를 부르지 않지만, 혹시 불려도 행동이 두 번 나가지 않게 null)
    public ActionData GetDesiredAction() => null;

    public float GetMoveInput()
    {
        if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
        if (Input.GetKey(KeyCode.RightArrow)) return 1f;
        return 0f;
    }

    public bool GetJumpInput()
    {
        if (!pendingJump) return false;

        pendingJump = false;
        bool isExpired = Time.time - pendingJumpTime > JUMP_BUFFER_SECONDS;
        return !isExpired;
    }

    public bool GetDashInput() => isDashing;

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
}
