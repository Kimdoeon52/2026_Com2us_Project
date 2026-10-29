using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueDatabase
    {
        public const string SpeakerKeyPrefix = "speaker.";
        public const int MaxSubPriority = 99;

        private readonly Dictionary<string, DialogueData> dialogues = new Dictionary<string, DialogueData>();
        private readonly List<DialogueData> dialogueList = new List<DialogueData>();
        private readonly Dictionary<string, List<DialogueData>> pools = new Dictionary<string, List<DialogueData>>();
        private readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        private readonly Dictionary<string, VariableDef> variableDefs = new Dictionary<string, VariableDef>();
        private readonly List<string> errors = new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<DialogueData> Dialogues => dialogueList;
        public IReadOnlyDictionary<string, VariableDef> VariableDefs => variableDefs;
        public IEnumerable<string> PoolIds => pools.Keys;

        public bool Load(IReadOnlyList<TextAsset> poolAssets, TextAsset stringTableAsset, TextAsset variablesAsset)
        {
            dialogues.Clear();
            dialogueList.Clear();
            pools.Clear();
            strings.Clear();
            variableDefs.Clear();
            errors.Clear();

            VariableTableData variableTable = Parse<VariableTableData>(variablesAsset, "변수 목록");
            StringTableData table = Parse<StringTableData>(stringTableAsset, "문자열 테이블");

            if (variableTable != null) LoadVariables(variableTable, variablesAsset.name);
            if (table != null) LoadStrings(table, stringTableAsset.name);

            if (poolAssets == null || poolAssets.Count == 0)
            {
                errors.Add("대사 풀: 파일이 하나도 연결되지 않았습니다");
            }
            else
            {
                Dictionary<string, string> groupEvents = new Dictionary<string, string>();

                for (int i = 0; i < poolAssets.Count; i++)
                {
                    DialoguePoolData pool = Parse<DialoguePoolData>(poolAssets[i], $"대사 풀 {i + 1}번째 칸");
                    if (pool != null) LoadPool(pool, poolAssets[i].name, table != null, variableTable != null, groupEvents);
                }

                ValidateFormerIds();
            }

            if (errors.Count > 0)
            {
                Debug.LogError($"[Dialogue] 데이터 로드 실패 ({errors.Count}건)\n - " + string.Join("\n - ", errors));
                return false;
            }

            Debug.Log($"[Dialogue] 로드 완료: 풀 {pools.Count}개 ({string.Join(", ", pools.Keys)}), 대화 {dialogues.Count}개, 문자열 {strings.Count}개, 변수 {variableDefs.Count}개");
            return true;
        }

        public bool TryGetPool(string poolId, out IReadOnlyList<DialogueData> pool)
        {
            pool = null;
            if (string.IsNullOrEmpty(poolId) || !pools.TryGetValue(poolId, out List<DialogueData> list)) return false;

            pool = list;
            return true;
        }

        public string GetSpeakerName(string speakerId)
        {
            return string.IsNullOrEmpty(speakerId) ? "" : GetString(SpeakerKeyPrefix + speakerId);
        }

        public bool TryGetLines(string dialogueId, out List<DialogueLine> lines)
        {
            lines = null;

            if (string.IsNullOrEmpty(dialogueId) || !dialogues.TryGetValue(dialogueId, out DialogueData data))
            {
                Debug.LogError($"[Dialogue] 대화 ID '{dialogueId}'를 찾을 수 없습니다.");
                return false;
            }

            return TryGetLines(data, out lines);
        }

        public bool TryGetLines(DialogueData data, out List<DialogueLine> lines)
        {
            lines = new List<DialogueLine>(data.lines.Count);
            foreach (LineData line in data.lines)
            {
                string speakerName = GetSpeakerName(line.speaker);
                lines.Add(new DialogueLine(line.speaker ?? "", speakerName, GetString(line.textKey))
                {
                    expression = line.expression ?? "",
                    effects = line.effects,
                });
            }

            return true;
        }

        public string GetString(string key)
        {
            return strings.TryGetValue(key, out string value) ? value : $"#{key}#";
        }

        private T Parse<T>(TextAsset asset, string label) where T : class
        {
            if (asset == null)
            {
                errors.Add($"{label}: 파일이 연결되지 않았습니다");
                return null;
            }

            try
            {
                T data = JsonUtility.FromJson<T>(asset.text);
                if (data == null) errors.Add($"{asset.name}: 내용이 비어 있습니다");
                return data;
            }
            catch (ArgumentException e)
            {
                errors.Add($"{asset.name}: JSON 문법 오류 — {e.Message}");
                return null;
            }
        }

        private void LoadVariables(VariableTableData table, string fileName)
        {
            if (table.variables == null || table.variables.Count == 0)
            {
                errors.Add($"{fileName}: variables가 비어 있습니다");
                return;
            }

            for (int i = 0; i < table.variables.Count; i++)
            {
                VariableDef def = table.variables[i];
                string where = $"{fileName} › {i + 1}번째 변수";

                if (string.IsNullOrEmpty(def.name))
                {
                    errors.Add($"{where}: name이 비어 있습니다");
                    continue;
                }

                where = $"{fileName} › {def.name}";

                if (variableDefs.ContainsKey(def.name))
                {
                    errors.Add($"{where}: 이름이 중복됩니다");
                    continue;
                }

                if (!DialogueConditions.TryParseType(def.type, out def.parsedType))
                {
                    errors.Add($"{where}: type '{def.type}'는 int / enum / string 중 하나여야 합니다");
                    continue;
                }

                if (def.parsedType == VariableType.Enum && (def.allowed == null || def.allowed.Count == 0))
                {
                    errors.Add($"{where}: enum 변수는 allowed 목록이 필요합니다");
                    continue;
                }

                if (!DialogueConditions.AcceptsValue(def, def.defaultValue ?? "", out string reason))
                {
                    errors.Add($"{where}: 기본값 {reason}");
                    continue;
                }

                variableDefs.Add(def.name, def);
            }
        }

        private void LoadStrings(StringTableData table, string fileName)
        {
            if (table.entries == null || table.entries.Count == 0)
            {
                errors.Add($"{fileName}: entries가 비어 있습니다");
                return;
            }

            for (int i = 0; i < table.entries.Count; i++)
            {
                StringEntry entry = table.entries[i];
                string where = $"{fileName} › {i + 1}번째 항목";

                if (string.IsNullOrEmpty(entry.key))
                {
                    errors.Add($"{where}: key가 비어 있습니다");
                    continue;
                }

                if (strings.ContainsKey(entry.key))
                {
                    errors.Add($"{where}: key '{entry.key}'가 중복됩니다");
                    continue;
                }

                strings.Add(entry.key, entry.value ?? "");
            }
        }

        private void LoadPool(DialoguePoolData pool, string fileName, bool checkKeys, bool checkVariables, Dictionary<string, string> groupEvents)
        {
            if (string.IsNullOrEmpty(pool.poolId))
            {
                errors.Add($"{fileName}: poolId가 비어 있습니다");
                return;
            }

            if (pools.ContainsKey(pool.poolId))
            {
                errors.Add($"{fileName}: poolId '{pool.poolId}'가 다른 대사 풀 파일과 중복됩니다 (같은 파일을 두 칸에 넣었는지 확인)");
                return;
            }

            if (pool.dialogues == null || pool.dialogues.Count == 0)
            {
                errors.Add($"{fileName}: dialogues가 비어 있습니다");
                return;
            }

            List<DialogueData> poolList = new List<DialogueData>(pool.dialogues.Count);
            pools.Add(pool.poolId, poolList);

            bool hasFallback = false;

            for (int i = 0; i < pool.dialogues.Count; i++)
            {
                DialogueData dialogue = pool.dialogues[i];
                string where = $"{fileName} › {i + 1}번째 대화";

                if (string.IsNullOrEmpty(dialogue.id))
                {
                    errors.Add($"{where}: id가 비어 있습니다");
                    continue;
                }

                where = $"{fileName} › {dialogue.id}";

                if (dialogues.ContainsKey(dialogue.id))
                {
                    errors.Add($"{where}: id가 중복됩니다");
                    continue;
                }

                ValidatePriority(dialogue, where);
                ValidateDisplay(dialogue, where);

                if (dialogue.conditions != null)
                {
                    for (int c = 0; c < dialogue.conditions.Count; c++)
                    {
                        ValidateCondition(dialogue.conditions[c], $"{where} › {c + 1}번째 조건", checkVariables);
                    }
                }

                ValidateConsume(dialogue, where, checkVariables, groupEvents);

                if (dialogue.IsAlwaysAvailable) hasFallback = true;

                if (dialogue.lines == null || dialogue.lines.Count == 0)
                {
                    errors.Add($"{where}: lines가 비어 있습니다");
                    continue;
                }

                for (int j = 0; j < dialogue.lines.Count; j++)
                {
                    ValidateLine(dialogue.lines[j], $"{where} › {j + 1}번째 줄", checkKeys);
                }

                dialogue.poolId = pool.poolId;
                dialogues.Add(dialogue.id, dialogue);
                dialogueList.Add(dialogue);
                poolList.Add(dialogue);
            }

            if (!hasFallback)
            {
                errors.Add($"{fileName}: 항상 나올 수 있는 기본 대사(조건·소비 규칙 없음)가 최소 1개 필요합니다");
            }
        }

        private void ValidateFormerIds()
        {
            Dictionary<string, string> owners = new Dictionary<string, string>();

            foreach (DialogueData dialogue in dialogueList)
            {
                if (dialogue.formerIds == null) continue;

                foreach (string formerId in dialogue.formerIds)
                {
                    string where = $"{dialogue.poolId} › {dialogue.id}";

                    if (string.IsNullOrEmpty(formerId))
                    {
                        errors.Add($"{where}: formerIds에 빈 값이 있습니다");
                        continue;
                    }

                    if (dialogues.ContainsKey(formerId))
                    {
                        errors.Add($"{where}: 이전 ID '{formerId}'가 현재 다른 대화의 ID로 쓰이고 있습니다");
                        continue;
                    }

                    if (owners.TryGetValue(formerId, out string owner))
                    {
                        errors.Add($"{where}: 이전 ID '{formerId}'가 '{owner}'에도 적혀 있습니다 (기록은 한 곳으로만 이어받을 수 있음)");
                        continue;
                    }

                    owners.Add(formerId, dialogue.id);
                }
            }
        }

        private void ValidateConsume(DialogueData dialogue, string where, bool checkVariables, Dictionary<string, string> groupEvents)
        {
            if (dialogue.maxPlays < 0)
            {
                errors.Add($"{where}: maxPlays {dialogue.maxPlays}는 0 이상이어야 합니다 (0 = 제한 없음)");
            }

            if (dialogue.cooldownSec < 0f)
            {
                errors.Add($"{where}: cooldownSec {dialogue.cooldownSec}는 0 이상이어야 합니다");
            }

            bool hasEvent = !string.IsNullOrEmpty(dialogue.perEvent);
            bool hasGroup = !string.IsNullOrEmpty(dialogue.eventGroup);

            if (hasGroup && !hasEvent)
            {
                errors.Add($"{where}: eventGroup은 perEvent와 함께 써야 합니다");
                return;
            }

            if (!hasEvent) return;

            if (checkVariables)
            {
                if (!variableDefs.TryGetValue(dialogue.perEvent, out VariableDef def))
                {
                    errors.Add($"{where}: perEvent '{dialogue.perEvent}'가 변수 목록에 없습니다");
                }
                else if (def.parsedType != VariableType.Int)
                {
                    errors.Add($"{where}: perEvent '{dialogue.perEvent}'는 int 횟수 변수여야 합니다");
                }
            }

            if (!hasGroup) return;

            if (groupEvents.TryGetValue(dialogue.eventGroup, out string groupEvent))
            {
                if (groupEvent != dialogue.perEvent)
                {
                    errors.Add($"{where}: 같은 eventGroup '{dialogue.eventGroup}' 안에서 perEvent가 다릅니다 ('{groupEvent}' vs '{dialogue.perEvent}')");
                }
            }
            else
            {
                groupEvents.Add(dialogue.eventGroup, dialogue.perEvent);
            }
        }

        private void ValidatePriority(DialogueData dialogue, string where)
        {
            bool isNumber = int.TryParse(dialogue.band, out _);

            if (string.IsNullOrEmpty(dialogue.band) || isNumber || !Enum.TryParse(dialogue.band, false, out dialogue.parsedBand))
            {
                errors.Add($"{where}: band '{dialogue.band}'는 Critical / Story / Reactive / Progress / Ambient / Fallback 중 하나여야 합니다");
            }

            if (dialogue.subPriority < 0 || dialogue.subPriority > MaxSubPriority)
            {
                errors.Add($"{where}: subPriority {dialogue.subPriority}는 0~{MaxSubPriority} 범위여야 합니다");
            }
        }

        private void ValidateDisplay(DialogueData dialogue, string where)
        {
            switch (dialogue.display)
            {
                case null:
                case "":
                case "bottom":
                    dialogue.parsedDisplay = DialogueDisplay.Bottom;
                    break;
                case "fullscreen":
                    dialogue.parsedDisplay = DialogueDisplay.Fullscreen;
                    break;
                case "intermission":
                    dialogue.parsedDisplay = DialogueDisplay.Intermission;
                    break;
                default:
                    errors.Add($"{where}: display '{dialogue.display}'는 bottom / fullscreen / intermission 중 하나여야 합니다");
                    return;
            }

            if (!string.IsNullOrEmpty(dialogue.background) && dialogue.parsedDisplay != DialogueDisplay.Fullscreen)
            {
                errors.Add($"{where}: background는 display가 fullscreen일 때만 쓸 수 있습니다");
            }
        }

        public bool TryGetDialogue(string dialogueId, out DialogueData data)
        {
            data = null;
            return !string.IsNullOrEmpty(dialogueId) && dialogues.TryGetValue(dialogueId, out data);
        }

        private void ValidateCondition(ConditionData condition, string where, bool checkVariables)
        {
            if (string.IsNullOrEmpty(condition.variable) || string.IsNullOrEmpty(condition.op))
            {
                errors.Add($"{where}: variable 또는 op가 비어 있습니다 (철자·대소문자 확인)");
                return;
            }

            if (!checkVariables) return;

            if (!variableDefs.TryGetValue(condition.variable, out VariableDef def))
            {
                errors.Add($"{where}: 변수 목록에 없는 변수 '{condition.variable}'");
                return;
            }

            if (!DialogueConditions.IsOpAllowed(def, condition.op))
            {
                errors.Add($"{where}: '{condition.op}'는 {def.type} 변수에 쓸 수 없습니다 (int만 크기 비교 가능)");
                return;
            }

            if (!DialogueConditions.AcceptsValue(def, condition.value ?? "", out string reason))
            {
                errors.Add($"{where}: {condition.variable}의 값 {reason}");
            }
        }

        private void ValidateLine(LineData line, string where, bool checkKeys)
        {
            ValidateEffects(line, where);

            if (string.IsNullOrEmpty(line.textKey))
            {
                errors.Add($"{where}: textKey가 비어 있습니다 (철자·대소문자 확인)");
                return;
            }

            if (!checkKeys) return;

            if (!strings.ContainsKey(line.textKey))
            {
                errors.Add($"{where}: 문자열 테이블에 없는 키 '{line.textKey}'");
            }

            if (!string.IsNullOrEmpty(line.speaker) && !strings.ContainsKey(SpeakerKeyPrefix + line.speaker))
            {
                errors.Add($"{where}: 화자 '{line.speaker}'의 이름 키 '{SpeakerKeyPrefix + line.speaker}'가 문자열 테이블에 없습니다");
            }
        }

        private void ValidateEffects(LineData line, string where)
        {
            if (line.effects == null) return;

            for (int i = 0; i < line.effects.Count; i++)
            {
                EffectData effect = line.effects[i];
                string at = $"{where} › {i + 1}번째 연출";

                if (effect == null || string.IsNullOrEmpty(effect.type))
                {
                    errors.Add($"{at}: type이 비어 있습니다 (철자·대소문자 확인)");
                    continue;
                }

                if (Array.IndexOf(DialogueEffectTypes.All, effect.type) < 0)
                {
                    errors.Add($"{at}: 알 수 없는 연출 '{effect.type}' ({string.Join(" / ", DialogueEffectTypes.All)})");
                    continue;
                }

                if (effect.value < 0f)
                {
                    errors.Add($"{at}: {effect.type}의 value {effect.value}는 0 이상이어야 합니다");
                }

                if (effect.type == DialogueEffectTypes.Sound && string.IsNullOrEmpty(effect.id))
                {
                    errors.Add($"{at}: sound는 id가 필요합니다");
                }
            }
        }
    }
}
