using UnityEngine;

/// <summary>
/// 장비 하나가 행동에 주는 "정해진 종류의 보정" 1세트 (박스 확대, 전진, 넉백 배율).
/// "어떤 행동에 적용되는가"는 기술 이름이 아니라 ActionTag / RequiredPart로 고른다 (기술 이름 분기 금지, §13).
///
/// 순수 수치 보정(이동속도, 공격력, 가드 방어력)은 여기 넣지 않는다 —
/// 이미 CombatantBuilder/CombatantSnapshot(KKH)이 처리한다. 이 효과는 "행동 자체가 달라지는" 경우 전용이다.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Effect/Action Modifier", fileName = "NewActionModifier")]
public class ActionModifierData : EquipmentEffect
{
    [Header("적용 대상")]
    [Tooltip("켜면 ActionData.RequiredPart가 아래 부위와 같은 행동에만 적용된다 (예: 오른팔 기술만 강화)")]
    [SerializeField] private bool limitToRequiredPart;

    [SerializeField] private BodyPart requiredPart;

    /// <summary>이 수정자가 주어진 행동에 적용되는지</summary>
    public bool Matches(ActionData action)
    {
        if (action == null) return false;
        if (limitToRequiredPart && action.RequiredPart != requiredPart) return false;
        return true;
    }

    public override EffectInstance CreateInstance(EffectContext ctx) => new Instance(this, ctx.Params);

    // 상태가 없는 효과라서 데이터 참조만 들고 있는다
    private sealed class Instance : EffectInstance
    {
        private readonly ActionModifierData data;
        private readonly SkillParamSet p;
        public Instance(ActionModifierData data, SkillParamSet p) { this.data = data; this.p = p; }

        public override void Contribute(ActionData action, ref ResolvedModifiers r)
        {
            if (!data.Matches(action)) return;

            
            
        }
}

}
