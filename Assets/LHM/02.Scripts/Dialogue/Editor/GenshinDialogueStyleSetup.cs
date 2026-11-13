using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    // 원신식 대화창: 틀 없이 화면 아래를 어둡게 깔고 가운데에 이름 · 대사, RS-OS 노란 선 · 작은 버튼으로 장식
    // 하데스 · RS-OS 스타일 오브젝트는 끄기만 하고 지우지 않음 (RS-OS 스타일을 다시 적용하면 이 스타일이 꺼지고 원래 배치로 돌아감)
    public static class GenshinDialogueStyleSetup
    {
        private const string Title = "원신 스타일 대화창";
        internal const string StyleRootName = "[Genshin] Style";
        private const string ArtFolder = "Assets/LHM/09.Asset/DialogueUI";
        private const string FadePath = ArtFolder + "/genshin_fade.png";
        private const string ArrowPath = ArtFolder + "/genshin_arrow.png";
        private const string ShadowPath = ArtFolder + "/genshin_text_shadow.mat";

        // 글자 크기 (1920×1080 기준) — 밝은 배경 위에서도 읽히도록 크게 + 그림자
        private const float NameSize = 34f;
        private const float TitleSize = 19f;
        private const float BodySize = 38f;
        private const float ButtonTextSize = 16f;
        private const float HintSize = 16f;
        private const float DirectionSize = 17f;
        private const float ButtonWidth = 72f;
        private const float ButtonHeight = 32f;
        private const float ButtonGap = 8f;

        // 대화창 영역: 화면 아래 전체 폭 (원래 배치는 하데스 · RS-OS 기준 0.30~0.88 × 0.05~0.31)
        private static readonly Vector2 BoxAnchorMin = new Vector2(0f, 0f);
        private static readonly Vector2 BoxAnchorMax = new Vector2(1f, 0.36f);
        internal static readonly Vector2 DefaultAnchorMin = new Vector2(0.30f, 0.05f);
        internal static readonly Vector2 DefaultAnchorMax = new Vector2(0.88f, 0.31f);

        // 박스 위쪽에서 잰 거리 (1920×1080 기준, 박스 높이 약 389)
        private const float TopLineY = 70f;
        private const float NameRowY = TopLineY + 14f;
        private const float NameRowHeight = 48f;
        private const float NameRuleY = NameRowY + NameRowHeight + 6f;
        private const float BodyTop = NameRuleY + 20f;

        private static readonly Color Yellow = Hex("FFE500");
        private static readonly Color NameColor = Hex("F4F4F4");
        private static readonly Color BodyColor = Hex("ECECEC");
        private static readonly Color SubColor = Hex("A9ACB0");
        private static readonly Color DirectionColor = Hex("5FC8F0");
        private static readonly Color HintColor = Hex("C8CACD");
        private static readonly Color ButtonFill = new Color(0.17f, 0.18f, 0.2f, 0.9f);

        internal static bool IsApplied()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RsOsDialogueStyleSetup.PrefabPath);
            return root != null && root.GetComponentsInChildren<Transform>(true).Any(t => t.name == StyleRootName && t.gameObject.activeSelf);
        }

        internal static void Setup()
        {
            string prefabPath = RsOsDialogueStyleSetup.PrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"프리팹이 없습니다: {prefabPath}", "확인");
                return;
            }

            bool again = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponentsInChildren<Transform>(true).Any(t => t.name == StyleRootName);
            if (!EditorUtility.DisplayDialog(Title,
                    $"{prefabPath} 를 원신 스타일로 바꿉니다.\n" +
                    "- 틀 없이 화면 아래 전체를 어둡게 (그라데이션)\n" +
                    "- 위쪽 가는 노란 선 / 가운데 화자 이름 + 노란 밑줄 / 가운데 정렬 대사\n" +
                    "- 오른쪽: LOG · AUTO · SKIP 버튼 (클릭 가능), 아래: SPACE · CLICK ▼ (줄이 다 나오면 표시)\n" +
                    "- 왼쪽: 화자 칭호 + 하늘색 지문 [ ... ] (대사 줄의 direction)\n" +
                    "- 하데스 · RS-OS 스타일은 끄기만 함 (RS-OS 스타일을 다시 적용하면 되돌아감)\n" +
                    (again ? "- 이미 만든 적이 있어 지우고 다시 만듭니다\n" : "") +
                    $"- 이미지 생성: {ArtFolder} (그라데이션 · ▼)\n" +
                    "\n프리팹 파일을 바로 저장합니다 (Ctrl+Z 안 됨). 되돌리려면 git으로 프리팹을 되돌리세요.",
                    "적용", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{Title}] {prefabPath}\n");
            Sprite fade = MakeFade(log);
            Sprite arrow = MakeArrow(log);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!Apply(root, fade, arrow, log)) return;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.AppendLine("완료. Play → NPC에게 말 걸어 확인하세요. LOG · AUTO · SKIP은 마우스로 눌러도 됩니다.");
                Debug.Log(log.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Apply(GameObject root, Sprite fade, Sprite arrow, StringBuilder log)
        {
            DialogueBox dialogueBox = root.GetComponentsInChildren<DialogueBox>(true)
                .FirstOrDefault(b => new SerializedObject(b).FindProperty("motion").objectReferenceValue != null);
            if (dialogueBox == null)
            {
                EditorUtility.DisplayDialog(Title, "프리팹에서 대화창 모션이 연결된 DialogueBox를 찾지 못했습니다.\n대화 툴의 '대화창 모션 (MTP-2)'을 먼저 적용하세요.", "확인");
                return false;
            }

            var boxSo = new SerializedObject(dialogueBox);
            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            var motion = boxSo.FindProperty("motion").objectReferenceValue as DialogueBoxMotion;
            if (boxObject == null || motion == null)
            {
                EditorUtility.DisplayDialog(Title, "DialogueBox의 Box 또는 Motion 칸이 비어 있습니다.", "확인");
                return false;
            }

            var boxRect = (RectTransform)boxObject.transform;
            int layer = boxObject.layer;
            TMP_FontAsset font = FindFont(boxSo);

            // 대화창 영역을 화면 아래 전체 폭으로 (MTP 모션은 안쪽 Content를 움직이므로 바깥 크기만 바꿔도 됨)
            var motionRect = (RectTransform)motion.transform;
            motionRect.anchorMin = BoxAnchorMin;
            motionRect.anchorMax = BoxAnchorMax;
            motionRect.offsetMin = Vector2.zero;
            motionRect.offsetMax = Vector2.zero;
            log.AppendLine("- 대화창 영역: 화면 아래 전체 폭 (높이 36%)");

            Transform line = motion.transform.Find("Content/Line");
            if (line != null && line.TryGetComponent(out Image lineImage))
            {
                lineImage.enabled = false;
                log.AppendLine("- MTP 상단 굵은 선 끔 (가는 노란 선으로 대체)");
            }

            foreach (string other in new[] { RsOsDialogueStyleSetup.StyleRootName, HadesDialogueStyleSetup.StyleRootName })
            {
                Transform old = boxRect.Find(other);
                if (old == null || !old.gameObject.activeSelf) continue;
                old.gameObject.SetActive(false);
                log.AppendLine($"- '{other}' 끔 (삭제 안 함)");
            }

            if (boxObject.TryGetComponent(out Image oldBackground)) oldBackground.enabled = false;

            Transform previous = boxRect.Find(StyleRootName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            RectTransform style = CreateRect(StyleRootName, boxRect, layer);
            Stretch(style, Vector2.zero, Vector2.zero);
            style.SetSiblingIndex(0);

            // 배경: 아래 진하고 위로 갈수록 투명
            RectTransform shade = CreateRect("Shade", style, layer);
            Stretch(shade, Vector2.zero, Vector2.zero);
            AddImage(shade, fade, Color.white);

            RectTransform topLine = CreateRect("TopLine", style, layer);
            TopBand(topLine, 0f, 1f, TopLineY, 1f, 0f, 0f);
            AddImage(topLine, null, new Color(Yellow.r, Yellow.g, Yellow.b, 0.45f));

            // 화자 이름 (가운데) + 칭호 (왼쪽) + 밑줄
            RectTransform nameRect = CreateRect("Name", style, layer);
            TopBand(nameRect, 0.3f, 0.7f, NameRowY, NameRowHeight, 0f, 0f);
            TMP_Text name = AddText(nameRect, font, NameSize, NameColor, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;

            RectTransform titleRect = CreateRect("Title", style, layer);
            TopBand(titleRect, 0f, 0.22f, NameRowY, NameRowHeight, 90f, 0f);
            TMP_Text title = AddText(titleRect, font, TitleSize, SubColor, TextAlignmentOptions.MidlineLeft);
            title.characterSpacing = 6f;

            // 지문: 칭호 아래 하늘색 작은 글씨 (대사 줄에 direction이 있을 때만 보임)
            RectTransform directionRect = CreateRect("Direction", style, layer);
            TopBand(directionRect, 0f, 0.2f, NameRowY + NameRowHeight + 4f, 64f, 90f, 12f);
            TMP_Text direction = AddText(directionRect, font, DirectionSize, DirectionColor, TextAlignmentOptions.TopLeft);
            direction.textWrappingMode = TextWrappingModes.Normal;
            direction.gameObject.SetActive(false);

            RectTransform nameRule = CreateRect("NameRule", style, layer);
            TopBand(nameRule, 0.22f, 0.78f, NameRuleY, 2f, 0f, 0f);
            AddImage(nameRule, null, new Color(Yellow.r, Yellow.g, Yellow.b, 0.8f));

            // 대사 (가운데 정렬)
            RectTransform body = CreateRect("Body", style, layer);
            body.anchorMin = new Vector2(0.2f, 0f);
            body.anchorMax = new Vector2(0.8f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = new Vector2(0f, 56f);
            body.offsetMax = new Vector2(0f, -BodyTop);
            TMP_Text bodyText = AddText(body, font, BodySize, BodyColor, TextAlignmentOptions.Top);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            bodyText.lineSpacing = 8f;
            log.AppendLine("- 배경 그라데이션 / 위쪽 선 / 가운데 이름 · 밑줄 / 왼쪽 칭호 / 가운데 대사");

            // LOG · AUTO · SKIP
            RectTransform buttons = CreateRect("Buttons", style, layer);
            buttons.anchorMin = buttons.anchorMax = new Vector2(1f, 1f);
            buttons.pivot = new Vector2(1f, 0.5f);
            buttons.anchoredPosition = new Vector2(-140f, -(NameRowY + NameRowHeight * 0.5f));
            buttons.sizeDelta = new Vector2(3 * ButtonWidth + 2 * ButtonGap, ButtonHeight);
            Image logButton = AddButton(buttons, "LOG", 0, font, layer);
            Image autoButton = AddButton(buttons, "AUTO", 1, font, layer);
            Image skipButton = AddButton(buttons, "SKIP", 2, font, layer);

            // 다음 표시
            RectTransform hint = CreateRect("NextHint", style, layer);
            hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
            hint.pivot = new Vector2(1f, 0f);
            hint.anchoredPosition = new Vector2(-40f, 14f);
            hint.sizeDelta = new Vector2(260f, 30f);

            RectTransform hintText = CreateRect("Text", hint, layer);
            Stretch(hintText, Vector2.zero, new Vector2(-50f, 0f));
            TMP_Text hintLabel = AddText(hintText, font, HintSize, HintColor, TextAlignmentOptions.MidlineRight);
            hintLabel.characterSpacing = 4f;
            hintLabel.text = "SPACE · CLICK";

            RectTransform arrowRect = CreateRect("Arrow", hint, layer);
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = Vector2.zero;
            arrowRect.sizeDelta = new Vector2(28f, 18f);
            AddImage(arrowRect, arrow, Yellow);
            log.AppendLine("- LOG · AUTO · SKIP 버튼 / SPACE · CLICK ▼");

            // 밝은 배경(클로즈업 초상화 등) 위에서도 읽히도록 이름 · 칭호 · 대사 · 안내에 그림자
            Material shadow = MakeShadow(font, log);
            if (shadow != null)
            {
                foreach (TMP_Text text in new[] { name, title, direction, bodyText, hintLabel }) text.fontSharedMaterial = shadow;
            }

            var controls = style.gameObject.AddComponent<DialogueBoxControls>();
            var controlsSo = new SerializedObject(controls);
            controlsSo.FindProperty("box").objectReferenceValue = dialogueBox;
            controlsSo.FindProperty("logButton").objectReferenceValue = logButton;
            controlsSo.FindProperty("autoButton").objectReferenceValue = autoButton;
            controlsSo.FindProperty("skipButton").objectReferenceValue = skipButton;
            controlsSo.FindProperty("nextHint").objectReferenceValue = hint.gameObject;
            controlsSo.FindProperty("nextArrow").objectReferenceValue = arrowRect;
            controlsSo.FindProperty("normalColor").colorValue = ButtonFill;
            controlsSo.FindProperty("activeColor").colorValue = Yellow;
            controlsSo.ApplyModifiedPropertiesWithoutUndo();

            boxSo.FindProperty("speakerNameText").objectReferenceValue = name;
            boxSo.FindProperty("bodyText").objectReferenceValue = bodyText;
            boxSo.FindProperty("speakerTitleText").objectReferenceValue = title;
            boxSo.FindProperty("directionText").objectReferenceValue = direction;
            boxSo.FindProperty("speakerPlate").objectReferenceValue = nameRule.gameObject;
            boxSo.FindProperty("controls").objectReferenceValue = controls;
            boxSo.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("- DialogueBox 연결: 이름 / 칭호 / 지문 / 밑줄(화자 없으면 숨김) / 본문 / 화면 버튼");

            // AUTO는 버튼이 노랗게 켜지므로 상태 글자는 '빨리감기'용으로 버튼 왼쪽에 작게
            if (boxSo.FindProperty("statusText").objectReferenceValue is TMP_Text status)
            {
                var statusRect = status.rectTransform;
                statusRect.anchorMin = statusRect.anchorMax = new Vector2(1f, 1f);
                statusRect.pivot = new Vector2(1f, 0.5f);
                statusRect.anchoredPosition = new Vector2(-140f - buttons.sizeDelta.x - 16f, -(NameRowY + NameRowHeight * 0.5f));
                statusRect.sizeDelta = new Vector2(220f, ButtonHeight);
                status.fontSize = 16f;
                status.alignment = TextAlignmentOptions.MidlineRight;
                status.color = Yellow;
                status.transform.SetAsLastSibling();
                log.AppendLine("- AUTO · 빨리감기 글자: 버튼 왼쪽");
            }

            if (boxSo.FindProperty("skipConfirm").objectReferenceValue is GameObject skip)
            {
                if (skip.TryGetComponent(out Image skipImage))
                {
                    skipImage.sprite = null;
                    skipImage.color = new Color(0.05f, 0.055f, 0.06f, 0.92f);
                }
                foreach (TMP_Text text in skip.GetComponentsInChildren<TMP_Text>(true)) text.color = BodyColor;
                skip.transform.SetAsLastSibling();
                log.AppendLine("- 스킵 확인창: 어두운 판");
            }

            return true;
        }

        // RS-OS 스타일을 다시 적용할 때 호출: 이 스타일을 끄고 대화창 영역 · 상단 선을 원래대로
        internal static void Deactivate(RectTransform boxRect, SerializedObject boxSo, StringBuilder log)
        {
            Transform style = boxRect.Find(StyleRootName);
            if (style == null) return;

            style.gameObject.SetActive(false);
            boxSo.FindProperty("controls").objectReferenceValue = null;
            boxSo.FindProperty("directionText").objectReferenceValue = null;

            if (boxSo.FindProperty("motion").objectReferenceValue is DialogueBoxMotion motion)
            {
                var motionRect = (RectTransform)motion.transform;
                motionRect.anchorMin = DefaultAnchorMin;
                motionRect.anchorMax = DefaultAnchorMax;
                motionRect.offsetMin = Vector2.zero;
                motionRect.offsetMax = Vector2.zero;

                Transform line = motion.transform.Find("Content/Line");
                if (line != null && line.TryGetComponent(out Image lineImage)) lineImage.enabled = true;
            }

            log.AppendLine("- 원신 스타일 끔 (삭제 안 함), 대화창 영역 · 상단 선 원래대로");
        }

        // 폰트 기본 머티리얼을 복사해 아래 그림자(Underlay)를 켠 머티리얼 — 폰트 원본은 건드리지 않음
        private static Material MakeShadow(TMP_FontAsset font, StringBuilder log)
        {
            if (font == null || font.material == null)
            {
                log.AppendLine("- 주의: 폰트 머티리얼이 없어 글자 그림자를 넣지 못함");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowPath);
            bool created = material == null;
            if (created) material = new Material(font.material);
            else
            {
                // 폰트가 바뀌었을 수 있어 아틀라스 · 셰이더를 다시 맞춤
                material.shader = font.material.shader;
                material.CopyPropertiesFromMaterial(font.material);
            }

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.9f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.45f);

            Directory.CreateDirectory(Path.GetDirectoryName(ShadowPath));
            if (created) AssetDatabase.CreateAsset(material, ShadowPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            log.AppendLine($"- 글자 그림자 머티리얼: {ShadowPath}");
            return material;
        }

        // ── 이미지 생성 ─────────────────────────────

        private static Sprite MakeFade(StringBuilder log)
        {
            const int width = 4;
            const int height = 256;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (height - 1f);
                // 아래 55%(대사 · 이름 자리)는 고르게 진하고, 그 위로 부드럽게 사라짐
                float alpha = Mathf.Lerp(0.93f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t)));
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                for (int x = 0; x < width; x++) pixels[y * width + x] = new Color32(8, 9, 11, a);
            }
            return Save(FadePath, width, height, pixels, log);
        }

        private static Sprite MakeArrow(StringBuilder log)
        {
            const int width = 48;
            const int height = 30;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 아래를 가리키는 삼각형: 위쪽 변이 넓고 아래 꼭짓점 (가장자리 1px 부드럽게)
                    float half = (y + 0.5f) / height * (width * 0.5f);
                    float edge = half - Mathf.Abs(x + 0.5f - width * 0.5f);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(edge) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            return Save(ArrowPath, width, height, pixels, log);
        }

        private static Sprite Save(string path, int width, int height, Color32[] pixels, StringBuilder log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            log.AppendLine($"- 이미지: {path}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ── 도우미 ─────────────────────────────

        private static Image AddButton(RectTransform parent, string label, int index, TMP_FontAsset font, int layer)
        {
            RectTransform rect = CreateRect(label, parent, layer);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(index * (ButtonWidth + ButtonGap), 0f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            Image image = AddImage(rect, null, ButtonFill);

            RectTransform text = CreateRect("Label", rect, layer);
            Stretch(text, Vector2.zero, Vector2.zero);
            TMP_Text caption = AddText(text, font, ButtonTextSize, Hex("D9DADB"), TextAlignmentOptions.Center);
            caption.fontStyle = FontStyles.Bold;
            caption.characterSpacing = 2f;
            caption.text = label;
            return image;
        }

        // 위쪽에서 y만큼 내려온 가로 띠 (가로는 앵커 비율 + 좌우 여백)
        private static void TopBand(RectTransform rect, float xMin, float xMax, float y, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(xMin, 1f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -y - height);
            rect.offsetMax = new Vector2(-right, -y);
        }

        private static TMP_FontAsset FindFont(SerializedObject boxSo)
        {
            foreach (string field in new[] { "bodyText", "speakerNameText", "statusText" })
            {
                if (boxSo.FindProperty(field).objectReferenceValue is TMP_Text text && text.font != null) return text.font;
            }
            return TMP_Settings.defaultFontAsset;
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
