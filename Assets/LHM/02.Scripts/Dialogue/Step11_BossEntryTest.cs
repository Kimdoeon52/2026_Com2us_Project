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

        [Header("시즌 보스")]
        [SerializeField] private string seasonBoss = "boss.wheel";

        private bool inBattle;

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
            Debug.Log("[Step11] B 또는 접수원 '토벌 신청' → 보스 진입 / 전투 중: N 패배, M 승리 / 난이도는 1~5 (Step09)");
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
            DialogueVariables variables = manager.Variables;
            variables.Set("hunt.targetBoss", seasonBoss);
            variables.Set("hunt.targetDifficulty", debug.SelectedDifficulty);
            manager.LogState($"토벌 진입 ({seasonBoss}, {debug.SelectedDifficulty})");

            entry.Play(StartBattle);
        }

        private void StartBattle()
        {
            manager.Variables.Add("hunt.bossAttemptCount", 1);
            manager.Save();

            inBattle = true;
            if (InputModeController.Current != null) InputModeController.Current.SetBaseMode(InputMode.Battle);
            Debug.Log($"[Step11] 전투 시작 — 도전 {manager.Variables.Get("hunt.bossAttemptCount")}회차 (N 패배 / M 승리)");
        }

        private void EndBattle(string result)
        {
            inBattle = false;
            if (InputModeController.Current != null) InputModeController.Current.SetBaseMode(InputMode.Field);

            debug.RecordHunt(result, seasonBoss);
            Debug.Log($"[Step11] 전투 종료 ({result}) → 필드 복귀");
        }
#endif
    }
}
