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
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "10-1단계 배치";
        private const string StackName = "[Step10] UiStack";
        private const string InputName = "[Step10] Input";
        private const string InputAssetPath = "Assets/LHM/03.Data/Input/RealSteelInput.inputactions";

        [MenuItem("Tools/Dialogue/10-1단계 배치 (UI 스택)")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            if (Object.FindFirstObjectByType<DummyUiStack>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog(UndoName, "DummyUiStack이 이미 씬에 있습니다. 배치를 건너뜁니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬에 UI 스택(더미)과 시계 표시를 배치합니다.\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas)
                .OrderByDescending(c => c.name == "DialogueCanvas")
                .FirstOrDefault();

            TMP_Text clock = null;
            if (canvas != null)
            {
                var clockGo = new GameObject("ClockText", typeof(RectTransform));
                clockGo.layer = LayerMask.NameToLayer("UI");
                clockGo.transform.SetParent(canvas.transform, false);
                Undo.RegisterCreatedObjectUndo(clockGo, UndoName);

                var rect = (RectTransform)clockGo.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-32f, -24f);
                rect.sizeDelta = new Vector2(320f, 56f);

                var text = clockGo.AddComponent<TextMeshProUGUI>();
                TMP_Text fontSource = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(t => t != text && t.font != null);
                if (fontSource != null) text.font = fontSource.font;
                text.fontSize = 40f;
                text.alignment = TextAlignmentOptions.Right;
                text.text = "08:00";
                text.raycastTarget = false;
                clock = text;
            }

            var stackGo = new GameObject(StackName);
            Undo.RegisterCreatedObjectUndo(stackGo, UndoName);
            var stack = stackGo.AddComponent<DummyUiStack>();
            var so = new SerializedObject(stack);
            so.FindProperty("clockText").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Step10 배치] {scene.path}\n- {StackName} 생성 (DummyUiStack)\n- {(clock != null ? "ClockText 생성 (화면 오른쪽 위)" : "주의: Canvas가 없어 시계 표시를 만들지 못했습니다")}\n완료. Ctrl+S로 저장 후 Play로 확인하세요.");
        }

        [MenuItem("Tools/Dialogue/10-2단계 배치 (입력 상태)")]
        private static void SetupInput()
        {
            const string inputUndo = "10-2단계 배치";
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(inputUndo, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            if (Object.FindFirstObjectByType<InputModeController>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog(inputUndo, "InputModeController가 이미 씬에 있습니다. 배치를 건너뜁니다.", "확인");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog(inputUndo, $"입력 파일을 찾지 못했습니다.\n{InputAssetPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(inputUndo, $"'{scene.name}' 씬에 입력 상태 전환(InputModeController)과 테스트 스크립트를 배치합니다.\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
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
