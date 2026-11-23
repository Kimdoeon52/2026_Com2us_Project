using System;
using System.Collections;
using System.Collections.Generic;
using RealSteel.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [Tooltip("씬에 로딩 화면이 없을 때 만들어 쓸 LJS 로딩 프리팹 (LodingManager.prefab). 비우면 로딩 화면 없이 비동기 이동")]
        [SerializeField] private GameObject loadingScreenPrefab;

        [Header("이번 시즌 보스 = race.id 순서의 보스")]
        [SerializeField] private string[] raceIds = { "race1", "race2", "race3", "final" };
        [SerializeField] private string fallbackBossId = "boss.wheel";
        [SerializeField] private string backLabel = "길드 접수처";

        [Header("프리시즌 (첫 시즌 지역 선택 전)")]
        [SerializeField] private bool blockBeforeSeason = true;
        [SerializeField] private string preseasonDialogueId = "hunt_preseason";

        private string lastDifficulty = "normal";
        private string bossId;
        private string difficulty;
        private bool busy;
        private bool maintaining;

        public bool IsBusy => busy;
        public event Action<string, string, string> Departing;

        private IBossRaceProgress Progress => progressSource as IBossRaceProgress;
        private IRobotPartStatus Parts => progressSource as IRobotPartStatus;

        private void Start()
        {
            AutoResolveReferencesIfMissing();

            if (manager == null || menu == null || bossList == null || difficultySelect == null || entryDialogue == null || Progress == null)
            {
                Debug.LogError("[Hunt] Manager, Menu, Boss List, Difficulty Select, Entry Dialogue, Progress Source(IBossRaceProgress) 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            menu.ActionChosen += OnActionChosen;
            difficultySelect.MaintenanceRequested += OnMaintenance;
        }

        private void AutoResolveReferencesIfMissing()
        {
            if (manager == null) manager = FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (menu == null) menu = FindAnyObjectByType<NpcChoiceMenu>(FindObjectsInactive.Include);
            if (bossList == null) bossList = FindAnyObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            if (difficultySelect == null) difficultySelect = FindAnyObjectByType<BossDifficultySelectUI>(FindObjectsInactive.Include);
            if (entryDialogue == null) entryDialogue = FindAnyObjectByType<BossEntryDialogue>(FindObjectsInactive.Include);
            if (progressSource == null)
            {
                var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < allBehaviours.Length; i++)
                {
                    if (allBehaviours[i] is IBossRaceProgress)
                    {
                        progressSource = allBehaviours[i];
                        break;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (menu != null) menu.ActionChosen -= OnActionChosen;
            if (difficultySelect != null) difficultySelect.MaintenanceRequested -= OnMaintenance;
        }

        // 정비하기: 난이도 창을 숨기고 YJW 수리창을 띄움 → 닫히면(닫기 버튼 · Esc) 난이도 창으로 돌아와 내구도 다시 표시
        private void OnMaintenance()
        {
            if (!busy || maintaining) return;

            MonoBehaviour owner = CraftingWindowBridge.FindOwner();
            if (owner == null)
            {
                Debug.LogWarning($"[Hunt] 정비하기 — 이 씬에 수리창({CraftingWindowBridge.OwnerTypeName})이 없습니다. 대화 툴 '길드 접수원 연결'을 다시 실행하면 JunkShop_Map에서 가져옵니다.", this);
                return;
            }

            GameObject window = CraftingWindowBridge.Open(owner, CraftingWindowBridge.FixWindowField, CraftingWindowBridge.FixOpenMethod, "수리");
            if (window == null) return;

            difficultySelect.Suspend();
            StartCoroutine(WaitMaintenance(owner, window));
        }

        private IEnumerator WaitMaintenance(MonoBehaviour owner, GameObject window)
        {
            maintaining = true;
            Debug.Log($"[Hunt] 정비 — 수리창 열림 ({window.name}), 닫으면 난이도 선택으로 돌아감");
            yield return null;   // 정비하기를 누른 입력이 바로 닫기로 이어지지 않게 한 프레임 쉼

            while (window != null && window.activeSelf)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CraftingWindowBridge.Close(owner, CraftingWindowBridge.FixCloseMethod, window);
                    yield return null;   // 같은 Esc가 난이도 창의 '뒤로'로 이어지지 않게
                    break;
                }
                yield return null;
            }

            maintaining = false;
            difficultySelect.Resume();
            Debug.Log("[Hunt] 정비 끝 → 난이도 선택");
        }

        private void OnActionChosen(NpcInteractable npc, NpcAction action)
        {
            if (!enabled || action != NpcAction.HuntApply || busy) return;
            Open();
        }

        public bool Open()
        {
            // 첫 시즌 지역을 고르기 전(프리시즌)에는 토벌 신청을 받지 않음 → 접수원 "시즌 시작 전이에요"
            DialogueDayProcessor days = FindAnyObjectByType<DialogueDayProcessor>();
            if (blockBeforeSeason && days != null && days.IsPreseason)
            {
                Debug.Log("[Hunt] 토벌 신청 거절 — 시즌 시작 전 (첫 시즌 지역 선택 전)", this);
                if (!manager.PlayDialogue(preseasonDialogueId)) Debug.LogWarning($"[Hunt] '{preseasonDialogueId}' 대사를 열지 못했습니다 (event_dialogues 풀 확인).", this);
                return false;
            }

            bossId = CurrentSeasonBoss();
            BossRaceEntry entry = FindEntry(bossId);
            if (entry == null)
            {
                Debug.LogError($"[Hunt] 보스 목록(BossRaceSelectUI Entries)에 '{bossId}'가 없어 난이도 선택을 열 수 없습니다.", this);
                return false;
            }

            if (entry.unreleased)
            {
                Debug.LogWarning($"[Hunt] 토벌 신청 거절 — {entry.RegionText} {entry.bossName}({bossId})는 미개방 지역입니다.", this);
                return false;
            }

            if (!difficultySelect.Open(Progress, entry, Parts, lastDifficulty, OnDifficultyChosen, OnCancelled, backLabel)) return false;

            busy = true;
            Debug.Log($"[Hunt] 토벌 신청 — 이번 시즌 보스 {entry.RegionText} {entry.bossName}({bossId}) 난이도 선택");
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

            // 기획서 §10.3 & §10.5: 토벌 입장 시 신청비 선지불 (VeryEasy: 0, Easy: 25G, Normal: 50G, Hard: 100G, Hell: 150G)
            BossDifficultyLayer layer = BossDifficultyLayer.GetDefault(battleDifficulty);
            int entryFee = Mathf.RoundToInt(500 * layer.entryFeeMultiplier);

            if (GlobalGold.Instance != null && entryFee > 0)
            {
                if (GlobalGold.Instance.PlayerGold < entryFee)
                {
                    Debug.LogWarning($"[Hunt] 골드가 부족하여 토벌을 신청할 수 없습니다. (필요 신청비: {entryFee}G, 보유 골드: {GlobalGold.Instance.PlayerGold}G)");
                    busy = false;
                    return;
                }

                GlobalGold.Instance.UseGold(0, entryFee);
                GlobalGold.Instance.SaveGame();
                Debug.Log($"<color=yellow>[Hunt] 토벌 신청비 {entryFee}G 선지불 완료 (잔여 골드: {GlobalGold.Instance.PlayerGold}G)</color>");
            }

            BattleManager.SetPreBattleDifficulty(battleDifficulty);
            if (progressSource is BossProgressStore store) store.AddAttempt(bossId);   // 도전 횟수 (boss_progress.json에 저장)
            manager.Save();
            Debug.Log($"[Hunt] 전투 출발 — {bossId} / {battleDifficulty} → 씬 '{scene}'");
            Departing?.Invoke(bossId, difficulty, scene);
            SceneLoader.Load(scene, loadingScreenPrefab);
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
            return id switch
            {
                "boss.region3" => "Battle_Boss_Test 3",
                "boss.kyodai" => "Battle3",
                "boss.wheel" => "Battle_Boss_Test",
                _ => defaultBattleScene
            };
        }
    }
}
