using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using RealSteel.Dialogue.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RealSteel.Dialogue.EditorTools
{
    /// <summary>
    /// 검증을 "사람이 기억해서 돌리는 것"에 맡기지 않는다.
    ///  1) Tools/Dialogue/Validate All      : 수동 실행
    ///  2) JSON 저장(임포트) 시 자동 실행     : 기획자가 저장하는 순간 콘솔에 에러
    ///  3) 빌드 직전 실행 → 에러 있으면 빌드 실패 : 깨진 데이터가 출시되지 않음
    /// 콘솔 로그를 클릭하면 해당 JSON 파일이 선택된다.
    /// </summary>
    public static class DialogueValidationTools
    {
        public const string DataFolder = "Assets / LHM / 03.Data / Dialogue";

        [MenuItem("Tools/Dialogue/Validate All")]
        public static void ValidateAllMenu()
        {
            var report = ValidateAll(logToConsole: true);
            EditorUtility.DisplayDialog("Dialogue Validation",
                $"에러 {report.ErrorCount}건, 경고 {report.WarningCount}건\n자세한 내용은 콘솔을 확인하세요.", "확인");
        }

        [MenuItem("Tools/Dialogue/Report Missing Translations")]
        public static void ReportMissingTranslations()
        {
            foreach (var manifest in FindManifests())
            {
                var report = new ValidationReport();
                var db = DialogueDatabase.Load(manifest.ToSourceSet(), report);
                foreach (var t in manifest.stringTables)
                {
                    if (t.table == null) continue;
                    var resolver = new LocalizedTextResolver(db);
                    resolver.SetTable(JsonConvert.DeserializeObject<StringTableFile>(t.table.text, DialogueJson.Strict));
                    var missing = resolver.FindMissingKeys();
                    if (missing.Count == 0) Debug.Log($"[Dialogue] {t.lang}: 누락 없음", t.table);
                    else Debug.LogWarning($"[Dialogue] {t.lang}: 누락 {missing.Count}건\n" + string.Join("\n", missing), t.table);
                }
            }
        }

        [MenuItem("Tools/Dialogue/Sync Manifest With Folder")]
        public static void SyncManifest()
        {
            // 폴더의 모든 풀 JSON을 매니페스트에 자동 등록 (등록 누락 사고 방지)
            foreach (var manifest in FindManifests())
            {
                var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder + "/Pools" });
                manifest.dialogueFiles = guids
                    .Select(g => AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(a => a != null)
                    .OrderBy(a => a.name)
                    .ToList();
                EditorUtility.SetDirty(manifest);
                Debug.Log($"[Dialogue] 매니페스트 동기화: 풀 파일 {manifest.dialogueFiles.Count}개", manifest);
            }
            AssetDatabase.SaveAssets();
        }

        public static ValidationReport ValidateAll(bool logToConsole)
        {
            var total = new ValidationReport();
            var manifests = FindManifests().ToList();
            if (manifests.Count == 0)
            {
                total.Error(null, null, "DialogueDataManifest 에셋이 없습니다.");
            }

            foreach (var manifest in manifests)
            {
                var report = new ValidationReport();
                DialogueDatabase.Load(manifest.ToSourceSet(), report);
                CheckUnregisteredFiles(manifest, report);

                var byName = AllTextAssets(manifest).GroupBy(a => a.name + ".json").ToDictionary(g => g.Key, g => (Object)g.First());
                foreach (var issue in report.Issues)
                {
                    if (!logToConsole) continue;
                    byName.TryGetValue(issue.File ?? "", out var ctx);
                    var msg = $"[Dialogue] {issue}";
                    if (issue.Severity == IssueSeverity.Error) Debug.LogError(msg, ctx ?? manifest);
                    else if (issue.Severity == IssueSeverity.Warning) Debug.LogWarning(msg, ctx ?? manifest);
                }
                foreach (var i in report.Issues)
                {
                    if (i.Severity == IssueSeverity.Error) total.Error(i.File, i.Context, i.Message);
                    else if (i.Severity == IssueSeverity.Warning) total.Warn(i.File, i.Context, i.Message);
                }
            }

            if (logToConsole && !total.HasErrors && total.WarningCount == 0)
                Debug.Log("[Dialogue] 검증 통과: 에러/경고 없음");
            return total;
        }

        /// <summary>폴더엔 있는데 매니페스트에 등록 안 된 풀 JSON → 런타임에 조용히 빠지는 사고 방지</summary>
        private static void CheckUnregisteredFiles(DialogueDataManifest manifest, ValidationReport report)
        {
            var registered = new HashSet<TextAsset>(manifest.dialogueFiles.Where(f => f != null));
            foreach (var g in AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder + "/Pools" }))
            {
                var a = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (a != null && !registered.Contains(a))
                    report.Error(a.name + ".json", null, "매니페스트에 등록되지 않은 풀 파일입니다 (Tools/Dialogue/Sync Manifest With Folder).");
            }
        }

        private static IEnumerable<TextAsset> AllTextAssets(DialogueDataManifest m)
        {
            if (m.variables != null) yield return m.variables;
            if (m.speakers != null) yield return m.speakers;
            foreach (var f in m.dialogueFiles) if (f != null) yield return f;
        }

        public static IEnumerable<DialogueDataManifest> FindManifests() =>
            AssetDatabase.FindAssets("t:DialogueDataManifest")
                .Select(g => AssetDatabase.LoadAssetAtPath<DialogueDataManifest>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null);
    }

    /// <summary>JSON 저장 시 자동 검증</summary>
    public sealed class DialogueJsonPostprocessor : AssetPostprocessor
    {
        private static bool _scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved)
                .Any(p => p.StartsWith(DialogueValidationTools.DataFolder) && p.EndsWith(".json"));
            if (!touched || _scheduled) return;

            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                _scheduled = false;
                DialogueValidationTools.ValidateAll(logToConsole: true);
            };
        }
    }

    /// <summary>빌드 전 검증. 에러가 하나라도 있으면 빌드 중단</summary>
    public sealed class DialogueBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var r = DialogueValidationTools.ValidateAll(logToConsole: true);
            if (r.HasErrors)
                throw new BuildFailedException($"대화 데이터 에러 {r.ErrorCount}건 — 콘솔 확인 후 수정하세요.");
        }
    }
}
