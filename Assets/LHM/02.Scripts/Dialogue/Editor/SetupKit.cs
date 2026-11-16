using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class SetupKit
    {
        private const string LhmFolder = "Assets/LHM/";

        private static List<string> batchNotes;

        internal static bool IsLhmScene(Scene scene)
        {
            return scene.path.StartsWith(LhmFolder, StringComparison.Ordinal);
        }

        // 이전에는 LHM 씬에서만 실행했으나 이제 모든 씬에서 실행 가능
        // LHM 밖(다른 팀) 씬이면 씬마다 처음 한 번만 확인을 받음 (에디터를 끄면 다시 물음)
        private static readonly HashSet<string> confirmedOutsideScenes = new HashSet<string>();

        internal static bool RequireLhmScene(Scene scene, string title)
        {
            if (IsLhmScene(scene)) return true;

            string key = string.IsNullOrEmpty(scene.path) ? $"(unsaved){scene.handle}" : scene.path;
            if (confirmedOutsideScenes.Contains(key)) return true;

            string current = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;
            bool ok = batchNotes != null || EditorUtility.DisplayDialog(title,
                $"LHM 밖의 씬입니다: {current}\n" +
                "다른 팀 씬이면 저장 전에 담당자와 확인하세요. (Ctrl+Z 또는 저장하지 않고 씬 다시 열기로 되돌릴 수 있음)\n\n" +
                "이 씬에서 대화 툴 작업을 계속할까요? (이 씬은 다시 묻지 않음)",
                "계속", "취소");
            if (ok) confirmedOutsideScenes.Add(key);
            return ok;
        }

        internal static bool Dialog(string title, string message, string ok)
        {
            if (batchNotes == null) return EditorUtility.DisplayDialog(title, message, ok);

            batchNotes.Add(message);
            return true;
        }

        internal static bool Dialog(string title, string message, string ok, string cancel)
        {
            if (batchNotes == null) return EditorUtility.DisplayDialog(title, message, ok, cancel);

            Debug.Log($"[대화 툴] {title}\n{message}");
            return true;
        }

        internal static void BeginBatch()
        {
            batchNotes = new List<string>();
        }

        internal static List<string> EndBatch()
        {
            List<string> notes = batchNotes ?? new List<string>();
            batchNotes = null;
            return notes;
        }

        internal static void AddPool(DialogueManager manager, TextAsset pool, StringBuilder log)
        {
            var so = new SerializedObject(manager);
            SerializedProperty pools = so.FindProperty("poolJsons");
            if (pools == null)
            {
                log.AppendLine($"- 주의: DialogueManager의 대사 풀 칸(poolJsons)을 찾지 못함 — {pool.name}이 연결되어 있는지 직접 확인");
                return;
            }

            for (int i = 0; i < pools.arraySize; i++)
            {
                if (pools.GetArrayElementAtIndex(i).objectReferenceValue == pool)
                {
                    log.AppendLine($"- {pool.name}는 이미 대사 풀 목록에 있음");
                    return;
                }
            }

            pools.arraySize++;
            pools.GetArrayElementAtIndex(pools.arraySize - 1).objectReferenceValue = pool;
            so.ApplyModifiedProperties();
            log.AppendLine($"- 대사 풀에 {pool.name} 추가 (총 {pools.arraySize}개)");
        }
    }
}
