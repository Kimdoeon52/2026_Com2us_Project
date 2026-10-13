using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step05_DialogueTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueBox dialogueBox;

        [Header("데이터")]
        [SerializeField] private TextAsset poolJson;
        [SerializeField] private TextAsset stringTableJson;
        [SerializeField] private TextAsset variablesJson;

        [Header("디버그 경기 상대")]
        [SerializeField] private string mainOpponent = "boss.wheel";
        [SerializeField] private string otherOpponent = "boss.hammer";

        private readonly DialogueDatabase database = new DialogueDatabase();
        private readonly DialogueSelector selector = new DialogueSelector();
        private readonly DialogueHistory history = new DialogueHistory();
        private DialogueVariables variables;
        private bool ready;

        private void Start()
        {
            if (dialogueBox == null)
            {
                Debug.LogError("[Step05] Dialogue Box가 연결되지 않았습니다.", this);
                return;
            }

            if (!database.Load(poolJson, stringTableJson, variablesJson)) return;

            variables = new DialogueVariables(database.VariableDefs);
            ready = true;

            Debug.Log("[Step05] F1 패배(주 상대) / Shift+F1 패배(다른 상대) / F2 승리 / F3 경매 유찰 / F4 말 걸기 / F5 기록 보기 / F12 전체 초기화");
            LogState("시작");
        }

        private void Update()
        {
            if (!ready || dialogueBox.IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                RecordMatch("lose", keyboard.shiftKey.isPressed ? otherOpponent : mainOpponent);
            }
            else if (keyboard.f2Key.wasPressedThisFrame)
            {
                RecordMatch("win", mainOpponent);
            }
            else if (keyboard.f3Key.wasPressedThisFrame)
            {
                RecordAuction("unsold");
            }
            else if (keyboard.f4Key.wasPressedThisFrame)
            {
                Talk();
            }
            else if (keyboard.f5Key.wasPressedThisFrame)
            {
                Debug.Log("[Step05] 본 대사 기록\n" + history.Dump());
            }
            else if (keyboard.f12Key.wasPressedThisFrame)
            {
                variables.Reset();
                history.Clear();
                LogState("변수·기록 초기화");
            }
        }

        private void RecordMatch(string result, string opponent)
        {
            variables.Set("match.lastResult", result);
            variables.Set("match.lastOpponent", opponent);
            variables.Add("match.count", 1);
            variables.Add(result == "win" ? "match.winCount" : "match.loseCount", 1);
            LogState(result == "win" ? $"승리 ({opponent})" : $"패배 ({opponent})");
        }

        private void RecordAuction(string result)
        {
            variables.Set("auction.lastResult", result);
            variables.Add("auction.settleCount", 1);
            LogState($"경매 정산 ({result})");
        }

        private void Talk()
        {
            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            DialogueData chosen = selector.Select(database.Dialogues, variables, history, now, report);
            Debug.Log("[Step05] 말 걸기\n" + report);

            if (chosen == null) return;

            if (database.TryGetLines(chosen, out List<DialogueLine> lines))
            {
                history.RecordPlay(chosen, variables, now);
                dialogueBox.Open(lines);
            }
        }

        private void LogState(string label)
        {
            Debug.Log($"[Step05] {label} → {variables.Dump()}");
        }
    }
}
