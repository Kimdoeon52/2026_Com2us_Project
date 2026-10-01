using UnityEngine;

/// <summary>
/// [코어 마스터 데이터 SO (ScriptableObject)]
/// 로봇의 심장인 코어(Core)의 기본 스탯 및 상태를 정의하는 에셋임.
/// 코어 HP가 0이 되면 전투에서 패배하며, 패배 시 불안정(Unstable) 상태가 됨.
/// </summary>
[CreateAssetMenu(fileName = "CoreMasterData", menuName = "RealSteel/DB/CoreMasterData", order = 0)]
public class CoreMasterData : ScriptableObject
{
    [Header("1. 기본 식별 정보")]
    [Tooltip("코어 고유 식별 번호임")]
    public string coreID;

    [Tooltip("코어 명칭임")]
    public string coreName;

    [Tooltip("코어 강화 레벨임")]
    [Range(1, 10)]
    public int coreLevel = 1;

    [Header("2. 코어 기본 스탯")]
    [Tooltip("기본 최대 체력 (HP)임")]
    public int baseHp = 1000;

    [Tooltip("기본 본체 방어력 (Def)임")]
    public int baseDefense = 50;

    [Tooltip("기본 공격력 (Base Atk) - 양팔 공격력 평균과 합산됨")]
    public int baseAttackPower = 20;

    [Header("3. 레벨당 성장치")]
    [Tooltip("레벨당 최대 체력 증가량임")]
    public int hpGrowthPerLevel = 100;

    [Tooltip("레벨당 방어력 증가량임")]
    public int defenseGrowthPerLevel = 5;

    [Header("4. 경험치 테이블")]
    [Tooltip("레벨당 필요 경험치임")]
    public int[] requiredEXPTable = new int[10] {100, 250, 450, 700, 1000, 1400, 1900, 2500, 3200, 4000 };

    #region 스탯 연산 헬퍼 메서드
    /// <summary>
    /// 코어 레벨을 반영한 최종 최대 체력(Max HP) 산출함
    /// 레벨 미지정(음수) 시 현재 coreLevel 기준임
    /// 공식: baseHp + (level - 1) * hpGrowthPerLevel
    /// </summary>
    public int GetMaxHp(int level = -1)
    {
        int targetLevel = level > 0 ? level : coreLevel;
        return baseHp + Mathf.Max(0, targetLevel - 1) * hpGrowthPerLevel;
    }

    /// <summary>
    /// 코어 레벨을 반영한 최종 본체 방어력(Defense) 산출함
    /// 레벨 미지정(음수) 시 현재 coreLevel 기준임
    /// 공식: baseDefense + (level - 1) * defenseGrowthPerLevel
    /// </summary>
    public int GetDefense(int level = -1)
    {
        int targetLevel = level > 0 ? level : coreLevel;
        return baseDefense + Mathf.Max(0, targetLevel - 1) * defenseGrowthPerLevel;
    }

    /// <summary>
    /// 코어 기본 공격력(Base Attack Power) 반환함
    /// </summary>
    public int GetBaseAttackPower()
    {
        return baseAttackPower;
    }
    #endregion
}
