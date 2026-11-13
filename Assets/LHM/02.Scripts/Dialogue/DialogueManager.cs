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

        [Header("빌드 패킹")]
        [SerializeField] private TextAsset packedData;
        [SerializeField] private bool usePackInEditor;
        [SerializeField] private bool debugForceFallback;

        [Header("세이브")]
        [SerializeField] private string saveFileName = "dialogue_save.json";

        private static readonly Dictionary<string, string> FallbackTexts = new Dictionary<string, string>
        {
            { "menu.hunt_apply", "토벌 신청" },
            { "menu.ranking_board", "랭킹 보드" },
            { "menu.reward_claim", "포상 수령" },
            { "menu.talk", "대화" },
            { "menu.talk_more", "대화하기" },
            { "menu.shop", "상점 열기" },
            { "menu.craft", "제작하기" },
            { "menu.repair", "수리하기" },
            { "menu.exit", "나가기" },
        };

        private const string FallbackLine = "...";

        private readonly DialogueDatabase database = new DialogueDatabase();
        private readonly DialogueSelector selector = new DialogueSelector();
        private readonly DialogueHistory history = new DialogueHistory();
        private DialogueVariables variables;
        private DialogueSaveSystem saveSystem;

        public bool IsReady { get; private set; }
        public bool IsFallback { get; private set; }
        public string DataSource { get; private set; } = "";
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

            dialogueBox.Closed += OnBoxClosed;

            if (!LoadData())
            {
                EnterFallback();
                return;
            }

            variables = new DialogueVariables(database.VariableDefs);
            saveSystem = new DialogueSaveSystem(Path.Combine(Application.persistentDataPath, saveFileName));
            LoadFromFile();
            dialogueBox.SetReadLog(history);
            IsReady = true;
            Debug.Log($"[DialogueManager] 데이터: {DataSource}");
        }

        private bool LoadData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugForceFallback)
            {
                Debug.LogWarning("[DialogueManager] Debug Force Fallback이 켜져 있어 데이터 로드를 건너뜁니다 (폴백 확인용).", this);
                return false;
            }
