using UnityEngine;

/// <summary>
/// [코어 마스터 데이터 SO (ScriptableObject)]
/// 로봇의 심장인 코어(Core)의 기본 스탯 및 레벨 성장을 정의하는 에셋임.
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

    [Tooltip("코어 현재 레벨 (1 ~ 30)")]
    [Range(1, 30)]
    public int coreLevel = 1;

    [Header("2. 코어 기본 스탯 (Lv.1 기준)")]
    [Tooltip("기본 최대 체력 (Lv.1: 300)")]
    public int baseHp = 300;

    [Tooltip("기본 본체 방어력 (Lv.1: 20)")]
    public int baseDefense = 20;

    [Tooltip("기본 공격력 (Base Atk) - 양팔 공격력 평균과 합산됨")]
    public int baseAttackPower = 20;

    [Header("3. 레벨 상한 및 경험치 테이블")]
    public const int MaxLevel = 30;

    [Tooltip("레벨당 다음 레벨까지 필요 경험치 (Lv.1 ~ Lv.30)")]
    public int[] requiredEXPTable = new int[30] 
    {
        230, 250, 270, 290, 320, 350, 380, 410, 440, 480,       // Lv.1 ~ 10
        520, 560, 610, 660, 720, 780, 850, 920, 1000, 1080,     // Lv.11 ~ 20
        1180, 1280, 1380, 1500, 1630, 1770, 1920, 2080, 2260, 0 // Lv.21 ~ 30 (MAX)
    };

    #region 스탯 연산 헬퍼 메서드
    /// <summary>
    /// 코어 레벨을 반영한 최종 최대 체력(Max HP) 산출함
    /// Lv.1: 300, Lv.2~10: +15/Lv, Lv.11~20: +20/Lv, Lv.21~30: +25/Lv
    /// </summary>
    public int GetMaxHp(int level = -1)
    {
        int lv = Mathf.Clamp(level > 0 ? level : coreLevel, 1, MaxLevel);
        int hp = baseHp;

        if (lv <= 10)
        {
            hp += (lv - 1) * 15;
        }
        else if (lv <= 20)
        {
            hp += (9 * 15) + (lv - 10) * 20;
        }
        else
        {
            hp += (9 * 15) + (10 * 20) + (lv - 20) * 25;
        }
        return hp;
    }

    /// <summary>
    /// 코어 레벨을 반영한 최종 본체 방어력(Defense) 산출함
    /// Lv.1: 20, Lv.2~10: +3/Lv, Lv.11~20: +4/Lv, Lv.21~30: +5/Lv
    /// </summary>
    public int GetDefense(int level = -1)
    {
        int lv = Mathf.Clamp(level > 0 ? level : coreLevel, 1, MaxLevel);
        int defense = baseDefense;

        if (lv <= 10)
        {
            defense += (lv - 1) * 3;
        }
        else if (lv <= 20)
        {
            defense += (9 * 3) + (lv - 10) * 4;
        }
        else
        {
            // baseDefense(20) 기반 누적 합산 정상화
            defense += (9 * 3) + (10 * 4) + (lv - 20) * 5;
        }
        return defense;
    }

    /// <summary>
    /// 코어 기본 공격력(Base Attack Power) 반환함
    /// </summary>
    public int GetBaseAttackPower() => baseAttackPower;

    /// <summary>
    /// 지정 레벨의 다음 레벨 필요 경험치 반환
    /// </summary>
    public int GetRequiredEXP(int level)
    {
        if (level < 1 || level > MaxLevel)
        {
            Debug.LogError($"[CoreMasterData] Invalid level: {level}. Level must be between 1 and {MaxLevel}.");
            return 0;
        }
        return requiredEXPTable[level - 1];
    }

    /// <summary>
    /// 패배 시 불안정한 코어 복구 비용 산출 (기획서 공식: 100G + Lv * 50G)
    /// </summary>
    public int GetRecoveryCost(int level = -1)
    {
        int lv = level > 0 ? level : coreLevel;
        return 100 + (lv * 50);
    }

    /// <summary>
    /// 설계서 및 테스터 호환용 alias: 패배 시 코어 복구 비용 반환함
    /// </summary>
    public int GetRestoreCost(int level = -1) => GetRecoveryCost(level);
    #endregion
}
