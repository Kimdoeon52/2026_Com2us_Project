using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [보스 난이도 5단계 열거형]
/// 기획서 §6.10.4: 아주 쉬움(0), 쉬움(1), 보통(2), 어려움(3), 헬(4)
/// </summary>
public enum BossDifficulty
{
    VeryEasy = 0, // 아주 쉬움
    Easy = 1,     // 쉬움
    Normal = 2,   // 보통 (기준 난이도)
    Hard = 3,     // 어려움
    Hell = 4      // 헬
}

/// <summary>
/// [보스 난이도 레이어 데이터 클래스 (BossDifficultyLayer)]
/// 기획서 §6.10.4 & §6.10.5:
/// 난이도별 스탯 배율, 전조 시간 배율, 허용 패턴 수, 기믹 수, 페이즈 2 개방, 패시브 및 정산 배율을 정의함.
/// </summary>
[Serializable]
public class BossDifficultyLayer
{
    [Tooltip("난이도 식별자")]
    public BossDifficulty difficulty = BossDifficulty.Normal;

    [Header("1. 보스 스탯 및 전투 규칙")]
    [Tooltip("보스 최대 HP 배율")]
    public float hpMultiplier = 1.0f;

    [Tooltip("보스 공격력(원시 대미지 및 부위 대미지) 배율")]
    public float attackMultiplier = 1.0f;

    [Tooltip("공격 패턴 전조(Tell) 시간 배율 (아주 쉬움: 1.5배, 그 외 1.0배)")]
    public float tellDurationMultiplier = 1.0f;

    [Tooltip("사용 가능한 최대 패턴 수 (아주 쉬움: 2종, 쉬움: 3종, 보통 이상: 4종 전 패턴)")]
    public int maxAllowedPatterns = 4;

    [Tooltip("필드 활성 기믹 수 (아주 쉬움: 0~1, 쉬움: 1, 보통: 2, 어려움: 3, 헬: 4)")]
    public int maxActiveGimmicks = 2;

    [Tooltip("페이즈 2 진입 허용 여부 (어려움, 헬만 true)")]
    public bool allowPhase2 = false;

    [Tooltip("페이즈 2 패턴 연속 연계 허용 여부 (헬 전용, 후딜 0.4초 초고속 연계)")]
    public bool allowPatternChain = false;

    [Tooltip("활성 패시브 개수 (어려움: 1종 과열가속, 헬: 2종 과열가속+배기잔화)")]
    public int passiveCount = 0;

    [Header("2. 토벌 보상 및 정산 (레이스 1 기준가 500G)")]
    [Tooltip("토벌 성공 시 획득 랭킹 점수")]
    public int rankingScore = 80;

    [Tooltip("처치 골드 배율 (기준가 대비 배율: 0.1, 0.25, 0.5, 0.9, 1.5)")]
    public float killGoldMultiplier = 0.5f;

    [Tooltip("토벌전 신청비 배율 (기준가 대비 배율: 0, 0.05, 0.1, 0.2, 0.3)")]
    public float entryFeeMultiplier = 0.1f;

    [Tooltip("코어 EXP 획득 배율 (기준가*0.2 대비 승리 배율: 0.5, 0.8, 1.2, 1.8, 3.0)")]
    public float coreExpMultiplier = 1.2f;

    [Tooltip("패배 시 부품 영구 파괴 확률 배율 (아주 쉬움: 0.0배-파괴 없음, 쉬움: 0.5배, 보통/어려움: 1.0배, 헬: 1.3배)")]
    public float destructionMultiplier = 1.0f;

    [Tooltip("패배 시 코어 불안정(Unstable) 상태 적용 여부 (아주 쉬움/쉬움: false, 보통 이상: true)")]
    public bool coreUnstableOnLoss = true;

