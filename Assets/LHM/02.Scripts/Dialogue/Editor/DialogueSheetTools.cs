using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Dialogue.EditorTools
{
    public static class DialogueSheetTools
    {
        public const string SheetFolder = DialogueDataTools.DataFolder + "/Sheets";

        private static readonly string[] Columns = { "id", "band", "sub", "conditions", "consume", "display", "background", "formerIds", "speaker", "expression", "direction", "text", "effects", "key" };
        private static readonly string[] DialogueColumns = { "band", "sub", "conditions", "consume", "display", "background", "formerIds" };
        private static readonly Regex ConditionPattern = new Regex(@"^\s*([^\s=!<>]+)\s*(==|!=|>=|<=|>|<)\s*(.*?)\s*$");
        private static readonly Regex EffectPattern = new Regex(@"^\s*([A-Za-z]+)\s*(?:\((.*)\))?\s*$");
        private static readonly Regex EntryLinePattern = new Regex("^\\s*\\{\\s*\"key\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*,\\s*\"value\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\}\\s*,?\\s*$");

        internal static bool SuppressWatcher;

        private static string StringsPath => DialogueDataTools.DataFolder + "/" + DialogueDataTools.StringsFile;

        internal static void ExportMenu()
        {
            List<string> pools = PoolJsonPaths().ToList();
            int existing = pools.Count(p => File.Exists(CsvPathFor(p)));

            if (!EditorUtility.DisplayDialog("CSV 내보내기", $"대사 풀 {pools.Count}개를 {SheetFolder}에 CSV로 내보냅니다.{(existing > 0 ? $"\n이미 있는 CSV {existing}개는 덮어씁니다 (CSV에서 고친 내용이 있다면 먼저 가져오기를 하세요)." : "")}\n\n내보낸 뒤에는 그 풀의 원본이 CSV가 됩니다. JSON을 직접 고치지 말고 CSV를 고치세요.", "내보내기", "취소"))
            {
                return;
            }

            Dictionary<string, string> strings = ReadStrings(out string error);
            if (strings == null)
            {
                EditorUtility.DisplayDialog("CSV 내보내기 실패", error, "확인");
                return;
            }

            Directory.CreateDirectory(SheetFolder);
            var log = new StringBuilder("[DialogueSheet] CSV 내보내기\n");
            SuppressWatcher = true;
            try
            {
                foreach (string jsonPath in pools)
                {
                    string csv = Export(jsonPath, strings, out string exportError);
                    if (csv == null)
                    {
                        log.AppendLine($" - {Path.GetFileName(jsonPath)}: 건너뜀 ({exportError})");
                        continue;
                    }

                    File.WriteAllText(CsvPathFor(jsonPath), csv, new UTF8Encoding(true));
                    log.AppendLine($" - {Path.GetFileName(jsonPath)} → {Path.GetFileName(CsvPathFor(jsonPath))}");
                }
                AssetDatabase.Refresh();
            }
            finally
            {
                SuppressWatcher = false;
            }

            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("CSV 내보내기", $"{SheetFolder}에 내보냈습니다.\n엑셀은 'CSV UTF-8'로, 구글 시트는 '.csv'로 저장하세요.", "확인");
        }

        internal static void ImportAllMenu()
        {
            if (!Directory.Exists(SheetFolder))
            {
                EditorUtility.DisplayDialog("CSV 가져오기", $"{SheetFolder} 폴더가 없습니다. 먼저 CSV 내보내기를 실행하세요.", "확인");
                return;
            }

            var summary = new StringBuilder();
            int failed = 0;
            foreach (string csv in Directory.GetFiles(SheetFolder, "*.csv").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal))
            {
                bool ok = Import(csv, out string message);
                if (!ok) failed++;
                summary.AppendLine($"{(ok ? "O" : "X")} {Path.GetFileName(csv)}: {message}");
            }

            Debug.Log($"[DialogueSheet] CSV 가져오기 (전체)\n{summary}");
            EditorUtility.DisplayDialog(failed > 0 ? $"CSV 가져오기 — 실패 {failed}개" : "CSV 가져오기", Trim(summary.ToString(), 1500), "확인");
        }

        public static bool Import(string csvPath, out string message)
        {
            string text = ReadCsvText(csvPath, out message);
            if (text == null) return false;

            List<List<string>> rows = ParseCsv(text, out string parseError);
            if (rows == null)
            {
                message = parseError;
                return false;
            }

            string jsonPath = DialogueDataTools.DataFolder + "/" + Path.GetFileNameWithoutExtension(csvPath) + ".json";
            Dictionary<string, string> strings = ReadStrings(out string stringsError);
            if (strings == null)
            {
                message = stringsError;
                return false;
            }

            var errors = new List<string>();
            var pool = ReadPool(rows, Path.GetFileNameWithoutExtension(csvPath), errors, out List<SheetLine> sheetLines);
            if (errors.Count > 0)
            {
                message = $"CSV 오류 {errors.Count}건\n - " + string.Join("\n - ", errors);
                return false;
            }

            HashSet<string> otherKeys = KeysUsedByOtherPools(jsonPath);
            HashSet<string> oldKeys = File.Exists(jsonPath) ? KeysOf(File.ReadAllText(jsonPath, Encoding.UTF8)) : new HashSet<string>();

            var updates = new List<KeyValuePair<string, string>>();
            var warnings = new List<string>();
            AssignKeys(pool, sheetLines, strings, updates, errors, warnings, otherKeys);
            if (errors.Count > 0)
            {
                message = $"CSV 오류 {errors.Count}건\n - " + string.Join("\n - ", errors);
                return false;
            }

            HashSet<string> newKeys = new HashSet<string>(sheetLines.Select(l => l.Line.textKey));
            List<string> removed = oldKeys.Where(k => !newKeys.Contains(k) && !otherKeys.Contains(k) && strings.ContainsKey(k)
                && !k.StartsWith(DialogueDatabase.SpeakerKeyPrefix) && !k.StartsWith(DialogueDatabase.MenuKeyPrefix)).ToList();

            string poolJson = WritePool(pool);
            string stringsJson = RewriteStrings(updates, removed, strings, oldKeys, out int added, out int changed);

            string oldPool = File.Exists(jsonPath) ? File.ReadAllText(jsonPath, Encoding.UTF8) : null;
            string oldStrings = File.ReadAllText(StringsPath, Encoding.UTF8);
            if (oldPool == poolJson && oldStrings == stringsJson)
            {
                message = "변경 없음";
                return true;
            }

            var overrides = new Dictionary<string, string> { { jsonPath, poolJson }, { StringsPath, stringsJson } };
            DialogueCheckResult check = DialogueDataTools.Check(overrides);
            if (!check.Ok)
            {
                message = $"변환 결과가 검사를 통과하지 못해 저장하지 않았습니다\n{DialogueDataTools.Summary(check, 10)}";
                return false;
            }

            File.WriteAllText(jsonPath, poolJson, new UTF8Encoding(false));
            File.WriteAllText(StringsPath, stringsJson, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(StringsPath, ImportAssetOptions.ForceUpdate);

            message = $"{Path.GetFileName(jsonPath)} 저장 — 대화 {pool.dialogues.Count}개, 줄 {sheetLines.Count}개 / 문자열 추가 {added}, 변경 {changed}, 삭제 {removed.Count}"
                + (warnings.Count > 0 ? "\n   주의: " + string.Join("\n   주의: ", warnings) : "");
            return true;
        }

        private class SheetLine
        {
            public int Row;
            public LineData Line;
            public string Text;
            public DialogueData Owner;
            public int Index;
        }

        private static DialoguePoolData ReadPool(List<List<string>> rows, string fileName, List<string> errors, out List<SheetLine> lines)
        {
            lines = new List<SheetLine>();
            var pool = new DialoguePoolData { poolId = fileName, dialogues = new List<DialogueData>() };
            Dictionary<string, int> header = null;
            DialogueData current = null;

            for (int r = 0; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                int rowNumber = r + 1;
                string first = Cell(row, 0);

                if (first.StartsWith("#"))
                {
                    for (int c = 0; c + 1 < row.Count; c += 2)
                    {
                        string name = Cell(row, c);
                        string value = Cell(row, c + 1);
                        if (name == "#eventOnly") pool.eventOnly = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    continue;
                }

                if (header == null)
                {
                    if (first != "id") continue;

                    header = new Dictionary<string, int>();
                    for (int c = 0; c < row.Count; c++)
                    {
                        string name = row[c].Trim();
                        if (name != "" && !header.ContainsKey(name)) header.Add(name, c);
                    }

                    foreach (string required in new[] { "id", "speaker", "text", "key" })
                    {
                        if (!header.ContainsKey(required)) errors.Add($"머리글에 '{required}' 칸이 없습니다");
                    }
                    if (errors.Count > 0) return pool;
                    continue;
                }

                if (row.All(cell => cell.Trim() == "")) continue;

                string Get(string column) => header.TryGetValue(column, out int index) ? Cell(row, index) : "";
                string id = Get("id");

                if (id != "")
                {
                    current = new DialogueData { id = id, conditions = new List<ConditionData>(), lines = new List<LineData>() };
                    pool.dialogues.Add(current);
                    ReadDialogue(current, Get, rowNumber, errors);
                }
                else
                {
                    if (current == null)
                    {
                        errors.Add($"{rowNumber}행: 첫 대화의 id가 비어 있습니다");
                        continue;
                    }

                    foreach (string column in DialogueColumns)
                    {
                        if (Get(column) != "") errors.Add($"{rowNumber}행: '{column}'은 id가 있는 줄(대화의 첫 줄)에만 적습니다");
                    }
                }

                var line = new LineData
                {
                    speaker = Get("speaker"),
                    expression = NullIfEmpty(Get("expression")),
                    direction = NullIfEmpty(Get("direction")),
                    textKey = Get("key"),
                    effects = ParseEffects(Get("effects"), rowNumber, errors),
                };
                current.lines.Add(line);
                lines.Add(new SheetLine { Row = rowNumber, Line = line, Text = Get("text"), Owner = current, Index = current.lines.Count });
            }

            if (header == null) errors.Add("'id'로 시작하는 머리글 행을 찾지 못했습니다");
            else if (pool.dialogues.Count == 0) errors.Add("대화가 하나도 없습니다");
            return pool;
        }

        private static void ReadDialogue(DialogueData dialogue, Func<string, string> get, int row, List<string> errors)
        {
            dialogue.band = get("band");
            dialogue.display = NullIfEmpty(get("display"));
            dialogue.background = NullIfEmpty(get("background"));

            string sub = get("sub");
            if (sub != "" && !int.TryParse(sub, out dialogue.subPriority)) errors.Add($"{row}행: sub '{sub}'는 숫자여야 합니다");

            string former = get("formerIds");
            if (former != "") dialogue.formerIds = Split(former).ToList();

            foreach (string part in Split(get("conditions")))
            {
                Match match = ConditionPattern.Match(part);
                if (!match.Success)
                {
                    errors.Add($"{row}행: 조건 '{part}'를 읽지 못했습니다 (예: hunt.count >= 3)");
                    continue;
                }

                string value = match.Groups[3].Value;
                if (value == "\"\"") value = "";
                dialogue.conditions.Add(new ConditionData { variable = match.Groups[1].Value, op = match.Groups[2].Value, value = value });
            }

            foreach (string part in Split(get("consume")))
            {
                int eq = part.IndexOf('=');
                string name = eq > 0 ? part.Substring(0, eq).Trim() : part;
                string value = eq > 0 ? part.Substring(eq + 1).Trim() : "";

                switch (name)
                {
                    case "maxPlays":
                        if (!int.TryParse(value, out dialogue.maxPlays)) errors.Add($"{row}행: maxPlays '{value}'는 숫자여야 합니다");
                        break;
                    case "cooldown":
                        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out dialogue.cooldownSec)) errors.Add($"{row}행: cooldown '{value}'는 숫자여야 합니다");
                        break;
                    case "perEvent":
                        dialogue.perEvent = value;
                        break;
                    case "group":
                        dialogue.eventGroup = value;
                        break;
                    default:
                        errors.Add($"{row}행: 소비 규칙 '{part}'를 읽지 못했습니다 (maxPlays=1 / perEvent=변수 / group=묶음 / cooldown=초)");
                        break;
                }
            }
        }

        private static List<EffectData> ParseEffects(string cell, int row, List<string> errors)
        {
            List<string> parts = Split(cell).ToList();
            if (parts.Count == 0) return null;

            var effects = new List<EffectData>();
            foreach (string part in parts)
            {
                Match match = EffectPattern.Match(part);
                if (!match.Success)
                {
                    errors.Add($"{row}행: 연출 '{part}'를 읽지 못했습니다 (예: zoom(1.2) / sound(sfx.stamp) / fadeOut(1, wait))");
                    continue;
                }

                var effect = new EffectData { type = match.Groups[1].Value };
                foreach (string raw in match.Groups[2].Value.Split(','))
                {
                    string arg = raw.Trim();
                    if (arg == "") continue;
                    if (arg == "wait") effect.wait = true;
                    else if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) effect.value = number;
                    else effect.id = arg;
                }
                effects.Add(effect);
            }
            return effects;
        }

        private static void AssignKeys(DialoguePoolData pool, List<SheetLine> lines, Dictionary<string, string> strings, List<KeyValuePair<string, string>> updates, List<string> errors, List<string> warnings, HashSet<string> otherKeys)
        {
            var texts = new Dictionary<string, string>();
            var taken = new HashSet<string>(strings.Keys);
            foreach (SheetLine line in lines)
            {
                if (line.Line.textKey != "") taken.Add(line.Line.textKey);
            }

            foreach (SheetLine line in lines)
            {
                string key = line.Line.textKey;
                if (key == "")
                {
                    string baseKey = $"{line.Owner.id}.{line.Index}";
                    key = baseKey;
                    for (int n = 2; taken.Contains(key); n++) key = $"{baseKey}_{n}";
                    taken.Add(key);
                    line.Line.textKey = key;
                }

                if (line.Text == "")
                {
                    if (!strings.ContainsKey(key) && !texts.ContainsKey(key)) errors.Add($"{line.Row}행: 문장이 비어 있습니다 (새 키 '{key}')");
                    continue;
                }

                if (texts.TryGetValue(key, out string earlier))
                {
                    if (earlier != line.Text) errors.Add($"{line.Row}행: 키 '{key}'를 쓰는 다른 줄과 문장이 다릅니다");
                    continue;
                }

                texts.Add(key, line.Text);
                updates.Add(new KeyValuePair<string, string>(key, line.Text));
                if (otherKeys.Contains(key) && strings.TryGetValue(key, out string old) && old != line.Text)
                {
                    warnings.Add($"'{key}'는 다른 풀에서도 쓰는 문장입니다 — 그쪽 대사도 함께 바뀝니다");
                }
            }
        }

        private static string Export(string jsonPath, Dictionary<string, string> strings, out string error)
        {
            error = null;
            DialoguePoolData pool;
            try
            {
                pool = JsonUtility.FromJson<DialoguePoolData>(File.ReadAllText(jsonPath, Encoding.UTF8));
            }
            catch (ArgumentException e)
            {
                error = $"JSON 문법 오류 — {e.Message}";
                return null;
            }

            if (pool == null || pool.dialogues == null)
            {
                error = "내용 없음";
                return null;
            }

            var rows = new List<string[]>();
            if (pool.eventOnly) rows.Add(new[] { "#eventOnly", "true" });
            rows.Add(Columns);

            foreach (DialogueData dialogue in pool.dialogues)
            {
                List<LineData> dialogueLines = dialogue.lines ?? new List<LineData>();
                if (dialogueLines.Count == 0) dialogueLines.Add(new LineData());

                for (int i = 0; i < dialogueLines.Count; i++)
                {
                    LineData line = dialogueLines[i];
                    bool head = i == 0;
                    string key = line.textKey ?? "";
                    rows.Add(new[]
                    {
                        head ? dialogue.id : "",
                        head ? dialogue.band ?? "" : "",
                        head && dialogue.subPriority != 0 ? dialogue.subPriority.ToString(CultureInfo.InvariantCulture) : "",
                        head ? FormatConditions(dialogue.conditions) : "",
                        head ? FormatConsume(dialogue) : "",
                        head ? dialogue.display ?? "" : "",
                        head ? dialogue.background ?? "" : "",
                        head && dialogue.formerIds != null ? string.Join("; ", dialogue.formerIds) : "",
                        line.speaker ?? "",
                        line.expression ?? "",
                        line.direction ?? "",
                        strings.TryGetValue(key, out string text) ? text : "",
                        FormatEffects(line.effects),
                        key,
                    });
                }
            }

            return WriteCsv(rows);
        }

        private static string FormatConditions(List<ConditionData> conditions)
        {
            if (conditions == null) return "";
            return string.Join("; ", conditions.Select(c => $"{c.variable} {c.op} {(string.IsNullOrEmpty(c.value) ? "\"\"" : c.value)}"));
        }

        private static string FormatConsume(DialogueData dialogue)
        {
            var parts = new List<string>();
            if (dialogue.maxPlays != 0) parts.Add($"maxPlays={dialogue.maxPlays}");
            if (!string.IsNullOrEmpty(dialogue.perEvent)) parts.Add($"perEvent={dialogue.perEvent}");
            if (!string.IsNullOrEmpty(dialogue.eventGroup)) parts.Add($"group={dialogue.eventGroup}");
            if (dialogue.cooldownSec != 0f) parts.Add($"cooldown={Number(dialogue.cooldownSec)}");
            return string.Join("; ", parts);
        }

        private static string FormatEffects(List<EffectData> effects)
        {
            if (effects == null || effects.Count == 0) return "";
            return string.Join("; ", effects.Select(e =>
            {
                var args = new List<string>();
                if (!string.IsNullOrEmpty(e.id)) args.Add(e.id);
                if (e.value != 0f) args.Add(Number(e.value));
                if (e.wait) args.Add("wait");
                return args.Count == 0 ? e.type : $"{e.type}({string.Join(", ", args)})";
            }));
        }

        private static string WritePool(DialoguePoolData pool)
        {
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append($"  \"poolId\": {Quote(pool.poolId)},\n");
            if (pool.eventOnly) b.Append("  \"eventOnly\": true,\n");
            b.Append("  \"dialogues\": [\n");

            for (int d = 0; d < pool.dialogues.Count; d++)
            {
                DialogueData dialogue = pool.dialogues[d];
                b.Append("    {\n");
                b.Append($"      \"id\": {Quote(dialogue.id)},\n");
                if (dialogue.formerIds != null && dialogue.formerIds.Count > 0) b.Append($"      \"formerIds\": [{string.Join(", ", dialogue.formerIds.Select(Quote))}],\n");
                b.Append($"      \"band\": {Quote(dialogue.band ?? "")},\n");
                if (dialogue.subPriority != 0) b.Append($"      \"subPriority\": {dialogue.subPriority},\n");
                if (dialogue.maxPlays != 0) b.Append($"      \"maxPlays\": {dialogue.maxPlays},\n");
                if (!string.IsNullOrEmpty(dialogue.perEvent)) b.Append($"      \"perEvent\": {Quote(dialogue.perEvent)},\n");
                if (!string.IsNullOrEmpty(dialogue.eventGroup)) b.Append($"      \"eventGroup\": {Quote(dialogue.eventGroup)},\n");
                if (dialogue.cooldownSec != 0f) b.Append($"      \"cooldownSec\": {Number(dialogue.cooldownSec)},\n");
                if (!string.IsNullOrEmpty(dialogue.display)) b.Append($"      \"display\": {Quote(dialogue.display)},\n");
                if (!string.IsNullOrEmpty(dialogue.background)) b.Append($"      \"background\": {Quote(dialogue.background)},\n");

                if (dialogue.conditions.Count == 0)
                {
                    b.Append("      \"conditions\": [],\n");
                }
                else
                {
                    b.Append("      \"conditions\": [\n");
                    for (int c = 0; c < dialogue.conditions.Count; c++)
                    {
                        ConditionData condition = dialogue.conditions[c];
                        b.Append($"        {{ \"variable\": {Quote(condition.variable)}, \"op\": {Quote(condition.op)}, \"value\": {Quote(condition.value ?? "")} }}");
                        b.Append(c < dialogue.conditions.Count - 1 ? ",\n" : "\n");
                    }
                    b.Append("      ],\n");
                }

                b.Append("      \"lines\": [\n");
                for (int l = 0; l < dialogue.lines.Count; l++)
                {
                    LineData line = dialogue.lines[l];
                    var fields = new List<string>();
                    if (!string.IsNullOrEmpty(line.speaker)) fields.Add($"\"speaker\": {Quote(line.speaker)}");
                    fields.Add($"\"textKey\": {Quote(line.textKey)}");
                    if (!string.IsNullOrEmpty(line.expression)) fields.Add($"\"expression\": {Quote(line.expression)}");
                    if (!string.IsNullOrEmpty(line.direction)) fields.Add($"\"direction\": {Quote(line.direction)}");
                    if (line.effects != null && line.effects.Count > 0) fields.Add($"\"effects\": [ {string.Join(", ", line.effects.Select(WriteEffect))} ]");
                    b.Append($"        {{ {string.Join(", ", fields)} }}");
                    b.Append(l < dialogue.lines.Count - 1 ? ",\n" : "\n");
                }
                b.Append("      ]\n");
                b.Append(d < pool.dialogues.Count - 1 ? "    },\n" : "    }\n");
            }

            b.Append("  ]\n}\n");
            return b.ToString();
        }

        private static string WriteEffect(EffectData effect)
        {
            var fields = new List<string> { $"\"type\": {Quote(effect.type)}" };
            if (effect.value != 0f) fields.Add($"\"value\": {Number(effect.value)}");
            if (!string.IsNullOrEmpty(effect.id)) fields.Add($"\"id\": {Quote(effect.id)}");
            if (effect.wait) fields.Add("\"wait\": true");
            return $"{{ {string.Join(", ", fields)} }}";
        }

        private static string RewriteStrings(List<KeyValuePair<string, string>> updates, List<string> removed, Dictionary<string, string> strings, HashSet<string> oldKeys, out int added, out int changed)
        {
            added = 0;
            changed = 0;
            string[] fileLines = File.ReadAllText(StringsPath, Encoding.UTF8).Replace("\r\n", "\n").Split('\n');

            var items = new List<KeyValuePair<string, string>>();
            int first = -1;
            int last = -1;
            for (int i = 0; i < fileLines.Length; i++)
            {
                Match match = EntryLinePattern.Match(fileLines[i]);
                if (!match.Success) continue;
                if (first < 0) first = i;
                last = i;
            }

            bool lineFormat = first >= 0 && Enumerable.Range(first, last - first + 1).All(i => fileLines[i].Trim() == "" || EntryLinePattern.IsMatch(fileLines[i]))
                && Enumerable.Range(first, last - first + 1).Count(i => EntryLinePattern.IsMatch(fileLines[i])) == strings.Count;

            if (lineFormat)
            {
                for (int i = first; i <= last; i++)
                {
                    Match match = EntryLinePattern.Match(fileLines[i]);
                    items.Add(new KeyValuePair<string, string>(match.Success ? match.Groups[1].Value : null, null));
                }
            }
            else
            {
                foreach (string key in strings.Keys) items.Add(new KeyValuePair<string, string>(key, null));
            }

            var values = new Dictionary<string, string>(strings);
            var removedSet = new HashSet<string>(removed);
            items.RemoveAll(item => item.Key != null && removedSet.Contains(item.Key));
            foreach (string key in removed) values.Remove(key);

            int anchor = -1;
            foreach (KeyValuePair<string, string> update in updates)
            {
                int index = items.FindIndex(item => item.Key == update.Key);
                if (index >= 0)
                {
                    if (values[update.Key] != update.Value) changed++;
                    values[update.Key] = update.Value;
                    anchor = index;
                    continue;
                }

                if (anchor < 0)
                {
                    anchor = items.FindLastIndex(item => item.Key != null && oldKeys.Contains(item.Key));
                    if (anchor < 0)
                    {
                        items.Add(new KeyValuePair<string, string>(null, null));
                        anchor = items.Count - 1;
                    }
                }

                items.Insert(anchor + 1, new KeyValuePair<string, string>(update.Key, null));
                values[update.Key] = update.Value;
                anchor++;
                added++;
            }

            var cleaned = new List<string>();
            foreach (KeyValuePair<string, string> item in items)
            {
                if (item.Key == null && (cleaned.Count == 0 || cleaned[cleaned.Count - 1] == null)) continue;
                cleaned.Add(item.Key);
            }
            while (cleaned.Count > 0 && cleaned[cleaned.Count - 1] == null) cleaned.RemoveAt(cleaned.Count - 1);

            int lastEntry = cleaned.FindLastIndex(k => k != null);
            var b = new StringBuilder();
            b.Append("{\n  \"entries\": [\n");
            for (int i = 0; i < cleaned.Count; i++)
            {
                string key = cleaned[i];
                if (key == null)
                {
                    b.Append('\n');
                    continue;
                }
                b.Append($"    {{ \"key\": {Quote(key)}, \"value\": {Quote(values[key])} }}{(i < lastEntry ? "," : "")}\n");
            }
            b.Append("  ]\n}\n");
            return b.ToString();
        }

        private static Dictionary<string, string> ReadStrings(out string error)
        {
            error = null;
            try
            {
                var table = JsonUtility.FromJson<StringTableData>(File.ReadAllText(StringsPath, Encoding.UTF8));
                var result = new Dictionary<string, string>();
                foreach (StringEntry entry in table.entries)
                {
                    if (!string.IsNullOrEmpty(entry.key) && !result.ContainsKey(entry.key)) result.Add(entry.key, entry.value ?? "");
                }
                return result;
            }
            catch (Exception e)
            {
                error = $"{StringsPath}를 읽지 못했습니다 — {e.Message}";
                return null;
            }
        }

        private static HashSet<string> KeysUsedByOtherPools(string jsonPath)
        {
            var keys = new HashSet<string>();
            foreach (string path in PoolJsonPaths())
            {
                if (path == jsonPath) continue;
                keys.UnionWith(KeysOf(File.ReadAllText(path, Encoding.UTF8)));
            }
            return keys;
        }

        private static HashSet<string> KeysOf(string json)
        {
            var keys = new HashSet<string>();
            try
            {
                var pool = JsonUtility.FromJson<DialoguePoolData>(json);
                if (pool?.dialogues == null) return keys;
                foreach (DialogueData dialogue in pool.dialogues)
                {
                    if (dialogue.lines == null) continue;
                    foreach (LineData line in dialogue.lines)
                    {
                        if (!string.IsNullOrEmpty(line.textKey)) keys.Add(line.textKey);
                    }
                }
            }
            catch (ArgumentException)
            {
            }
            return keys;
        }

        private static IEnumerable<string> PoolJsonPaths()
        {
            string[] special = { DialogueDataTools.StringsFile, DialogueDataTools.VariablesFile, DialogueDataTools.TriggersFile };
            return Directory.GetFiles(DialogueDataTools.DataFolder, "*.json", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => Array.IndexOf(special, Path.GetFileName(p)) < 0)
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        private static string CsvPathFor(string jsonPath)
        {
            return SheetFolder + "/" + Path.GetFileNameWithoutExtension(jsonPath) + ".csv";
        }

        private static string ReadCsvText(string path, out string error)
        {
            error = null;
            byte[] bytes = File.ReadAllBytes(path);
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                error = "UTF-8이 아닌 파일입니다. 엑셀은 '다른 이름으로 저장 → CSV UTF-8(쉼표로 분리)'로 저장하세요";
                return null;
            }
        }

        private static List<List<string>> ParseCsv(string text, out string error)
        {
            error = null;
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool fieldStarted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"' when !fieldStarted:
                        quoted = true;
                        fieldStarted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        fieldStarted = false;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        rows.Add(row);
                        row = new List<string>();
                        field.Clear();
                        fieldStarted = false;
                        break;
                    default:
                        field.Append(c);
                        fieldStarted = true;
                        break;
                }
            }

            if (quoted)
            {
                error = "따옴표(\")가 닫히지 않았습니다";
                return null;
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }

        private static string WriteCsv(List<string[]> rows)
        {
            var b = new StringBuilder();
            foreach (string[] row in rows)
            {
                b.Append(string.Join(",", row.Select(EscapeCsv)));
                b.Append("\r\n");
            }
            return b.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? "";
            bool needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || value != value.Trim();
            return needsQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static IEnumerable<string> Split(string cell)
        {
            return (cell ?? "").Split(';').Select(s => s.Trim()).Where(s => s != "");
        }

        private static string Cell(List<string> row, int index)
        {
            return index < row.Count ? row[index].Trim() : "";
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string Number(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            var b = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append($"\\u{(int)c:x4}");
                        else b.Append(c);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        private static string Trim(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max) + "\n… (콘솔 참고)";
        }
    }

    public class DialogueSheetWatcher : AssetPostprocessor
    {
        private static readonly HashSet<string> pending = new HashSet<string>();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (DialogueSheetTools.SuppressWatcher || BuildPipeline.isBuildingPlayer) return;

            bool any = false;
            foreach (string path in imported)
            {
                if (path.StartsWith(DialogueSheetTools.SheetFolder + "/") && path.EndsWith(".csv"))
                {
                    any |= pending.Add(path);
                }
            }
            if (!any) return;

            EditorApplication.delayCall += () =>
            {
                if (pending.Count == 0) return;
                string[] paths = pending.ToArray();
                pending.Clear();

                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    bool ok = DialogueSheetTools.Import(path, out string message);
                    if (ok) Debug.Log($"[DialogueSheet] CSV 저장 → 변환 {Path.GetFileName(path)}: {message}");
                    else Debug.LogError($"[DialogueSheet] CSV 저장 → 변환 실패 {Path.GetFileName(path)} (JSON은 그대로): {message}");
                }
            };
        }
    }
}
