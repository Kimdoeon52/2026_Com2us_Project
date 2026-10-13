using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step06_DialogueTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueBox dialogueBox;

        [Header("데이터")]
        [SerializeField] private TextAsset poolJson;
        [SerializeField] private TextAsset stringTableJson;
        [SerializeField] private TextAsset variablesJson;

        [Header("세이브")]
        [SerializeField] private string saveFileName = "dialogue_save.json";

        [Header("디버그 경기 상대")]
        [SerializeField] private string mainOpponent = "boss.wheel";
        [SerializeField] private string otherOpponent = "boss.hammer";

        private readonly DialogueDatabase database = new DialogueDatabase();
        private readonly DialogueSelector selector = new DialogueSelector();
        private readonly DialogueHistory history = new DialogueHistory();
        private DialogueVariables variables;
        private DialogueSaveSystem saveSystem;
        private bool ready;

        private void Start()
        {
            if (dialogueBox == null)
            {
                Debug.LogError("[Step06] Dialogue Box가 연결되지 않았습니다.", this);
                return;
            }

            if (!database.Load(poolJson, stringTableJson, variablesJson)) return;

            variables = new DialogueVariables(database.VariableDefs);
            saveSystem = new DialogueSaveSystem(Path.Combine(Application.persistentDataPath, saveFileName));
            LoadFromFile();
            ready = true;

            Debug.Log("[Step06] F1 패배 / Shift+F1 다른 상대 패배 / F2 승리 / F3 유찰 / F4 말 걸기 / F5 기록 / F6 저장 / F7 파일에서 다시 읽기 / F8 세이브 폴더 열기 / F12 초기화 후 저장 / Shift+F12 세이브 삭제");
        }

        private void Update()
        {
            if (!ready || dialogueBox.IsOpen) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.f1Key.wasPressedThisFrame) RecordMatch("lose", shift ? otherOpponent : mainOpponent);
            else if (keyboard.f2Key.wasPressedThisFrame) RecordMatch("win", mainOpponent);
            else if (keyboard.f3Key.wasPressedThisFrame) RecordAuction("unsold");
            else if (keyboard.f4Key.wasPressedThisFrame) Talk();
            else if (keyboard.f5Key.wasPressedThisFrame) Debug.Log("[Step06] 본 대사 기록\n" + history.Dump());
            else if (keyboard.f6Key.wasPressedThisFrame) SaveToFile();
            else if (keyboard.f7Key.wasPressedThisFrame) LoadFromFile();
            else if (keyboard.f8Key.wasPressedThisFrame) OpenSaveFolder();
            else if (keyboard.f12Key.wasPressedThisFrame) ResetAll(shift);
        }

        private void RecordMatch(string result, string opponent)
        {
            variables.Set("match.lastResult", result);
            variables.Set("match.lastOpponent", opponent);
            variables.Add("match.count", 1);
            variables.Add(result == "win" ? "match.winCount" : "match.loseCount", 1);
            LogState(result == "win" ? $"승리 ({opponent})" : $"패배 ({opponent})");
            SaveToFile();
        }

        private void RecordAuction(string result)
        {
            variables.Set("auction.lastResult", result);
            variables.Add("auction.settleCount", 1);
            LogState($"경매 정산 ({result})");
            SaveToFile();
        }

        private void Talk()
        {
            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            DialogueData chosen = selector.Select(database.Dialogues, variables, history, now, report);
            Debug.Log("[Step06] 말 걸기\n" + report);

            if (chosen == null) return;

            if (database.TryGetLines(chosen, out List<DialogueLine> lines))
            {
                history.RecordPlay(chosen, variables, now);
                SaveToFile();
                dialogueBox.Open(lines);
            }
        }

        private void SaveToFile()
        {
            SaveFile data = new SaveFile
            {
                dialogue = history.ToSaveData(),
                variables = variables.ToSaveList(),
            };
            saveSystem.Save(data);
        }

        private void LoadFromFile()
        {
            if (saveSystem.TryLoad(out SaveFile data))
            {
                history.LoadFrom(data.dialogue);
                variables.LoadFrom(data.variables);
                history.MigrateIds(database.Dialogues);
            }
            else
            {
                history.Clear();
                variables.Reset();
            }

            LogState("불러오기");
        }

        private void ResetAll(bool deleteFile)
        {
            history.Clear();
            variables.Reset();

            if (deleteFile)
            {
                saveSystem.DeleteAll();
                LogState("세이브 삭제 + 초기화");
            }
            else
            {
                LogState("초기화");
                SaveToFile();
            }
        }

        private void OpenSaveFolder()
        {
            string folder = Path.GetDirectoryName(saveSystem.MainPath);
            Debug.Log($"[Step06] 세이브 위치: {saveSystem.MainPath}");
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }

        private void LogState(string label)
        {
            Debug.Log($"[Step06] {label} → {variables.Dump()}\n{history.Dump()}");
        }
    }
}
