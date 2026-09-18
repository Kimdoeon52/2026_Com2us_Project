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

    public void Consume(int amount)
    {
        
    }

}



[Serializable]public class CombatantSnapshot
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
