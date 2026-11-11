using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RealSteel.BossRace.EditorTools
{
    internal sealed class RsOsSprites
    {
        public Sprite Fill;
        public Sprite Outline;
        public Sprite Chip;
        public Sprite ChipOff;
    }

    internal static class RsOsStyleKit
    {
        internal const string AddedPrefix = "[RS-OS] ";
        internal const float Cut = 1.6f;

        private const string SpriteRoot = "Assets/SYS/UI/BG/Button/";
        private const string FillPath = SpriteRoot + "miniButton/bg_miniButton_default_disabled.png";
        private const string OutlinePath = SpriteRoot + "iconButton/bg_iconButton_v1_lg_enabled.png";
        private const string ChipPath = SpriteRoot + "button/bg_button_primary_enabled.png";
        private const string ChipOffPath = SpriteRoot + "button/bg_button_primary_disabled.png";
        private const float SpriteGray = 136f / 255f;

        internal static readonly Color Yellow = Hex("FFE500");
        internal static readonly Color OldGold = Hex("F2A900");
        internal static readonly Color Page = Hex("0D0E10");
        internal static readonly Color HeaderColor = Hex("17191C");
        internal static readonly Color Panel = Hex("1A1C1F");
        internal static readonly Color PanelSelected = Hex("24272B");
        internal static readonly Color Box = Hex("26292D");
        internal static readonly Color Edge = Hex("3A3D41");
        internal static readonly Color Dim = Hex("5E6166");

        internal static bool IsApplied(string prefabPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return root != null && root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith(AddedPrefix));
        }

        // 못 찾으면 안내 창을 띄우고 null
        internal static RsOsSprites LoadSprites(string title)
        {
            var sprites = new RsOsSprites
            {
                Fill = AssetDatabase.LoadAssetAtPath<Sprite>(FillPath),
                Outline = AssetDatabase.LoadAssetAtPath<Sprite>(OutlinePath),
                Chip = AssetDatabase.LoadAssetAtPath<Sprite>(ChipPath),
                ChipOff = AssetDatabase.LoadAssetAtPath<Sprite>(ChipOffPath),
            };
            var missing = new[] { (FillPath, sprites.Fill), (OutlinePath, sprites.Outline), (ChipPath, sprites.Chip), (ChipOffPath, sprites.ChipOff) }
                .Where(p => p.Item2 == null).Select(p => p.Item1).ToList();
            if (missing.Count == 0) return sprites;

            EditorUtility.DisplayDialog(title, "스프라이트를 찾지 못했습니다 (Single 모드 스프라이트여야 함):\n" + string.Join("\n", missing), "확인");
            return null;
        }

        internal static void Run(string title, string prefabPath, string changes, string checkHint, Func<GameObject, RsOsSprites, StringBuilder, bool> apply)
        {
            RsOsSprites sprites = LoadSprites(title);
            if (sprites == null) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                EditorUtility.DisplayDialog(title, $"프리팹이 없습니다: {prefabPath}", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(title,
                    $"{prefabPath} 를 RS-OS 스타일로 바꿉니다.\n" + changes +
                    (IsApplied(prefabPath) ? "- 이미 적용되어 있어 추가 장식을 지우고 다시 만듭니다\n" : "") +
                    "\n프리팹 파일을 바로 저장합니다 (Ctrl+Z 안 됨). 되돌리려면 git으로 프리팹을 되돌리세요.\n" +
                    "주의: 대화 툴의 '프리팹 만들기'를 다시 누르면 이 변경이 덮어써집니다.",
                    "적용", "취소"))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            var log = new StringBuilder($"[RS-OS 스타일] {prefabPath}\n");
            try
            {
                foreach (Transform added in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(AddedPrefix)).ToList())
                {
                    if (added != null) Object.DestroyImmediate(added.gameObject);
                }

                if (!apply(root, sprites, log)) return;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.AppendLine("완료. " + checkHint);
                Debug.Log(log.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static void RecolorGold(GameObject root, StringBuilder log)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (Near(graphic.color, OldGold)) graphic.color = Yellow;
                if (graphic is TMP_Text text && text.text != null) text.text = text.text.Replace("#F2A900", "#FFE500");
            }
            log.AppendLine("- 금색(#F2A900) → 노란색(#FFE500)");
        }

        internal static void Header(Transform layout, RsOsSprites sprites, string systemTag, StringBuilder log)
        {
            int layer = layout.gameObject.layer;
            TMP_FontAsset font = layout.GetComponentInChildren<TMP_Text>(true)?.font;

            var title = Find<RectTransform>(layout, "Title");
            if (title != null)
            {
                RectTransform diamond = Rect(AddedPrefix + "Diamond", layout, layer);
                diamond.anchorMin = diamond.anchorMax = new Vector2(0f, 1f);
                diamond.pivot = new Vector2(0.5f, 0.5f);
                diamond.anchoredPosition = title.anchoredPosition + new Vector2(10f, -title.sizeDelta.y * 0.5f);
                diamond.sizeDelta = new Vector2(11f, 11f);
                diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
                AddImage(diamond, null, Yellow);
                if (title.TryGetComponent(out TMP_Text titleText)) titleText.margin = new Vector4(26f, 0f, 0f, 0f);
            }

            AddImage(Place(AddedPrefix + "Underline", layout, 28f, 62f, 140f, 3f, layer), null, Yellow);

            RectTransform tag = Place(AddedPrefix + "SystemTag", layout, 182f, 66f, 600f, 16f, layer);
            TMP_Text tagText = tag.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tagText.font = font;
            tagText.fontSize = 10f;
            tagText.characterSpacing = 6f;
            tagText.color = Dim;
            tagText.alignment = TextAlignmentOptions.MidlineLeft;
            tagText.raycastTarget = false;
            tagText.text = systemTag + "  <color=#FFE500>////</color>";

            Transform back = layout.Find("BackButton");
            if (back != null)
            {
                Fill(back.GetComponent<Image>(), sprites, HeaderColor);
                Outline(Find<Image>(back, "Frame"), sprites, Edge);
            }

            log.AppendLine("- 상단: 제목 앞 노란 마름모, 밑줄 강조, 시스템 표시, 뒤로 버튼 깎인 모서리");
        }

        internal static void Fill(Image image, RsOsSprites sprites, Color target, float cut = Cut)
        {
            if (image == null) return;
            image.sprite = sprites.Fill;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = cut;
            image.color = Tint(target);
        }

        internal static void Outline(Image image, RsOsSprites sprites, Color color)
        {
            if (image == null) return;
            image.sprite = sprites.Outline;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = Cut;
            image.color = color;
        }

        internal static void Chip(Image image, RsOsSprites sprites)
        {
            if (image == null) return;
            image.sprite = sprites.Chip;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ChipScale(image, sprites.Chip);
            image.color = Color.white;
        }

        internal static float ChipScale(Image image, Sprite chip)
        {
            float height = Mathf.Max(1f, image.rectTransform.rect.height);
            float borders = chip.border.y + chip.border.w;
            return Mathf.Max(Cut, borders / (height * 0.9f));
        }

        internal static Color Tint(Color target)
        {
            return new Color(Mathf.Clamp01(target.r / SpriteGray), Mathf.Clamp01(target.g / SpriteGray), Mathf.Clamp01(target.b / SpriteGray), target.a);
        }

        internal static void AddOutline(Transform parent, RsOsSprites sprites, Color color)
        {
            RectTransform rect = Rect(AddedPrefix + "Outline", parent, parent.gameObject.layer);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Outline(AddImage(rect, null, color), sprites, color);
        }

        internal static void AddBrackets(Transform parent)
        {
            int layer = parent.gameObject.layer;
            RectTransform group = Rect(AddedPrefix + "Brackets", parent, layer);
            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.offsetMin = new Vector2(-7f, -7f);
            group.offsetMax = new Vector2(7f, 7f);

            foreach (Vector2 corner in new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) })
            {
                foreach (Vector2 size in new[] { new Vector2(18f, 2f), new Vector2(2f, 18f) })
                {
                    RectTransform bar = Rect("Bar", group, layer);
                    bar.anchorMin = bar.anchorMax = bar.pivot = corner;
                    bar.anchoredPosition = Vector2.zero;
                    bar.sizeDelta = size;
                    AddImage(bar, null, Yellow);
                }
            }
        }

        internal static void SetColor(Image image, Color color)
        {
            if (image != null) image.color = color;
        }

        internal static T Find<T>(Transform parent, string path) where T : Component
        {
            Transform child = parent.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        internal static RectTransform Rect(string name, Transform parent, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static RectTransform Place(string name, Transform parent, float x, float y, float w, float h, int layer)
        {
            RectTransform rect = Rect(name, parent, layer);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        internal static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        internal static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }
    }
}
