using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RealSteel.Reward
{
    [Serializable]
    public class SeasonReward
    {
        public string season;
        public int rank;
        public int gold;
    }

    public static class SeasonRewardStore
    {
        public const string SaveFileName = "season_reward.json";

        [Serializable]
        private class SaveData
        {
            public List<SeasonReward> pending = new List<SeasonReward>();
        }

        private static SaveData data;

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static IReadOnlyList<SeasonReward> Pending => Data.pending;
        public static bool HasPending => Data.pending.Count > 0;
        public static int PendingGold => Data.pending.Sum(r => r.gold);

        public static event Action Changed;

        private static SaveData Data => data ??= Load();

        public static void Add(int rank, int gold, string seasonOverride = null)
        {
            string season = string.IsNullOrEmpty(seasonOverride) ? EndedSeasonLabel() : seasonOverride;
            if (season != null && Data.pending.Any(r => r.season == season))
            {
                Debug.LogWarning($"[SeasonReward] {season} 포상은 이미 기록되어 있어 건너뜀 ({rank}위 {gold}골드)");
                return;
            }

            var reward = new SeasonReward { season = season ?? "지난", rank = rank, gold = gold };
            Data.pending.Add(reward);
            Save();
            Debug.Log($"[SeasonReward] 받을 포상 추가: {reward.season} {rank}위 {gold}골드 (대기 {Data.pending.Count}건)");
            Changed?.Invoke();
        }

        public static List<SeasonReward> TakeAll()
        {
            var taken = new List<SeasonReward>(Data.pending);
            Data.pending.Clear();
            Save();
            Changed?.Invoke();
            return taken;
        }

        public static void DeleteAll()
        {
            data = new SaveData();
            if (File.Exists(SavePath)) File.Delete(SavePath);
            Changed?.Invoke();
        }

        private static string EndedSeasonLabel()
        {
            TimeSystemManager clock = TimeSystemManager.Instance != null
                ? TimeSystemManager.Instance
                : UnityEngine.Object.FindAnyObjectByType<TimeSystemManager>(FindObjectsInactive.Include);
            if (clock == null)
            {
                Debug.LogWarning("[SeasonReward] LJS TimeSystemManager가 없어 시즌 이름을 정하지 못했습니다 ('지난 시즌'으로 기록, 중복 검사 안 함)");
                return null;
            }

            int quarter = (clock.month - 1) / 3;
            int year = clock.year;
            if (quarter == 0)
            {
                quarter = 4;
                year--;
            }
            return $"{year}년 {quarter}분기";
        }

        private static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath)) return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SeasonReward] {SavePath} 읽기 실패 — 빈 기록으로 시작: {e.Message}");
            }
            return new SaveData();
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SeasonReward] {SavePath} 저장 실패: {e.Message}");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            data = null;
            Changed = null;
        }
    }
}