    /// <summary>
    /// 기획서 §6.10.4 및 §6.10.5 기반 난이도별 기본 레이어 데이터를 생성하여 반환함.
    /// </summary>
    public static BossDifficultyLayer GetDefault(BossDifficulty diff)
    {
        switch (diff)
        {
            case BossDifficulty.VeryEasy:
                return new BossDifficultyLayer
                {
                    difficulty = BossDifficulty.VeryEasy,
                    hpMultiplier = 0.6f,
                    attackMultiplier = 0.6f,
                    tellDurationMultiplier = 1.5f,
                    maxAllowedPatterns = 2,
                    maxActiveGimmicks = 1,
                    allowPhase2 = false,
                    allowPatternChain = false,
                    passiveCount = 0,
                    rankingScore = 10,
                    killGoldMultiplier = 0.1f,    // 50G
                    entryFeeMultiplier = 0.0f,    // 0G
                    coreExpMultiplier = 0.5f,     // Win 50 EXP, Loss 10 EXP
                    destructionMultiplier = 0.0f, // 영구 파괴 없음 (내구도 0만)
                    coreUnstableOnLoss = false    // 코어 불안정 없음
                };

            case BossDifficulty.Easy:
                return new BossDifficultyLayer
                {
                    difficulty = BossDifficulty.Easy,
                    hpMultiplier = 0.8f,
                    attackMultiplier = 0.8f,
                    tellDurationMultiplier = 1.0f,
                    maxAllowedPatterns = 3,
                    maxActiveGimmicks = 1,
                    allowPhase2 = false,
                    allowPatternChain = false,
                    passiveCount = 0,
                    rankingScore = 30,
                    killGoldMultiplier = 0.25f,   // 125G
                    entryFeeMultiplier = 0.05f,   // 25G
                    coreExpMultiplier = 0.8f,     // Win 80 EXP, Loss 16 EXP
                    destructionMultiplier = 0.5f, // 파괴 확률 50% 감쇄
                    coreUnstableOnLoss = false    // 코어 불안정 없음
                };

            case BossDifficulty.Normal:
            default:
                return new BossDifficultyLayer
                {
                    difficulty = BossDifficulty.Normal,
                    hpMultiplier = 1.0f,
                    attackMultiplier = 1.0f,
                    tellDurationMultiplier = 1.0f,
                    maxAllowedPatterns = 4,
                    maxActiveGimmicks = 2,
                    allowPhase2 = false,
                    allowPatternChain = false,
                    passiveCount = 0,
                    rankingScore = 80,
                    killGoldMultiplier = 0.5f,    // 250G
                    entryFeeMultiplier = 0.1f,    // 50G
                    coreExpMultiplier = 1.2f,     // Win 120 EXP, Loss 24 EXP
                    destructionMultiplier = 1.0f, // 표준 파괴 확률
                    coreUnstableOnLoss = true
                };

            case BossDifficulty.Hard:
                return new BossDifficultyLayer
                {
                    difficulty = BossDifficulty.Hard,
                    hpMultiplier = 1.5f,
                    attackMultiplier = 1.3f,
                    tellDurationMultiplier = 1.0f,
                    maxAllowedPatterns = 5,
                    maxActiveGimmicks = 3,
                    allowPhase2 = true,
                    allowPatternChain = false,
                    passiveCount = 1,             // 1종: 과열 가속 (이속 +15%, 후딜 -20%)
                    rankingScore = 200,
                    killGoldMultiplier = 0.9f,    // 450G
                    entryFeeMultiplier = 0.2f,    // 100G
                    coreExpMultiplier = 1.8f,     // Win 180 EXP, Loss 36 EXP
                    destructionMultiplier = 1.0f,
                    coreUnstableOnLoss = true
                };

            case BossDifficulty.Hell:
                return new BossDifficultyLayer
                {
                    difficulty = BossDifficulty.Hell,
                    hpMultiplier = 2.2f,
                    attackMultiplier = 1.6f,
                    tellDurationMultiplier = 1.0f,
                    maxAllowedPatterns = 5,
                    maxActiveGimmicks = 4,
                    allowPhase2 = true,
                    allowPatternChain = true,     // 2페이즈 패턴 연계 후딜 0.4초
                    passiveCount = 2,             // 2종: 과열 가속 + 배기 잔화(돌진 시 2초 화염 지대)
                    rankingScore = 500,
                    killGoldMultiplier = 1.5f,    // 750G
                    entryFeeMultiplier = 0.3f,    // 150G
                    coreExpMultiplier = 3.0f,     // Win 300 EXP, Loss 60 EXP
                    destructionMultiplier = 1.3f, // 파괴 확률 1.3배 증폭
                    coreUnstableOnLoss = true
                };
        }
    }
}

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
/// 기획서 §6.10, §6.17 지역 1 보스(폐타이어 & 폐엔진)를 포함하여 향후 추가될 모든 보스의 불변 원장 에셋임.
/// </summary>
[CreateAssetMenu(fileName = "BossMasterData", menuName = "RealSteel/DB/BossMasterData", order = 2)]
public class BossMasterData : ScriptableObject
{
    [Header("1. 보스 기본 식별 및 지역 정보")]
    public string bossID = "BOSS_REGION_01";
    public string bossName = "정크 휠러 (폐타이어 & 폐엔진)";
    [Tooltip("소속 지역 번호 (기획서 §14.2: 지역 1=500G, 지역 2=800G, 지역 3=1300G)")]
    public int regionIndex = 1;
    [Tooltip("지역 기준 플레이 가격 (기본: 500G)")]
    public int regionBasePrice = 500;
    [Tooltip("지역 레벨 상한 (기획서 §6.6: 지역 1 상한=10레벨)")]
    public int regionLevelCap = 10;

    [Header("2. 보스 본체 기준 스탯 (Normal 난이도 기준 1.0배 원형)")]
    public int maxHp = 4000;
    public int baseDefense = 40;
    public float patternPostDelay = 1.0f;  // 모든 패턴 종료 후 1초 딜레이 (기본 딜타임)

    [Header("3. 난이도 레이어 목록 (미지정 시 GDD 기본값 자동 생성)")]
    public List<BossDifficultyLayer> difficultyLayers = new List<BossDifficultyLayer>();

    [Header("4. 보스 전용 기믹 목록 (기물 및 부위)")]
    public List<BossGimmickPartData> gimmickParts = new List<BossGimmickPartData>();

    [Header("5. 공격 패턴 목록 (BossPatternData)")]
    public List<BossPatternData> patterns = new List<BossPatternData>();

    [Header("6. 페이즈 구성")]
    public List<BossPhaseData> phases = new List<BossPhaseData>();

    [Header("7. 토벌 승리 기본 보상 (Normal 기준 폴백)")]
    public int rewardGold = 250;
    public int rewardCoreExp = 120;

    /// <summary>
    /// 지정된 난이도 레이어 데이터를 반환함. 커스텀 설정이 없으면 GDD 규격 기본값을 반환함.
    /// </summary>
    public BossDifficultyLayer GetDifficultyLayer(BossDifficulty diff)
    {
        if (difficultyLayers != null)
        {
            var found = difficultyLayers.Find(l => l != null && l.difficulty == diff);
            if (found != null) return found;
        }
        return BossDifficultyLayer.GetDefault(diff);
    }
}
