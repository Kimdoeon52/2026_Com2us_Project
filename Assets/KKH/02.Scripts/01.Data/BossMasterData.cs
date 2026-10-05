using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [보스 기믹/부위 데이터 클래스]
/// 전장 설치형 기물(못판 등) 또는 보스 고유 신체 부위(타이어 등)의 파훼 조건을 정의함.
/// </summary>
[Serializable]
public class BossGimmickPartData
{
    [Tooltip("기믹 고유 식별자 (예: GIMMICK_NAIL_BOARD, TIRE_PART)")]
    public string gimmickID;

    [Tooltip("기믹 표시 명칭 (예: 고철 못판, 폐타이어)")]
    public string gimmickName;

    [Tooltip("true: 보스 본체 부위, false: 전장 설치형 기물")]
    public bool isPhysicalBossPart;

    [Tooltip("파괴 또는 저지에 필요한 요구 수치/내구도")]
    public int maxDurability = 100;

    [Tooltip("파훼에 유효한 스킬 태그")]
    public GimmickTag requiredGimmickTag = GimmickTag.None;

    [Tooltip("파괴 시 영구 봉인되는 보스 패턴 스킬 ID")]
    public string targetSkillIDToSeal;

    [Tooltip("파훼 성공 시 그로기(무력화) 지속 시간 (초)")]
    public float groggyDuration = 4.0f;

    [Tooltip("그로기 중 받는 피해 증폭 배율 (기본 1.5배)")]
    public float damageAmplification = 1.5f;
}

/// <summary>
/// [보스 페이즈 데이터 클래스]
/// 체력 임계치별 페이즈 전환 및 사용 가능 스킬 패턴 정의.
/// </summary>
[Serializable]
public class BossPhaseData
{
    public int phaseIndex = 1;
    [Range(0f, 1f)] public float hpThresholdRatio = 1.0f; // 진입 체력 비율
    public List<string> availableSkillIDs = new List<string>();
}

/// <summary>
/// [보스 마스터 데이터 SO (BossMasterData)]
/// 기획서 §6.17 지역 1 보스(폐타이어 & 폐엔진)를 포함하여 향후 추가될 모든 보스의 불변 원장 에셋임.
/// </summary>
[CreateAssetMenu(fileName = "BossMasterData", menuName = "RealSteel/DB/BossMasterData", order = 2)]
public class BossMasterData : ScriptableObject
{
    [Header("1. 보스 기본 식별 정보")]
    public string bossID = "BOSS_REGION_01";
    public string bossName = "정크 휠러 (폐타이어 & 폐엔진)";

    [Header("2. 보스 본체 스탯")]
    public int maxHp = 4000;
    public int baseDefense = 40;
    public float patternPostDelay = 1.0f;  // 모든 패턴 종료 후 1초 딜레이 (딜타임)

    [Header("3. 보스 전용 기믹 목록 (기물 및 부위)")]
    public List<BossGimmickPartData> gimmickParts = new List<BossGimmickPartData>();

    [Header("4. 페이즈 구성")]
    public List<BossPhaseData> phases = new List<BossPhaseData>();

    [Header("5. 토벌 승리 보상")]
    public int rewardGold = 1500;
    public int rewardCoreExp = 500;
}
