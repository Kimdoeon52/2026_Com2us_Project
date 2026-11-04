using UnityEngine;

// 스킬 레벨 수치. 데미지 배율 표의 길이가 곧 최대 레벨 (0번 = Lv1)
[CreateAssetMenu(menuName = "KDU/스킬/스킬 레벨 정의", fileName = "SkillLevelDefinition")]
public class SkillLevelDefinition : ScriptableObject
{
    [Tooltip("스킬 매핑")]
    [SerializeField] private PartSkillCatalog _catalog;

    [Tooltip("레벨별 데미지 배율")]
    [SerializeField] private float[] _damageScales = { 1f };

    [Tooltip("기본 스킬 포인트")]
    [Min(0)][SerializeField] private int _startingPoints;

    [Tooltip("코어 레벨업당 스킬 포인트")]
    [Min(0)][SerializeField] private int _pointsPerCoreLevel;

    public PartSkillCatalog Catalog => _catalog;
    public int MaxLevel => _damageScales != null ? Mathf.Max(1, _damageScales.Length) : 1;

    public float GetDamageScale(int level)
    {
        if (_damageScales == null || _damageScales.Length == 0)
            return 1f;

        return _damageScales[Mathf.Clamp(level - 1, 0, _damageScales.Length - 1)];
    }

    public int GetGrantedPoints(int coreLevel)
    {
        return _startingPoints + _pointsPerCoreLevel * Mathf.Max(0, coreLevel - 1);
    }
}
