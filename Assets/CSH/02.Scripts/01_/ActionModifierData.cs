using UnityEngine;
using UnityEngine.Serialization;
/// <summary>
/// 장비 하나가 행동에 주는 "정해진 종류의 보정" 1세트 (박스 확대, 전진, 넉백 배율).
/// "어떤 행동에 적용되는가"는 기술 이름이 아니라 부위로 고른다 (기술 이름 분기 금지, §13).
/// 플레이어는 행동이 나온 슬롯의 부위, 적/보스는 ActionData.RequiredPart 기준.
///
/// 순수 수치 보정(이동속도, 공격력, 가드 방어력)은 여기 넣지 않는다 —
/// 이미 CombatantBuilder/CombatantSnapshot(KKH)이 처리한다. 이 효과는 "행동 자체가 달라지는" 경우 전용이다.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Effect/Action Modifier", fileName = "NewActionModifier")]
public class ActionModifierData : EquipmentEffect
{
    [Header("적용 대상")]
    // 예전 필드명 limitToRequiredPart를 그대로 이어받는다 (값의 의미도 같음: 켜면 전체, 끄면 부위 한정).
    // 이름만 의미에 맞게 바꿨으니 기존 에셋 값은 유지된다
    [Tooltip("켜면 모든 행동에 적용. 끄면 아래 부위의 행동에만 적용된다 (예: 오른팔 기술만 강화). 플레이어는 슬롯(LeftArm=Q, RightArm=W, LeftLeg=E, RightLeg=R) 기준, 적/보스는 ActionData.RequiredPart 기준")]
    [FormerlySerializedAs("limitToRequiredPart")]
    [SerializeField] private bool applyToAllActions;

    [SerializeField] private BodyPart SourcePart;

    /// <summary>
    /// 이 수정자가 지금 시작되는 행동에 적용되는지.
    /// 플레이어(SkillDriver 있음): ActionData.RequiredPart는 무시하고, 행동이 나온 슬롯의 부위로 판정한다.
    /// 적/보스(SkillDriver 없음): 슬롯 개념이 없으므로 기존처럼 ActionData.RequiredPart로 판정한다.
    /// </summary>
    public bool Matches(ActionData action, ActionExecutor executor)
    {
        if (action == null) return false;
        if (applyToAllActions) return true;

        SkillDriver driver = executor != null ? executor.SkillDriver : null;
        if (driver == null)
            return action.RequiredPart == SourcePart;

        ActionSlot? slot = driver.ActiveSlot;
        if (!slot.HasValue) return false;

        return slot.Value.TryGetBodyPart(out BodyPart part) && part == SourcePart;
    }

    public override EffectInstance CreateInstance(EffectContext ctx) => new Instance(this, ctx.Params, ctx.Executor);

    // 상태가 없는 효과라서 데이터 참조만 들고 있는다
    private sealed class Instance : EffectInstance
    {
        private readonly ActionModifierData data;
        private readonly SkillParamSet p;
        private readonly ActionExecutor executor;
        public Instance(ActionModifierData data, SkillParamSet p, ActionExecutor executor)
        {
            this.data = data;
            this.p = p;
            this.executor = executor;
        }

        public override void Contribute(ActionData action, ResolvedModifiers r)
        {
            if (!data.Matches(action, executor)) return;


            var list = data.BaseParams;
            for (int i = 0; i < list.Length; i++)
            {
                SkillParamEntry m = list[i];
                switch (m.op)
                {
                    case ParamOption.Set: r.Set(m.id, m.value); break;
                    case ParamOption.Add: r.Add(m.id, m.value); break;
                    case ParamOption.Multiply: r.Multiply(m.id, m.value); break;
                }
            }
        }
    }


}