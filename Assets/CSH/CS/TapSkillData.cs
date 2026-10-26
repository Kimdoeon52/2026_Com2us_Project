using UnityEngine;

/// <summary>
/// 탭형 키 베이스 (기획서 "스킬 키 유형 - 탭형": 짧게 누름 = 일반, 0.3초 이상 누름 = 강화(탄 1발)).
///
/// - 강화판정(chargedVariant)이 없으면: 누른 순간 이 에셋으로 시작 (기존 D/E/R과 같음)
/// - 강화판정이 있으면: 뗄 때 누른 길이로 일반/강화를 고른다 (기존 PlayerInputSource.UpdateArmCharge와 같음).
///   누르고 있는 동안은 행동이 아니므로 이동할 수 있다.
/// - 탄이 모자라면 강화판정 에셋의 fallbackAction(일반판정)으로 나간다 — 기존 실린더 규칙 그대로.
/// - 실행 흐름은 기본 러너(선딜 → 활성 → 후딜, 시작 시 쿨·탄 확정).
///
/// 이 에셋 자체가 "일반판정"이다. 개별 스킬(부스터 샷, 지면 강타, 붐버)은 이걸로 에셋을 만들거나,
/// 흐름이 달라야 하면 상속해서 CreateRunner를 바꾼다.
/// </summary>
[CreateAssetMenu(menuName = "CSH/Skill/Tap Skill (탭형 베이스)", fileName = "NewTapSkill")]
public class TapSkillData : SkillData
{
    [Header("탭형 — 짧게 = 이 에셋(일반판정), 길게 = 강화판정")]
    [Tooltip("길게 눌렀을 때 나갈 강화판정. 실린더 비용·탄 부족 시 폴백은 그 에셋의 cylinderCost / fallbackAction을 쓴다. 비우면 누르는 즉시 시작")]
    [SerializeField] private ActionData chargedVariant;

    [Tooltip("이 틱 수 이상 누르고 떼면 강화판정. 60fps 기준 18 = 0.3초")]
    [Min(1)][SerializeField] private int chargeThresholdFrames = 18;

    [Tooltip("0이 아니면, 이 틱 수만큼 누르고 있을 때 떼지 않아도 자동으로 강화판정이 나간다. 0 = 뗄 때까지 기다림")]
    [Min(0)][SerializeField] private int autoReleaseFrames;

    public ActionData ChargedVariant => chargedVariant;
    public int ChargeThresholdFrames => chargeThresholdFrames;
    public int AutoReleaseFrames => autoReleaseFrames;

    public override ActionData EvaluateTrigger(in SlotState slot)
    {
        if (chargedVariant == null) return slot.Pressed ? this : null;

        bool autoRelease = autoReleaseFrames > 0 && slot.Held && slot.HoldTicks == autoReleaseFrames;
        if (autoRelease) return chargedVariant;

        if (!slot.Released) return null;

        // 자동 발동 뒤에 손을 뗀 경우 — 이미 나갔으므로 무시
        if (autoReleaseFrames > 0 && slot.HoldTicks > autoReleaseFrames) return null;

        return slot.HoldTicks >= chargeThresholdFrames ? chargedVariant : this;
    }

    public override SkillRunner CreateRunner() => new StandardSkillRunner();
}
