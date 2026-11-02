using System;
using System.Collections.Generic;
using RealSteel.Dialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.BossRace
{
    [Serializable]
    public class BossBattleScene
    {
        public string bossId = "boss.wheel";
        public string sceneName = "Copys_1020/Battle";
    }

    public class HuntDeparture : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private NpcChoiceMenu menu;
        [SerializeField] private BossRaceSelectUI bossList;
        [SerializeField] private BossDifficultySelectUI difficultySelect;
        [SerializeField] private BossEntryDialogue entryDialogue;
        [SerializeField] private MonoBehaviour progressSource;

        [Header("전투 씬 (빌드 설정에 등록된 씬)")]
        [SerializeField] private string defaultBattleScene = "Copys_1020/Battle";
        [SerializeField] private List<BossBattleScene> battleScenes = new List<BossBattleScene>();

        [Header("이번 시즌 보스 = race.id 순서의 보스")]
        [SerializeField] private string[] raceIds = { "race1", "race2", "race3", "final" };
        [SerializeField] private string fallbackBossId = "boss.wheel";
        [SerializeField] private string backLabel = "길드 접수처";

        private string lastDifficulty = "normal";
        private string bossId;
        private string difficulty;
        private bool busy;

        public bool IsBusy => busy;
        public event Action<string, string, string> Departing;

        private IBossRaceProgress Progress => progressSource as IBossRaceProgress;
        private IRobotPartStatus Parts => progressSource as IRobotPartStatus;

        private void Start()
        {
            if (manager == null || menu == null || bossList == null || difficultySelect == null || entryDialogue == null || Progress == null)
            {
                Debug.LogError("[Hunt] Manager, Menu, Boss List, Difficulty Select, Entry Dialogue, Progress Source(IBossRaceProgress) 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            menu.ActionChosen += OnActionChosen;
        }

        private void OnDestroy()
        {
            if (menu != null) menu.ActionChosen -= OnActionChosen;
        }

        private void OnActionChosen(NpcInteractable npc, NpcAction action)
        {
            if (!enabled || action != NpcAction.HuntApply || busy) return;
            Open();
        }

        public bool Open()
        {
            bossId = CurrentSeasonBoss();
            BossRaceEntry entry = FindEntry(bossId);
            if (entry == null)
            {
                Debug.LogError($"[Hunt] 보스 목록(BossRaceSelectUI Entries)에 '{bossId}'가 없어 난이도 선택을 열 수 없습니다.", this);
                return false;
            }

            if (!difficultySelect.Open(Progress, entry, Parts, lastDifficulty, OnDifficultyChosen, OnCancelled, backLabel)) return false;

            busy = true;
            Debug.Log($"[Hunt] 토벌 신청 — 이번 시즌 보스 {entry.regionLabel} {entry.bossName}({bossId}) 난이도 선택");
            return true;
        }

        private void OnCancelled()
        {
            busy = false;
            Debug.Log("[Hunt] 토벌 신청 취소");
        }

        private void OnDifficultyChosen(string chosen)
        {
            difficulty = chosen;
            lastDifficulty = chosen;
            Debug.Log($"[Hunt] 난이도 {BossRaceDifficulty.Label(BossRaceDifficulty.IndexOf(chosen))} 선택 → 보스 진입 대사");
            entryDialogue.Play(bossId, chosen, Progress.AttemptsOf(bossId), Progress.LosesOf(bossId), Depart);
        }

        private void Depart()
        {
            int index = Mathf.Clamp(BossRaceDifficulty.IndexOf(difficulty), 0, 4);
            BossDifficulty battleDifficulty = (BossDifficulty)index;
            string scene = SceneFor(bossId);

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[Hunt] 전투 씬 '{scene}'을 불러올 수 없습니다. File > Build Settings에 등록되어 있는지, HuntDeparture의 씬 이름을 확인하세요. 필드에 남습니다.", this);
                busy = false;
                return;
            }

            BattleManager.SetPreBattleDifficulty(battleDifficulty);
            manager.Save();
            Debug.Log($"[Hunt] 전투 출발 — {bossId} / {battleDifficulty} → 씬 '{scene}'");
            Departing?.Invoke(bossId, difficulty, scene);
            SceneManager.LoadScene(scene);
        }

        private string CurrentSeasonBoss()
        {
            string race = manager.Variables != null ? manager.Variables.Get("race.id") : null;
            int index = Array.IndexOf(raceIds, race);
            if (index >= 0 && index < bossList.Entries.Count && bossList.Entries[index] != null) return bossList.Entries[index].bossId;
            return fallbackBossId;
        }

        private BossRaceEntry FindEntry(string id)
        {
            foreach (BossRaceEntry entry in bossList.Entries)
            {
                if (entry != null && entry.bossId == id) return entry;
            }
            return null;
        }

        private string SceneFor(string id)
        {
            foreach (BossBattleScene item in battleScenes)
            {
                if (item != null && item.bossId == id && !string.IsNullOrEmpty(item.sceneName)) return item.sceneName;
            }
            return defaultBattleScene;
        }
    }
}
