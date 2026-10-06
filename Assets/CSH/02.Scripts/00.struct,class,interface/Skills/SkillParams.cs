public enum SkillParamId
{
    Damage = 0,
    Cooldown = 1,
    Range = 2,
    Duration = 3,
    HitBoxWidth = 4,
    HitBoxHeight = 5,
    KnockbackDistance = 6,
    ProjectileCount = 7,
    CylinderCost = 8,
    PartDamage = 9,
    // 새 스킬이 새 종류의 값이 필요하면 추가
}

public enum ParamOption { Add = 0, Multiply = 1, Set = 2}

[System.Serializable]
public struct SkillParamEntry
{
    public SkillParamId id;
    public float value;
}

[System.Serializable]
public struct SkillParamOverride
{
    public int skillId;
    public SkillParamId id;
    public ParamOption op;
    public float value;
}