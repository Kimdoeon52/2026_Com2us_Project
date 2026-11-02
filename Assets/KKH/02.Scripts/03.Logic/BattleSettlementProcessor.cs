using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [전투 정산 결과 DTO]
/// 승패 판정 후 도출된 최종 결과 데이터를 담는 DTO임.
/// </summary>
[Serializable]
public class BattleSettlementResult
{
    public bool PlayerWon;
    public BossDifficulty SelectedDifficulty = BossDifficulty.Normal;
    public int RankingScoreGained;
    public int EntryFeePaid;
    public int GoldDelta;
    public List<string> DestroyedPartIDs = new List<string>();
    public Dictionary<string, int> FinalPartDurabilities = new Dictionary<string, int>(); // [YJW 연동] 부품별 최종 잔여 내구도
    public bool CoreUnstable;
    public int CoreExpGained;
    public string SummaryMessage;
}

/// <summary>
/// [하드코어 승패 정산기 (BattleSettlementProcessor)]
/// 기획서 §6.10.5 및 §6.6:
/// 전투 종료 시 난이도별 보스 토벌 규칙(점수, 처치 골드, 신청비, 코어 EXP 배율, 파괴 확률 감쇄/증폭, 코어 불안정 여부) 및
/// 일반전 정산 규칙에 따라 긴급 복구, 영구 파괴 롤링 및 외부 파트로 데이터를 브로드캐스팅하는 정산기임.
/// </summary>
public static class BattleSettlementProcessor
{
    // ========================================================================
    // 외부 파트 연동용 이벤트 (C# Events)
    // ========================================================================
    /// <summary>전투 정산 종합 완료 이벤트임</summary>
    public static event Action<BattleSettlementResult> OnSettlementCompleted;

    /// <summary>크래프팅/파괴 파트(YJW) 연동: 영구 파괴된 부품 ID 목록 전달 (인벤토리 영구 삭제용임)</summary>
    public static event Action<List<string>> OnPartsPermanentlyDestroyed;

    /// <summary>골드/판돈 파트(LSH) 연동: 획득/차감 골드량 전달함</summary>
    public static event Action<int> OnGoldReward;

    /// <summary>고물상/코어 파트(KDU) 연동: 코어 불안정(Unstable) 상태 플래그 전달함</summary>
    public static event Action<bool> OnCoreStateChanged;

    /// <summary>
    /// 기획서 §6.5.6 경험치 획득 공식:
    /// 획득 EXP = 지역 기준가 * 0.2 * 경기 배율 * 레벨차 보정 (패배 시 20% 위로금)
    /// </summary>
    public static int CalculateGainedEXP(
        int regionBasePrice, float matchMultiplier = 1.0f, float levelDiffMultiplier = 1.0f, bool isWin = true)
    {
        float baseExp = regionBasePrice * 0.2f * matchMultiplier * levelDiffMultiplier;
        return isWin ? Mathf.RoundToInt(baseExp) : Mathf.RoundToInt(baseExp * 0.2f);
    }

    /// <summary>
    /// 기획서 §6.6 레벨차 보정 배율:
    /// - 지역 상한 이하: x1.0
    /// - 상한 +3 이상: x0.5
    /// - 상한 +6 이상: x0.1
    /// </summary>
    public static float GetLevelDiffMultiplier(int playerLevel, int regionLevelCap)
    {
        int diff = playerLevel - regionLevelCap;
        if (diff >= 6) return 0.1f;
        if (diff >= 3) return 0.5f;
        return 1.0f;
    }

    /// <summary>
    /// 기획서 §14.2 지역별 플레이 기준가 (G):
    /// 지역 1(500G), 지역 2(800G), 지역 3(1300G)
    /// </summary>
    public static int GetRegionBasePrice(int regionIndex)
    {
        switch (regionIndex)
        {
            case 1: return 500;
            case 2: return 800;
            case 3: return 1300;
            default: return 500;
        }
    }

