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
    public string fighterID;          // 전투 참가자 고유 식별 번호 (DB 및 인벤토리 연동 키)
    public bool isPlayer;                // 플레이어 소속 여부 (true: 플레이어, false: AI)

    // 2. 코어 스탯(기본 체력 및 본체 방어력)
    public int currentHp; // 현재 체력 (0 ~ maxHp)
    public int maxHp; // 최대 체력 (기본 체력 + 강화치)
    public int baseDefense; // 본체 방어력 (기본 방어력 + 강화치)

    // 3.기획서 복합 연산 스탯

      /// <summary>
      /// 기본 공격력 + (왼팔 공격력 + 오른팔 공격력) / 2
      /// </summary>
    public int totalAttackPower;
    /// <summary>(왼다리 속도 + 오른다리 속도) / 2</summary>
    public float finalMoveSpeed;
    /// <summary>머리 파츠: 상대 치명타 확률 합연산 감소 (0.0 ~ 1.0)</summary>
    public float critResistance;
    /// <summary>머리 파츠: 치명타 피격 시 추가 대미지 삭감 배율 (0.0 ~ 1.0)</summary>
    public float critDamageReduction;
    /// <summary>팔 파츠: 가드 성공 시 코어 방어력에 추가 합산되는 보정치</summary>
    public int guardDefBonus;
    /// <summary>다리 파츠: 위빙 성공 시 제공되는 추가 무적 시간 보정치</summary>
    public float invincibleBonus;

    // 4. 실시간 부위별 내구도 맵
    public Dictionary<BodyPart, PartRuntimeState> partStates = new Dictionary<BodyPart, PartRuntimeState>();

    //5. 헬퍼 및 판정 메서드
    /// <summary>
    /// 해당 부위가 파손이 되었거나 미장착 상태인지 검사함
    /// 전트 행동 파트에서 ActionSource.Part 기술 시전시 호출됨
    /// </summary>
    
    public bool IsPartBrokenOrNotEquipped(BodyPart partType)
    {
        if (!partStates.TryGetValue(partType, out var partState))
            return true; // 미장착 상태임
        return partStates[partType].isBroken; // 파손 여부 반환함
    }

    /// <summary>
    /// 설계서 및 전투 행동 파트용 alias: 부위 파손 여부 검사함
    /// </summary>
    public bool IsPartBroken(BodyPart partType)
    {
        return IsPartBrokenOrNotEquipped(partType);
    }

    /// <summary>
    /// 생존 여부 (현재 코어 HP > 0) 반환함
    /// </summary>
    public bool IsAlive => currentHp > 0;

    /// <summary>
    /// 지정된 부위의 내구도를 차감함
    /// </summary>
    public void ConsumePartDurability(BodyPart partType, int amount)
    {
        if (partStates.TryGetValue(partType, out var partState))
        {
            partState.Consume(amount);
        }
    }

    /// <summary>
    /// 설계서 및 연산 엔진용 alias: 지정된 부위의 내구도를 차감함
    /// </summary>
    public void ConsumeDurability(BodyPart partType, int amount)
    {
        ConsumePartDurability(partType, amount);
    }

    /// <summary>
    /// 지정된 부위의 런타임 상태를 반환함
    /// </summary>
    public PartRuntimeState GetPartRuntimeState(BodyPart partType)
    {
        partStates.TryGetValue(partType, out var partState);
        return partState;
    }

    /// <summary>
    /// 다리 1개만 파괴된 상태인지 확인함 (위빙 50% 실패 대상임)
    /// </summary>
    public bool IsOneLegBroken()
    {
        bool leftLegBroken = IsPartBrokenOrNotEquipped(BodyPart.LeftLeg);
        bool rightLegBroken = IsPartBrokenOrNotEquipped(BodyPart.RightLeg);

        return (leftLegBroken ^ rightLegBroken); // XOR 연산으로 한쪽만 파손된 경우 true 반환함
    }

    /// <summary>
    /// 다리 2개 모두 파괴된 상태인지 확인함 (위빙 100% 실패 대상임)
    /// </summary>
    public bool IsBothLegsBroken()
    {
        bool leftLegBroken = IsPartBrokenOrNotEquipped(BodyPart.LeftLeg);
        bool rightLegBroken = IsPartBrokenOrNotEquipped(BodyPart.RightLeg);

        return (leftLegBroken && rightLegBroken); // AND 연산으로 양쪽 모두 파손된 경우 true 반환함
    }
}
