using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [보스 기믹 런타임 상태 클래스 (BossGimmickRuntimeState)]
/// 전장 기물 또는 보스 약점 부위의 실시간 내구도 및 파훼 카운트를 관리함.
/// </summary>
[Serializable]
public class BossGimmickRuntimeState
{
    public string gimmickID;               // 예: "GIMMICK_NAIL_BOARD", "TIRE_PART"
    public string gimmickName;
    public bool isPhysicalBossPart;        // true: 보스 본체 부위, false: 전장 설치형 기물
    public int currentDurability;
    public int maxDurability;
    public bool isBroken => currentDurability <= 0;
    public int triggerCount = 0;           // 누적 파훼/발동 횟수 (예: 타이어 펑크 2회 누적)

    public BossGimmickRuntimeState(string id, string name, bool isPart, int maxDur)
    {
        gimmickID = id;
        gimmickName = name;
        isPhysicalBossPart = isPart;
        maxDurability = maxDur;
        currentDurability = maxDur;
    }
}

/// <summary>
/// [보스 런타임 스냅샷 클래스 (BossSnapshot)]
/// 기획서 §6.10 난이도 레이어 시스템을 지원하는 범용 기믹 런타임 DTO임.
/// 보스 본체 HP, 난이도별 스탯/전조/패턴/패시브 배율 및 gimmickStates 딕셔너리를 관리함.
/// </summary>
[Serializable]
public class BossSnapshot
{
    // ========================================================================
    // 1. 보스 기본 식별 및 본체 스탯
    // ========================================================================
    public string bossID = "BOSS_REGION_01";
    public string bossName = "정크 휠러";
    public int currentHp;
    public int maxHp;
    public int baseDefense;
    public int currentPhase = 1;

    /// <summary>보스 생존 여부</summary>
    public bool IsAlive => currentHp > 0;

    // ========================================================================
    // 2. 난이도 레이어 런타임 수치 (기획서 §6.10.4)
    // ========================================================================
    public BossDifficulty difficulty = BossDifficulty.Normal;
    public float attackMultiplier = 1.0f;          // 공격력 배율 (0.6, 0.8, 1.0, 1.3, 1.6)
    public float tellDurationMultiplier = 1.0f;     // 전조 시간 배율 (아주 쉬움: 1.5배, 그 외 1.0배)
    public int maxAllowedPatterns = 4;             // 패턴 풀 제한 (2, 3, 4, 4, 4)
    public int maxActiveGimmicks = 2;              // 필드 기믹 수 (0~1, 1, 2, 3, 4)
    public bool allowPhase2 = false;               // 페이즈 2 개방 (어려움, 헬)
    public bool allowPatternChain = false;         // 헬 전용 페이즈 2 후딜 0.4초 초고속 연계
    public int passiveCount = 0;                   // 패시브 수 (어려움: 1, 헬: 2)

    /// <summary>패시브 1: 과열 가속 (어려움, 헬) - 이동속도 +15%, 딜타임 후딜 -20%</summary>
    public bool hasOverheatAcceleration => passiveCount >= 1;

    /// <summary>패시브 2: 배기 잔화 (헬) - 돌진 시 2초간 화염 지대 생성</summary>
    public bool hasFireTrailHazard => passiveCount >= 2;

    // ========================================================================
    // 3. 상태 이상 및 피해 배율 플래그
    // ========================================================================
    public bool isGroggy = false;          // 기믹 파훼 시 4초 무력화 (받는 피해 1.5배)
    public float currentDamageMultiplier = 1.0f;

    // ========================================================================
    // 4. 보스 다변화 대비 범용 기믹 런타임 맵
    // ========================================================================
    public Dictionary<string, BossGimmickRuntimeState> gimmickStates = new();

    // ========================================================================
    // 5. 기믹 파훼로 인해 영구 봉인된 보스 스킬 패턴 목록
    // ========================================================================
    public HashSet<string> sealedSkills = new();

    #region 범용 기믹 헬퍼 메서드
    public BossGimmickRuntimeState GetGimmick(string gimmickId)
    {
        gimmickStates.TryGetValue(gimmickId, out var state);
        return state;
    }

    /// <summary>
    /// 기믹에 피해를 가함. 파손 도달 시 true 반환
    /// </summary>
    public bool DamageGimmick(string gimmickId, int amount)
    {
        if (gimmickStates.TryGetValue(gimmickId, out var state) && !state.isBroken)
        {
            state.currentDurability = Mathf.Max(0, state.currentDurability - amount);
            return state.isBroken;
        }
        return false;
    }

    /// <summary>
    /// 기믹 파훼로 인한 보스 특정 스킬 패턴 봉인 등록
    /// </summary>
    public void SealSkill(string skillId)
    {
        if (!string.IsNullOrEmpty(skillId))
            sealedSkills.Add(skillId);
    }

    /// <summary>
    /// 특정 스킬이 봉인되었는지 검사
    /// </summary>
    public bool IsSkillSealed(string skillId) => sealedSkills.Contains(skillId);
    #endregion
}
