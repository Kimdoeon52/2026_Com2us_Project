// ============================================================================
// 스킬 입력 공통 타입
// ----------------------------------------------------------------------------
// 기존 IInputSource.GetDesiredAction()은 "완성된 ActionData 하나"만 넘겨서,
// 행동이 시작된 뒤 "키를 아직 누르고 있는지 / 홀드 중 D를 눌렀는지"를 행동 쪽이 알 수 없었다.
// 그래서 입력은 "슬롯별 버튼 상태"까지만 만들고, 그 상태를 어떻게 해석할지는 스킬(SkillData/SkillRunner)이 정한다.
// → Q 슬롯에 탭형이 끼든 홀드형이 끼든 입력 코드는 바뀌지 않는다.
// ============================================================================

/// <summary>
/// 행동 슬롯. 키가 아니라 "몸의 어느 자리에 붙은 행동인가"를 뜻한다.
/// 키 배치는 PlayerSkillInputSource가, 슬롯에 어떤 행동이 꽂히는지는 ISkillLoadout이 정한다.
/// </summary>
public enum ActionSlot
{
    Basic,    // D — 고정 기본기
    LeftArm,  // Q
    RightArm, // W
    LeftLeg,  // E
    RightLeg, // R
    Dodge,    // Space
}

/// <summary>
/// 한 슬롯의 이번 틱 버튼 상태. 전부 CombatTick 단위로 센다 (시간이 아니라 틱 — 프레임레이트와 무관하게 판정이 같다).
/// </summary>
public readonly struct SlotState
{
    /// <summary>이번 틱에 눌렸다 (지난 샘플 이후 한 번이라도 눌렸으면 true — 틱 사이의 짧은 탭도 놓치지 않는다)</summary>
    public readonly bool Pressed;

    /// <summary>지금 누르고 있다</summary>
    public readonly bool Held;

    /// <summary>이번 틱에 뗐다</summary>
    public readonly bool Released;

    /// <summary>
    /// 누른 틱부터 센 틱 수 (누른 틱 = 1). 뗀 틱에도 마지막 값이 그대로 들어온다 — 탭/길게 판정은 Released 틱에 이 값을 본다.
    /// 누르고 있지 않으면 0.
    /// </summary>
    public readonly int HoldTicks;

    public SlotState(bool pressed, bool held, bool released, int holdTicks)
    {
        Pressed = pressed;
        Held = held;
        Released = released;
        HoldTicks = holdTicks;
    }

    public bool Any => Pressed || Held || Released;
}

/// <summary>
/// 한 틱 분량의 스킬 입력 스냅샷. SkillDriver가 틱마다 1번 받아서 시작 판정과 실행 중인 러너에 같이 넘긴다.
/// struct라 매 틱 만들어도 GC가 생기지 않도록 슬롯 6개를 필드로 펼쳐 둔다.
/// </summary>
public readonly struct SkillInputFrame
{
    private readonly SlotState basic, leftArm, rightArm, leftLeg, rightLeg, dodge;

    public SkillInputFrame(SlotState basic, SlotState leftArm, SlotState rightArm,
                           SlotState leftLeg, SlotState rightLeg, SlotState dodge)
    {
        this.basic = basic;
        this.leftArm = leftArm;
        this.rightArm = rightArm;
        this.leftLeg = leftLeg;
        this.rightLeg = rightLeg;
        this.dodge = dodge;
    }

    public SlotState this[ActionSlot slot]
    {
        get
        {
            switch (slot)
            {
                case ActionSlot.Basic: return basic;
                case ActionSlot.LeftArm: return leftArm;
                case ActionSlot.RightArm: return rightArm;
                case ActionSlot.LeftLeg: return leftLeg;
                case ActionSlot.RightLeg: return rightLeg;
                case ActionSlot.Dodge: return dodge;
                default: return default;
            }
        }
    }
}

/// <summary>
/// 슬롯 단위 입력을 줄 수 있는 입력원. ActionExecutor.Init에서 입력원이 이 인터페이스를 구현하면
/// 기존 GetDesiredAction 경로 대신 SkillDriver 경로를 탄다 (플레이어 전용. 적/보스는 기존 경로 유지).
/// </summary>
public interface ISkillInputSource
{
    /// <summary>CombatTick마다 정확히 1번 호출된다. 호출 시점에 "지난 샘플 이후" 눌림/뗌을 소비한다</summary>
    SkillInputFrame SampleSkillInput();

    /// <summary>행동 중에 들어온 시작 입력을 몇 틱 동안 들고 있을지 (기존 PlayerInputSource의 2틱 버퍼와 같은 역할)</summary>
    int InputBufferTicks { get; }
}

/// <summary>
/// 슬롯 → 행동 매핑. 지금은 PlayerSkillLoadout(인스펙터)이 채우고,
/// 나중에 파츠 조립(RobotAssembler)이 생기면 그쪽이 같은 인터페이스로 채우면 된다.
/// </summary>
public interface ISkillLoadout
{
    /// <summary>슬롯에 꽂힌 행동. 비어 있으면 null. ActionData(기존 단발 행동)와 SkillData(새 스킬) 둘 다 올 수 있다</summary>
    ActionData GetSlotAction(ActionSlot slot);
}
