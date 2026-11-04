using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RealSteel.Dialogue.EditorTools
{
    public class DialogueCheckResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public DialogueDatabase Database;
        public DialoguePackBody Body;
        public int TriggerCount;

        public bool Ok => Errors.Count == 0;
    }

    public static class DialogueDataTools
    {
        public const string DataFolder = "Assets/LHM/03.Data/Dialogue";
        public const string StringsFile = "strings_ko.json";
        public const string VariablesFile = "variables.json";
        public const string TriggersFile = "dialogue_triggers.json";
        public const string PackFolder = DataFolder + "/Packed";
        public const string PackPath = PackFolder + "/dialogue_pack.bytes";

        private static readonly string[] OneLinePools = { "battle_bark", "crowd" };
        private const string MonologuePrefix = "mono_";

        [MenuItem("Tools/Dialogue/데이터 검사", priority = 1)]
        internal static void CheckMenu()
        {
            DialogueCheckResult result = Check();
            Log(result, "데이터 검사");
            EditorUtility.DisplayDialog("대사 데이터 검사", Summary(result, 12), "확인");
        }

        internal static void PackMenu()
        {
            if (TryPack(out string message)) EditorUtility.DisplayDialog("대사 데이터 패킹", message, "확인");
            else EditorUtility.DisplayDialog("대사 데이터 패킹 실패", message, "확인");
        }

        public static DialogueCheckResult Check(IReadOnlyDictionary<string, string> overrides = null)
        {
            var result = new DialogueCheckResult();

            if (!AssetDatabase.IsValidFolder(DataFolder))
            {
                result.Errors.Add($"데이터 폴더가 없습니다: {DataFolder}");
                return result;
            }

            IEnumerable<string> found = Directory.GetFiles(DataFolder, "*.json", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/'));
            if (overrides != null) found = found.Concat(overrides.Keys);
            string[] paths = found.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();

            var body = new DialoguePackBody { builtAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            DialogueSourceFile triggers = null;

            foreach (string path in paths)
            {
                string fileName = Path.GetFileName(path);
                string text = overrides != null && overrides.TryGetValue(path, out string replaced) ? replaced : File.ReadAllText(path, Encoding.UTF8);
                var source = new DialogueSourceFile(Path.GetFileNameWithoutExtension(path), text);

                if (fileName == StringsFile) body.strings = source;
                else if (fileName == VariablesFile) body.variables = source;
                else if (fileName == TriggersFile) triggers = source;
                else body.pools.Add(source);
            }

            var database = new DialogueDatabase();
            database.Load(body.pools, body.strings, body.variables, false);
            result.Errors.AddRange(database.Errors);
            result.Warnings.AddRange(database.Warnings);
            result.Database = database;
            result.Body = body;

            if (!database.Errors.Any())
            {
                CheckTriggers(triggers, database, result);
                CheckOneLine(database, result);
            }

            return result;
        }

        private static void CheckTriggers(DialogueSourceFile triggers, DialogueDatabase database, DialogueCheckResult result)
        {
            if (triggers == null)
            {
                result.Warnings.Add($"{TriggersFile} 없음 — 00시 트리거 없이 동작");
                return;
            }

            var valid = DialogueTriggerTable.Validate(
                triggers.name,
                triggers.text,
                id => database.TryGetDialogue(id, out _),
                id => database.TryGetDialogue(id, out DialogueData data) && data.lines.Count > 0 ? data.lines[0].speaker : null,
                result.Errors);
            result.TriggerCount = valid.Count;

            foreach (DialogueTriggerData trigger in valid)
            {
                if (trigger.parsedKind == DialogueEventKind.Bubble && database.TryGetDialogue(trigger.dialogueId, out DialogueData data) && data.lines.Count > 1)
                {
                    result.Warnings.Add($"말풍선 '{trigger.dialogueId}'은 {data.lines.Count}줄 — 첫 줄만 나옵니다");
                }
            }
        }

        private static void CheckOneLine(DialogueDatabase database, DialogueCheckResult result)
        {
            foreach (string poolId in database.PoolIds)
            {
                bool monologue = poolId.StartsWith(MonologuePrefix);
                if (!monologue && Array.IndexOf(OneLinePools, poolId) < 0) continue;
                if (!database.TryGetPool(poolId, out IReadOnlyList<DialogueData> pool)) continue;

                string owner = monologue ? poolId.Substring(MonologuePrefix.Length) : null;
                foreach (DialogueData dialogue in pool)
                {
                    if (dialogue.lines.Count > 1) result.Warnings.Add($"{poolId} › {dialogue.id}: {dialogue.lines.Count}줄 — 바크·말풍선은 첫 줄만 나옵니다");

                    if (owner != null && dialogue.lines[0].speaker != owner)
                    {
                        result.Warnings.Add($"{poolId} › {dialogue.id}: 혼잣말 화자 '{dialogue.lines[0].speaker}'가 풀 이름의 NPC '{owner}'와 다릅니다");
                    }
                }
            }
        }

        public static bool TryPack(out string message)
        {
            DialogueCheckResult result = Check();
            Log(result, "패킹 전 검사");

            if (!result.Ok)
            {
                message = $"데이터 오류가 있어 패킹하지 않았습니다.\n\n{Summary(result, 10)}";
                return false;
            }

            byte[] bytes = DialoguePack.Write(result.Body);
            if (!DialoguePack.TryRead(bytes, out _, out string readError))
            {
                message = $"만든 패킹 파일을 다시 읽지 못했습니다: {readError}";
                return false;
            }

            Directory.CreateDirectory(PackFolder);
            File.WriteAllBytes(PackPath, bytes);
            AssetDatabase.ImportAsset(PackPath, ImportAssetOptions.ForceUpdate);

            message = $"{PackPath}\n풀 {result.Body.pools.Count}개, 대화 {result.Database.Dialogues.Count}개, 문자열 {result.Database.StringCount}개, {bytes.Length:N0} bytes ({result.Body.builtAt})";
            Debug.Log($"[DialogueData] 패킹 완료 — {message.Replace('\n', ' ')}");
            return true;
        }

        public static void Log(DialogueCheckResult result, string label)
        {
            string text = $"[DialogueData] {label}: {Summary(result, int.MaxValue)}";
            if (!result.Ok) Debug.LogError(text);
            else if (result.Warnings.Count > 0) Debug.LogWarning(text);
            else Debug.Log(text);
        }

        public static string Summary(DialogueCheckResult result, int maxLines)
        {
            var builder = new StringBuilder();
            if (result.Ok)
            {
                int dialogues = result.Database != null ? result.Database.Dialogues.Count : 0;
                int pools = result.Database != null ? result.Database.PoolIds.Count() : 0;
                builder.Append($"통과 — 풀 {pools}개, 대화 {dialogues}개, 트리거 {result.TriggerCount}개, 경고 {result.Warnings.Count}건");
            }
            else
            {
                builder.Append($"오류 {result.Errors.Count}건, 경고 {result.Warnings.Count}건");
            }

            AppendList(builder, "오류", result.Errors, maxLines);
            AppendList(builder, "경고", result.Warnings, maxLines);
            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, string label, List<string> items, int maxLines)
        {
            if (items.Count == 0) return;

            builder.Append($"\n[{label}]");
            for (int i = 0; i < items.Count && i < maxLines; i++) builder.Append("\n - ").Append(items[i]);
            if (items.Count > maxLines) builder.Append($"\n - … 외 {items.Count - maxLines}건 (콘솔 참고)");
        }
    }

    public class DialogueDataWatcher : AssetPostprocessor
    {
        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (BuildPipeline.isBuildingPlayer || scheduled) return;
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsDialogueJson)) return;

            scheduled = true;
            EditorApplication.delayCall += () =>
            {
                scheduled = false;
                DialogueDataTools.Log(DialogueDataTools.Check(), "저장 시 자동 검사");
            };
        }

        private static bool IsDialogueJson(string path)
        {
            return path.StartsWith(DialogueDataTools.DataFolder + "/") && path.EndsWith(".json") && path.IndexOf('/', DialogueDataTools.DataFolder.Length + 1) < 0;
        }
    }

    public class DialogueBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            DialogueCheckResult result = DialogueDataTools.Check();
            DialogueDataTools.Log(result, "빌드 전 검사");

            if (!result.Ok)
            {
                throw new BuildFailedException($"[DialogueData] 대사 데이터 오류 {result.Errors.Count}건으로 빌드를 중단합니다. Tools > Dialogue > 데이터 검사로 확인하세요.\n - {string.Join("\n - ", result.Errors)}");
            }

            if (!DialogueDataTools.TryPack(out string message))
            {
                throw new BuildFailedException($"[DialogueData] 대사 데이터 패킹 실패로 빌드를 중단합니다. {message}");
            }
        }
    }
}
