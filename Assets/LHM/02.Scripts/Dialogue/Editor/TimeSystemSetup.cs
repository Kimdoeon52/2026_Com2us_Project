using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class TimeSystemSetup
    {
        private const string UndoName = "LJS 시간 시스템 연결";
        private const string PrefabPath = "Assets/LJS/04. Prefab/CoreSystemRoot.prefab";
        private const string StackName = "[Step10] UiStack (LJS 시간)";

        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, UndoName)) return;

            var existingBridge = Object.FindFirstObjectByType<TimeSystemUiStack>(FindObjectsInactive.Include);
            var timeSystem = Object.FindFirstObjectByType<TimeSystemManager>(FindObjectsInactive.Include);
            DummyUiStack[] dummies = Object.FindObjectsByType<DummyUiStack>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (existingBridge != null && timeSystem != null && dummies.Length == 0)
            {
                SetupKit.Dialog(UndoName, $"이미 연결되어 있습니다. ({existingBridge.name} → {timeSystem.name})\n배치를 건너뜁니다.", "확인");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (timeSystem == null && prefab == null)
            {
                SetupKit.Dialog(UndoName, $"씬에 TimeSystemManager가 없고, LJS 프리팹도 찾지 못했습니다.\n{PrefabPath}", "확인");
                return;
            }

            if (!SetupKit.Dialog(UndoName,
                    $"'{scene.name}' 씬의 임시 시계를 LJS 시간 시스템으로 교체합니다.\n" +
                    (timeSystem == null ? $"- LJS CoreSystemRoot 프리팹 배치 (시간·달력·랭킹·지도·뉴스)\n  프리팹 원본은 수정하지 않음\n" : $"- 씬의 TimeSystemManager 사용 ({timeSystem.name})\n") +
                    "- GameSceneManager의 F1~F5 씬 전환 단축키를 이 씬에서만 끔 (대화 테스트 키와 겹침)\n" +
                    $"- DummyUiStack {dummies.Length}개와 그 시계 글자(ClockText) 삭제\n" +
                    $"- {(existingBridge != null ? existingBridge.name + " 재사용" : "TimeSystemUiStack 생성")}, 00시 테스트(Step112) 연결\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "교체", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[LJS 시간 연결] {scene.path}\n");

            if (timeSystem == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, UndoName);
                timeSystem = instance.GetComponentInChildren<TimeSystemManager>(true);
                log.AppendLine($"- {instance.name} 프리팹 배치");
                if (timeSystem == null)
                {
                    Undo.CollapseUndoOperations(group);
                    Debug.LogError($"[LJS 시간 연결] 프리팹 안에 TimeSystemManager가 없습니다. Ctrl+Z로 되돌리세요.\n{PrefabPath}");
                    return;
                }
            }

            foreach (GameSceneManager sceneManager in Object.FindObjectsByType<GameSceneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(sceneManager);
                SerializedProperty enabledProp = so.FindProperty("_shortcutEnabled");
                if (enabledProp == null || !enabledProp.boolValue) continue;
                enabledProp.boolValue = false;
                so.ApplyModifiedProperties();
                log.AppendLine($"- {sceneManager.name}: F1~F5 씬 전환 단축키 끔 (이 씬에서만, 프리팹 덮어쓰기)");
            }

            GameObject host = existingBridge != null ? existingBridge.gameObject : null;
            foreach (DummyUiStack dummy in dummies)
            {
                if (host == null) host = dummy.gameObject;

                var clockText = new SerializedObject(dummy).FindProperty("clockText").objectReferenceValue as Component;
                if (clockText != null && clockText.gameObject.name == "ClockText")
                {
                    Undo.DestroyObjectImmediate(clockText.gameObject);
                    log.AppendLine("- ClockText 삭제 (시간 표시는 LJS UI)");
                }

                string dummyName = dummy.gameObject.name;
                Undo.DestroyObjectImmediate(dummy);
                log.AppendLine($"- {dummyName}: DummyUiStack 삭제");
            }

            if (host == null)
            {
                host = new GameObject(StackName);
                Undo.RegisterCreatedObjectUndo(host, UndoName);
            }
            else if (host.name == "[Step10] UiStack")
            {
                Undo.RecordObject(host, UndoName);
                host.name = StackName;
            }

            TimeSystemUiStack bridge = existingBridge != null ? existingBridge : Undo.AddComponent<TimeSystemUiStack>(host);
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("timeSystem").objectReferenceValue = timeSystem;
            bridgeSo.ApplyModifiedProperties();
            log.AppendLine($"- {host.name}: TimeSystemUiStack → {timeSystem.name}");

            foreach (Step112_MidnightTest test in Object.FindObjectsByType<Step112_MidnightTest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var testSo = new SerializedObject(test);
                testSo.FindProperty("clock").objectReferenceValue = bridge;
                testSo.ApplyModifiedProperties();
                log.AppendLine($"- {test.name}: 00시 테스트 시계 연결");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → LJS 시계가 흐르다 NPC 대화 중 정지, 자정에 00시 처리.");
            Debug.Log(log.ToString());
        }
    }
}
