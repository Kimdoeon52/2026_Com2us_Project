using UnityEngine;

/// <summary>
/// 장비 효과의 기반 클래스 (불변 데이터 SO). 파라미터만 가지고 상태는 갖지 않는다.
/// ActionData와 같은 원칙이다 — Player와 Enemy가 같은 에셋을 쓸 때 쿨타임·누적 횟수 같은 상태가 섞이지 않도록,
/// 실제 동작과 상태는 CreateInstance가 로봇마다 새로 만드는 EffectInstance가 가진다.
///
/// 새 효과를 추가하는 방법: 이 클래스를 상속하고 CreateInstance에서 EffectInstance 하위 클래스를 반환한다.
/// 수치만 다른 효과는 상속하지 말고 같은 클래스의 에셋을 복제해서 값만 바꾼다.
/// </summary>
public abstract class EquipmentEffect : ScriptableObject
{
    [SerializeField] private int skillId;
    [SerializeField] private SkillParamEntry[] baseParams = new SkillParamEntry[0];

    public int SkillId => skillId;
    public SkillParamEntry[] BaseParams => baseParams;
    /// <summary>전투 시작 시 로봇마다 1번 호출된다. 그 로봇 전용 런타임 인스턴스를 만들어 반환한다 (null 가능)</summary>
    public abstract EffectInstance CreateInstance(EffectContext ctx);
}
