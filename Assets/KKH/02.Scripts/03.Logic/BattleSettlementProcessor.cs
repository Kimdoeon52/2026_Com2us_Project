using System;
using System.Collections.Generic;
using System.Reflection;
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
    public int CoreLevelBefore = 1;
    public int CoreExpBefore = 0;
    public int CoreRequiredExpBefore = 200;
    public int CoreLevelAfter = 1;
    public int CoreExpAfter = 0;
    public int CoreRequiredExpAfter = 200;
    public bool CoreLeveledUp => CoreLevelAfter > CoreLevelBefore;
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

    private static readonly int[] DefaultCoreRequiredExpTable =
    {
        200, 260, 340, 450, 470, 500, 520, 550, 580, 610,
        640, 670, 700, 740, 770, 810, 850, 890, 940, 990,
        1030, 1090, 1140, 1200, 1260, 1320, 1390, 1460, 1530, 0
    };

    private static int GetCoreMaxLevel(CoreMasterData coreMaster)
    {
        var hostCore = InventoryHost.Instance != null ? InventoryHost.Instance.Core : null;
        if (hostCore != null && hostCore.MaxLevel > 0)
        {
            return hostCore.MaxLevel;
        }
        return CoreMasterData.MaxLevel;
    }

    private static int GetCoreRequiredExp(int level, CoreMasterData coreMaster)
    {
        int maxLevel = GetCoreMaxLevel(coreMaster);
        if (level < 1 || level >= maxLevel) return 0;

        var hostCore = InventoryHost.Instance != null ? InventoryHost.Instance.Core : null;
        if (hostCore != null && level <= hostCore.MaxLevel)
        {
            int expToNext = hostCore.GetLevel(level).ExpToNext;
            if (expToNext > 0) return expToNext;
        }

        if (coreMaster != null && level <= CoreMasterData.MaxLevel)
        {
            int req = coreMaster.GetRequiredEXP(level);
            if (req > 0) return req;
        }

        if (level - 1 < DefaultCoreRequiredExpTable.Length)
        {
            return DefaultCoreRequiredExpTable[level - 1];
        }

        return CoreMasterData.CalculateRequiredExpByFormula(level);
    }

    private static void ApplyCoreLevelStats(CombatantSnapshot player, CoreMasterData coreMaster)
    {
        if (player == null) return;

        var hostCore = InventoryHost.Instance != null ? InventoryHost.Instance.Core : null;
        if (hostCore != null && hostCore.MaxLevel > 0)
        {
            CoreLevel levelData = hostCore.GetLevel(player.coreLevel);
            if (levelData.Hp > 0) player.maxHp = levelData.Hp;
            player.baseDefense = levelData.Defense;
        }
        else if (coreMaster != null)
        {
            player.maxHp = coreMaster.GetMaxHp(player.coreLevel);
            player.baseDefense = coreMaster.GetDefense(player.coreLevel);
        }

        player.currentHp = player.maxHp; // 레벨업 시 체력 회복 처리함
    }

    private static void ApplyCoreExpAndRecord(CombatantSnapshot player, BattleSettlementResult result, CoreMasterData coreMaster)
    {
        if (result == null) return;

        if (player == null)
        {
            result.CoreLevelBefore = 1;
            result.CoreExpBefore = 0;
            result.CoreRequiredExpBefore = GetCoreRequiredExp(1, coreMaster);
            result.CoreLevelAfter = 1;
            result.CoreExpAfter = Mathf.Max(0, result.CoreExpGained);
            result.CoreRequiredExpAfter = result.CoreRequiredExpBefore;
            return;
        }

        player.coreLevel = Mathf.Max(1, player.coreLevel);
        player.currentCoreExp = Mathf.Max(0, player.currentCoreExp);

        result.CoreLevelBefore = player.coreLevel;
        result.CoreExpBefore = player.currentCoreExp;
        result.CoreRequiredExpBefore = GetCoreRequiredExp(result.CoreLevelBefore, coreMaster);

        ProcessCoreExp(player, result.CoreExpGained, coreMaster);

        result.CoreLevelAfter = player.coreLevel;
        result.CoreExpAfter = player.currentCoreExp;
        result.CoreRequiredExpAfter = GetCoreRequiredExp(result.CoreLevelAfter, coreMaster);
    }

    /// <summary>
    /// 코어 경험치 누적 및 레벨업 처리 (CoreMasterData 미할당 시 InventoryHost CoreDefinition / 기본 테이블 폴백 지원)
    /// </summary>
    public static void ProcessCoreExp(CombatantSnapshot player, int gainedExp, CoreMasterData coreMaster = null)
    {
        if (player == null || gainedExp <= 0)
        {
            return;
        }

        player.coreLevel = Mathf.Max(1, player.coreLevel);
        player.currentCoreExp = Mathf.Max(0, player.currentCoreExp);

        int maxLevel = GetCoreMaxLevel(coreMaster);
        if (player.coreLevel >= maxLevel)
        {
            player.currentCoreExp = 0;
            return;
        }

        player.currentCoreExp += gainedExp;
        int reqExp = GetCoreRequiredExp(player.coreLevel, coreMaster);

        while (reqExp > 0 && player.currentCoreExp >= reqExp && player.coreLevel < maxLevel)
        {
            player.currentCoreExp -= reqExp;
            player.coreLevel++;
            ApplyCoreLevelStats(player, coreMaster);
            reqExp = player.coreLevel < maxLevel ? GetCoreRequiredExp(player.coreLevel, coreMaster) : 0;
        }

        if (player.coreLevel >= maxLevel)
        {
            player.currentCoreExp = 0;
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

                ApplyCoreExpAndRecord(player, result, coreMaster);

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
                result.GoldDelta = 0; // 입장 시 선지불 완료, 패배 시 추가 차감 0 (신청비 몰수)
                result.CoreExpGained = CalculateGainedEXP(basePrice, layer.coreExpMultiplier, levelDiffMultiplier, isWin: false);
                result.CoreUnstable = layer.coreUnstableOnLoss;

                ApplyCoreExpAndRecord(player, result, coreMaster);

                if (player != null)
                {
                    foreach (var kvp in player.partStates)
                    {
                        var partState = kvp.Value;
                        if (partState.partID.StartsWith("DEFAULT_", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        partState.currentDurability = 0; // 전 부품 내구도 0 강제 세팅
                        result.FinalPartDurabilities[partState.partID] = 0;

                        // 기획서 §6.10.5: 아주 쉬움은 파괴 판정 없음!
                        if (layer.destructionMultiplier <= 0f)
                        {
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
                        }
                    }
                }

                result.SummaryMessage = $"[보스 토벌 실패 ({difficulty})] 신청비 {entryFee}G 몰수(선지불), 코어 불안정: {result.CoreUnstable}, 파괴된 부품: {result.DestroyedPartIDs.Count}개, 위로 EXP: {result.CoreExpGained}";
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

                ApplyCoreExpAndRecord(player, result, coreMaster);

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
                result.GoldDelta = 0; // ArenaBettingManager에서 이미 선차감되었으므로 추가 차감 0 (판돈 몰수)
                result.CoreExpGained = CalculateGainedEXP(regionBasePrice, 1.0f, 1.0f, isWin: false);
                result.CoreUnstable = true;

                ApplyCoreExpAndRecord(player, result, coreMaster);

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

                result.SummaryMessage = $"일반전 패배! 판돈 몰수(선지불), 코어 손상(Unstable), 파괴된 부품: {result.DestroyedPartIDs.Count}개임";
            }
        }

        // ====================================================================
        // 대외 브로드캐스팅 및 영구 데이터(세이브) 동기화 처리 (타 파트 스크립트 수정 0줄)
        // ====================================================================

        // 1. 코어 경험치 및 레벨 영구 세이브 데이터 동기화 (KDU InventoryHost 연동)
        if (player != null)
        {
            // 씬에 InventoryHost가 없다면 Resources에서 로드 시도 (단독 씬 테스트 지원)
            if (InventoryHost.Instance == null)
            {
                var hostPrefab = Resources.Load<GameObject>("InventoryHost");
                if (hostPrefab != null)
                {
                    UnityEngine.Object.Instantiate(hostPrefab);
                }
            }

            if (InventoryHost.Instance != null && InventoryHost.Instance.Save?.Core != null)
            {
                var coreSave = InventoryHost.Instance.Save.Core;
                var coreType = typeof(CoreSaveData);

                // 리플렉션으로 _level, _exp 안전하게 주입 (KDU 스크립트 수정 0줄)
                var fieldLevel = coreType.GetField("_level", BindingFlags.NonPublic | BindingFlags.Instance);
                var fieldExp = coreType.GetField("_exp", BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldLevel != null) fieldLevel.SetValue(coreSave, player.coreLevel);
                if (fieldExp != null) fieldExp.SetValue(coreSave, player.currentCoreExp);
            }
        }

        // 2. 대외 브로드캐스팅 처리 (KDU InventoryHost가 파츠 내구도/파괴를 처리하고 inventory.json 자동 저장)
        try
        {
            OnSettlementCompleted?.Invoke(result);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        // 기본 파츠만 장착했거나 무피격 승리 시에도 코어 EXP/레벨 변경이 inventory.json에 즉시 디스크 저장되도록 보장
        if (InventoryHost.Instance != null)
        {
            try
            {
                var hostType = typeof(InventoryHost);
                var saveToFile = hostType.GetMethod("SaveToFile", BindingFlags.NonPublic | BindingFlags.Instance);
                if (saveToFile != null)
                {
                    saveToFile.Invoke(InventoryHost.Instance, null);
                }
                else
                {
                    hostType.GetMethod("MarkSaveDirty", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(InventoryHost.Instance, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[정산기] InventoryHost 저장 동기화 중 경고: {e.Message}");
            }
        }

        try
        {
            if (result.DestroyedPartIDs.Count > 0)
            {
                OnPartsPermanentlyDestroyed?.Invoke(result.DestroyedPartIDs);
            }

            OnGoldReward?.Invoke(result.GoldDelta);
            OnCoreStateChanged?.Invoke(result.CoreUnstable);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        // 3. LSH 골드 파트 GlobalGold 싱글톤 연동 및 디스크 파일(.mainGold.json) 즉시 영구 저장
        try
        {
            if (GlobalGold.Instance != null && result.GoldDelta != 0)
            {
                const int playerId = 0;
                if (result.GoldDelta > 0)
                    GlobalGold.Instance.GetGold(playerId, result.GoldDelta);
                else
                    GlobalGold.Instance.UseGold(playerId, Mathf.Abs(result.GoldDelta));

                // 변경된 골드 디스크 즉시 저장 호출
                GlobalGold.Instance.SaveGame();
            }
            else if (GlobalGold.Instance == null && result.GoldDelta != 0)
            {
                Debug.LogWarning($"[정산기] GlobalGold.Instance가 씬에 없어 골드 변동({result.GoldDelta:+#;-#;0}G)이 디스크에 기록되지 않았습니다 (단독 씬 테스트 환경).");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[정산기] GlobalGold 저장 연동 중 경고: {e.Message}");
        }

        // 4. LHM 보스 레이스 진행도 및 대사 변수(BossProgressStore) 동기화
        if (bossMaster != null)
        {
            try
            {
                var progressStore = UnityEngine.Object.FindFirstObjectByType<RealSteel.BossRace.BossProgressStore>();
                if (progressStore != null)
                {
                    string resultStr = playerWon ? "win" : "lose";
                    string diffStr = difficulty.ToString().ToLower();
                    string bossId = bossMaster.bossID switch
                    {
                        "BOSS_REGION_02" => "boss.kyodai",
                        "BOSS_REGION_03" => "boss.region3",
                        null or "" or "BOSS_REGION_01" => "boss.wheel",
                        _ => bossMaster.bossID
                    };
                    progressStore.RecordHunt(resultStr, bossId, diffStr);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[정산기] BossProgressStore 연동 중 경고: {e.Message}");
            }
        }

        return result;
    }
}
