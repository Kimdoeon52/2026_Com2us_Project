using UnityEngine;

[CreateAssetMenu(menuName = "NYH/Combat/Effect/Action Replace", fileName = "NewActionReplace")]
public class ActionReplaceEffect : EquipmentEffect
{
    [Header("바꿔치기 당할 기본 슬롯 행동 (PlayerInputSource 슬롯에 꽂힌 에셋)")]
    [Tooltip("Q/W 짧게 누를 때 큐에 들어가는 기본 행동")]
    [SerializeField] private ActionData slotNormal;

    [Tooltip("Q/W 길게 누를 때 큐에 들어가는 기본 강화 행동. 비워두면 강화 입력은 바꾸지 않는다")]
    [SerializeField] private ActionData slotCharged;

    [Header("대신 나갈 스킬")]
    [Tooltip("일반판정 (예: Booster_Shot)")]
    [SerializeField] private ActionData normal;

    [Tooltip("강화판정 (예: Booster_Shot_Charged). 비워두면 강화 입력도 일반판정으로 나간다")]
    [SerializeField] private ActionData charged;

    [Tooltip("같은 슬롯을 여러 효과가 바꾸려 할 때 높은 값이 이긴다")]
    [SerializeField] private int replacePriority;

    public ActionData Normal => normal;
    public ActionData Charged => charged;

    public override EffectInstance CreateInstance(EffectContext ctx) => new Instance(this);

    // 상태가 없는 효과라서 데이터 참조만 들고 있는다
    private sealed class Instance : EffectInstance
    {
        private readonly ActionReplaceEffect data;
        public Instance(ActionReplaceEffect data) { this.data = data; }

        public override int ReplacePriority => data.replacePriority;

        public override ActionData Replace(ActionData requested)
        {
            if (requested == null) return null;

            if (data.slotNormal != null && requested == data.slotNormal)
                return data.normal;

            if (data.slotCharged != null && requested == data.slotCharged)
                return data.charged != null ? data.charged : data.normal;

            return null;
        }
    }
}
