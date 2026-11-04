using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step14_SceneSetup
    {
        private const string UndoName = "14단계 배치";
        private const string ObjectName = "[Step14] BattleBark";
        private const string SubtitleName = "[Step14] BarkSubtitle";
        private const string PoolPath = "Assets/LHM/03.Data/Dialogue/battle_bark.json";

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, UndoName)) return;

            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            DialogueBox dialogueBox = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            if (dialogueBox == null)
            {
                SetupKit.Dialog(UndoName, "DialogueManager 또는 연결된 DialogueBox가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            if (Object.FindFirstObjectByType<BattleBarkPlayer>(FindObjectsInactive.Include) != null)
            {
                SetupKit.Dialog(UndoName, "BattleBarkPlayer가 이미 씬에 있습니다. 배치를 건너뜁니다.", "확인");
                return;
            }

            var pool = AssetDatabase.LoadAssetAtPath<TextAsset>(PoolPath);
            if (pool == null)
            {
                SetupKit.Dialog(UndoName, $"데이터 파일을 찾지 못했습니다.\n{PoolPath}", "확인");
                return;
            }

            Canvas canvas = dialogueBox.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                SetupKit.Dialog(UndoName, "대화창이 들어 있는 Canvas를 찾지 못했습니다.", "확인");
                return;
            }
            canvas = canvas.rootCanvas;

            if (!SetupKit.Dialog(UndoName, $"'{scene.name}' 씬에 전투 바크를 배치합니다.\n- DialogueManager 대사 풀에 battle_bark 추가\n- '{canvas.name}' 아래 {SubtitleName} (하단 자막)\n- {ObjectName} (BattleBarkPlayer + Step14_BarkTest)\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step14 배치] {scene.path}\n");

            SetupKit.AddPool(manager, pool, log);

            var boxSo = new SerializedObject(dialogueBox);
            var body = boxSo.FindProperty("bodyText").objectReferenceValue as TMP_Text;
            TMP_FontAsset font = body != null ? body.font : null;
            BattleBarkSubtitle subtitle = CreateSubtitle(canvas.transform, font);
            log.AppendLine($"- {SubtitleName} 생성 ('{canvas.name}' 아래, 화면 하단)");

            var go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            var player = go.AddComponent<BattleBarkPlayer>();
            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("manager").objectReferenceValue = manager;
            playerSo.FindProperty("subtitle").objectReferenceValue = subtitle;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            var test = go.AddComponent<Step14_BarkTest>();
            var testSo = new SerializedObject(test);
            testSo.FindProperty("barks").objectReferenceValue = player;
            testSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"- {ObjectName} 생성 (BattleBarkPlayer + Step14_BarkTest)");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 6 / 7 / 8 / 9 / 0 키로 확인하세요.");
            Debug.Log(log.ToString());
        }

        private static BattleBarkSubtitle CreateSubtitle(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject(SubtitleName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) root.layer = uiLayer;
            root.transform.SetParent(parent, false);
            root.transform.SetAsLastSibling();

            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.15f, 0f);
            rect.anchorMax = new Vector2(0.85f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(0f, 64f);

            var image = root.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.6f);
            image.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textGo, UndoName);
            if (uiLayer >= 0) textGo.layer = uiLayer;
            textGo.transform.SetParent(root.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 0f);
            textRect.offsetMax = new Vector2(-20f, 0f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = "";

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var subtitle = root.AddComponent<BattleBarkSubtitle>();
            var so = new SerializedObject(subtitle);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return subtitle;
        }

    }
}