    /// <summary>
    /// 코어 경험치 누적 및 레벨업 처리 (최대 30레벨)
    /// </summary> 
    public static void ProcessCoreExp(CombatantSnapshot player, int gainedExp, CoreMasterData coreMaster)
    {
        if (player == null || coreMaster == null)
        {
            Debug.LogError("[정산기] 코어 경험치 처리 실패: player 또는 coreMaster가 null임");
            return;
        }
        if (player.coreLevel >= CoreMasterData.MaxLevel)
        {
            Debug.Log("[정산기] 코어 경험치 처리: 이미 최대 레벨 도달함");
            return;
        }

        player.currentCoreExp += gainedExp;
        int reqExp = coreMaster.requiredEXPTable[player.coreLevel - 1];

        while (player.currentCoreExp >= reqExp && player.coreLevel < CoreMasterData.MaxLevel)
        {
            player.currentCoreExp -= reqExp;
            player.coreLevel++;
            player.maxHp = coreMaster.GetMaxHp(player.coreLevel);
            player.baseDefense = coreMaster.GetDefense(player.coreLevel);
            player.currentHp = player.maxHp; // 레벨업 시 체력 회복 처리함
            Debug.Log($"[정산기] 코어 레벨업! 현재 레벨: {player.coreLevel}, 남은 EXP: {player.currentCoreExp}");
            CombatDataHub.Instance?.BroadcastCoreLevelUp(player.coreLevel, player.maxHp, player.baseDefense);
            if (player.coreLevel < CoreMasterData.MaxLevel)
            {
                reqExp = coreMaster.requiredEXPTable[player.coreLevel - 1];
            }
        }
    }

