using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueBox dialogueBox;

        [Header("데이터")]
        [SerializeField] private TextAsset[] poolJsons;
        [SerializeField] private TextAsset stringTableJson;
        [SerializeField] private TextAsset variablesJson;

        [Header("세이브")]
        [SerializeField] private string saveFileName = "dialogue_save.json";

        private readonly DialogueDatabase database = new DialogueDatabase();
        private readonly DialogueSelector selector = new DialogueSelector();
        private readonly DialogueHistory history = new DialogueHistory();
        private DialogueVariables variables;
        private DialogueSaveSystem saveSystem;

        public bool IsReady { get; private set; }
        public bool IsTalking => dialogueBox != null && dialogueBox.IsOpen;
        public DialogueBox Box => dialogueBox;
        public DialogueVariables Variables => variables;
        public DialogueHistory History => history;
        public string SavePath => saveSystem != null ? saveSystem.MainPath : "";

        private void Awake()
        {
            if (dialogueBox == null)
            {
                Debug.LogError("[DialogueManager] Dialogue Box가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (!database.Load(poolJsons, stringTableJson, variablesJson)) return;

            variables = new DialogueVariables(database.VariableDefs);
            saveSystem = new DialogueSaveSystem(Path.Combine(Application.persistentDataPath, saveFileName));
            LoadFromFile();
            IsReady = true;
        }

        public bool HasPool(string poolId)
        {
            return database.TryGetPool(poolId, out _);
        }

        public string GetSpeakerName(string speakerId)
        {
            return database.GetSpeakerName(speakerId);
        }

        public string GetText(string key)
        {
            return database.GetString(key);
        }

        public bool Talk(string poolId)
        {
            if (!IsReady || IsTalking) return false;

            if (!database.TryGetPool(poolId, out IReadOnlyList<DialogueData> pool))
            {
                Debug.LogWarning($"[DialogueManager] 대사 풀 '{poolId}'가 없습니다. Pool Jsons 목록과 NPC의 Pool Id를 확인하세요.", this);
                return false;
            }

            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            DialogueData chosen = selector.Select(pool, variables, history, now, report);
            Debug.Log($"[DialogueManager] {poolId} 말 걸기\n{report}");

            if (chosen == null || !database.TryGetLines(chosen, out List<DialogueLine> lines)) return false;

            history.RecordPlay(chosen, variables, now);
            Save();
            dialogueBox.Open(lines, chosen.parsedDisplay, chosen.background);
            return true;
        }

        public bool OpenDirect(string dialogueId)
        {
            if (!IsReady || IsTalking) return false;

            if (!database.TryGetDialogue(dialogueId, out DialogueData data))
            {
                Debug.LogWarning($"[DialogueManager] 대화 '{dialogueId}'가 대사 풀에 없습니다.", this);
                return false;
            }

            if (!database.TryGetLines(data, out List<DialogueLine> lines)) return false;

            Debug.Log($"[DialogueManager] 직접 열기: {dialogueId} ({data.parsedDisplay})");
            dialogueBox.Open(lines, data.parsedDisplay, data.background);
            return true;
        }

        public void Save()
        {
            if (saveSystem == null) return;

            SaveFile data = new SaveFile
            {
                dialogue = history.ToSaveData(),
                variables = variables.ToSaveList(),
            };
            saveSystem.Save(data);
        }

        public void LoadFromFile()
        {
            if (saveSystem == null) return;

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

        public void ResetAll(bool deleteFile)
        {
            if (saveSystem == null) return;

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
                Save();
            }
        }

        public void LogState(string label)
        {
            Debug.Log($"[DialogueManager] {label} → {variables.Dump()}\n{history.Dump()}");
        }
    }
}
