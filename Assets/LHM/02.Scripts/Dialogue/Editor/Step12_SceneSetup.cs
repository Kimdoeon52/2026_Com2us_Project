using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step12_SceneSetup
    {
        private const string UndoName = "12단계 배치";
        private const string StatusName = "StatusText";
        private const string ConfirmName = "SkipConfirm";
        private const string ConfirmMessage = "안 읽은 대사가 있습니다.\nEsc: 스킵    Space: 계속 읽기";

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

            var boxSo = new SerializedObject(dialogueBox);
            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            var speaker = boxSo.FindProperty("speakerNameText").objectReferenceValue as TMP_Text;
            if (boxObject == null || boxObject.GetComponent<RectTransform>() == null)
            {
                SetupKit.Dialog(UndoName, "DialogueBox의 Box 칸이 비어 있거나 UI 오브젝트가 아닙니다.", "확인");
                return;
            }

            if (!SetupKit.Dialog(UndoName, $"'{scene.name}' 씬의 대화창({boxObject.name})에 다음을 만듭니다.\n- {StatusName}: AUTO / 빨리감기 표시 (오른쪽 위)\n- {ConfirmName}: 스킵 확인 창 (가운데)\n이미 연결되어 있으면 건너뜁니다. 씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step12 배치] {scene.path}\n");
            TMP_FontAsset font = speaker != null ? speaker.font : null;

            Undo.RecordObject(dialogueBox, UndoName);
            SerializedProperty statusProp = boxSo.FindProperty("statusText");
            if (statusProp.objectReferenceValue != null)
            {
                log.AppendLine($"- {StatusName}는 이미 연결되어 있음");
            }
            else
            {
                TMP_Text status = CreateText(boxObject.transform, StatusName, "AUTO", font, 24f, TextAlignmentOptions.TopRight);
                RectTransform rect = status.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-20f, -10f);
                rect.sizeDelta = new Vector2(240f, 40f);
                statusProp.objectReferenceValue = status;
                log.AppendLine($"- {StatusName} 생성 + 연결");
            }

            SerializedProperty confirmProp = boxSo.FindProperty("skipConfirm");
            if (confirmProp.objectReferenceValue != null)
            {
                log.AppendLine($"- {ConfirmName}는 이미 연결되어 있음");
            }
            else
            {
                var panel = new GameObject(ConfirmName, typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(panel, UndoName);
                panel.transform.SetParent(boxObject.transform, false);
                var panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(520f, 120f);
                panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

                TMP_Text message = CreateText(panel.transform, "Message", ConfirmMessage, font, 26f, TextAlignmentOptions.Center);
                RectTransform messageRect = message.rectTransform;
                messageRect.anchorMin = Vector2.zero;
                messageRect.anchorMax = Vector2.one;
                messageRect.offsetMin = messageRect.offsetMax = Vector2.zero;

                panel.SetActive(false);
                confirmProp.objectReferenceValue = panel;
                log.AppendLine($"- {ConfirmName} 생성 + 연결 (평소에는 꺼져 있음)");
            }

            boxSo.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → Shift+F12(세이브 초기화)부터 확인하세요.");
            Debug.Log(log.ToString());
        }

        private static TMP_Text CreateText(Transform parent, string name, string content, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
