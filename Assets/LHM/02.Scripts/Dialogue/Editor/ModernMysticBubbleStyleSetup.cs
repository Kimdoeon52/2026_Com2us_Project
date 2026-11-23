using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class ModernMysticBubbleStyleSetup
    {
        private const string Title = "Modern-Mystic 말풍선";
        internal const string StyleRootName = "[MM] Style";
        private const string PrefabPath = RsOsDialogueStyleSetup.PrefabPath;
        private const string SourceFolder = "Assets/Sprites/UI/Modern-Mystic";
        private const string ArtFolder = "Assets/LHM/09.Asset/BubbleUI";

        private const string FillSource = SourceFolder + "/message conatiner.png";
        private const string FrameSource = SourceFolder + "/containers/type=v1.png";
        private const string FillPath = ArtFolder + "/mm_bubble_fill.png";
        private const string FramePath = ArtFolder + "/mm_bubble_frame.png";
        private const int FillBorder = 32;
        private const int FrameBorder = 30;

        private const float FillCorner = 2.5f;
        private const float FrameCorner = 2.2f;
        private const float FrameWidth = 2f;
        private const float TailOuter = 16f;
        private const float TailInner = 12f;
        private const string PromptName = "InteractPrompt";
        private const float PromptPadX = 18f;
        private const float PromptPadY = 3f;

        private static readonly Color Cream = Hex("FFF6D9");
        private static readonly Color FillTint = new Color(1f, 1f, 1f, 0.96f);

        internal static bool IsApplied()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return root != null && root.GetComponentsInChildren<BubbleView>(true).Any(v => v.transform.Find(StyleRootName) != null);
        }

        internal static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"프리팹이 없습니다: {PrefabPath}", "확인");
                return;
            }

            if (!File.Exists(FillSource) || !File.Exists(FrameSource))
            {
                EditorUtility.DisplayDialog(Title, $"Modern-Mystic 이미지를 찾지 못했습니다.\n- {FillSource}\n- {FrameSource}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title,
                    $"{PrefabPath} 의 말풍선(BubbleView)을 Modern-Mystic 스타일로 바꿉니다.\n" +
                    "- 남색 둥근 박스 + 크림색 테두리 + 아래쪽 꼬리\n" +
                    "- 글자색 크림\n" +
                    $"- NPC 이름표({PromptName}): 같은 남색 판 + 크림 테두리를 글자 뒤에 깔고, 판 폭은 이름 길이에 맞춤\n" +
                    $"- 이미지는 {ArtFolder} 로 복사해서 씀 (원본 Modern-Mystic 폴더는 수정 안 함)\n" +
                    (IsApplied() ? "- 이미 적용되어 있어 지우고 다시 만듭니다\n" : "") +
                    "\n프리팹 파일을 바로 저장합니다 (Ctrl+Z 안 됨). 되돌리려면 git으로 프리팹을 되돌리세요.",
                    "적용", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{Title}] {PrefabPath}\n");
            Sprite fill = CopySprite(FillSource, FillPath, FillBorder, log);
            Sprite frame = CopySprite(FrameSource, FramePath, FrameBorder, log);
            if (fill == null || frame == null)
            {
                EditorUtility.DisplayDialog(Title, "이미지 복사 · 가져오기에 실패했습니다. 콘솔을 확인하세요.", "확인");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BubbleView[] views = root.GetComponentsInChildren<BubbleView>(true);
                if (views.Length == 0)
                {
                    EditorUtility.DisplayDialog(Title, "프리팹에서 BubbleView를 찾지 못했습니다.\n대화 툴의 '비강제 말풍선 (15단계)'을 먼저 적용하세요.", "확인");
                    return;
                }

                foreach (BubbleView view in views) Apply(view, fill, frame, log);

                Transform prompt = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == PromptName);
                if (prompt != null && prompt.TryGetComponent(out TMP_Text promptText)) ApplyPrompt(promptText, fill, frame, log);
                else log.AppendLine($"- '{PromptName}'을 찾지 못해 이름표는 건너뜀");

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                log.AppendLine("완료. Play → NPC 근처에서 혼잣말 말풍선 · [E] 이름표를 확인하세요.");
                Debug.Log(log.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Apply(BubbleView view, Sprite fill, Sprite frame, StringBuilder log)
        {
            var bubble = (RectTransform)view.transform;
            int layer = bubble.gameObject.layer;

            Transform old = bubble.Find(StyleRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            if (bubble.TryGetComponent(out Image baseImage)) baseImage.enabled = false;

            RectTransform style = CreateRect(StyleRootName, bubble, layer);
            Stretch(style, Vector2.zero, Vector2.zero);
            style.SetAsFirstSibling();

            RectTransform tailOuter = CreateRect("TailOuter", style, layer);
            Tail(tailOuter, TailOuter, -FrameWidth);
            AddImage(tailOuter, frame, Color.white, Image.Type.Sliced, FrameCorner * 3f);

            RectTransform border = CreateRect("Frame", style, layer);
            Stretch(border, new Vector2(-FrameWidth, -FrameWidth), new Vector2(FrameWidth, FrameWidth));
            AddImage(border, frame, Color.white, Image.Type.Sliced, FrameCorner);

            RectTransform body = CreateRect("Fill", style, layer);
            Stretch(body, Vector2.zero, Vector2.zero);
            AddImage(body, fill, FillTint, Image.Type.Sliced, FillCorner);

            RectTransform tailInner = CreateRect("TailInner", style, layer);
            Tail(tailInner, TailInner, 0f);
            AddImage(tailInner, fill, Color.white, Image.Type.Simple, 1f);

            var so = new SerializedObject(view);
            if (so.FindProperty("text").objectReferenceValue is TMP_Text text)
            {
                text.color = Cream;
                text.transform.SetAsLastSibling();
                EditorUtility.SetDirty(text);
            }

            log.AppendLine($"- {bubble.name}: 테두리 · 박스 · 꼬리 생성, 글자색 크림");
        }

        private static void ApplyPrompt(TMP_Text text, Sprite fill, Sprite frame, StringBuilder log)
        {
            var prompt = (RectTransform)text.transform;
            int layer = prompt.gameObject.layer;

            Transform old = prompt.Find(StyleRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            if (!prompt.TryGetComponent(out ContentSizeFitter fitter)) fitter = prompt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Cream;

            RectTransform plate = CreateRect(StyleRootName, prompt, layer);
            Stretch(plate, new Vector2(-PromptPadX, PromptPadY), new Vector2(PromptPadX, -PromptPadY));

            Canvas parentCanvas = prompt.GetComponentInParent<Canvas>(true);
            var canvas = plate.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerID = parentCanvas != null ? parentCanvas.sortingLayerID : 0;
            canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) - 1;

            RectTransform border = CreateRect("Frame", plate, layer);
            Stretch(border, new Vector2(-FrameWidth, -FrameWidth), new Vector2(FrameWidth, FrameWidth));
            AddImage(border, frame, Color.white, Image.Type.Sliced, FrameCorner);

            RectTransform body = CreateRect("Fill", plate, layer);
            Stretch(body, Vector2.zero, Vector2.zero);
            AddImage(body, fill, FillTint, Image.Type.Sliced, FillCorner);

            EditorUtility.SetDirty(text);
            log.AppendLine($"- {prompt.name}: 글자 길이에 맞는 남색 판 + 크림 테두리 (정렬 {canvas.sortingOrder}, 글자 뒤), 글자색 크림");
        }

        private static void Tail(RectTransform rect, float size, float drop)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(0f, drop);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static Sprite CopySprite(string source, string path, int border, StringBuilder log)
        {
            Directory.CreateDirectory(ArtFolder);
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogError($"[{Title}] 가져오기 실패: {path}");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            log.AppendLine($"- 이미지: {source} → {path} (9-slice {border}px)");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color, Image.Type type, float cornerMultiplier)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.pixelsPerUnitMultiplier = cornerMultiplier;
            image.raycastTarget = false;
            return image;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
        }
    }
}
