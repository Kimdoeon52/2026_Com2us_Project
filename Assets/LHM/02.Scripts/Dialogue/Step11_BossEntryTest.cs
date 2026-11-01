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
            if (action == NpcAction.HuntApply && !inBattle) StartEntry();
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
