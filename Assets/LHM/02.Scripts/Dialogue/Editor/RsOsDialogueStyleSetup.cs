using System.Linq;
using System.Text;
using Michsky.UI.MTP;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class RsOsDialogueStyleSetup
    {
        private const string Title = "RS-OS 스타일 대화창";
        internal const string PrefabPath = "Assets/LHM/05.Prefab/Dialogue/DialogueCanvas.prefab";
        internal const string StyleRootName = "[RS-OS] Style";

        private const string SpriteRoot = "Assets/SYS/UI/BG/";
        private const string PanelPath = SpriteRoot + "Panel/bg_panelWide_chamfer.png";
        private const string PlatePath = SpriteRoot + "Button/button/bg_button_primary_enabled.png";
        private const string OptionPath = SpriteRoot + "Button/miniButton/bg_miniButton_default_enabled.png";

        private const float PanelCut = 2.6f;
        private const float PanelInset = 10f;
        private const float PlateHeight = 46f;
        private const float PlateWidth = 230f;
        private const float PlateTop = 26f;
        private const float BracketLength = 26f;
        private const float BracketThickness = 3f;

        private static readonly Color Yellow = Hex("FFE500");
        private static readonly Color Edge = Hex("3A3D41");
        private static readonly Color BodyColor = Hex("E8E9EA");
        private static readonly Color SubColor = Hex("9A9DA1");
        private static readonly Color DimColor = Hex("5E6166");
        private static readonly Color KeyColor = Hex("D9DADB");
        private static readonly Color Ink = Hex("111214");

        internal static bool IsApplied()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return root != null && root.GetComponentsInChildren<Transform>(true).Any(t => t.name == StyleRootName && t.gameObject.activeSelf);
        }

        internal static void Setup()
        {
            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            var plate = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
            var option = AssetDatabase.LoadAssetAtPath<Sprite>(OptionPath);
            var missing = new[] { (PanelPath, panel), (PlatePath, plate), (OptionPath, option) }.Where(p => p.Item2 == null).Select(p => p.Item1).ToList();
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog(Title, "스프라이트를 찾지 못했습니다 (Single 모드 스프라이트여야 함):\n" + string.Join("\n", missing), "확인");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"프리팹이 없습니다: {PrefabPath}", "확인");
                return;
            }

            bool again = IsApplied();
            if (!EditorUtility.DisplayDialog(Title,
                    $"{PrefabPath} 를 RS-OS 스타일로 바꿉니다.\n" +
                    "- 대화창: 어두운 깎인 모서리 패널 + 노란 이름표 + 노란 모서리 괄호\n" +
                    "- NPC 선택지 · 스킵 확인 · 백로그 패널도 같은 톤으로\n" +
                    "- 하데스 스타일 오브젝트는 끄기만 하고 지우지 않음\n" +
                    (again ? "- 이미 적용되어 있어 지우고 다시 만듭니다\n" : "") +
                    "\n프리팹 파일을 바로 저장합니다 (Ctrl+Z 안 됨). 되돌리려면 git으로 프리팹을 되돌리세요.\n" +
                    "주의: 대화 툴의 '프리팹 만들기'를 다시 누르면 이 변경이 덮어써집니다.",
                    "적용", "취소"))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var log = new StringBuilder($"[RS-OS 스타일] {PrefabPath}\n");
            try
            {
                if (!Apply(root, panel, plate, option, log)) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.AppendLine("완료. DialogueTest_Prefab 씬에서 Play → NPC에게 말 걸어 확인하세요.");
                Debug.Log(log.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Apply(GameObject root, Sprite panel, Sprite plate, Sprite option, StringBuilder log)
        {
            DialogueBox dialogueBox = FindLiveDialogueBox(root);
            if (dialogueBox == null)
            {
                EditorUtility.DisplayDialog(Title, "프리팹에서 사용 중인 DialogueBox를 찾지 못했습니다.", "확인");
                return false;
            }

            var boxSo = new SerializedObject(dialogueBox);
            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            if (boxObject == null)
            {
                EditorUtility.DisplayDialog(Title, "DialogueBox의 Box 칸이 비어 있습니다.", "확인");
                return false;
            }

            var boxRect = (RectTransform)boxObject.transform;
            int layer = boxObject.layer;
            TMP_FontAsset font = FindFont(boxSo);

            Transform previous = boxRect.Find(StyleRootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            Transform hades = boxRect.Find(HadesDialogueStyleSetup.StyleRootName);
            if (hades != null && hades.gameObject.activeSelf)
            {
                hades.gameObject.SetActive(false);
                log.AppendLine("- 하데스 스타일 끔 (삭제 안 함)");
            }

            GenshinDialogueStyleSetup.Deactivate(boxRect, boxSo, log);

            var oldBackground = boxObject.GetComponent<Image>();
            if (oldBackground != null) oldBackground.enabled = false;

            RectTransform style = CreateRect(StyleRootName, boxRect, layer);
            Stretch(style, Vector2.zero, Vector2.zero);
            style.SetSiblingIndex(0);

            BuildPanel(style, panel, layer);
            BuildBrackets(style, layer);

            RectTransform stripe = CreateRect("AccentStripe", style, layer);
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.offsetMin = new Vector2(PanelInset + 14f, PanelInset + 30f);
            stripe.offsetMax = new Vector2(PanelInset + 19f, -PanelInset - 30f);
            AddImage(stripe, null, Yellow);

            float left = PanelInset + 34f;
            RectTransform plateRect = CreateRect("Nameplate", style, layer);
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 1f);
            plateRect.anchoredPosition = new Vector2(left, -PlateTop);
            plateRect.sizeDelta = new Vector2(PlateWidth, PlateHeight);
            Image plateImage = AddImage(plateRect, plate, Color.white);
            plateImage.type = Image.Type.Sliced;
            plateImage.pixelsPerUnitMultiplier = 1.6f;

            RectTransform nameRect = CreateRect("Name", plateRect, layer);
            Stretch(nameRect, new Vector2(18f, 2f), new Vector2(-24f, -2f));
            TMP_Text name = AddText(nameRect, font, 28f, Ink, TextAlignmentOptions.MidlineLeft);
            name.fontStyle = FontStyles.Bold;
            name.enableAutoSizing = true;
            name.fontSizeMin = 18f;
            name.fontSizeMax = 28f;

            RectTransform titleRect = CreateRect("Title", plateRect, layer);
            titleRect.anchorMin = new Vector2(1f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(16f, 0f);
            titleRect.sizeDelta = new Vector2(460f, 0f);
            TMP_Text title = AddText(titleRect, font, 20f, SubColor, TextAlignmentOptions.MidlineLeft);
            title.characterSpacing = 4f;

            RectTransform tag = CreateRect("SystemTag", style, layer);
            tag.anchorMin = tag.anchorMax = new Vector2(1f, 1f);
            tag.pivot = new Vector2(1f, 1f);
            tag.anchoredPosition = new Vector2(-PanelInset - 34f, -PlateTop);
            tag.sizeDelta = new Vector2(320f, PlateHeight);
            TMP_Text tagText = AddText(tag, font, 15f, DimColor, TextAlignmentOptions.MidlineRight);
            tagText.characterSpacing = 8f;
            tagText.text = "RS-OS  //  DIALOGUE  <color=#FFE500>////</color>";

            float ruleY = PlateTop + PlateHeight + 12f;
            RectTransform rule = CreateRect("Rule", style, layer);
            rule.anchorMin = new Vector2(0f, 1f);
            rule.anchorMax = new Vector2(1f, 1f);
            rule.pivot = new Vector2(0.5f, 1f);
            rule.offsetMin = new Vector2(left, -ruleY - 1f);
            rule.offsetMax = new Vector2(-PanelInset - 34f, -ruleY);
            AddImage(rule, null, Edge);

            RectTransform body = CreateRect("Body", style, layer);
            Stretch(body, new Vector2(left, PanelInset + 46f), new Vector2(-PanelInset - 34f, -ruleY - 16f));
            TMP_Text bodyText = AddText(body, font, 29f, BodyColor, TextAlignmentOptions.TopLeft);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 10f;

            BuildKeyHint(style, font, layer);
            log.AppendLine("- 대화창: 패널(bg_panelWide_chamfer) / 모서리 괄호 / 노란 띠 / 이름표(bg_button_primary) / 칭호 / 본문 / 키 안내");

            boxSo.FindProperty("speakerNameText").objectReferenceValue = name;
            boxSo.FindProperty("bodyText").objectReferenceValue = bodyText;
            boxSo.FindProperty("speakerTitleText").objectReferenceValue = title;
            boxSo.FindProperty("speakerPlate").objectReferenceValue = plateRect.gameObject;
            boxSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("- DialogueBox 연결: 이름 / 칭호 / 이름표 / 본문");

            if (boxSo.FindProperty("statusText").objectReferenceValue is TMP_Text status)
            {
                status.color = Yellow;
                log.AppendLine("- AUTO · 빨리감기 표시: 노란색");
            }

            if (boxSo.FindProperty("skipConfirm").objectReferenceValue is GameObject skip)
            {
                StylePanel(skip.GetComponent<Image>(), panel);
                foreach (TMP_Text text in skip.GetComponentsInChildren<TMP_Text>(true)) text.color = BodyColor;
                log.AppendLine("- 스킵 확인창: 같은 패널");
            }

            if (boxSo.FindProperty("motion").objectReferenceValue is DialogueBoxMotion motion)
            {
                Transform line = motion.transform.Find("Content/Line");
                if (line != null)
                {
                    var lineImage = line.GetComponent<Image>();
                    if (lineImage != null) lineImage.color = Yellow;
                    var gradient = line.GetComponent<UIGradient>();
                    if (gradient != null) gradient.enabled = false;
                    log.AppendLine("- MTP-2 상단 선: 노란색");
                }
            }

            StyleNpcMenu(root, panel, option, log);
            StyleBacklog(root, panel, log);
            return true;
        }

        private static DialogueBox FindLiveDialogueBox(GameObject root)
        {
            DialogueBox[] boxes = root.GetComponentsInChildren<DialogueBox>(true);
            return boxes.FirstOrDefault(b => new SerializedObject(b).FindProperty("motion").objectReferenceValue != null)
                   ?? boxes.FirstOrDefault(b => new SerializedObject(b).FindProperty("speakerPlate").objectReferenceValue != null)
                   ?? boxes.FirstOrDefault();
        }

        private static TMP_FontAsset FindFont(SerializedObject boxSo)
        {
            foreach (string field in new[] { "bodyText", "speakerNameText", "statusText" })
            {
                if (boxSo.FindProperty(field).objectReferenceValue is TMP_Text text && text.font != null) return text.font;
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static void BuildPanel(RectTransform parent, Sprite panel, int layer)
        {
            RectTransform edge = CreateRect("Edge", parent, layer);
            Stretch(edge, Vector2.one * PanelInset, -Vector2.one * PanelInset);
            Image edgeImage = AddImage(edge, panel, Edge);
            edgeImage.type = Image.Type.Sliced;
            edgeImage.pixelsPerUnitMultiplier = PanelCut;

            RectTransform fill = CreateRect("Panel", parent, layer);
            Stretch(fill, Vector2.one * (PanelInset + 2f), -Vector2.one * (PanelInset + 2f));
            Image fillImage = AddImage(fill, panel, new Color(1f, 1f, 1f, 0.97f));
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = PanelCut;
        }

        private static void BuildBrackets(RectTransform parent, int layer)
        {
            RectTransform group = CreateRect("Brackets", parent, layer);
            Stretch(group, Vector2.zero, Vector2.zero);
            foreach (Vector2 corner in new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) })
            {
                AddBracketBar(group, corner, new Vector2(BracketLength, BracketThickness), layer);
                AddBracketBar(group, corner, new Vector2(BracketThickness, BracketLength), layer);
            }
        }

        private static void AddBracketBar(RectTransform parent, Vector2 corner, Vector2 size, int layer)
        {
            RectTransform bar = CreateRect("Bar", parent, layer);
            bar.anchorMin = bar.anchorMax = corner;
            bar.pivot = corner;
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = size;
            AddImage(bar, null, Yellow);
        }

        private static void BuildKeyHint(RectTransform parent, TMP_FontAsset font, int layer)
        {
            RectTransform hint = CreateRect("KeyHint", parent, layer);
            hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
            hint.pivot = new Vector2(1f, 0f);
            hint.anchoredPosition = new Vector2(-PanelInset - 34f, PanelInset + 14f);
            hint.sizeDelta = new Vector2(170f, 28f);

            RectTransform key = CreateRect("Key", hint, layer);
            key.anchorMin = new Vector2(0f, 0f);
            key.anchorMax = new Vector2(0f, 1f);
            key.pivot = new Vector2(0f, 0.5f);
            key.anchoredPosition = Vector2.zero;
            key.sizeDelta = new Vector2(78f, 0f);
            AddImage(key, null, KeyColor);

            RectTransform keyLabel = CreateRect("Label", key, layer);
            Stretch(keyLabel, Vector2.zero, Vector2.zero);
            TMP_Text keyText = AddText(keyLabel, font, 15f, Ink, TextAlignmentOptions.Center);
            keyText.fontStyle = FontStyles.Bold;
            keyText.text = "SPACE";

            RectTransform label = CreateRect("Text", hint, layer);
            Stretch(label, new Vector2(88f, 0f), Vector2.zero);
            TMP_Text labelText = AddText(label, font, 18f, SubColor, TextAlignmentOptions.MidlineLeft);
            labelText.text = "다음";
        }

        private static void StyleNpcMenu(GameObject root, Sprite panel, Sprite option, StringBuilder log)
        {
            var menu = root.GetComponentInChildren<NpcChoiceMenu>(true);
            if (menu == null) return;

            var so = new SerializedObject(menu);
            if (so.FindProperty("panel").objectReferenceValue is GameObject menuPanel) StylePanel(menuPanel.GetComponent<Image>(), panel);
            if (so.FindProperty("titleText").objectReferenceValue is TMP_Text title) title.color = Yellow;
            if (so.FindProperty("optionTemplate").objectReferenceValue is Button template)
            {
                if (template.targetGraphic is Image image)
                {
                    image.sprite = option;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 1.4f;
                    image.color = Color.white;
                }
            }
            so.FindProperty("normalColor").colorValue = BodyColor;
            so.FindProperty("selectedColor").colorValue = Yellow;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("- NPC 선택지: 패널 / 항목(bg_miniButton_default) / 선택 색 노란색");
        }

        private static void StyleBacklog(GameObject root, Sprite panel, StringBuilder log)
        {
            var backlog = root.GetComponentInChildren<DialogueBacklog>(true);
            if (backlog == null) return;

            Transform backlogPanel = backlog.transform.Find("Panel");
            if (backlogPanel == null) return;
            StylePanel(backlogPanel.GetComponent<Image>(), panel);
            Transform title = backlogPanel.Find("Title");
            if (title != null && title.TryGetComponent(out TMP_Text titleText)) titleText.color = Yellow;
            log.AppendLine("- 백로그: 같은 패널, 제목 노란색");
        }

        private static void StylePanel(Image image, Sprite panel)
        {
            if (image == null) return;
            image.sprite = panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PanelCut;
            image.color = new Color(1f, 1f, 1f, 0.97f);
        }

        private static RectTransform CreateRect(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = layer;
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

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text AddText(RectTransform rect, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
        }
    }
}
