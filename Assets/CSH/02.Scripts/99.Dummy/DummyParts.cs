
/// <summary>
/// 더미 파츠(사용안함)
/// </summary>
public class DummyParts
{
    public float partsHP = 100f;
    public float partsMaxHP = 100f;

    // 방어력 퍼센트 ex(5=5%)
    public float partsDefense = 5f;

    public float partsDamage = 10f;

    SkillBase skillBase;

    public void SetSkill(SkillBase skillBase)
    {
        this.skillBase = skillBase;
    }

    public void TakeDamage(SkillBase skillBase)
    {

    }


}
