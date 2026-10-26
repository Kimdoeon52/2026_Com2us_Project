// ============================================================================
// SkillData — 새 스킬 베이스의 데이터(정의) 쪽
// ----------------------------------------------------------------------------
// ActionData를 상속하는 이유
//   ActionData를 직접 읽는 곳이 많다: HitDetection(박스·무적), KKH CombatCalculator(Damage/IsGuardable/StaggerValue),
//   CombatDataHub.CanExecuteAction(부위), 쿨타임 키(CooldownKey), 실린더(CylinderCost/FallbackAction),
//   장비 효과(ActionReplaceEffect/ActionModifierData), RobotView(클립 이름).
//   SkillData가 ActionData이면 이 계약을 전부 그대로 쓰므로 다른 팀원 코드가 하나도 안 바뀐다.
//
// ActionData와 다른 점은 딱 두 가지다
//   1) EvaluateTrigger : 슬롯 버튼 상태를 보고 "이번 틱에 시작할지, 무엇으로 시작할지"를 스스로 정한다
//      (탭/길게 판정이 PlayerInputSource에서 여기로 옮겨옴)
//   2) CreateRunner    : 실행 흐름(선딜/홀드/활성/후딜, 강화 분기, 비용 확정 시점)을 정하는 러너를 만든다
//
// SO는 Player/Enemy가 같이 쓰는 불변 에셋이므로 런타임 상태는 절대 여기 두지 않는다. 상태는 SkillRunner가 가진다.
// ============================================================================

/// <summary>
/// 새 스킬 베이스. 직접 에셋으로 만들지 않고, 키 유형 베이스(TapSkillData / HoldSkillData)나
/// 그걸 상속한 개별 스킬 데이터로 만든다.
/// </summary>
public abstract class SkillData : ActionData
{
    /// <summary>
    /// 이 슬롯의 이번 틱 버튼 상태를 보고 시작할 행동을 돌려준다. 시작하지 않으면 null.
    /// 돌려주는 행동은 보통 this이고, 강화판정처럼 다른 에셋(변형)일 수도 있다.
    ///
    /// 호출: SkillDriver, 매 틱 (행동 중이어도 불린다 — 결과는 입력 버퍼에 들어갔다가 Idle이 되면 시작된다).
    /// 장비 효과(ActionReplaceEffect)로 슬롯 행동이 바뀌었으면 바뀐 쪽의 이 함수가 불린다.
    /// 기본: 누른 순간 바로 시작.
    /// </summary>
    public virtual ActionData EvaluateTrigger(in SlotState slot) => slot.Pressed ? this : null;

    /// <summary>
    /// 이 스킬을 실행할 러너를 만든다. 로봇(SkillDriver)마다 스킬당 1개만 만들어 재사용한다.
    /// </summary>
    public abstract SkillRunner CreateRunner();
}
