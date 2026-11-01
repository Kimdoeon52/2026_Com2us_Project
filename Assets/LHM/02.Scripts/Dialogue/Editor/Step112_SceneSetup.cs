using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step112_SceneSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "11-2단계 배치";
        private const string ObjectName = "[Step11-2] EventQueue";
        private const string PoolPath = "Assets/LHM/03.Data/Dialogue/event_dialogues.json";
        private const string TriggersPath = "Assets/LHM/03.Data/Dialogue/dialogue_triggers.json";

        [MenuItem("Tools/Dialogue/11-2단계 배치 (00시 트리거 + 이벤트 큐)")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueManager가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(PoolPath);
            var triggers = AssetDatabase.LoadAssetAtPath<TextAsset>(TriggersPath);
            if (pool == null || triggers == null)
            {
                EditorUtility.DisplayDialog(UndoName, $"데이터 파일을 찾지 못했습니다.\n{PoolPath}\n{TriggersPath}", "확인");
                return;
            }

            var clock = Object.FindFirstObjectByType<DummyUiStack>(FindObjectsInactive.Include);

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬에 00시 트리거와 이벤트 큐를 배치합니다.\n- DialogueManager 대사 풀에 events 추가\n- {ObjectName} 생성\n{(clock == null ? "- 주의: DummyUiStack이 없어 시계 연동 없이 T키로만 확인합니다 (10-1단계 배치 권장)\n" : "")}씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step11-2 배치] {scene.path}\n");

            AddPool(manager, pool, log);

            if (Object.FindFirstObjectByType<DialogueEventQueue>(FindObjectsInactive.Include) != null)
            {
                log.AppendLine("- DialogueEventQueue가 이미 있어 생성을 건너뜀");
            }
            else
            {
                var go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, UndoName);

                var queue = go.AddComponent<DialogueEventQueue>();
                var queueSo = new SerializedObject(queue);
                queueSo.FindProperty("manager").objectReferenceValue = manager;
                queueSo.FindProperty("triggersJson").objectReferenceValue = triggers;
                queueSo.ApplyModifiedPropertiesWithoutUndo();

                var test = go.AddComponent<Step112_MidnightTest>();
                var testSo = new SerializedObject(test);
                testSo.FindProperty("manager").objectReferenceValue = manager;
                testSo.FindProperty("queue").objectReferenceValue = queue;
                testSo.FindProperty("clock").objectReferenceValue = clock;
                testSo.ApplyModifiedPropertiesWithoutUndo();

                log.AppendLine($"- {ObjectName} 생성 (DialogueEventQueue + Step112_MidnightTest{(clock != null ? ", 시계 연결" : ", 시계 없음")})");
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
                    log.AppendLine("- events는 이미 대사 풀 목록에 있음");
                    return;
                }
            }

            pools.arraySize++;
            pools.GetArrayElementAtIndex(pools.arraySize - 1).objectReferenceValue = pool;
            so.ApplyModifiedProperties();
            log.AppendLine($"- DialogueManager 대사 풀에 events 추가 (총 {pools.arraySize}개)");
        }
    }
}
