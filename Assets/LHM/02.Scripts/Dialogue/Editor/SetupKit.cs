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

        internal static bool RequireLhmScene(Scene scene, string title)
        {
            if (IsLhmScene(scene)) return true;

            string current = string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path;
            Dialog(title, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {current}", "확인");
            return false;
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
