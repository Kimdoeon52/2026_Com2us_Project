using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step13_SceneSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "13단계 배치";
        private const string RootName = "[Step13] Backlog";

        [MenuItem("Tools/Dialogue/13단계 배치 (백로그)")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            DialogueBox dialogueBox = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            if (dialogueBox == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueManager 또는 연결된 DialogueBox가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            var boxSo = new SerializedObject(dialogueBox);
            SerializedProperty backlogProp = boxSo.FindProperty("backlog");
            if (backlogProp.objectReferenceValue != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"DialogueBox에 백로그가 이미 연결되어 있습니다. ({backlogProp.objectReferenceValue.name})\n배치를 건너뜁니다.", "확인");
                return;
            }

            Canvas canvas = dialogueBox.GetComponentInParent<Canvas>(true);
            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            if (canvas == null && boxObject != null) canvas = boxObject.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(UndoName, "대화창이 들어 있는 Canvas를 찾지 못했습니다.", "확인");
                return;
            }
            canvas = canvas.rootCanvas;

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬의 '{canvas.name}' 맨 위에 백로그 창을 만들고 대화창에 연결합니다.\n- {RootName} (DialogueBacklog)\n  └ Panel (평소에는 꺼져 있음) / Title / Viewport / Content\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step13 배치] {scene.path}\n");

            var speaker = boxSo.FindProperty("speakerNameText").objectReferenceValue as TMP_Text;
            var body = boxSo.FindProperty("bodyText").objectReferenceValue as TMP_Text;
            TMP_FontAsset font = body != null && body.font != null ? body.font : speaker != null ? speaker.font : null;
            int uiLayer = LayerMask.NameToLayer("UI");

            RectTransform root = CreateRect(RootName, canvas.transform, uiLayer);
            Stretch(root, Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();
            var backlog = root.gameObject.AddComponent<DialogueBacklog>();

            RectTransform panel = CreateRect("Panel", root, uiLayer);
            Stretch(panel, new Vector2(60f, 40f), new Vector2(-60f, -40f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.9f);

            RectTransform title = CreateRect("Title", panel, uiLayer);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.offsetMin = new Vector2(30f, -70f);
            title.offsetMax = new Vector2(-30f, -16f);
            TMP_Text titleText = AddText(title, font, 26f, TextAlignmentOptions.MidlineLeft, "백로그");
            titleText.color = new Color(0.8f, 0.8f, 0.8f);

            RectTransform viewport = CreateRect("Viewport", panel, uiLayer);
            Stretch(viewport, new Vector2(30f, 24f), new Vector2(-30f, -84f));
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport, uiLayer);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 100f);
            TMP_Text contentText = AddText(content, font, 30f, TextAlignmentOptions.TopLeft, "");
            contentText.textWrappingMode = TextWrappingModes.Normal;
            contentText.overflowMode = TextOverflowModes.Overflow;

            panel.gameObject.SetActive(false);

            var backlogSo = new SerializedObject(backlog);
            backlogSo.FindProperty("panel").objectReferenceValue = panel.gameObject;
            backlogSo.FindProperty("viewport").objectReferenceValue = viewport;
            backlogSo.FindProperty("contentText").objectReferenceValue = contentText;
            backlogSo.FindProperty("titleText").objectReferenceValue = titleText;
            backlogSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"- {RootName} 생성 ('{canvas.name}' 맨 아래 자식 = 화면 맨 위)");

            Undo.RecordObject(dialogueBox, UndoName);
            backlogProp.objectReferenceValue = backlog;
            boxSo.ApplyModifiedProperties();
            log.AppendLine("- DialogueBox의 Backlog 칸 연결");
            if (font == null) log.AppendLine("- 주의: 대화창 폰트를 찾지 못해 기본 폰트 사용 (한글이 네모로 나오면 Content·Title 폰트 교체)");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 대화 중 L.");
            Debug.Log(log.ToString());
        }

        private static RectTransform CreateRect(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (layer >= 0) go.layer = layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static TMP_Text AddText(RectTransform rect, TMP_FontAsset font, float size, TextAlignmentOptions alignment, string content)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }
    }
}
