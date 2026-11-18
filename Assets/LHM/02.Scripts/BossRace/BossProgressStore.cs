using System;
using System.Collections.Generic;
using System.IO;
using RealSteel.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.BossRace
{
    // 보스별 토벌 기록 (도전 · 패배 · 최고 클리어 난이도 · 지역 클리어)
    // - 기록은 boss_progress.json에 저장 → 필드 ↔ 전투 씬을 오가도 유지
    // - 전투 씬(대화 매니저 없음)에서 KKH 정산이 RecordHunt를 부르면 대사 변수(hunt.*)는 '대기 결과'로 저장했다가
    //   필드 씬의 기록기가 시작될 때 대화 매니저에 반영
    public class BossProgressStore : MonoBehaviour, IBossRaceProgress, IRobotPartStatus
    {
        public const string SaveFileName = "boss_progress.json";

        [Header("연결")]
        [SerializeField] private DialogueManager manager;

        [Header("시즌")]
        [SerializeField, Min(1)] private int seasonLength = 90;

        [Header("파츠 내구도 0~1 (머리·왼팔·오른팔·왼다리·오른다리) — 장비 시스템 연결 전 임시 값")]
        [SerializeField] private float[] partDurability = { 0.82f, 0.64f, 0.30f, 0.91f, 0f };

        [Serializable]
        private class BossRecord
        {
            public string boss;
            public int attempts;
            public int loses;
            public int bestCleared = -1;
        }

        [Serializable]
        private class PendingHunt
        {
            public string result;
            public string boss;
            public string difficulty;
        }

        [Serializable]
        private class SaveData
        {
            public int version = 1;
            public List<BossRecord> bosses = new List<BossRecord>();
            public List<string> clearedRegions = new List<string>();
            public List<PendingHunt> pending = new List<PendingHunt>();
        }

        private SaveData data = new SaveData();

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public int AttemptsOf(string boss) => Find(boss)?.attempts ?? 0;
        public int LosesOf(string boss) => Find(boss)?.loses ?? 0;
        public int BestDifficulty(string boss) => Find(boss)?.bestCleared ?? -1;
        public bool IsRegionCleared(string boss) => data.clearedRegions.Contains(boss);
        public int SeasonDaysLeft => Mathf.Max(0, seasonLength - VariableInt("season.day"));
        public string RankText => $"{VariableText("rank.band")}위";
        public string ScoreText => "-";

        private bool CanWriteVariables => manager != null && manager.IsReady && !manager.IsFallback && manager.Variables != null;

        private void Awake()
        {
            Load();
        }

        private void Start()
        {
            if (manager == null) manager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
            ApplyPending();
        }

        public void Configure(DialogueManager newManager, int newSeasonLength, float[] newPartDurability)
        {
            manager = newManager;
            seasonLength = newSeasonLength;
            partDurability = newPartDurability;
        }

        // KDU 인벤토리(장착 파츠)의 실제 내구도. 인벤토리가 없는 테스트 씬만 인스펙터 값 사용
        public bool TryGetDurability(BodyPart part, out float ratio)
        {
            if (InventoryHost.Instance != null && Enum.TryParse(part.ToString(), out Slot slot))
            {
                PartsDefinition equipped = InventoryHost.Instance.GetEquipped(slot);
                ratio = equipped != null && equipped.Durability > 0 ? (float)InventoryHost.Instance.GetDurability(slot) / equipped.Durability : 0f;
                return true;
            }

            int index = (int)part;
            bool exists = partDurability != null && index >= 0 && index < partDurability.Length;
            ratio = exists ? partDurability[index] : 0f;
            return exists;
        }

        public void AddAttempt(string boss)
        {
            GetOrAdd(boss).attempts++;
            SaveToFile();
        }

        public void ToggleRegionCleared(string boss)
        {
            if (!data.clearedRegions.Remove(boss)) data.clearedRegions.Add(boss);
            SaveToFile();
        }

        public void RecordHunt(string result, string boss, string difficulty)
        {
            difficulty = NormalizeDifficulty(difficulty);
            BossRecord record = GetOrAdd(boss);

            if (result == "lose")
            {
                record.loses++;
            }
            else
            {
                int index = BossRaceDifficulty.IndexOf(difficulty);
                if (index > record.bestCleared) record.bestCleared = index;
            }

            if (CanWriteVariables)
            {
                ApplyVariables(result, boss, difficulty);
                manager.LogState($"{(result == "win" ? "승리" : "패배")} ({boss}, {difficulty}) — 이 보스 패배 {record.loses}회");
                manager.Save();
            }
            else
            {
                data.pending.Add(new PendingHunt { result = result, boss = boss, difficulty = difficulty });
                Debug.Log($"[BossProgress] {(result == "win" ? "승리" : "패배")} ({boss}, {difficulty}) 기록 저장 — 이 보스 패배 {record.loses}회. 대사 변수는 필드로 돌아가면 반영합니다.", this);
            }

            SaveToFile();
        }

        // 전투 씬에서 쌓인 결과를 대사 변수에 반영 (필드 씬 시작 시)
        private void ApplyPending()
        {
            if (data.pending.Count == 0 || !CanWriteVariables) return;

            foreach (PendingHunt hunt in data.pending) ApplyVariables(hunt.result, hunt.boss, hunt.difficulty);

            int count = data.pending.Count;
            data.pending.Clear();
            manager.LogState($"전투 결과 {count}건 반영");
            manager.Save();
            SaveToFile();
        }

        private void ApplyVariables(string result, string boss, string difficulty)
        {
            DialogueVariables variables = manager.Variables;
            variables.Set("hunt.lastResult", result);
            variables.Set("hunt.lastBoss", boss);
            variables.Set("hunt.lastDifficulty", difficulty);
            variables.Add("hunt.count", 1);
            variables.Add(result == "win" ? "hunt.winCount" : "hunt.loseCount", 1);
        }

        // KKH는 enum.ToString().ToLower()로 넘김 ("veryeasy") → 대사 변수 id ("veryEasy")로 맞춤
        private static string NormalizeDifficulty(string difficulty)
        {
            foreach (string id in BossRaceDifficulty.Ids)
            {
                if (string.Equals(id, difficulty, StringComparison.OrdinalIgnoreCase)) return id;
            }
            return difficulty;
        }

        private BossRecord Find(string boss)
        {
            foreach (BossRecord record in data.bosses)
            {
                if (record.boss == boss) return record;
            }
            return null;
        }

        private BossRecord GetOrAdd(string boss)
        {
            BossRecord record = Find(boss);
            if (record != null) return record;

            record = new BossRecord { boss = boss };
            data.bosses.Add(record);
            return record;
        }

        private void Load()
        {
            string path = SavePath;
            try
            {
                if (File.Exists(path))
                {
                    data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) ?? new SaveData();
                    data.bosses ??= new List<BossRecord>();
                    data.clearedRegions ??= new List<string>();
                    data.pending ??= new List<PendingHunt>();
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BossProgress] 보스 기록을 읽지 못해 빈 기록으로 시작합니다 — {path} ({e.Message})", this);
            }
            data = new SaveData();
        }

        private void SaveToFile()
        {
            string path = SavePath;
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BossProgress] 보스 기록 저장 실패 — {path} ({e.Message})", this);
            }
        }

        public static void DeleteSave()
        {
            string path = SavePath;
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BossProgress] 보스 기록 삭제 실패 — {path} ({e.Message})");
            }
        }

        private int VariableInt(string name)
        {
            return manager != null && manager.Variables != null ? manager.Variables.GetInt(name) : 0;
        }

        private string VariableText(string name)
        {
            return manager != null && manager.Variables != null ? manager.Variables.Get(name) : "-";
        }

        // 전투 씬(KKH BattleManager가 있는 씬)에 기록기가 없으면 자동으로 만듦 → 정산의 RecordHunt가 닿게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookBattleScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInBattleScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInBattleScene();
        }

        private static void EnsureInBattleScene()
        {
            if (FindAnyObjectByType<BattleManager>(FindObjectsInactive.Include) == null) return;
            if (FindAnyObjectByType<BossProgressStore>(FindObjectsInactive.Include) != null) return;

            new GameObject("BossProgressStore (자동 · 전투 기록)").AddComponent<BossProgressStore>();
        }
    }
}
