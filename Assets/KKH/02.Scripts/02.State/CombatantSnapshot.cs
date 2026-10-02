using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// [부위별 런타임 상태 클래스]
/// 전투중 각 부위의 실시간 내구도 및 파손여부를 관리하는 클래스임
/// </summary>

public class PartRuntimeState
{
    public string partID;           // 부품 고유 식별 번호 (DB 및 인벤토리 연동 키)

    public BodyPart partType;       // 장착 대상 부위 (전투 행동 파트의 BodyPart 공통 enum 사용)
    public int currentDurability;   // 현재 내구도 (0 ~ maxDurability)
    public int maxDurability;       // 최대 내구도 (baseDurability + 강화치)
    public bool isBroken => currentDurability <= 0;          // 부품 파손 여부 (true: 파손, false: 정상)

    public PartRuntimeState(string partID, BodyPart partType, int maxDurability)
    {
        this.partID = partID;
        this.partType = partType;
        this.maxDurability = maxDurability;
        this.currentDurability = maxDurability;
    }

/// <summary>
/// 부품 내구도 소모 처리 (0 이하로 내려가지 않도록 제한)
/// </summary>
/// <param name="amount"></param>
    public void Consume(int amount)
    {
        currentDurability = Mathf.Max(0, currentDurability - amount);
    }

/// <summary>
/// 부품 내구도 회복 처리 (maxDurability 이상으로 올라가지 않도록 제한)
/// </summary>
/// <param name="amount"></param>
    public void Repair(int amount)
    {
        currentDurability = Mathf.Min(maxDurability, currentDurability + amount);
    }

}


/// <summary>
/// [전투중 로봇 상태 스냅샷 클래스]
/// 전투중 각 부위의 실시간 내구도 및 파손여부를 관리하는 클래스임
/// 조립된 로봇 1대의 모든 부위 상태를 스냅샷으로 저장하고, 전투 종료 후 결과를 기록하는 용도로 사용됨
/// 전투 행동 파트와 DB 파트간의 수치 교환 및 참조를 위해, 부품 고유 식별 번호(partID)와 장착 대상 부위(partType)를 함께 저장함
/// </summary>

[Serializable]public class CombatantSnapshot
{
    // 1. 기본 식별 정보
    public string fighterID = "Player";          // 전투 참가자 고유 식별 번호 (DB 및 인벤토리 연동 키)
    public bool isPlayer = true;                // 플레이어 소속 여부 (true: 플레이어, false: AI)

    // 2. 코어 스탯(기본 체력 및 본체 방어력)
    public int currentHp; // 현재 체력 (0 ~ maxHp)
    public int maxHp; // 최대 체력 (기본 체력 + 강화치)
    public int baseDefense; // 본체 방어력 (기본 방어력 + 강화치)
    public int coreLevel = 1; // 코어 레벨 (1 ~ 30)
    public int currentCoreExp; // 현재 누적 코어 경험치 (플레이어 전용)

    // 3. 실린더 시스템
    public int currentCylinderCount = 1; // 현재 실린더 수 (0 ~ maxCylinderCount)
    public int maxCylinderCount = 3; // 최대 실린더 수 (양팔 공용)

    // 4. 복합 연산 스탯
    public int totalAttackPower; // 총 공격력 (기본 Atk + (왼팔Atk + 오른팔Atk)/2)
    public int finalMovementSpeed; // 최종 이동속도 (기본 MoveSpeed + (왼다리MoveSpeed + 오른다리MoveSpeed)/2)

    // 5. 5개 파츠 실시간 내구도 맵 (부위 타격 스킬 피격시에만 차감)
    public Dictionary<BodyPart, PartRuntimeState> partStates = new();

    /// <summary>
    /// 내구도가 0 이하(파손)인지 검사(스킬 봉인 판정)
    /// </summary>
    
    public bool IsPartBroken(BodyPart partType)
    {
        return !partStates.TryGetValue(partType, out var partState) || partState.isBroken;
    }

    /// <summary>
    /// 특정 부위 타격 스킬 피격 시 해당 부위의 내구도 차감
    /// 내구도 0 도달 시 true 반환 (부품 파손 및 스킬 봉인 판정)
    /// </summary>
    
   // public bool ConsumePart
}