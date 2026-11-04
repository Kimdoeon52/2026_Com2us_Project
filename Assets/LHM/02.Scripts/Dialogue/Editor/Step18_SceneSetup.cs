using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step18_SceneSetup
    {
        private const string UndoName = "18단계 배치";
        private const string ObjectName = "[Step18] TutorialTest";
        private const string PoolPath = "Assets/LHM/03.Data/Dialogue/tutorial.json";

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, UndoName)) return;

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(PoolPath);
            if (manager == null || pool == null)
            {
                SetupKit.Dialog(UndoName, manager == null ? "DialogueManager가 없습니다. 9단계 배치를 먼저 실행하세요." : $"데이터 파일을 찾지 못했습니다.\n{PoolPath}", "확인");
                return;
            }

            var bossTest = Object.FindFirstObjectByType<Step11_BossEntryTest>(FindObjectsInactive.Include);
            var barks = Object.FindFirstObjectByType<BattleBarkPlayer>(FindObjectsInactive.Include);
            var queue = Object.FindFirstObjectByType<DialogueEventQueue>(FindObjectsInactive.Include);

            if (!SetupKit.Dialog(UndoName, $"'{scene.name}' 씬에 기획서 v6 반영분을 배치합니다.\n- DialogueManager 대사 풀에 tutorial 추가\n- {ObjectName} (UI 직접 재생 튜토리얼 테스트)\n- Step11 보스 진입 테스트에 바크({(barks != null ? "있음" : "없음")})·이벤트 큐({(queue != null ? "있음" : "없음")}) 연결 → 전투 시작 시 전투 자막 튜토리얼, 귀가 시 트리거 평가\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step18 배치] {scene.path}\n");

            SetupKit.AddPool(manager, pool, log);

            if (Object.FindFirstObjectByType<Step18_TutorialTest>(FindObjectsInactive.Include) != null)
            {
                log.AppendLine($"- {ObjectName}가 이미 있어 생성을 건너뜀");
            }
            else
            {
                var go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                var test = go.AddComponent<Step18_TutorialTest>();
                var testSo = new SerializedObject(test);
                testSo.FindProperty("manager").objectReferenceValue = manager;
                testSo.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"- {ObjectName} 생성");
            }

            if (bossTest != null)
            {
                var bossSo = new SerializedObject(bossTest);
                bossSo.FindProperty("barks").objectReferenceValue = barks;
                bossSo.FindProperty("queue").objectReferenceValue = queue;
                bossSo.ApplyModifiedProperties();
                log.AppendLine($"- Step11_BossEntryTest 연결: 바크 {(barks != null ? "O" : "X (14단계 배치 필요)")}, 이벤트 큐 {(queue != null ? "O" : "X (11-2단계 배치 필요)")}");
            }
            else
            {
                log.AppendLine("- Step11_BossEntryTest 없음 (11단계 배치 권장)");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → Shift+F12(세이브 초기화)부터 확인하세요.");
            Debug.Log(log.ToString());
        }
    }
}
