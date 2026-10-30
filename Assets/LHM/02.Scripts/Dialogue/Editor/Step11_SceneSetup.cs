using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step11_SceneSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "11단계 배치";
        private const string ObjectName = "[Step11] BossEntry";
        private const string PoolPath = "Assets/LHM/03.Data/Dialogue/boss_entry.json";

        [MenuItem("Tools/Dialogue/11단계 배치 (보스 진입)")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            var debug = Object.FindFirstObjectByType<Step09_DialogueDebug>(FindObjectsInactive.Include);
            if (manager == null || debug == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueManager 또는 Step09_DialogueDebug가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(PoolPath);
            if (pool == null)
            {
                EditorUtility.DisplayDialog(UndoName, $"대사 풀을 찾지 못했습니다.\n{PoolPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬에 보스 진입 대사를 배치합니다.\n- DialogueManager 대사 풀에 boss_entry 추가\n- {ObjectName} 생성\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step11 배치] {scene.path}\n");

            AddPool(manager, pool, log);

            if (Object.FindFirstObjectByType<BossEntryDialogue>(FindObjectsInactive.Include) != null)
            {
                log.AppendLine("- BossEntryDialogue가 이미 있어 생성을 건너뜀");
            }
            else
            {
                var go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, UndoName);

                var entry = go.AddComponent<BossEntryDialogue>();
                var entrySo = new SerializedObject(entry);
                entrySo.FindProperty("manager").objectReferenceValue = manager;
                entrySo.ApplyModifiedPropertiesWithoutUndo();

                var test = go.AddComponent<Step11_BossEntryTest>();
                var testSo = new SerializedObject(test);
                testSo.FindProperty("manager").objectReferenceValue = manager;
                testSo.FindProperty("entry").objectReferenceValue = entry;
                testSo.FindProperty("debug").objectReferenceValue = debug;
                testSo.FindProperty("menu").objectReferenceValue = Object.FindFirstObjectByType<NpcChoiceMenu>(FindObjectsInactive.Include);
                testSo.ApplyModifiedPropertiesWithoutUndo();

                log.AppendLine($"- {ObjectName} 생성 (BossEntryDialogue + Step11_BossEntryTest)");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → Shift+F12(세이브 초기화)부터 확인하세요.");
            Debug.Log(log.ToString());
        }

        private static void AddPool(DialogueManager manager, TextAsset pool, StringBuilder log)
        {
            var so = new SerializedObject(manager);
            SerializedProperty pools = so.FindProperty("poolJsons");

            for (int i = 0; i < pools.arraySize; i++)
            {
                if (pools.GetArrayElementAtIndex(i).objectReferenceValue == pool)
                {
                    log.AppendLine("- boss_entry는 이미 대사 풀 목록에 있음");
                    return;
                }
            }

            pools.arraySize++;
            pools.GetArrayElementAtIndex(pools.arraySize - 1).objectReferenceValue = pool;
            so.ApplyModifiedProperties();
            log.AppendLine($"- DialogueManager 대사 풀에 boss_entry 추가 (총 {pools.arraySize}개)");
        }
    }
}
