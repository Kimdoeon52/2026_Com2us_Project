using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [부위별 런타임 상태 클래스 (PartRuntimeState)]
/// 전투 중 각 부위의 실시간 내구도 및 파손 여부를 관리하는 클래스임.
/// </summary>
[Serializable]
public class PartRuntimeState
{
    public string partID;           // 부품 고유 식별 번호 (DB 및 인벤토리 연동 키)
    public BodyPart partType;       // 장착 대상 부위 (NYH CombatEnums.BodyPart 공통 enum 사용)
    public int currentDurability;   // 현재 내구도 (0 ~ maxDurability)
    public int maxDurability;       // 최대 내구도 (baseDurability + 강화치)
    public bool isBroken => currentDurability <= 0; // 부품 파손 여부 (true: 파손 및 스킬 봉인)

    // 스킬 및 파츠 마스터 연동 필드
    public int activeSkillId = -1;
    public List<int> passiveSkillIds = new List<int>();
    public GimmickTag gimmickTag = GimmickTag.None;
    public int cylinderCost = 1;
    [NonSerialized] public PartMasterData masterData;

    public PartRuntimeState(string partID, BodyPart partType, int maxDurability, PartMasterData master = null)
    {
        this.partID = partID;
        this.partType = partType;
        this.maxDurability = maxDurability;
        this.currentDurability = maxDurability;
        this.masterData = master;

        if (master != null)
        {
            this.activeSkillId = master.activeSkillId;
            if (master.passiveSkillIds != null)
                this.passiveSkillIds = new List<int>(master.passiveSkillIds);
            this.gimmickTag = master.gimmickTag;
            this.cylinderCost = master.cylinderCost;
        }
    }

    public PartRuntimeState(string partID, BodyPart partType, int maxDurability)
        : this(partID, partType, maxDurability, null)
    {
    }

    /// <summary>부품 내구도 소모 처리 (0 이하로 내려가지 않도록 제한)</summary>
    public void Consume(int amount)
    {
        currentDurability = Mathf.Max(0, currentDurability - amount);
    }

    /// <summary>부품 내구도 회복 처리 (maxDurability 이상으로 올라가지 않도록 제한)</summary>
    public void Repair(int amount)
    {
        currentDurability = Mathf.Min(maxDurability, currentDurability + amount);
    }
}

/// <summary>
/// [로봇 런타임 스냅샷 클래스 (CombatantSnapshot)]
/// 플레이어 및 일반 적 로봇(NPC A, B, C)의 1:1 대전 상태를 표현하는 공용 DTO임.
/// 체력(HP)은 오직 코어(Core)만 소유하며, 5개 파츠는 런타임 내구도(Durability)만 독립 관리함.
/// </summary>
[Serializable]
public class CombatantSnapshot
{
    // ========================================================================
    // 1. 기본 식별 정보
    // ========================================================================
    public string fighterID = "Player"; // "Player" 또는 "NPC_A", "NPC_B", "NPC_C"
    public bool isPlayer = true;        // true: 플레이어, false: AI 적 로봇

    // ========================================================================
    // 2. 코어 실시간 스탯 (체력은 코어만 소유, 0 도달 시 즉시 K.O 패배)
    // ========================================================================
    public int currentHp;               // 코어 현재 체력
    public int maxHp;                   // 코어 최대 체력
    public int baseDefense;             // 코어 본체 방어력
    public int coreLevel = 1;           // 코어 레벨 (1 ~ 30)
    public int currentCoreExp;          // 현재 누적 코어 경험치 (플레이어 전용)
    [NonSerialized] public CoreMasterData coreMasterData; // 코어 마스터 데이터 참조

    /// <summary>생존 여부 (코어 HP > 0)</summary>
    public bool IsAlive => currentHp > 0;

    /// <summary>기획서 §4.7 방어력 기반 피해 감소율 = 방어력 / (방어력 + 100)</summary>
    public float DamageReductionRate => (float)baseDefense / (Mathf.Max(0, baseDefense) + 100f);

    /// <summary>기획서 §6.7 현재 유효 체력(EHP) = HP * (방어력 + 100) / 100</summary>
    public int EffectiveHp => Mathf.RoundToInt((float)currentHp * (Mathf.Max(0, baseDefense) + 100f) / 100f);

    /// <summary>기획서 §6.7 최대 유효 체력(Max EHP)</summary>
    public int MaxEffectiveHp => Mathf.RoundToInt((float)maxHp * (Mathf.Max(0, baseDefense) + 100f) / 100f);

    // ========================================================================
    // 3. 실린더 시스템 (기획서 §6.11 / NYH 연동)
    // ========================================================================
    public int currentCylinder = 1;     // 전투 시작 시 1발 기본 장전
    public const int MaxCylinder = 3;   // 최대 3발 (양팔 공용)

    // 하위 호환 프로퍼티
    public int currentCylinderCount
    {
        get => currentCylinder;
        set => currentCylinder = value;
    }
    public int maxCylinderCount => MaxCylinder;

    // ========================================================================
    // 4. 복합 연산 스탯 (기획서 §6.12.3)
    // ========================================================================
    public int totalAttackPower;        // 기본Atk + (왼팔Atk + 오른팔Atk) / 2
    public float finalMoveSpeed = 3f;   // (왼다리Speed + 오른다리Speed) / 2
    public int finalMovementSpeed       // 정수형 호환
    {
        get => Mathf.RoundToInt(finalMoveSpeed);
        set => finalMoveSpeed = value;
    }

