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
    public string coreID = "CORE_01";

    [Tooltip("코어 명칭임")]
    public string coreName = "표준형 티타늄 코어";

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

    [Header("3. 런타임/세이브 상태")]
    [Tooltip("전투 패배 등으로 코어가 손상된 불안정 상태인지 여부임 (출격 제한 연동됨)")]
    public bool isUnstable = false;

    /// <summary>
    /// 레벨에 따른 유효 최대 체력 반환함
    /// </summary>
    public int GetMaxHp()
    {
        return baseHp + (coreLevel - 1) * 100;
    }

    /// <summary>
    /// 레벨에 따른 유효 본체 방어력 반환함
    /// </summary>
    public int GetDefense()
    {
        return baseDefense + (coreLevel - 1) * 5;
    }

    /// <summary>
    /// 레벨에 따른 기본 공격력 반환함
    /// </summary>
    public int GetBaseAttackPower()
    {
        return baseAttackPower + (coreLevel - 1) * 3;
    }
}
