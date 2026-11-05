using System.Collections.Generic;
using RealSteel.Dialogue;
using UnityEngine;

namespace RealSteel.BossRace
{
    public class BossProgressStore : MonoBehaviour, IBossRaceProgress, IRobotPartStatus
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("시즌")]
        [SerializeField, Min(1)] private int seasonLength = 90;

        [Header("파츠 내구도 0~1 (머리·왼팔·오른팔·왼다리·오른다리) — 장비 시스템 연결 전 임시 값")]
        [SerializeField] private float[] partDurability = { 0.82f, 0.64f, 0.30f, 0.91f, 0f };

        private readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        private readonly Dictionary<string, int> loses = new Dictionary<string, int>();
        private readonly Dictionary<string, int> bestCleared = new Dictionary<string, int>();
        private readonly HashSet<string> clearedRegions = new HashSet<string>();

        public int AttemptsOf(string boss) => attempts.TryGetValue(boss, out int count) ? count : 0;
        public int LosesOf(string boss) => loses.TryGetValue(boss, out int count) ? count : 0;
        public int BestDifficulty(string boss) => bestCleared.TryGetValue(boss, out int index) ? index : -1;
        public bool IsRegionCleared(string boss) => clearedRegions.Contains(boss);
        public int SeasonDaysLeft => Mathf.Max(0, seasonLength - VariableInt("season.day"));
        public string RankText => $"{VariableText("rank.band")}위";
        public string ScoreText => "-";

        public void Configure(DialogueManager newManager, int newSeasonLength, float[] newPartDurability)
        {
            manager = newManager;
            seasonLength = newSeasonLength;
            partDurability = newPartDurability;
        }

        public bool TryGetDurability(BodyPart part, out float ratio)
        {
            int index = (int)part;
            bool exists = partDurability != null && index >= 0 && index < partDurability.Length;
            ratio = exists ? partDurability[index] : 0f;
            return exists;
        }

        public void AddAttempt(string boss)
        {
            attempts[boss] = AttemptsOf(boss) + 1;
        }

        public void ToggleRegionCleared(string boss)
        {
            if (!clearedRegions.Remove(boss)) clearedRegions.Add(boss);
        }

        public void RecordHunt(string result, string boss, string difficulty)
        {
            if (manager == null || manager.Variables == null)
            {
                Debug.LogWarning($"[BossProgress] 대사 변수를 쓸 수 없어 토벌 결과를 기록하지 않습니다 ({result}, {boss}).", this);
                return;
            }

            DialogueVariables variables = manager.Variables;
            variables.Set("hunt.lastResult", result);
            variables.Set("hunt.lastBoss", boss);
            variables.Set("hunt.lastDifficulty", difficulty);
            variables.Add("hunt.count", 1);
            variables.Add(result == "win" ? "hunt.winCount" : "hunt.loseCount", 1);

            if (result == "lose")
            {
                loses[boss] = LosesOf(boss) + 1;
            }
            else
            {
                int index = BossRaceDifficulty.IndexOf(difficulty);
                if (index > BestDifficulty(boss)) bestCleared[boss] = index;
            }

            manager.LogState($"{(result == "win" ? "승리" : "패배")} ({boss}, {difficulty}) — 이 보스 패배 {LosesOf(boss)}회");
            manager.Save();
        }

        private int VariableInt(string name)
        {
            return manager != null && manager.Variables != null ? manager.Variables.GetInt(name) : 0;
        }

        private string VariableText(string name)
        {
            return manager != null && manager.Variables != null ? manager.Variables.Get(name) : "-";
        }
    }
}
