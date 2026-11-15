using System.Linq;
using RealSteel.Dialogue.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.TestTools.EditorTools
{
    public static class WorkshopTestKitSetup
    {
        private const string Title = "제작 · 수리 테스트 지급";
        private const string ObjectName = "[Test] WorkshopTestKit";

        internal static bool Exists()
        {
            return Object.FindFirstObjectByType<WorkshopTestKit>(FindObjectsInactive.Include) != null;
        }

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, Title)) return;

            if (Exists())
            {
                Selection.activeObject = Object.FindFirstObjectByType<WorkshopTestKit>(FindObjectsInactive.Include);
                SetupKit.Dialog(Title, "이미 배치되어 있습니다. 선택해 두었으니 인스펙터에서 설정을 바꾸세요.", "확인");
                return;
            }

            bool hasInventory = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InventoryHost>(true)).Any();
            if (!SetupKit.Dialog(Title,
                    $"'{scene.name}' 씬에 영상 촬영용 테스트 지급을 배치합니다.\n" +
                    "Play하면 자동으로 한 번 지급, F9로 다시 지급:\n" +
                    "- 제작: 모든 부품(재료)을 최대 개수까지\n" +
                    "- 수리: 5부위 파츠 장착 + 부위마다 다른 내구도 (80 · 45 · 20 · 60 · 5%)\n" +
                    "- 복구: 잔해 1개 + 코어 불안정\n" +
                    "- 골드: 50,000G까지 채움\n" +
                    "- 제작 · 수리 · 인벤토리 튜토리얼은 촬영 중 안 뜨게 본 것으로 처리 (Ctrl + F10으로 되돌림)\n" +
                    (hasInventory ? "" : "\n주의: 이 씬에 InventorySystem이 없습니다. '작업대 연결 + 제작 · 수리 창'을 먼저 실행하세요.\n") +
                    "\n인벤토리 · 골드 세이브에 그대로 저장됩니다. 씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)",
                    "배치", "취소"))
            {
                return;
            }

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, Title);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<WorkshopTestKit>();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[{Title}] {scene.path}에 {ObjectName} 배치. Ctrl+S로 저장 후 Play → 콘솔에 지급 내용이 나옵니다. F9 = 다시 지급");
        }
    }
}