#endif

            bool usePack = packedData != null && (!Application.isEditor || usePackInEditor);

            if (usePack)
            {
                if (DialoguePack.TryRead(packedData.bytes, out DialoguePackBody body, out string error))
                {
                    if (database.Load(body.pools, body.strings, body.variables, true))
                    {
                        DataSource = $"패킹 데이터 ({packedData.name}, {body.builtAt})";
                        return true;
                    }
                    Debug.LogError("[DialogueManager] 패킹 데이터 내용 오류 — 원본 JSON으로 다시 시도합니다.", this);
                }
                else
                {
                    Debug.LogError($"[DialogueManager] 패킹 데이터 손상 — {error}. 원본 JSON으로 다시 시도합니다.", this);
                }
            }

            if (database.Load(poolJsons, stringTableJson, variablesJson))
            {
                DataSource = usePack ? "원본 JSON (패킹 데이터 실패 → 대체)" : "원본 JSON";
                return true;
            }

            return false;
        }

        private void EnterFallback()
        {
            IsFallback = true;
            DataSource = "기본 대사 폴백 (데이터 로드 실패)";
            variables = new DialogueVariables(new Dictionary<string, VariableDef>());
            history.Clear();
            saveSystem = null;
            IsReady = true;
            Debug.LogError("[DialogueManager] 대사 데이터를 불러오지 못해 기본 대사 폴백으로 동작합니다. 말 걸기는 '...' 한 줄, 이벤트·바크·말풍선은 나오지 않고, 세이브는 읽지도 쓰지도 않습니다.", this);
        }

        private bool PlayFallback(string label)
        {
            Debug.LogWarning($"[DialogueManager] {label} — 기본 대사 폴백", this);
            dialogueBox.Open(new[] { new DialogueLine("", FallbackLine) });
            return true;
        }

        private void OnDestroy()
        {
            if (dialogueBox != null) dialogueBox.Closed -= OnBoxClosed;
        }

        private void OnBoxClosed()
        {
            Save();
        }

        public bool HasPool(string poolId)
        {
            return database.TryGetPool(poolId, out _);
        }

        public string GetSpeakerName(string speakerId)
        {
            return IsFallback ? "" : database.GetSpeakerName(speakerId);
        }

        public string GetText(string key)
        {
            if (IsFallback) return FallbackTexts.TryGetValue(key ?? "", out string text) ? text : "";
            return database.GetString(key);
        }

        public bool Talk(string poolId)
        {
            if (!IsReady || IsTalking) return false;
            if (IsFallback) return PlayFallback($"{poolId} 말 걸기");

            if (!database.TryGetPool(poolId, out IReadOnlyList<DialogueData> pool))
            {
                Debug.LogWarning($"[DialogueManager] 대사 풀 '{poolId}'가 없습니다. Pool Jsons 목록과 NPC의 Pool Id를 확인하세요.", this);
                return false;
            }

            return PlayFrom(pool, $"{poolId} 말 걸기");
        }

        public bool CanPlay(string dialogueId, StringBuilder report)
        {
            if (!IsReady || !database.TryGetDialogue(dialogueId, out DialogueData data)) return false;

            return selector.Select(new[] { data }, variables, history, Time.unscaledTime, report) != null;
        }

        public bool PlayDialogue(string dialogueId)
        {
            if (!IsReady || IsTalking) return false;
            if (IsFallback) return false;

            if (!database.TryGetDialogue(dialogueId, out DialogueData data))
            {
                Debug.LogWarning($"[DialogueManager] 대화 '{dialogueId}'가 대사 풀에 없습니다.", this);
                return false;
            }

            return PlayFrom(new[] { data }, $"{dialogueId} 재생");
        }

        public bool TryBark(string poolId, out DialogueLine line)
        {
            return TryPickLine(poolId, "바크", out line);
        }

        public bool TryPickLine(string poolId, string label, out DialogueLine line, bool verbose = true)
        {
            line = default;
            if (!IsReady || IsFallback) return false;

            if (!database.TryGetPool(poolId, out IReadOnlyList<DialogueData> pool))
            {
                Debug.LogWarning($"[DialogueManager] {label} 풀 '{poolId}'가 없습니다. Pool Jsons 목록을 확인하세요.", this);
                return false;
            }

            return PickLine(pool, $"{label} {poolId}", false, verbose, out line, out _);
        }

        public bool TryPlayBest(IReadOnlyList<string> dialogueIds, string label, out DialogueLine line, out string chosenId)
        {
            line = default;
            chosenId = null;
            if (!IsReady || IsFallback || dialogueIds == null) return false;

            List<DialogueData> candidates = new List<DialogueData>();
            foreach (string id in dialogueIds)
            {
                if (database.TryGetDialogue(id, out DialogueData data)) candidates.Add(data);
                else Debug.LogWarning($"[DialogueManager] 대화 '{id}'가 대사 풀에 없습니다.", this);
            }

            if (candidates.Count == 0) return false;

            if (!PickLine(candidates, $"{label} [{string.Join(", ", dialogueIds)}]", true, true, out line, out DialogueData chosen)) return false;

            chosenId = chosen.id;
            return true;
        }

        public bool TryPlayLines(string dialogueId, string label, out List<DialogueLine> lines)
        {
            lines = null;
            if (!IsReady || IsFallback) return false;

            if (!database.TryGetDialogue(dialogueId, out DialogueData data))
            {
                Debug.LogWarning($"[DialogueManager] 대화 '{dialogueId}'가 대사 풀에 없습니다.", this);
                return false;
            }

            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            if (selector.Select(new[] { data }, variables, history, now, report) == null || !database.TryGetLines(data, out lines)) return false;

            history.RecordPlay(data, variables, now);
            Save();
            Debug.Log($"[DialogueManager] {label} {dialogueId} ({lines.Count}줄, 저장)\n{report}");
            return true;
        }

        public bool PlayDialogueIfAvailable(string dialogueId)
        {
            if (!IsReady || IsFallback || IsTalking || !CanPlay(dialogueId, null)) return false;
            return PlayDialogue(dialogueId);
        }

        public bool TryGetFirstLine(string dialogueId, out DialogueLine line)
        {
            line = default;
            if (!database.TryGetDialogue(dialogueId, out DialogueData data) || !database.TryGetLines(data, out List<DialogueLine> lines) || lines.Count == 0) return false;

            line = lines[0];
            return true;
        }

        private bool PickLine(IReadOnlyList<DialogueData> candidates, string label, bool save, bool verbose, out DialogueLine line, out DialogueData chosen)
        {
            line = default;
            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            chosen = selector.Select(candidates, variables, history, now, report);

            if (chosen == null || !database.TryGetLines(chosen, out List<DialogueLine> lines) || lines.Count == 0)
            {
                Debug.Log($"[DialogueManager] {label} — 고를 대사 없음\n{report}");
                return false;
            }

            history.RecordPlay(chosen, variables, now);
            if (save) Save();
            line = lines[0];
            if (verbose) Debug.Log($"[DialogueManager] {label} → {chosen.id}{(lines.Count > 1 ? $" (한 줄 출력: {lines.Count}줄 중 첫 줄만)" : "")}{(save ? " (저장)" : "")}\n{report}");
            return true;
        }

        public bool HasDialogue(string dialogueId)
        {
            return database.TryGetDialogue(dialogueId, out _);
        }

        private bool PlayFrom(IReadOnlyList<DialogueData> candidates, string label)
        {
            float now = Time.unscaledTime;
            StringBuilder report = new StringBuilder();
            DialogueData chosen = selector.Select(candidates, variables, history, now, report);
            Debug.Log($"[DialogueManager] {label}\n{report}");

            if (chosen == null || !database.TryGetLines(chosen, out List<DialogueLine> lines)) return false;

            history.RecordPlay(chosen, variables, now);
            Save();
            dialogueBox.Open(lines, chosen.parsedDisplay, chosen.background);
            return true;
        }

        public bool OpenDirect(string dialogueId)
        {
            if (!IsReady || IsTalking) return false;
            if (IsFallback) return false;

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
