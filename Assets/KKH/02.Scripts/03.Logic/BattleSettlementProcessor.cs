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
    public int GoldDelta;
    public List<string> DestroyedPartIDs = new List<string>();
    public Dictionary<string, int> FinalPartDurabilities = new Dictionary<string, int>(); // [YJW 연동] 부품별 최종 잔여 내구도
    public bool CoreUnstable;
    public int CoreExpGained;
    public string SummaryMessage;
}

/// <summary>
/// [하드코어 승패 정산기 (BattleSettlementProcessor)]
/// 전투 종료 시 승패 규칙에 따라 긴급 복구, 영구 파괴 롤링 및 외부 파트로 데이터를 브로드캐스팅하는 정산기임.
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
    public static int CalculateGainedEXP
    (int regionBasePrice, float matchMultiplier = 1.0f, float levelDiffMultiplier = 1.0f, bool isWin = true)
    {
        float baseExp = regionBasePrice * 0.2f * matchMultiplier * levelDiffMultiplier;
        return isWin ? Mathf.RoundToInt(baseExp) : Mathf.RoundToInt(baseExp * 0.2f);
    }

/// <summary>
/// 코어 경험치 누적 및 레벨업 처리 (최대 30레벨)
/// </summary> 
public static void ProcessCoreExp(CombatantSnapshot player, int gainedExp, CoreMasterData coreMaster)
    {
        if(player ==  null || coreMaster == null)
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
    /// 기획서 공식: 등급별 영구 파괴 확률 반환함
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
        int regionBasePrice = 1000, //(500G ~ 3000G)
        CoreMasterData coreMaster = null,
        Dictionary<string, PartMasterData> partMasterLookup = null)
    {
        var result = new BattleSettlementResult
        {
            PlayerWon = playerWon
        };

        if (playerWon)
        {
            // ────────────────────────────────────────────────────────────────
            // [승리 시 정산]
            // 1. 판돈 2배 지급함
            // 2. 코어 EXP 획득함
            // 3. 파손된(내구도 0) 부품은 긴급 응급복구 (내구도 1로 복구함)
            // ────────────────────────────────────────────────────────────────
            result.GoldDelta = betGold * 2;
            result.CoreExpGained = CalculateGainedEXP(regionBasePrice, 1.0f, 1.0f, isWin: true);
            result.CoreUnstable = false;
            if (coreMaster != null)
            {
                ProcessCoreExp(player, result.CoreExpGained, coreMaster);
            }


            // 코어 경험치 반영 및 레벨업 처리
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

            result.SummaryMessage = $"전투 승리! 판돈 {result.GoldDelta}G 획득 및 코어 EXP {result.CoreExpGained} 누적됨";
        }
        else
        {
            // ────────────────────────────────────────────────────────────────
            // [패배 시 정산]
            // 1. 판돈 전액 몰수됨
            // 2. 코어 Unstable(불안정) 전환됨 (수리 전까지 출격 불가함)
            // 3. 모든 장착 파츠 내구도 0 강제 전환됨
            // 4. 등급별 확률 롤링으로 영구 파괴(삭제) 목록 결정함
            // ────────────────────────────────────────────────────────────────
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
                    if(partState.partID.StartsWith("DEFAULT_", StringComparison.OrdinalIgnoreCase))
                    {
                         Debug.Log($"[정산기] 기본 장착 부품은 영구 파괴 대상에서 제외됨: {kvp.Key} (ID: {partState.partID})");
                        continue;
                    }
                    partState.currentDurability = 0; // 전 부품 내구도 0 강제 세팅함
                    result.FinalPartDurabilities[partState.partID] = 0;

                    // 등급별 영구 파괴 롤링함
                    PartGrade grade = PartGrade.Common;
                    if (partMasterLookup != null && partMasterLookup.TryGetValue(partState.partID, out var masterData))
                    {
                        grade = masterData.partGrade;
                    }

                    float destroyChance = GetDestructionProbability(grade);
                    float roll = UnityEngine.Random.value;

                    if (roll < destroyChance)
                    {
                        result.DestroyedPartIDs.Add(partState.partID);
                        Debug.Log($"[정산기] 부품 영구 파괴됨: {kvp.Key} (ID: {partState.partID}, 등급: {grade}, 확률: {destroyChance:P0}, 롤: {roll:F2})");
                    }
                    else
                    {
                        Debug.Log($"[정산기] 부품 파괴 면제(보존됨): {kvp.Key} (ID: {partState.partID}, 등급: {grade})");
                    }
                }
            }

            result.SummaryMessage = $"전투 패배! 판돈 몰수, 코어 손상(Unstable), 파괴된 부품: {result.DestroyedPartIDs.Count}개임";
        }

        // ====================================================================
        // 외부 파트 브로드캐스팅 처리함
        // ====================================================================
        OnSettlementCompleted?.Invoke(result);

        if (result.DestroyedPartIDs.Count > 0)
        {
            OnPartsPermanentlyDestroyed?.Invoke(result.DestroyedPartIDs);
        }

        OnGoldReward?.Invoke(result.GoldDelta);
        OnCoreStateChanged?.Invoke(result.CoreUnstable);

        // LSH 골드 파트 GlobalGold 싱글톤 연동 (프로젝트 내에 존재할 경우 안전하게 반영함)
        if (GlobalGold.Instance != null && result.GoldDelta != 0)
        {
            if (result.GoldDelta > 0)
                GlobalGold.Instance.GetGold("골드 정산", result.GoldDelta);
            else
                GlobalGold.Instance.UseGold("골드 정산", Mathf.Abs(result.GoldDelta));
        }

        return result;
    }
}
