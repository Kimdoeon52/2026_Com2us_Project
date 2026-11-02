using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class NpcTalkCameraSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "NPC 클로즈업 카메라 배치";
        private const string RootName = "[TalkCam] NpcTalkCamera";
        private const string FadeName = "[TalkCam] Fade";

        [MenuItem("Tools/Dialogue/NPC 클로즈업 카메라 배치")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            var menu = Object.FindFirstObjectByType<NpcChoiceMenu>(FindObjectsInactive.Include);
            if (interactor == null || menu == null)
            {
                EditorUtility.DisplayDialog(UndoName, "PlayerInteractor 또는 NpcChoiceMenu가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            var interactorSo = new SerializedObject(interactor);
            SerializedProperty talkCameraProp = interactorSo.FindProperty("talkCamera");
            if (talkCameraProp.objectReferenceValue != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"PlayerInteractor에 클로즈업 카메라가 이미 연결되어 있습니다. ({talkCameraProp.objectReferenceValue.name})\n배치를 건너뜁니다.", "확인");
                return;
            }

            Camera mainCamera = interactorSo.FindProperty("worldCamera").objectReferenceValue as Camera;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null)
            {
                EditorUtility.DisplayDialog(UndoName, "메인 카메라(MainCamera 태그)를 찾지 못했습니다.", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            DialogueBox box = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            DialogueDirector director = box != null ? new SerializedObject(box).FindProperty("director").objectReferenceValue as DialogueDirector : null;

            Canvas canvas = menu.GetComponentInParent<Canvas>(true);
            if (canvas == null && box != null) canvas = box.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas = canvas.rootCanvas;

            var mover = interactor.GetComponent<Step09_TestMover>();
            bool xyPlane = mover != null && new SerializedObject(mover).FindProperty("plane").enumValueIndex == (int)TestMovePlane.XY;

            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{scene.name}' 씬에 NPC 클로즈업 카메라를 배치합니다.\n" +
                    $"- {RootName} (NpcTalkCamera)\n  └ CloseUpCamera ('{mainCamera.name}' 설정 복사, 평소에는 꺼짐)\n" +
                    $"- {FadeName} ({(canvas != null ? canvas.name : "Canvas 없음 — 페이드 없이 바로 전환")} 맨 위, 검정 페이드)\n" +
                    $"- PlayerInteractor의 Talk Camera 칸 연결\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[TalkCam 배치] {scene.path}\n");

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            root.transform.position = interactor.transform.position;
            var talkCamera = root.AddComponent<NpcTalkCamera>();

            var cameraObject = new GameObject("CloseUpCamera");
            Undo.RegisterCreatedObjectUndo(cameraObject, UndoName);
            cameraObject.layer = mainCamera.gameObject.layer;
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.SetPositionAndRotation(interactor.transform.position + new Vector3(0f, 2f, -2f), mainCamera.transform.rotation);

            var closeUp = cameraObject.AddComponent<Camera>();
            EditorUtility.CopySerialized(mainCamera, closeUp);
            closeUp.depth = mainCamera.depth + 1f;
            log.AppendLine($"- CloseUpCamera 생성 ('{mainCamera.name}' 카메라 설정 복사)");

            foreach (Component component in mainCamera.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "UniversalAdditionalCameraData") continue;
                Component data = cameraObject.GetComponent(component.GetType());
                if (data == null) data = cameraObject.AddComponent(component.GetType());
                EditorUtility.CopySerialized(component, data);
                log.AppendLine("- URP 카메라 설정 복사");
            }

            cameraObject.SetActive(false);

            Image fade = null;
            if (canvas != null)
            {
                var fadeObject = new GameObject(FadeName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(fadeObject, UndoName);
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0) fadeObject.layer = uiLayer;
                var rect = (RectTransform)fadeObject.transform;
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
                fade = fadeObject.AddComponent<Image>();
                fade.color = new Color(0f, 0f, 0f, 0f);
                fade.raycastTarget = false;
                fade.enabled = false;
                log.AppendLine($"- {FadeName} 생성 ('{canvas.name}' 맨 아래 자식 = 화면 맨 위)");
            }
            else
            {
                log.AppendLine("- 주의: Canvas를 찾지 못해 페이드 없이 바로 전환");
            }

            var cameraSo = new SerializedObject(talkCamera);
            cameraSo.FindProperty("menu").objectReferenceValue = menu;
            cameraSo.FindProperty("closeUpCamera").objectReferenceValue = closeUp;
            cameraSo.FindProperty("mainCamera").objectReferenceValue = mainCamera;
            cameraSo.FindProperty("fadeOverlay").objectReferenceValue = fade;
            cameraSo.FindProperty("director").objectReferenceValue = director;
            if (xyPlane)
            {
                cameraSo.FindProperty("approach").enumValueIndex = (int)TalkCameraApproach.MainCameraSide;
                log.AppendLine("- 테스트 이동 평면이 XY라 접근 방향을 '메인 카메라 쪽'으로 설정");
            }
            cameraSo.ApplyModifiedPropertiesWithoutUndo();
            if (director == null) log.AppendLine("- 주의: DialogueDirector를 찾지 못함 — 대사 연출(확대·흔들림)이 메인 카메라에 걸림");

            Undo.RecordObject(interactor, UndoName);
            talkCameraProp.objectReferenceValue = talkCamera;
            interactorSo.ApplyModifiedProperties();
            log.AppendLine("- PlayerInteractor의 Talk Camera 칸 연결");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → NPC 앞에서 E.");
            Debug.Log(log.ToString());
        }
    }
}
