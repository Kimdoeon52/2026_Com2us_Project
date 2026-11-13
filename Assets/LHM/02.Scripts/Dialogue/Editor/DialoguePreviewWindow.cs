using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Dialogue.EditorTools
{
    // Play 없이 변수 · 기록을 바꿔 가며 "이 풀에 말 걸면 / 오늘 00시에 어떤 대사가 나오는지"와 이유를 보는 창
    // 판정은 게임과 같은 DialogueSelector 규칙(조건 → 소비 규칙 → 우선순위 비교)을 그대로 씀
    public sealed class DialoguePreviewWindow : EditorWindow
    {
        private const string WindowTitle = "대사 미리보기";
        private const float VariablePaneWidth = 330f;
        private const int TalkLogLimit = 30;
        internal const string PresetPath = DialogueDataTools.DataFolder + "/Preview/preview_presets.json";

        private static readonly Color ChosenColor = new Color(0.45f, 0.85f, 0.45f);
        private static readonly Color PassedColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color FailedColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color BlockedColor = new Color(0.9f, 0.65f, 0.3f);
        private static readonly Color ChangedColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f);
        private static readonly string[] ModeLabels = { "풀에 말 걸기", "00시 이벤트" };

        private enum Mode
        {
            Pool,
            Midnight,
        }

        private sealed class Row
        {
            public DialogueData Data;
            public string Failed;
            public string Blocked;
            public int Rank;
            public bool Passed => Failed == null && Blocked == null;
        }

        private sealed class EventRow
        {
            public DialogueTriggerData Trigger;
            public DialogueData Data;
            public string Failed;
            public string Blocked;
            public bool Loaded => Failed == null && Blocked == null;
        }

        [Serializable]
        internal class PresetFile
        {
            public List<Preset> presets = new List<Preset>();
        }

        [Serializable]
        internal class Preset
        {
            public string name;
            public List<VariableValue> values = new List<VariableValue>();
        }

        // 도메인 리로드 · 창 다시 열기에도 남도록 직렬화
        [SerializeField] private Mode mode;
        [SerializeField] private string poolId;
        [SerializeField] private bool onlyUsedVariables = true;
        [SerializeField] private string variableFilter = "";
        [SerializeField] private float simTime;
        [SerializeField] private List<VariableValue> savedValues = new List<VariableValue>();
        [SerializeField] private DialogueSaveData savedHistory;
        [SerializeField] private List<string> talkLog = new List<string>();
        [SerializeField] private int presetIndex;
        [SerializeField] private string presetName = "";

        [NonSerialized] private DialogueDatabase database;
        [NonSerialized] private DialogueVariables variables;
        [NonSerialized] private DialogueHistory history;
        [NonSerialized] private string dataSignature;
        [NonSerialized] private string[] poolIds = Array.Empty<string>();
        [NonSerialized] private List<Row> rows = new List<Row>();
        [NonSerialized] private readonly List<DialogueTriggerData> triggers = new List<DialogueTriggerData>();
        [NonSerialized] private readonly List<EventRow> eventRows = new List<EventRow>();
        [NonSerialized] private PresetFile presets = new PresetFile();
        [NonSerialized] private int tieCount;
        [NonSerialized] private int errorCount;
        [NonSerialized] private string firstError;
        [NonSerialized] private readonly HashSet<string> usedVariables = new HashSet<string>();
        [NonSerialized] private string expandedId;
        [NonSerialized] private GUIStyle wrapStyle;
        [NonSerialized] private GUIStyle boldStyle;

        private Vector2 variableScroll;
        private Vector2 resultScroll;

        [MenuItem("Tools/Dialogue/대사 미리보기 (조건 시뮬레이터)", priority = 2)]
        internal static void Open()
        {
            var window = GetWindow<DialoguePreviewWindow>(WindowTitle);
            window.minSize = new Vector2(820f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            Reload();
        }

        // JSON 파일이 실제로 바뀌었을 때만 다시 읽음 (값 · 기록은 유지)
        private void OnFocus()
        {
            if (DataSignature() != dataSignature) Reload();
        }

        // ── 데이터 읽기 ─────────────────────────────

        // 최상위 JSON 파일들의 경로 · 수정 시각 · 크기 — 하나라도 바뀌면 다시 읽음
        private static string DataSignature()
        {
            if (!Directory.Exists(DialogueDataTools.DataFolder)) return "";

            var builder = new StringBuilder();
            foreach (string path in Directory.GetFiles(DialogueDataTools.DataFolder, "*.json", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.Ordinal))
            {
                var info = new FileInfo(path);
                builder.Append(path).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('|').Append(info.Length).Append(';');
            }
            return builder.ToString();
        }

        private void Reload()
        {
            dataSignature = DataSignature();
            DialogueCheckResult result = DialogueDataTools.Check();
            errorCount = result.Errors.Count;
            firstError = errorCount > 0 ? result.Errors[0] : null;
            database = result.Database;
            LoadPresets();

            if (database == null || database.VariableDefs.Count == 0)
            {
                variables = null;
                history = null;
                poolIds = Array.Empty<string>();
                rows.Clear();
                triggers.Clear();
                eventRows.Clear();
                return;
            }

            variables = new DialogueVariables(database.VariableDefs);
            history = new DialogueHistory();
            RestoreState();
            LoadTriggers();

            poolIds = database.PoolIds.OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (string.IsNullOrEmpty(poolId) || Array.IndexOf(poolIds, poolId) < 0) poolId = poolIds.FirstOrDefault();
            Evaluate();
        }

        // 트리거 오류는 데이터 검사(Check)가 이미 경고하므로 여기선 통과한 것만 씀
        private void LoadTriggers()
        {
            triggers.Clear();
            string path = DialogueDataTools.DataFolder + "/" + DialogueDataTools.TriggersFile;
            if (!File.Exists(path)) return;

            triggers.AddRange(DialogueTriggerTable.Validate(
                DialogueDataTools.TriggersFile,
                File.ReadAllText(path, Encoding.UTF8),
                id => database.TryGetDialogue(id, out _),
                id => database.TryGetDialogue(id, out DialogueData data) && data.lines.Count > 0 ? data.lines[0].speaker : null,
                new List<string>()));
        }

        private void RestoreState()
        {
            ApplyValues(savedValues);
            if (savedHistory != null) history.LoadFrom(savedHistory);
        }

        // 변수 목록에서 빠졌거나 허용값이 바뀐 값은 조용히 건너뜀 (DialogueVariables.LoadFrom은 콘솔 경고를 냄)
        private void ApplyValues(List<VariableValue> values)
        {
            if (values == null) return;

            foreach (VariableValue item in values)
            {
                if (item == null || !database.VariableDefs.TryGetValue(item.name, out VariableDef def)) continue;
                if (DialogueConditions.AcceptsValue(def, item.value ?? "", out _)) variables.Set(item.name, item.value ?? "");
            }
        }

        private void SaveState()
        {
            savedValues = variables.ToSaveList();
            savedHistory = history.ToSaveData();
        }

        // ── 판정 ─────────────────────────────

        private void Evaluate()
        {
            usedVariables.Clear();
            EvaluatePool();
            EvaluateEvents();
        }

        private void CollectUsed(DialogueData dialogue)
        {
            if (dialogue.conditions != null)
            {
                foreach (ConditionData condition in dialogue.conditions) usedVariables.Add(condition.variable);
            }
            if (!string.IsNullOrEmpty(dialogue.perEvent)) usedVariables.Add(dialogue.perEvent);
        }

        private void EvaluatePool()
        {
            rows.Clear();
            tieCount = 0;
            if (database == null || !database.TryGetPool(poolId, out IReadOnlyList<DialogueData> pool)) return;

            foreach (DialogueData dialogue in pool)
            {
                if (mode == Mode.Pool) CollectUsed(dialogue);

                var row = new Row { Data = dialogue, Failed = DialogueSelector.FindFailedCondition(dialogue, variables) };
                if (row.Failed == null) row.Blocked = history.GetBlockReason(dialogue, variables, simTime);
                rows.Add(row);
            }

            List<Row> passed = rows.Where(r => r.Passed).ToList();
            passed.Sort((a, b) => DialogueSelector.Compare(a.Data, b.Data, history));
            for (int i = 0; i < passed.Count; i++) passed[i].Rank = i + 1;

            if (passed.Count > 0)
            {
                tieCount = 1;
                while (tieCount < passed.Count && DialogueSelector.Compare(passed[0].Data, passed[tieCount].Data, history) == 0) tieCount++;
            }

            rows = passed
                .Concat(rows.Where(r => r.Failed == null && r.Blocked != null))
                .Concat(rows.Where(r => r.Failed != null))
                .ToList();
        }

        // DialogueEventQueue.Evaluate와 같은 규칙: 트리거마다 조건 + 소비 규칙을 통과하면 적재
        private void EvaluateEvents()
        {
            eventRows.Clear();
            if (database == null) return;

            foreach (DialogueTriggerData trigger in triggers)
            {
                if (!database.TryGetDialogue(trigger.dialogueId, out DialogueData dialogue)) continue;
                if (mode == Mode.Midnight) CollectUsed(dialogue);

                var row = new EventRow { Trigger = trigger, Data = dialogue, Failed = DialogueSelector.FindFailedCondition(dialogue, variables) };
                if (row.Failed == null) row.Blocked = history.GetBlockReason(dialogue, variables, simTime);
                eventRows.Add(row);
            }
        }

        // 대기열 순서: 스토리 → 시스템 → 말풍선, 같은 종류는 트리거 파일 순서 (OrderBy는 안정 정렬)
        private List<EventRow> QueueOrder()
        {
            return eventRows.Where(r => r.Loaded).OrderBy(r => r.Trigger.parsedKind).ToList();
        }

        // ── 화면 ─────────────────────────────

        private void OnGUI()
        {
            wrapStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            boldStyle ??= new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };

            DrawToolbar();

            if (database == null || variables == null)
            {
                EditorGUILayout.HelpBox($"대사 데이터를 읽지 못했습니다.\n{firstError ?? DialogueDataTools.DataFolder}", MessageType.Error);
                return;
            }

            if (errorCount > 0)
            {
                EditorGUILayout.HelpBox($"데이터 오류 {errorCount}건 — 오류가 난 대사는 목록에서 빠졌을 수 있습니다. (대화 툴 › 데이터 검사)\n첫 오류: {firstError}", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            DrawVariablePane();
            DrawResultPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("데이터 다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(100f))) Reload();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var content = new GUIContent("게임 값 가져오기", "Play 중인 DialogueManager의 변수 · 기록을 복사합니다 (쿨다운 시각은 복사되지 않음)");
                if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(100f))) CopyFromGame();
            }

            using (new EditorGUI.DisabledScope(variables == null))
            {
                if (GUILayout.Button("변수 기본값으로", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    variables.Reset();
                    Changed();
                }

                if (GUILayout.Button("본 기록 지우기", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    history.Clear();
                    talkLog.Clear();
                    simTime = 0f;
                    Changed();
                }
            }

            GUILayout.FlexibleSpace();

            if (history != null)
            {
                GUILayout.Label("시뮬 시각", GUILayout.Width(56f));
                float newTime = EditorGUILayout.FloatField(simTime, EditorStyles.toolbarTextField, GUILayout.Width(56f));
                if (GUILayout.Button("+60초", EditorStyles.toolbarButton, GUILayout.Width(48f))) newTime += 60f;
                if (!Mathf.Approximately(newTime, simTime))
                {
                    simTime = Mathf.Max(0f, newTime);
                    Evaluate();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CopyFromGame()
        {
            DialogueManager manager = FindAnyObjectByType<DialogueManager>();
            if (manager == null || !manager.IsReady || manager.Variables == null)
            {
                ShowNotification(new GUIContent("Play 중인 DialogueManager가 없거나 준비 전입니다"));
                return;
            }

            // 게임 쪽 데이터(패킹 파일일 수 있음)와 변수 목록이 다를 수 있어 아는 변수만 받음
            savedValues = manager.Variables.ToSaveList();
            savedHistory = manager.History.ToSaveData();
            variables.Reset();
            history.Clear();
            RestoreState();
            simTime = 0f;
            Changed();
            ShowNotification(new GUIContent("게임 값을 가져왔습니다"));
        }

        private void Changed()
        {
            SaveState();
            Evaluate();
            Repaint();
        }

        // ── 왼쪽: 변수 ─────────────────────────────

        private void DrawVariablePane()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(VariablePaneWidth));

            DrawPresets();
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("변수", EditorStyles.boldLabel);
            variableFilter = EditorGUILayout.TextField("검색", variableFilter);
            string usedLabel = mode == Mode.Pool ? "이 풀의 조건에 쓰인 변수만" : "00시 트리거 조건에 쓰인 변수만";
            onlyUsedVariables = EditorGUILayout.ToggleLeft(usedLabel, onlyUsedVariables);

            variableScroll = EditorGUILayout.BeginScrollView(variableScroll);
            int shown = 0;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 170f;

            foreach (KeyValuePair<string, VariableDef> pair in database.VariableDefs)
            {
                if (onlyUsedVariables && !usedVariables.Contains(pair.Key)) continue;
                if (!string.IsNullOrEmpty(variableFilter) && pair.Key.IndexOf(variableFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                DrawVariable(pair.Value);
                shown++;
            }

            EditorGUIUtility.labelWidth = labelWidth;
            if (shown == 0) EditorGUILayout.LabelField(onlyUsedVariables ? "조건에 쓰는 변수가 없습니다" : "맞는 변수가 없습니다", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawVariable(VariableDef def)
        {
            string current = variables.Get(def.name) ?? "";
            bool changed = current != (def.defaultValue ?? "");

            EditorGUILayout.BeginHorizontal();
            Color previous = GUI.contentColor;
            if (changed) GUI.contentColor = ChangedColor;

            string next = current;
            var label = new GUIContent(def.name, $"{def.type} · 기본값 {Show(def.defaultValue)}");
            switch (def.parsedType)
            {
                case VariableType.Enum:
                    int index = def.allowed.IndexOf(current);
                    int picked = EditorGUILayout.Popup(label, index, def.allowed.ToArray());
                    if (picked != index && picked >= 0) next = def.allowed[picked];
                    break;
                case VariableType.Int:
                    int.TryParse(current, out int number);
                    next = EditorGUILayout.IntField(label, number).ToString();
                    break;
                default:
                    next = EditorGUILayout.TextField(label, current);
                    break;
            }

            GUI.contentColor = previous;

            using (new EditorGUI.DisabledScope(!changed))
            {
                if (GUILayout.Button(new GUIContent("↺", "기본값으로"), EditorStyles.miniButton, GUILayout.Width(22f))) next = def.defaultValue ?? "";
            }

            EditorGUILayout.EndHorizontal();

            if (next != current && variables.Set(def.name, next)) Changed();
        }

        // ── 상황 저장 (변수 값 묶음) ─────────────────────────────

        private void DrawPresets()
        {
            EditorGUILayout.LabelField(new GUIContent("저장된 상황", $"기본값과 다른 변수만 저장합니다. 팀 공용 파일: {PresetPath}"), EditorStyles.boldLabel);

            bool hasAny = presets.presets.Count > 0;
            string[] names = hasAny ? presets.presets.Select(p => p.name).ToArray() : new[] { "(저장된 상황 없음)" };
            presetIndex = Mathf.Clamp(presetIndex, 0, names.Length - 1);

            EditorGUILayout.BeginHorizontal();
            presetIndex = EditorGUILayout.Popup(presetIndex, names);
            using (new EditorGUI.DisabledScope(!hasAny))
            {
                if (GUILayout.Button(new GUIContent("불러오기", "변수를 기본값으로 되돌린 뒤 이 상황의 값을 넣습니다 (본 기록은 그대로)"), EditorStyles.miniButtonLeft, GUILayout.Width(58f)))
                {
                    LoadPreset(presets.presets[presetIndex]);
                }
                if (GUILayout.Button("삭제", EditorStyles.miniButtonRight, GUILayout.Width(36f))) DeletePreset(presetIndex);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            presetName = EditorGUILayout.TextField(presetName);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(presetName)))
            {
                if (GUILayout.Button(new GUIContent("지금 값 저장", "지금 변수 값을 이 이름으로 저장합니다 (같은 이름이면 덮어씀)"), EditorStyles.miniButton, GUILayout.Width(98f)))
                {
                    SavePreset(presetName.Trim());
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void LoadPresets()
        {
            presets = new PresetFile();
            if (!File.Exists(PresetPath)) return;

            try
            {
                presets = JsonUtility.FromJson<PresetFile>(File.ReadAllText(PresetPath, Encoding.UTF8)) ?? new PresetFile();
                presets.presets ??= new List<Preset>();
                presets.presets.RemoveAll(p => p == null || string.IsNullOrEmpty(p.name));
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"[대사 미리보기] {PresetPath} 읽기 실패 — {e.Message}");
            }
        }

        private void LoadPreset(Preset preset)
        {
            variables.Reset();
            ApplyValues(preset.values);

            int skipped = preset.values.Count(v => v == null || !database.VariableDefs.ContainsKey(v.name) || variables.Get(v.name) != (v.value ?? ""));
            presetName = preset.name;
            Changed();
            ShowNotification(new GUIContent(skipped > 0 ? $"'{preset.name}' 불러옴 — 변수 목록과 맞지 않는 값 {skipped}개는 건너뜀" : $"'{preset.name}' 불러옴"));
        }

        private void SavePreset(string name)
        {
            int existing = presets.presets.FindIndex(p => p.name == name);
            if (existing >= 0 && !EditorUtility.DisplayDialog(WindowTitle, $"'{name}' 상황이 이미 있습니다. 지금 값으로 덮어쓸까요?", "덮어쓰기", "취소")) return;

            var preset = new Preset
            {
                name = name,
                values = variables.ToSaveList()
                    .Where(v => database.VariableDefs.TryGetValue(v.name, out VariableDef def) && v.value != (def.defaultValue ?? ""))
                    .ToList(),
            };

            if (existing >= 0) presets.presets[existing] = preset;
            else presets.presets.Add(preset);

            presets.presets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            presetIndex = presets.presets.IndexOf(preset);
            WritePresets();
            ShowNotification(new GUIContent($"'{name}' 저장 (바뀐 변수 {preset.values.Count}개)"));
        }

        private void DeletePreset(int index)
        {
            string name = presets.presets[index].name;
            if (!EditorUtility.DisplayDialog(WindowTitle, $"'{name}' 상황을 지울까요?\n(팀 공용 파일에서 지워집니다)", "지우기", "취소")) return;

            presets.presets.RemoveAt(index);
            presetIndex = Mathf.Max(0, index - 1);
            WritePresets();
        }

        private void WritePresets()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PresetPath));
            File.WriteAllText(PresetPath, JsonUtility.ToJson(presets, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(PresetPath);
        }

        // ── 오른쪽: 결과 ─────────────────────────────

        private void DrawResultPane()
        {
            EditorGUILayout.BeginVertical();

            var newMode = (Mode)GUILayout.Toolbar((int)mode, ModeLabels);
            if (newMode != mode)
            {
                mode = newMode;
                expandedId = null;
                Evaluate();
            }

            if (mode == Mode.Pool) DrawPoolPane();
            else DrawMidnightPane();

            EditorGUILayout.EndVertical();
        }

        private void DrawPoolPane()
        {
            int poolIndex = Mathf.Max(0, Array.IndexOf(poolIds, poolId));
            int newPool = EditorGUILayout.Popup("대사 풀 (말 거는 대상)", poolIndex, poolIds);
            if (newPool != poolIndex)
            {
                poolId = poolIds[newPool];
                expandedId = null;
                Evaluate();
            }

            DrawVerdict();
            EditorGUILayout.Space(4f);

            resultScroll = EditorGUILayout.BeginScrollView(resultScroll);
            foreach (Row row in rows) DrawRow(row);
            DrawTalkLog();
            EditorGUILayout.EndScrollView();
        }

        private void DrawVerdict()
        {
            int passedCount = rows.Count(r => r.Passed);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (passedCount == 0)
            {
                GUI.contentColor = ErrorColor;
                EditorGUILayout.LabelField("나올 대사 없음 — 통과한 후보가 없습니다 (게임에서는 말을 걸어도 아무 일도 없음)", boldStyle);
                GUI.contentColor = Color.white;
            }
            else
            {
                DialogueData chosen = rows[0].Data;
                DialogueData runnerUp = tieCount < passedCount ? rows[tieCount].Data : null;
                string pick = tieCount > 1 ? $"{tieCount}개 중 무작위: {string.Join(", ", rows.Take(tieCount).Select(r => r.Data.id))}" : chosen.id;

                GUI.contentColor = ChosenColor;
                EditorGUILayout.LabelField($"나올 대사: {pick}", boldStyle);
                GUI.contentColor = Color.white;
                EditorGUILayout.LabelField($"이유: {DialogueSelector.Explain(chosen, runnerUp, passedCount, tieCount, history)}", wrapStyle);
                if (chosen.lines.Count > 0) EditorGUILayout.LabelField($"첫 줄: {LineText(chosen.lines[0])}", wrapStyle);
            }

            EditorGUILayout.LabelField($"후보 {rows.Count}개 · 통과 {passedCount} · 소비됨 {rows.Count(r => r.Failed == null && r.Blocked != null)} · 조건 불충족 {rows.Count(r => r.Failed != null)}", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(passedCount == 0))
            {
                var talk = new GUIContent("말 걸기 (나온 대사를 본 것으로 기록)", "게임처럼 선택된 대사를 소비합니다. 여러 번 눌러 대사가 어떻게 돌아가는지 확인하세요.");
                if (GUILayout.Button(talk, GUILayout.Height(24f))) Talk();
            }

            EditorGUILayout.EndVertical();
        }

        private void Talk()
        {
            DialogueData chosen = rows[UnityEngine.Random.Range(0, tieCount)].Data;
            history.RecordPlay(chosen, variables, simTime);

            string first = chosen.lines.Count > 0 ? LineText(chosen.lines[0]) : "";
            AddLog($"{chosen.id} — {first}");
            Changed();
        }

        private void AddLog(string entry)
        {
            talkLog.Insert(0, entry);
            if (talkLog.Count > TalkLogLimit) talkLog.RemoveAt(talkLog.Count - 1);
        }

        private void DrawRow(Row row)
        {
            DialogueData d = row.Data;
            bool chosenTier = row.Passed && row.Rank <= tieCount;
            Color color = chosenTier ? ChosenColor : row.Passed ? PassedColor : row.Blocked != null ? BlockedColor : FailedColor;
            string mark = row.Passed ? $"{row.Rank}위" : row.Blocked != null ? "소비" : "X";
            DrawCard(d, color, mark, "", row.Failed, row.Blocked);
        }

        private void DrawCard(DialogueData d, Color color, string mark, string extra, string failed, string blocked)
        {
            bool expanded = expandedId == d.id;
            string head = $"{(expanded ? "▼" : "▶")} {mark,-4} {d.id}{extra}   [{d.parsedBand} / 세부 {d.subPriority} / 조건 {d.ConditionCount} / 본 횟수 {history.GetPlayCount(d.id)}]";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.contentColor = color;
            if (GUILayout.Button(head, EditorStyles.label)) expandedId = expanded ? null : d.id;
            GUI.contentColor = Color.white;

            if (failed != null) EditorGUILayout.LabelField($"   불충족: {failed}", EditorStyles.miniLabel);
            else if (blocked != null) EditorGUILayout.LabelField($"   소비됨: {blocked}", EditorStyles.miniLabel);

            if (expanded) DrawDetail(d);
            EditorGUILayout.EndVertical();
        }

        // ── 00시 이벤트 ─────────────────────────────

        private void DrawMidnightPane()
        {
            List<EventRow> queue = QueueOrder();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (triggers.Count == 0)
            {
                EditorGUILayout.LabelField($"트리거가 없습니다 ({DialogueDataTools.TriggersFile})", boldStyle);
            }
            else if (queue.Count == 0)
            {
                GUI.contentColor = ErrorColor;
                EditorGUILayout.LabelField("오늘 00시에 쌓일 이벤트 대사 없음", boldStyle);
                GUI.contentColor = Color.white;
            }
            else
            {
                GUI.contentColor = ChosenColor;
                EditorGUILayout.LabelField($"00시 대기열 ({queue.Count}개): {string.Join(" → ", queue.Select(r => $"{r.Data.id}({KindLabel(r.Trigger.parsedKind)})"))}", boldStyle);
                GUI.contentColor = Color.white;
            }

            EditorGUILayout.LabelField("게임에서는 필드로 돌아온 뒤 스토리 → 시스템 → 말풍선 순으로 하나씩 나옵니다. 나오는 순간 조건이 바뀌었으면 건너뜁니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"트리거 {triggers.Count}개 · 적재 {queue.Count} · 소비됨 {eventRows.Count(r => r.Failed == null && r.Blocked != null)} · 조건 불충족 {eventRows.Count(r => r.Failed != null)}", EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(queue.Count == 0))
            {
                var pass = new GUIContent("00시 지나기 (쌓인 대사를 모두 본 것으로 기록)", "대기열의 대사를 순서대로 소비합니다. 1회 대사는 다음 날 다시 쌓이지 않습니다.");
                if (GUILayout.Button(pass, GUILayout.Height(24f))) PassMidnight(queue);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4f);
            resultScroll = EditorGUILayout.BeginScrollView(resultScroll);

            foreach (EventRow row in queue) DrawEventRow(row, queue.IndexOf(row) + 1);
            foreach (EventRow row in eventRows.Where(r => !r.Loaded)) DrawEventRow(row, 0);

            DrawTalkLog();
            EditorGUILayout.EndScrollView();
        }

        private void DrawEventRow(EventRow row, int order)
        {
            Color color = row.Loaded ? ChosenColor : row.Blocked != null ? BlockedColor : FailedColor;
            string mark = row.Loaded ? $"{order}번" : row.Blocked != null ? "소비" : "X";
            DrawCard(row.Data, color, mark, $"  ({KindLabel(row.Trigger.parsedKind)})", row.Failed, row.Blocked);
        }

        private void PassMidnight(List<EventRow> queue)
        {
            foreach (EventRow row in queue)
            {
                // 앞 대사를 본 결과로 뒤 대사가 막힐 수 있음 (같은 eventGroup 등) — 게임 출력 시점 재확인과 같게 처리
                string blocked = DialogueSelector.FindFailedCondition(row.Data, variables) ?? history.GetBlockReason(row.Data, variables, simTime);
                if (blocked != null)
                {
                    AddLog($"[00시 · 건너뜀] {row.Data.id} — {blocked}");
                    continue;
                }

                history.RecordPlay(row.Data, variables, simTime);
                string first = row.Data.lines.Count > 0 ? LineText(row.Data.lines[0]) : "";
                AddLog($"[00시 · {KindLabel(row.Trigger.parsedKind)}] {row.Data.id} — {first}");
            }
            Changed();
        }

        private static string KindLabel(DialogueEventKind kind)
        {
            switch (kind)
            {
                case DialogueEventKind.Story: return "스토리";
                case DialogueEventKind.System: return "시스템";
                default: return "말풍선";
            }
        }

        // ── 상세 ─────────────────────────────

        private void DrawDetail(DialogueData d)
        {
            EditorGUI.indentLevel++;

            if (d.ConditionCount > 0)
            {
                EditorGUILayout.LabelField("조건", EditorStyles.boldLabel);
                foreach (ConditionData condition in d.conditions)
                {
                    bool ok = DialogueConditions.Evaluate(condition, variables);
                    GUI.contentColor = ok ? ChosenColor : ErrorColor;
                    EditorGUILayout.LabelField($"{(ok ? "O" : "X")} {condition}   (현재: {Show(variables.Get(condition.variable))})");
                }
                GUI.contentColor = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("조건 없음", EditorStyles.miniLabel);
            }

            var rules = new List<string>();
            if (d.maxPlays > 0) rules.Add($"최대 {d.maxPlays}회");
            if (!string.IsNullOrEmpty(d.perEvent)) rules.Add($"사건당 1회 ({d.perEvent}{(string.IsNullOrEmpty(d.eventGroup) ? "" : $", 그룹 {d.eventGroup}")})");
            if (d.cooldownSec > 0f) rules.Add($"쿨다운 {d.cooldownSec}초");
            if (rules.Count > 0) EditorGUILayout.LabelField($"소비 규칙: {string.Join(" · ", rules)}", EditorStyles.miniLabel);
            if (d.parsedDisplay != DialogueDisplay.Bottom) EditorGUILayout.LabelField($"표시: {d.display}{(string.IsNullOrEmpty(d.background) ? "" : $" · 배경 {d.background}")}", EditorStyles.miniLabel);

            EditorGUILayout.LabelField($"대사 ({d.lines.Count}줄)", EditorStyles.boldLabel);
            foreach (LineData line in d.lines)
            {
                string expression = string.IsNullOrEmpty(line.expression) ? "" : $" ({line.expression})";
                if (!string.IsNullOrEmpty(line.direction)) EditorGUILayout.LabelField($"[ {line.direction} ]", EditorStyles.miniLabel);
                string effects = line.effects == null || line.effects.Count == 0 ? "" : $"  [연출: {string.Join(", ", line.effects.Select(e => e.type))}]";
                EditorGUILayout.LabelField($"{LineText(line)}{expression}{effects}", wrapStyle);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            using (new EditorGUI.DisabledScope(d.ConditionCount == 0))
            {
                if (GUILayout.Button(new GUIContent("조건 맞추기", "이 대사의 조건이 모두 참이 되도록 변수를 바꿉니다"), GUILayout.Width(90f))) Satisfy(d);
            }
            if (GUILayout.Button(new GUIContent("본 것으로 +1", "이 대사를 한 번 본 것으로 기록합니다 (1회 대사 · 사건당 1회 확인용)"), GUILayout.Width(90f)))
            {
                history.RecordPlay(d, variables, simTime);
                Changed();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawTalkLog()
        {
            if (talkLog.Count == 0) return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("기록 (최신이 위)", EditorStyles.boldLabel);
            for (int i = 0; i < talkLog.Count; i++)
            {
                EditorGUILayout.LabelField($"{talkLog.Count - i}. {talkLog[i]}", wrapStyle);
            }
        }

        // ── 조건 맞추기 ─────────────────────────────

        private void Satisfy(DialogueData d)
        {
            var failed = new List<string>();
            foreach (ConditionData condition in d.conditions)
            {
                if (DialogueConditions.Evaluate(condition, variables)) continue;
                if (!database.VariableDefs.TryGetValue(condition.variable, out VariableDef def)) continue;

                string value = SatisfyingValue(def, condition);
                if (value == null || !variables.Set(def.name, value)) failed.Add(condition.ToString());
            }

            // 같은 변수에 조건이 여럿이면 앞의 값을 뒤 조건이 깰 수 있어 한 번 더 확인
            failed.AddRange(d.conditions.Where(c => !DialogueConditions.Evaluate(c, variables)).Select(c => c.ToString()).Except(failed));
            if (failed.Count > 0) ShowNotification(new GUIContent($"맞추지 못한 조건: {string.Join(", ", failed)}"));
            Changed();
        }

        private static string SatisfyingValue(VariableDef def, ConditionData condition)
        {
            string target = condition.value ?? "";
            switch (condition.op)
            {
                case "==":
                    return target;
                case "!=":
                    if (def.parsedType == VariableType.Enum) return def.allowed.FirstOrDefault(v => v != target);
                    if (def.parsedType == VariableType.Int) return int.TryParse(target, out int n) ? (n + 1).ToString() : null;
                    return target + "_";
            }

            if (!int.TryParse(target, out int bound)) return null;
            switch (condition.op)
            {
                case ">": return (bound + 1).ToString();
                case ">=": return bound.ToString();
                case "<": return (bound - 1).ToString();
                case "<=": return bound.ToString();
                default: return null;
            }
        }

        // ── 표시 도우미 ─────────────────────────────

        private string LineText(LineData line)
        {
            string speaker = string.IsNullOrEmpty(line.speaker) ? "(해설)" : database.GetSpeakerName(line.speaker);
            return $"{speaker}: {database.GetString(line.textKey)}";
        }

        private static string Show(string value)
        {
            return string.IsNullOrEmpty(value) ? "\"\"" : value;
        }
    }
}