    // ========================================================================
    // 5. 5개 파츠 실시간 내구도 맵 (부위 타격 스킬 피격 시에만 차감)
    // ========================================================================
    public Dictionary<BodyPart, PartRuntimeState> partStates = new();

    #region 헬퍼 메서드
    /// <summary>
    /// 특정 부위 파츠의 상태 객체 조회
    /// </summary>
    public PartRuntimeState GetPartRuntimeState(BodyPart partType)
    {
        partStates.TryGetValue(partType, out var state);
        return state;
    }

    /// <summary>
    /// 내구도가 0 이하(파손)인지 검사 (스킬 봉인 판정)
    /// </summary>
    public bool IsPartBroken(BodyPart partType)
    {
        return !partStates.TryGetValue(partType, out var partState) || partState.isBroken;
    }

    /// <summary>
    /// 특정 부위 타격 스킬 피격 시 해당 부위의 내구도 차감
    /// 내구도 0 도달 시 true 반환 (부품 파손 및 스킬 봉인 판정)
    /// </summary>
    public bool ConsumePartDurability(BodyPart part, int amount)
    {
        if (partStates.TryGetValue(part, out var state))
        {
            state.Consume(amount);
            return state.isBroken;
        }
        return false;
    }

    /// <summary>
    /// 특정 부위 내구도 회복
    /// </summary>
    public void RepairPartDurability(BodyPart part, int amount)
    {
        if (partStates.TryGetValue(part, out var state))
        {
            state.Repair(amount);
        }
    }

    // ========================================================================
    // 파츠 마스터 및 스킬 연동 헬퍼 메서드 (CSH / NYH 스킬 시스템 연동)
    // ========================================================================
    /// <summary>장착된 5부위 부품 원장 목록 (null 제외)</summary>
    public IEnumerable<PartMasterData> EquippedPartMasters
    {
        get
        {
            foreach (var kvp in partStates)
            {
                if (kvp.Value != null && kvp.Value.masterData != null)
                    yield return kvp.Value.masterData;
            }
        }
    }

    /// <summary>특정 부위의 부품 원장 데이터 조회</summary>
    public PartMasterData GetPartMaster(BodyPart part)
    {
        return partStates.TryGetValue(part, out var state) ? state.masterData : null;
    }

    /// <summary>특정 부위의 액티브 스킬 ID 조회 (파손 시 -1 반환)</summary>
    public int GetActiveSkillId(BodyPart part, bool checkBroken = true)
    {
        if (!partStates.TryGetValue(part, out var state)) return -1;
        if (checkBroken && state.isBroken) return -1;
        return state.activeSkillId;
    }

    /// <summary>장착된 모든 패시브 스킬 ID 목록 조회 (기본: 파손 부품 제외)</summary>
    public List<int> GetPassiveSkillIds(bool includeBroken = false)
    {
        var list = new List<int>();
        foreach (var kvp in partStates)
        {
            var state = kvp.Value;
            if (state == null) continue;
            if (!includeBroken && state.isBroken) continue;
            if (state.passiveSkillIds != null)
            {
                list.AddRange(state.passiveSkillIds);
            }
        }
        return list;
    }

    /// <summary>장착된 모든 스킬 ID(액티브 + 패시브) 목록 조회</summary>
    public List<int> GetAllSkillIds(bool includeBroken = false)
    {
        var list = new List<int>();
        foreach (var kvp in partStates)
        {
            var state = kvp.Value;
            if (state == null) continue;
            if (!includeBroken && state.isBroken) continue;

            if (state.activeSkillId >= 0)
                list.Add(state.activeSkillId);
            if (state.passiveSkillIds != null)
                list.AddRange(state.passiveSkillIds);
        }
        return list;
    }

    /// <summary>특정 부위의 기믹 파훼 태그 조회</summary>
    public GimmickTag GetGimmickTag(BodyPart part, bool checkBroken = true)
    {
        if (!partStates.TryGetValue(part, out var state)) return GimmickTag.None;
        if (checkBroken && state.isBroken) return GimmickTag.None;
        return state.gimmickTag;
    }

    // ========================================================================
    // 실린더 헬퍼 메서드 (NYH 스킬 액션 연동)
    // ========================================================================
    public bool CanSpendCylinder(int cost) => currentCylinder >= cost;

    public void SpendCylinder(int cost)
    {
        currentCylinder = Mathf.Max(0, currentCylinder - cost);
    }

    public void AddCylinder(int count = 1)
    {
        currentCylinder = Mathf.Min(MaxCylinder, currentCylinder + count);
    }

    // ========================================================================
    // 레거시 호환 헬퍼 (기존 테스터 및 구버전 메서드)
    // ========================================================================
    public bool IsBothLegsBroken() => IsPartBroken(BodyPart.LeftLeg) && IsPartBroken(BodyPart.RightLeg);
    public bool IsOneLegBroken() => IsPartBroken(BodyPart.LeftLeg) ^ IsPartBroken(BodyPart.RightLeg);
    #endregion
}
