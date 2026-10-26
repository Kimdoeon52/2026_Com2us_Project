using UnityEngine;

/// <summary>
/// 슬롯 → 행동 매핑 (인스펙터 수동 연결 버전).
/// 기존 PlayerInputSource에 있던 basicAttack / leftArmSkill / ... 필드가 여기로 옮겨온 것이다.
///
/// 기존과 다른 점: Q/W의 "일반판정 / 강화판정" 두 칸이 없다.
/// 강화판정은 슬롯에 꽂힌 스킬(TapSkillData.chargedVariant, HoldSkillData.enhancedVariant)이 직접 들고 있다.
/// 일반 ActionData(D, 회피 등)를 꽂으면 기존처럼 "누르면 바로 시작"으로 동작한다.
///
/// 나중에 파츠 조립이 생기면 SetSlot으로 채우거나, ISkillLoadout을 구현한 다른 컴포넌트로 교체하면 된다.
/// </summary>
public class PlayerSkillLoadout : MonoBehaviour, ISkillLoadout
{
    [Tooltip("D — 파츠 무관 고정 기본기")]
    [SerializeField] private ActionData basic;

    [Tooltip("Q — 왼팔")]
    [SerializeField] private ActionData leftArm;

    [Tooltip("W — 오른팔")]
    [SerializeField] private ActionData rightArm;

    [Tooltip("E — 왼다리")]
    [SerializeField] private ActionData leftLeg;

    [Tooltip("R — 오른다리")]
    [SerializeField] private ActionData rightLeg;

    [Tooltip("Space — 회피")]
    [SerializeField] private ActionData dodge;

    public ActionData GetSlotAction(ActionSlot slot)
    {
        switch (slot)
        {
            case ActionSlot.Basic: return basic;
            case ActionSlot.LeftArm: return leftArm;
            case ActionSlot.RightArm: return rightArm;
            case ActionSlot.LeftLeg: return leftLeg;
            case ActionSlot.RightLeg: return rightLeg;
            case ActionSlot.Dodge: return dodge;
            default: return null;
        }
    }

    /// <summary>런타임에 슬롯 행동을 바꾼다 (파츠 장착 등). 실행 중인 행동에는 영향 없고 다음 입력부터 적용된다</summary>
    public void SetSlot(ActionSlot slot, ActionData action)
    {
        switch (slot)
        {
            case ActionSlot.Basic: basic = action; break;
            case ActionSlot.LeftArm: leftArm = action; break;
            case ActionSlot.RightArm: rightArm = action; break;
            case ActionSlot.LeftLeg: leftLeg = action; break;
            case ActionSlot.RightLeg: rightLeg = action; break;
            case ActionSlot.Dodge: dodge = action; break;
        }
    }
}
