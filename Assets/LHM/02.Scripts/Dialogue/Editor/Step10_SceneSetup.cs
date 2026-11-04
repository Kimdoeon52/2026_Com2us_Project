using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step10_SceneSetup
    {
        private const string InputName = "[Step10] Input";
        private const string InputAssetPath = "Assets/LHM/03.Data/Input/RealSteelInput.inputactions";

        internal static void SetupInput()
        {
            const string inputUndo = "10-2단계 배치";
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, inputUndo)) return;

            if (Object.FindFirstObjectByType<InputModeController>(FindObjectsInactive.Include) != null)
            {
                SetupKit.Dialog(inputUndo, "InputModeController가 이미 씬에 있습니다. 배치를 건너뜁니다.", "확인");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (asset == null)
            {
                SetupKit.Dialog(inputUndo, $"입력 파일을 찾지 못했습니다.\n{InputAssetPath}", "확인");
                return;
            }

            if (!SetupKit.Dialog(inputUndo, $"'{scene.name}' 씬에 입력 상태 전환(InputModeController)과 테스트 스크립트를 배치합니다.\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            var go = new GameObject(InputName);
            Undo.RegisterCreatedObjectUndo(go, inputUndo);

            var controller = go.AddComponent<InputModeController>();
            var so = new SerializedObject(controller);
            so.FindProperty("actions").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<Step10_InputTest>();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Step10 배치] {scene.path}\n- {InputName} 생성 (InputModeController + Step10_InputTest)\n- 입력 파일: {InputAssetPath}\n완료. Ctrl+S로 저장 후 Play로 확인하세요.");
        }
    }
}
