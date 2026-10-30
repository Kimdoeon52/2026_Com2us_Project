using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class Step09_SceneSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string DataFolder = "Assets/LHM/03.Data/Dialogue/";
        private const string UndoName = "9단계 배치";
        private const string FieldRootName = "[Step09] Field";
        private const string ManagerName = "[Step09] DialogueManager";

        private struct NpcSpec
        {
            public string name;
            public NpcRole role;
            public string poolId;
            public string speakerId;
            public Vector3 position;
        }

        private static readonly NpcSpec[] Npcs =
        {
            new NpcSpec { name = "NPC_길드접수원", role = NpcRole.GuildReceptionist, poolId = "guild_npc", speakerId = "receptionist", position = new Vector3(-6f, -3.5f, 0f) },
            new NpcSpec { name = "NPC_고물상아저씨", role = NpcRole.JunkDealer, poolId = "junk_npc", speakerId = "junkdealer", position = new Vector3(0f, -3.5f, 0f) },
            new NpcSpec { name = "NPC_경매장", role = NpcRole.Other, poolId = "auction_npc", speakerId = "auctioneer", position = new Vector3(6f, -3.5f, 0f) },
        };

        [MenuItem("Tools/Dialogue/9단계 배치")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬에 9단계 오브젝트를 배치합니다.\n씬은 자동 저장하지 않습니다. 결과를 보고 Ctrl+S로 저장하세요.\n(Ctrl+Z로 한 번에 되돌릴 수 있습니다)", "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            var log = new StringBuilder($"[Step09 배치] {scene.path}\n");

            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[Step09 배치] 씬에 Canvas가 없습니다. 중단합니다.");
                return;
            }

            DialogueBox box = FindDialogueBox(log);
            if (box == null)
            {
                Debug.LogError("[Step09 배치] 씬에 DialogueBox가 없습니다. 중단합니다.");
                return;
            }

            TMP_FontAsset font = FindFont(log);

            DisableStep06(log);
            DialogueManager manager = SetupManager(box, log);
            NpcChoiceMenu menu = SetupMenu(canvas, manager, font, log);
            TMP_Text prompt = SetupPrompt(canvas, font, log);
            SetupField(manager, menu, prompt, log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);

            log.AppendLine("완료. 위치·크기를 다듬은 뒤 Ctrl+S로 저장하고 Play로 확인하세요.");
            Debug.Log(log.ToString());
        }

        private static Canvas FindCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return canvases.FirstOrDefault(c => c.isRootCanvas && c.name == "DialogueCanvas") ?? canvases.FirstOrDefault(c => c.isRootCanvas);
        }

        private static DialogueBox FindDialogueBox(StringBuilder log)
        {
            var step06 = Object.FindFirstObjectByType<Step06_DialogueTest>(FindObjectsInactive.Include);
            if (step06 != null)
            {
                var so = new SerializedObject(step06);
                if (so.FindProperty("dialogueBox").objectReferenceValue is DialogueBox fromStep06)
                {
                    log.AppendLine($"- DialogueBox: Step06이 쓰던 '{fromStep06.name}' 사용");
                    return fromStep06;
                }
            }

            DialogueBox[] boxes = Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (boxes.Length > 1) log.AppendLine($"- 주의: DialogueBox가 {boxes.Length}개 있습니다 ({string.Join(", ", boxes.Select(b => b.name))}). '{boxes[0].name}' 사용");
            return boxes.FirstOrDefault();
        }

        private static TMP_FontAsset FindFont(StringBuilder log)
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            TMP_Text source = texts.FirstOrDefault(t => t.name == "SpeakerName" && t.font != null) ?? texts.FirstOrDefault(t => t.font != null);

            if (source == null)
            {
                log.AppendLine("- 주의: 한글 폰트를 찾지 못했습니다. 새 텍스트의 Font Asset을 직접 넣어 주세요");
                return null;
            }

            log.AppendLine($"- 폰트: '{source.name}'의 {source.font.name} 사용");
            return source.font;
        }

        private static void DisableStep06(StringBuilder log)
        {
            foreach (var step06 in Object.FindObjectsByType<Step06_DialogueTest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!step06.enabled) continue;
                Undo.RecordObject(step06, UndoName);
                step06.enabled = false;
                log.AppendLine($"- Step06_DialogueTest ('{step06.name}') 비활성화 (삭제 안 함, F키·세이브 중복 방지)");
            }
        }

        private static DialogueManager SetupManager(DialogueBox box, StringBuilder log)
        {
            DialogueManager manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                log.AppendLine($"- DialogueManager: 기존 '{manager.name}' 사용 (설정 변경 안 함)");
            }
            else
            {
                var go = new GameObject(ManagerName);
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                manager = go.AddComponent<DialogueManager>();

                var so = new SerializedObject(manager);
                so.FindProperty("dialogueBox").objectReferenceValue = box;
                so.FindProperty("stringTableJson").objectReferenceValue = LoadData("strings_ko.json", log);
                so.FindProperty("variablesJson").objectReferenceValue = LoadData("variables.json", log);

                List<TextAsset> pools = Npcs.Select(n => LoadData(n.poolId + ".json", log)).Where(a => a != null).ToList();
                SerializedProperty poolsProp = so.FindProperty("poolJsons");
                poolsProp.arraySize = pools.Count;
                for (int i = 0; i < pools.Count; i++) poolsProp.GetArrayElementAtIndex(i).objectReferenceValue = pools[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                log.AppendLine($"- DialogueManager 생성 (대사 풀 {pools.Count}개: {string.Join(", ", pools.Select(p => p.name))})");
            }

            if (Object.FindFirstObjectByType<Step09_DialogueDebug>(FindObjectsInactive.Include) == null)
            {
                var debug = Undo.AddComponent<Step09_DialogueDebug>(manager.gameObject);
                var so = new SerializedObject(debug);
                so.FindProperty("manager").objectReferenceValue = manager;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("- Step09_DialogueDebug 추가 (F키 디버그)");
            }

            return manager;
        }

        private static TextAsset LoadData(string fileName, StringBuilder log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + fileName);
            if (asset == null) log.AppendLine($"- 주의: {DataFolder}{fileName} 을 찾지 못했습니다");
            return asset;
        }

        private static NpcChoiceMenu SetupMenu(Canvas canvas, DialogueManager manager, TMP_FontAsset font, StringBuilder log)
        {
            NpcChoiceMenu existing = Object.FindFirstObjectByType<NpcChoiceMenu>(FindObjectsInactive.Include);
            if (existing != null)
            {
                log.AppendLine($"- NpcChoiceMenu: 기존 '{existing.name}' 사용 (설정 변경 안 함)");
                return existing;
            }

            Transform rootTransform = canvas.transform.Find("NpcMenu");
            RectTransform root;
            bool reusedRoot = rootTransform != null && rootTransform.childCount == 0;
            if (reusedRoot)
            {
                root = (RectTransform)rootTransform;
                Undo.RecordObject(root, UndoName);
                log.AppendLine("- 기존 빈 오브젝트 'NpcMenu' 사용");
            }
            else
            {
                root = CreateUi(rootTransform != null ? "NpcMenu_Step09" : "NpcMenu", canvas.transform);
                Undo.RegisterCreatedObjectUndo(root.gameObject, UndoName);
            }
            Stretch(root);

            RectTransform panel = CreateUi("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(560f, 0f);
            panel.sizeDelta = new Vector2(380f, 0f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text title = CreateText("Title", panel, font, 32f, TextAlignmentOptions.Left, "NPC 이름");
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            RectTransform option = CreateUi("OptionTemplate", panel);
            var optionImage = option.gameObject.AddComponent<Image>();
            optionImage.color = new Color(1f, 1f, 1f, 0.08f);
            var button = option.gameObject.AddComponent<Button>();
            button.targetGraphic = optionImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            option.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            TMP_Text optionLabel = CreateText("Label", option, font, 28f, TextAlignmentOptions.Left, "선택지");
            Stretch(optionLabel.rectTransform);
            optionLabel.rectTransform.offsetMin = new Vector2(16f, 0f);
            optionLabel.rectTransform.offsetMax = new Vector2(-16f, 0f);

            RectTransform shop = CreateUi("ShopPanel", root);
            shop.anchorMin = shop.anchorMax = new Vector2(0.5f, 0.5f);
            shop.pivot = new Vector2(0.5f, 0.5f);
            shop.anchoredPosition = Vector2.zero;
            shop.sizeDelta = new Vector2(800f, 480f);
            shop.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            TMP_Text shopText = CreateText("Text", shop, font, 36f, TextAlignmentOptions.Center, "상점 (임시)\nEsc 또는 E로 닫기");
            Stretch(shopText.rectTransform);

            var menu = Undo.AddComponent<NpcChoiceMenu>(root.gameObject);
            var so = new SerializedObject(menu);
            so.FindProperty("manager").objectReferenceValue = manager;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("optionTemplate").objectReferenceValue = button;
            so.FindProperty("shopPanel").objectReferenceValue = shop.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
            shop.gameObject.SetActive(false);

            if (reusedRoot)
            {
                Undo.RegisterCreatedObjectUndo(panel.gameObject, UndoName);
                Undo.RegisterCreatedObjectUndo(shop.gameObject, UndoName);
            }

            log.AppendLine($"- NpcChoiceMenu 구성 ('{root.name}' 아래 Panel / Title / OptionTemplate / ShopPanel)");
            return menu;
        }

        private static TMP_Text SetupPrompt(Canvas canvas, TMP_FontAsset font, StringBuilder log)
        {
            Transform existing = canvas.transform.Find("InteractPrompt");
            if (existing != null && existing.TryGetComponent(out TMP_Text text))
            {
                log.AppendLine("- 기존 'InteractPrompt' 사용 (설정 변경 안 함)");
                return text;
            }

            RectTransform rect = CreateUi("InteractPrompt", canvas.transform);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, UndoName);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(400f, 50f);
            TMP_Text prompt = AddText(rect.gameObject, font, 30f, TextAlignmentOptions.Center, "[E] 이름");
            rect.gameObject.SetActive(false);
            log.AppendLine("- InteractPrompt 생성");
            return prompt;
        }

        private static void SetupField(DialogueManager manager, NpcChoiceMenu menu, TMP_Text prompt, StringBuilder log)
        {
            if (Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include) != null ||
                Object.FindFirstObjectByType<NpcInteractable>(FindObjectsInactive.Include) != null)
            {
                log.AppendLine("- 필드: 이미 PlayerInteractor 또는 NpcInteractable이 있어 건너뜀");
                return;
            }

            var fieldRoot = new GameObject(FieldRootName);
            Undo.RegisterCreatedObjectUndo(fieldRoot, UndoName);

            foreach (NpcSpec spec in Npcs)
            {
                GameObject npc = CreatePrimitive(PrimitiveType.Capsule, spec.name, fieldRoot.transform, spec.position);
                var interactable = npc.AddComponent<NpcInteractable>();
                var so = new SerializedObject(interactable);
                so.FindProperty("role").enumValueIndex = (int)spec.role;
                so.FindProperty("poolId").stringValue = spec.poolId;
                so.FindProperty("speakerId").stringValue = spec.speakerId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject player = CreatePrimitive(PrimitiveType.Sphere, "Player", fieldRoot.transform, new Vector3(-3f, -3.5f, 0f));
            var interactor = player.AddComponent<PlayerInteractor>();
            var interactorSo = new SerializedObject(interactor);
            interactorSo.FindProperty("manager").objectReferenceValue = manager;
            interactorSo.FindProperty("menu").objectReferenceValue = menu;
            interactorSo.FindProperty("prompt").objectReferenceValue = prompt;
            interactorSo.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            interactorSo.ApplyModifiedPropertiesWithoutUndo();

            var mover = player.AddComponent<Step09_TestMover>();
            var moverSo = new SerializedObject(mover);
            moverSo.FindProperty("interactor").objectReferenceValue = interactor;
            moverSo.FindProperty("plane").enumValueIndex = (int)TestMovePlane.XY;
            moverSo.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"- 필드 생성 ('{FieldRootName}' 아래 NPC {Npcs.Length}명 + Player). 이동 평면 XY (카메라 정면 기준)");
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        private static RectTransform CreateUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, string text)
        {
            return AddText(CreateUi(name, parent).gameObject, font, size, alignment, text);
        }

        private static TMP_Text AddText(GameObject go, TMP_FontAsset font, float size, TextAlignmentOptions alignment, string text)
        {
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