    /// <summary>
    /// 기획서 공식: 등급별 기본 영구 파괴 확률 반환함
    /// 일반(70%), 레어(45%), 에픽(20%), 전설(5%), 프로토타입(1%)
    /// </summary>
    public static float GetDestructionProbability(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Common:
                return 0.70f;
            case PartGrade.Rare:
                return 0.45f;
            case PartGrade.Epic:
                return 0.20f;
            case PartGrade.Legendary:
                return 0.05f;
            case PartGrade.Prototype:
                return 0.01f;
            default:
                return 0.50f;
        }
    }

    /// <summary>
    /// 전투 정산 프로세스 메인 진입점임
    /// </summary>
    public static BattleSettlementResult ProcessSettlement(
        bool playerWon,
        CombatantSnapshot player,
        CombatantSnapshot enemy,
        int betGold = 0,
        int regionBasePrice = 500, // 레이스 1 기본가: 500G
        CoreMasterData coreMaster = null,
        Dictionary<string, PartMasterData> partMasterLookup = null,
        BossMasterData bossMaster = null,
        BossDifficulty difficulty = BossDifficulty.Normal)
    {
        var result = new BattleSettlementResult
        {
            PlayerWon = playerWon,
            SelectedDifficulty = difficulty
        };

        // ====================================================================
        // 1. 보스전 특화 정산 분기 (기획서 §6.10.5 난이도 레이어 테이블 적용)
        // ====================================================================
        if (bossMaster != null)
        {
            var layer = bossMaster.GetDifficultyLayer(difficulty);
            int basePrice = bossMaster.regionBasePrice > 0 ? bossMaster.regionBasePrice : GetRegionBasePrice(bossMaster.regionIndex);
            int levelCap = bossMaster.regionLevelCap > 0 ? bossMaster.regionLevelCap : 10;
            int playerLevel = player != null ? player.coreLevel : 1;
            float levelDiffMultiplier = GetLevelDiffMultiplier(playerLevel, levelCap);

            int entryFee = Mathf.RoundToInt(basePrice * layer.entryFeeMultiplier);
            result.EntryFeePaid = entryFee;

            if (playerWon)
            {
                // [승리 시]
                // 1. 랭킹 점수 획득 (10, 30, 80, 200, 500점)
                // 2. 처치 골드 획득 (50G, 125G, 250G, 450G, 750G)
                // 3. 코어 EXP 획득 (50, 80, 120, 180, 300 EXP * 레벨차 보정)
                // 4. 코어 불안정 없음
                // 5. 파손 부품 긴급 응급복구 (내구도 1)
                result.RankingScoreGained = layer.rankingScore;
                result.GoldDelta = Mathf.RoundToInt(basePrice * layer.killGoldMultiplier);
                result.CoreExpGained = CalculateGainedEXP(basePrice, layer.coreExpMultiplier, levelDiffMultiplier, isWin: true);
                result.CoreUnstable = false;

                if (coreMaster != null)
                {
                    ProcessCoreExp(player, result.CoreExpGained, coreMaster);
                }

                if (player != null)
                {
                    foreach (var kvp in player.partStates)
                    {
                        var partState = kvp.Value;
                        if (partState.isBroken)
                        {
                            partState.Repair(1);
                            Debug.Log($"[정산기] 승리 응급복구 완료: {kvp.Key} (ID: {partState.partID}) 내구도 1로 복구됨");
                        }
                        result.FinalPartDurabilities[partState.partID] = partState.currentDurability;
                    }
                }

                result.SummaryMessage = $"[보스 토벌 성공! ({difficulty})] 랭킹 점수 +{result.RankingScoreGained}점, 처치 골드 +{result.GoldDelta}G, 코어 EXP +{result.CoreExpGained} (레벨차 보정: {levelDiffMultiplier}x)";
            }
            else
            {
                // [패배 시]
                // 1. 랭킹 점수 0점
                // 2. 신청비 몰수 (반환 없음)
                // 3. 위로 코어 EXP (승리 EXP의 20%: 10, 16, 24, 36, 60 EXP)
                // 4. 코어 불안정: 아주쉬움/쉬움은 false, 보통 이상은 true
                // 5. 부품 파괴: 아주쉬움은 내구도 0만 (파괴 판정 없음 0%), 쉬움 0.5x, 보통/어려움 1.0x, 헬 1.3x
                result.RankingScoreGained = 0;
                result.GoldDelta = -entryFee;
                result.CoreExpGained = CalculateGainedEXP(basePrice, layer.coreExpMultiplier, levelDiffMultiplier, isWin: false);
                result.CoreUnstable = layer.coreUnstableOnLoss;

                if (coreMaster != null)
                {
                    ProcessCoreExp(player, result.CoreExpGained, coreMaster);
                }

                if (player != null)
                {
                    foreach (var kvp in player.partStates)
                    {
                        var partState = kvp.Value;
                        if (partState.partID.StartsWith("DEFAULT_", StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[정산기] 기본 장착 부품은 영구 파괴 대상에서 제외됨: {kvp.Key} (ID: {partState.partID})");
                            continue;
                        }

                        partState.currentDurability = 0; // 전 부품 내구도 0 강제 세팅
                        result.FinalPartDurabilities[partState.partID] = 0;

                        // 기획서 §6.10.5: 아주 쉬움은 파괴 판정 없음!
                        if (layer.destructionMultiplier <= 0f)
                        {
                            Debug.Log($"[정산기] [아주 쉬움 난이도] 부품 파괴 면제: {kvp.Key} (내구도 0만 적용)");
                            continue;
                        }

                        // 등급별 파괴 확률 롤링 (난이도 파괴 배율 적용)
                        PartGrade grade = PartGrade.Common;
                        if (partMasterLookup != null && partMasterLookup.TryGetValue(partState.partID, out var masterData))
                        {
                            grade = masterData.partGrade;
                        }
                        else if (InventoryHost.Instance != null && InventoryHost.Instance.Catalog != null)
                        {
                            PartsDefinition definition = InventoryHost.Instance.Catalog.Find(partState.partID);
                            if (definition != null)
                                grade = definition.Grade;
                        }

                        float baseChance = GetDestructionProbability(grade);
                        float finalChance = Mathf.Clamp01(baseChance * layer.destructionMultiplier);
                        float roll = UnityEngine.Random.value;

                        if (roll < finalChance)
                        {
                            result.DestroyedPartIDs.Add(partState.partID);
                            Debug.Log($"[정산기] 부품 영구 파괴됨: {kvp.Key} (ID: {partState.partID}, 등급: {grade}, 확률: {finalChance:P0}, 롤: {roll:F2})");
                        }
                        else
                        {
                            Debug.Log($"[정산기] 부품 파괴 면제(보존됨): {kvp.Key} (ID: {partState.partID}, 등급: {grade})");
                        }
                    }
                }

                result.SummaryMessage = $"[보스 토벌 실패 ({difficulty})] 신청비 {entryFee}G 몰수, 코어 불안정: {result.CoreUnstable}, 파괴된 부품: {result.DestroyedPartIDs.Count}개, 위로 EXP: {result.CoreExpGained}";
            }
        }
        else
        {
            // ====================================================================
            // 2. 일반 로봇 대전 정산 분기 (레거시/PVP 호환)
            // ====================================================================
            if (playerWon)
            {
                result.GoldDelta = betGold * 2;
                result.CoreExpGained = CalculateGainedEXP(regionBasePrice, 1.0f, 1.0f, isWin: true);
                result.CoreUnstable = false;

                if (coreMaster != null)
                {
                    ProcessCoreExp(player, result.CoreExpGained, coreMaster);
                }

                if (player != null)
                {
                    foreach (var kvp in player.partStates)
                    {
                        var partState = kvp.Value;
                        if (partState.isBroken)
                        {
                            partState.Repair(1);
                        }
                        result.FinalPartDurabilities[partState.partID] = partState.currentDurability;
                    }
                }

                result.SummaryMessage = $"일반전 승리! 판돈 {result.GoldDelta}G 획득 및 코어 EXP {result.CoreExpGained} 누적됨";
            }
            else
            {
                result.GoldDelta = -betGold;
                result.CoreExpGained = CalculateGainedEXP(regionBasePrice, 1.0f, 1.0f, isWin: false);
                result.CoreUnstable = true;

                if (coreMaster != null)
                {
                    ProcessCoreExp(player, result.CoreExpGained, coreMaster);
                }

                if (player != null)
                {
                    foreach (var kvp in player.partStates)
                    {
                        var partState = kvp.Value;
                        if (partState.partID.StartsWith("DEFAULT_", StringComparison.OrdinalIgnoreCase)) continue;

                        partState.currentDurability = 0;
                        result.FinalPartDurabilities[partState.partID] = 0;

                        PartGrade grade = PartGrade.Common;
                        if (partMasterLookup != null && partMasterLookup.TryGetValue(partState.partID, out var masterData))
                        {
                            grade = masterData.partGrade;
                        }
                        else if (InventoryHost.Instance != null && InventoryHost.Instance.Catalog != null)
                        {
                            PartsDefinition definition = InventoryHost.Instance.Catalog.Find(partState.partID);
                            if (definition != null) grade = definition.Grade;
                        }

                        float destroyChance = GetDestructionProbability(grade);
                        if (UnityEngine.Random.value < destroyChance)
                        {
                            result.DestroyedPartIDs.Add(partState.partID);
                        }
                    }
                }

                result.SummaryMessage = $"일반전 패배! 판돈 몰수, 코어 손상(Unstable), 파괴된 부품: {result.DestroyedPartIDs.Count}개임";
            }
        }

        // ====================================================================
        // 대외 브로드캐스팅 처리
        // ====================================================================
        OnSettlementCompleted?.Invoke(result);

        if (result.DestroyedPartIDs.Count > 0)
        {
            OnPartsPermanentlyDestroyed?.Invoke(result.DestroyedPartIDs);
        }

        OnGoldReward?.Invoke(result.GoldDelta);
        OnCoreStateChanged?.Invoke(result.CoreUnstable);

        // LSH 골드 파트 GlobalGold 싱글톤 연동
        if (GlobalGold.Instance != null && result.GoldDelta != 0)
        {
            const int playerId = 0;
            if (result.GoldDelta > 0)
                GlobalGold.Instance.GetGold(playerId, result.GoldDelta);
            else
                GlobalGold.Instance.UseGold(playerId, Mathf.Abs(result.GoldDelta));
        }

        return result;
    }
}
