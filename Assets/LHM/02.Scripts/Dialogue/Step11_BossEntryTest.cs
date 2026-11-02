using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step11_BossEntryTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BossEntryDialogue entry;
        [SerializeField] private Step09_DialogueDebug debug;
        [SerializeField] private NpcChoiceMenu menu;

        [Header("선택 연결 (v6)")]
        [SerializeField] private BattleBarkPlayer barks;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private string battleTutorialId = "tutorial_t10_battle";

        [Header("보스 목록(BossRaceSelectUI) · 난이도 선택 UI (비우면 1~5키 난이도로 바로 진입)")]
        [SerializeField] private RealSteel.BossRace.BossRaceSelectUI bossSelect;
        [SerializeField] private RealSteel.BossRace.BossDifficultySelectUI difficultySelect;

        private bool inBattle;
        private string battleBoss;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null || entry == null || debug == null)
            {
                Debug.LogError("[Step11] Manager, Entry, Debug 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            if (menu != null) menu.ActionChosen += OnActionChosen;
            Debug.Log("[Step11] B 또는 접수원 '토벌 신청' → 보스 진입 / 전투 중: N 패배, M 승리 / 난이도 1~5, 대상 보스 G (Step09)");
        }

        private void OnDestroy()
        {
            if (menu != null) menu.ActionChosen -= OnActionChosen;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady || manager.IsTalking || entry.IsPlaying) return;
            if (bossSelect != null && bossSelect.IsOpen) return;
            if (difficultySelect != null && difficultySelect.IsOpen) return;

            if (inBattle)
            {
                if (keyboard.nKey.wasPressedThisFrame) EndBattle("lose");
                else if (keyboard.mKey.wasPressedThisFrame) EndBattle("win");
            }
            else if (keyboard.bKey.wasPressedThisFrame && (menu == null || !menu.IsBusy))
            {
                StartEntry();
            }
        }

        private void OnActionChosen(NpcInteractable npc, NpcAction action)
        {
            if (action != NpcAction.HuntApply || inBattle) return;

            var departure = FindFirstObjectByType<RealSteel.BossRace.HuntDeparture>();
            if (departure != null && departure.isActiveAndEnabled) return;

            string seasonBoss = debug.TargetBoss;
            RealSteel.BossRace.BossRaceEntry target = FindEntry(seasonBoss);

            if (difficultySelect != null && difficultySelect.isActiveAndEnabled && target != null &&
                difficultySelect.Open(debug, target, debug, debug.SelectedDifficulty, OnDifficultyChosen, OnHuntCancelled, "길드 접수처"))
            {
                Debug.Log($"[Step11] 토벌 신청 → 이번 시즌 보스 {target.bossName}({seasonBoss}) 난이도 선택");
                return;
            }

            Debug.Log($"[Step11] 난이도 선택 UI 없음{(target == null ? $" (보스 목록에 '{seasonBoss}' 없음)" : "")} → 현재 난이도 {debug.SelectedDifficulty}(1~5키)로 진입");
            StartEntry();
        }

        private RealSteel.BossRace.BossRaceEntry FindEntry(string bossId)
        {
            if (bossSelect == null) return null;
            foreach (RealSteel.BossRace.BossRaceEntry item in bossSelect.Entries)
            {
                if (item != null && item.bossId == bossId) return item;
            }
            return null;
        }

        private void OnHuntCancelled()
        {
            Debug.Log("[Step11] 토벌 신청 취소 → 필드");
        }

        private void OnDifficultyChosen(string difficulty)
        {
            debug.SetDifficulty(difficulty);
            StartEntry();
        }

        private void StartEntry()
        {
            battleBoss = debug.TargetBoss;
            entry.Play(battleBoss, debug.SelectedDifficulty, debug.AttemptsOf(battleBoss), debug.LosesOf(battleBoss), StartBattle);
        }

        private void StartBattle()
        {
            debug.AddAttempt(battleBoss);
            manager.Save();

            inBattle = true;
            if (InputModeController.Current != null) InputModeController.Current.SetBaseMode(InputMode.Battle);
            Debug.Log($"[Step11] 전투 시작 — {battleBoss} 도전 {debug.AttemptsOf(battleBoss)}회차 (N 패배 / M 승리)");

            if (barks != null)
            {
                barks.ResetBattle();
                if (!string.IsNullOrEmpty(battleTutorialId) && manager.CanPlay(battleTutorialId, null)) barks.PlaySubtitleDialogue(battleTutorialId);
            }
        }

        private void EndBattle(string result)
        {
            inBattle = false;
            if (InputModeController.Current != null) InputModeController.Current.SetBaseMode(InputMode.Field);

            debug.RecordHunt(result, battleBoss);
            Debug.Log($"[Step11] 전투 종료 ({result}) → 필드 복귀");

            if (queue != null) queue.Evaluate("귀가");
        }
#endif
    }
}
