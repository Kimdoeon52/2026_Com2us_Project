using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step15_SceneSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "15단계 배치";
        private const string SystemName = "[Step15] BubbleSystem";
        private const string UiRootName = "[Step15] Bubbles";
        private const string CrowdName = "[Step15] Crowd";
        private const string DataFolder = "Assets/LHM/03.Data/Dialogue/";
        private static readonly string[] PoolFiles = { "crowd", "mono_receptionist", "mono_junkdealer", "mono_auctioneer", "event_dialogues" };
        private static readonly float[] CrowdX = { -8f, -4f, 0f, 4f, 8f };
        private const float CrowdY = 1.5f;

        [MenuItem("Tools/Dialogue/15단계 배치 (비강제 말풍선)")]
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
            var interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (dialogueBox == null || interactor == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueManager(대화창 연결) 또는 PlayerInteractor가 없습니다. 9단계 배치를 먼저 실행하세요.", "확인");
                return;
            }

            if (Object.FindFirstObjectByType<BubbleManager>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog(UndoName, "BubbleManager가 이미 씬에 있습니다. 배치를 건너뜁니다.", "확인");
                return;
            }

            Canvas canvas = dialogueBox.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(UndoName, "대화창이 들어 있는 Canvas를 찾지 못했습니다.", "확인");
                return;
            }
            canvas = canvas.rootCanvas;

            var queue = Object.FindFirstObjectByType<DialogueEventQueue>(FindObjectsInactive.Include);

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬에 비강제 말풍선을 배치합니다.\n- 대사 풀 추가: {string.Join(", ", PoolFiles)}\n- '{canvas.name}' 아래 {UiRootName} (말풍선 틀)\n- {SystemName} (BubbleManager + NpcBubbles + Step15_BubbleTest)\n- {CrowdName} (테스트용 관중 {CrowdX.Length}명, y={CrowdY})\n{(queue == null ? "- 주의: DialogueEventQueue가 없어 반응 말풍선은 받지 않습니다 (11-2단계 배치 권장)\n" : "")}씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Step15 배치] {scene.path}\n");

            AddPools(manager, log);

            var body = new SerializedObject(dialogueBox).FindProperty("bodyText").objectReferenceValue as TMP_Text;
            BubbleView template = CreateTemplate(canvas.transform, body != null ? body.font : null);
            log.AppendLine($"- {UiRootName} 생성 (말풍선 틀 1개, Play 시 8개로 복제)");

            var system = new GameObject(SystemName);
            Undo.RegisterCreatedObjectUndo(system, UndoName);

            var bubbles = system.AddComponent<BubbleManager>();
            var bubblesSo = new SerializedObject(bubbles);
            bubblesSo.FindProperty("manager").objectReferenceValue = manager;
            bubblesSo.FindProperty("template").objectReferenceValue = template;
            bubblesSo.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            bubblesSo.ApplyModifiedPropertiesWithoutUndo();

            var npcBubbles = system.AddComponent<NpcBubbles>();
            var npcSo = new SerializedObject(npcBubbles);
            npcSo.FindProperty("manager").objectReferenceValue = manager;
            npcSo.FindProperty("bubbles").objectReferenceValue = bubbles;
            npcSo.FindProperty("queue").objectReferenceValue = queue;
            npcSo.FindProperty("player").objectReferenceValue = interactor.transform;
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            CrowdBubbles crowd = CreateCrowd(manager, bubbles);
            log.AppendLine($"- {CrowdName} 생성 (관중 {CrowdX.Length}명)");

            var test = system.AddComponent<Step15_BubbleTest>();
            var testSo = new SerializedObject(test);
            testSo.FindProperty("manager").objectReferenceValue = manager;
            testSo.FindProperty("queue").objectReferenceValue = queue;
            testSo.FindProperty("crowd").objectReferenceValue = crowd;
            testSo.FindProperty("npcBubbles").objectReferenceValue = npcBubbles;
            testSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"- {SystemName} 생성 (플레이어 '{interactor.name}', 이벤트 큐 {(queue != null ? "연결" : "없음")})");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → NPC 근처로 이동 / H / J / K 키로 확인하세요.");
            Debug.Log(log.ToString());
        }

        private static void AddPools(DialogueManager manager, StringBuilder log)
        {
            var so = new SerializedObject(manager);
            SerializedProperty pools = so.FindProperty("poolJsons");

            foreach (string file in PoolFiles)
            {
                string path = DataFolder + file + ".json";
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    log.AppendLine($"- 주의: {path} 없음");
                    continue;
                }

                bool exists = false;
                for (int i = 0; i < pools.arraySize; i++) exists |= pools.GetArrayElementAtIndex(i).objectReferenceValue == asset;
                if (exists)
                {
                    log.AppendLine($"- {file}는 이미 대사 풀 목록에 있음");
                    continue;
                }

                pools.arraySize++;
                pools.GetArrayElementAtIndex(pools.arraySize - 1).objectReferenceValue = asset;
                log.AppendLine($"- 대사 풀에 {file} 추가");
            }

            so.ApplyModifiedProperties();
        }

        private static BubbleView CreateTemplate(Transform canvas, TMP_FontAsset font)
        {
            int uiLayer = LayerMask.NameToLayer("UI");

            var root = new GameObject(UiRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, UndoName);
            if (uiLayer >= 0) root.layer = uiLayer;
            root.transform.SetParent(canvas, false);
            root.transform.SetAsLastSibling();
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            Undo.RegisterCreatedObjectUndo(bubble, UndoName);
            if (uiLayer >= 0) bubble.layer = uiLayer;
            bubble.transform.SetParent(root.transform, false);
            var rect = (RectTransform)bubble.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(200f, 50f);

            var image = bubble.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);
            image.raycastTarget = false;

            var group = bubble.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textGo, UndoName);
            if (uiLayer >= 0) textGo.layer = uiLayer;
            textGo.transform.SetParent(bubble.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 8f);
            textRect.offsetMax = new Vector2(-14f, -8f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.1f, 0.1f, 0.1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.text = "말풍선";

            var view = bubble.AddComponent<BubbleView>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            bubble.SetActive(false);
            return view;
        }

        private static CrowdBubbles CreateCrowd(DialogueManager manager, BubbleManager bubbles)
        {
            var root = new GameObject(CrowdName);
            Undo.RegisterCreatedObjectUndo(root, UndoName);

            var members = new Transform[CrowdX.Length];
            for (int i = 0; i < CrowdX.Length; i++)
            {
                GameObject member = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(member, UndoName);
                member.name = $"관중_{i + 1}";
                Object.DestroyImmediate(member.GetComponent<Collider>());
                member.transform.SetParent(root.transform, false);
                member.transform.position = new Vector3(CrowdX[i], CrowdY, 0f);
                member.transform.localScale = Vector3.one * 0.7f;
                members[i] = member.transform;
            }

            var crowd = root.AddComponent<CrowdBubbles>();
            var so = new SerializedObject(crowd);
            so.FindProperty("manager").objectReferenceValue = manager;
            so.FindProperty("bubbles").objectReferenceValue = bubbles;
            SerializedProperty list = so.FindProperty("members");
            list.arraySize = members.Length;
            for (int i = 0; i < members.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return crowd;
        }
    }
}
