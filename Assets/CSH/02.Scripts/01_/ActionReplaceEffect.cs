using UnityEngine;

/// <summary>
/// 슬롯에 꽂힌 행동을 다른 행동으로 바꿔치기하는 장비 효과.
/// 강화판정은 대신 나갈 스킬(TapSkillData.chargedVariant / HoldSkillData.enhancedVariant)이 직접 들고 있으므로
/// 여기서는 슬롯 행동 하나만 바꾼다.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Effect/Action Replace", fileName = "NewActionReplace")]
public class ActionReplaceEffect : EquipmentEffect
{
    [Tooltip("바꿔치기 당할 슬롯 행동 (PlayerSkillLoadout 슬롯에 꽂힌 에셋)")]
    [SerializeField] private ActionData slotAction;

    [Tooltip("대신 나갈 스킬 (예: BoostShot)")]
    [SerializeField] private ActionData replacement;

    [Tooltip("같은 슬롯을 여러 효과가 바꾸려 할 때 높은 값이 이긴다")]
    [SerializeField] private int replacePriority;

    public override EffectInstance CreateInstance(EffectContext ctx) => new Instance(this);

    // 상태가 없는 효과라서 데이터 참조만 들고 있는다
    private sealed class Instance : EffectInstance
    {
        private readonly ActionReplaceEffect data;
        public Instance(ActionReplaceEffect data) { this.data = data; }

        public override int ReplacePriority => data.replacePriority;

        public override ActionData Replace(ActionData requested)
        {
            if (requested == null || data.slotAction == null) return null;
            return requested == data.slotAction ? data.replacement : null;
        }
    }
}
