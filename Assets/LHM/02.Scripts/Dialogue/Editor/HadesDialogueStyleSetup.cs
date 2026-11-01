using System.IO;
using System.Text;
using Michsky.UI.MTP;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.Dialogue.EditorTools
{
    public static class HadesDialogueStyleSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "하데스 스타일 대화창";
        private const string SpriteFolder = "Assets/LHM/09.Asset/DialogueUI";
        private const string FramePath = SpriteFolder + "/hades_frame.png";
        private const string PlatePath = SpriteFolder + "/hades_nameplate.png";
        private const string StyleRootName = "[Hades] Style";

        private static readonly Vector2 BoxAnchorMin = new Vector2(0.30f, 0.05f);
        private static readonly Vector2 BoxAnchorMax = new Vector2(0.88f, 0.31f);
        private static readonly Vector2 PlateSize = new Vector2(340f, 96f);
        private static readonly Vector2 PlateOffset = new Vector2(30f, -14f);
        private static readonly Vector2 PortraitSize = new Vector2(640f, 860f);
        private const float FrameSkew = 18f;
        private const float PlateSkew = 12f;

        private static readonly Color Outline = Hex("1A1410");
        private static readonly Color Gold = Hex("C9A24A");
        private static readonly Color BandColor = Hex("3A2C22");
        private static readonly Color InnerGold = Hex("8C6A2E");
        private static readonly Color PaperTop = Hex("F2E9D6");
        private static readonly Color PaperBottom = Hex("E0D0B2");
        private static readonly Color PlateTop = Hex("35302B");
        private static readonly Color PlateBottom = Hex("1F1B18");
        private static readonly Color PlateInner = Hex("5A4630");
        private static readonly Color NameColor = Hex("F3EAD7");
        private static readonly Color TitleColor = Hex("D8483A");
        private static readonly Color BodyColor = Hex("2E2318");
        private static readonly Color StatusColor = Hex("7A5A34");

        [MenuItem("Tools/Dialogue/하데스 스타일 대화창 적용")]
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
            var boxObject = boxSo.FindProperty("box").objectReferenceValue as GameObject;
            var boxRect = boxObject != null ? boxObject.transform as RectTransform : null;
            var oldName = boxSo.FindProperty("speakerNameText").objectReferenceValue as TMP_Text;
            var oldBody = boxSo.FindProperty("bodyText").objectReferenceValue as TMP_Text;
            if (boxRect == null || oldName == null || oldBody == null)
            {
                EditorUtility.DisplayDialog(UndoName, "DialogueBox의 Box / Speaker Name Text / Body Text 칸을 확인하세요.", "확인");
                return;
            }

            if (boxRect.Find(StyleRootName) != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"이미 적용되어 있습니다 ('{StyleRootName}'). 다시 적용하려면 Ctrl+Z로 되돌리거나 그 오브젝트를 지우고 이름·본문 칸을 원래 텍스트로 되돌린 뒤 실행하세요.\n\n색·크기만 바꾸려면 '하데스 스타일 이미지 다시 만들기' 메뉴를 쓰세요.", "확인");
                return;
            }

            var motion = boxSo.FindProperty("motion").objectReferenceValue as DialogueBoxMotion;
            RectTransform layoutRect = motion != null && boxRect.IsChildOf(motion.transform) ? (RectTransform)motion.transform : boxRect;
            var portrait = boxSo.FindProperty("portrait").objectReferenceValue as DialoguePortrait;
            Image portraitImage = portrait != null ? new SerializedObject(portrait).FindProperty("portraitImage").objectReferenceValue as Image : null;

            if (!EditorUtility.DisplayDialog(UndoName, $"'{scene.name}' 씬의 대화창을 하데스 스타일로 바꿉니다.\n- 테두리·이름표 이미지 생성: {SpriteFolder}\n- 대화창 위치·크기: 화면 하단 중앙~오른쪽 ({(layoutRect == boxRect ? "박스" : "MTP-2 모션 오브젝트")} 기준)\n- 새 이름표(이름 + 칭호)와 본문 글자를 만들고 DialogueBox에 연결, 예전 글자는 끔\n- 초상화: {(portraitImage != null ? "왼쪽 하단 크게" : "없음 (건너뜀)")}\n{(motion != null ? "- MTP-2 상단 선을 금색으로\n" : "")}씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "적용", "취소"))
            {
                return;
            }

            Vector2 reference = ReferenceSize(boxRect);
            Vector2 boxPixels = new Vector2(reference.x * (BoxAnchorMax.x - BoxAnchorMin.x), reference.y * (BoxAnchorMax.y - BoxAnchorMin.y));
            Sprite frame = BuildFrame(Mathf.RoundToInt(boxPixels.x), Mathf.RoundToInt(boxPixels.y));
            Sprite plate = BuildPlate(Mathf.RoundToInt(PlateSize.x), Mathf.RoundToInt(PlateSize.y));

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[하데스 스타일] {scene.path}\n");
            int uiLayer = LayerMask.NameToLayer("UI");

            Undo.RecordObject(layoutRect, UndoName);
            layoutRect.anchorMin = BoxAnchorMin;
            layoutRect.anchorMax = BoxAnchorMax;
            layoutRect.pivot = new Vector2(0.5f, 0f);
            layoutRect.offsetMin = Vector2.zero;
            layoutRect.offsetMax = Vector2.zero;
            log.AppendLine($"- 대화창 배치: 화면 가로 {BoxAnchorMin.x:P0}~{BoxAnchorMax.x:P0}, 세로 {BoxAnchorMin.y:P0}~{BoxAnchorMax.y:P0} (약 {boxPixels.x:0}×{boxPixels.y:0})");

            var oldBackground = boxObject.GetComponent<Image>();
            if (oldBackground != null && oldBackground.enabled)
            {
                Undo.RecordObject(oldBackground, UndoName);
                oldBackground.enabled = false;
                log.AppendLine("- 예전 박스 배경 Image 끔");
            }

            RectTransform styleRoot = CreateRect(StyleRootName, boxRect, uiLayer);
            Stretch(styleRoot, Vector2.zero, Vector2.zero);
            styleRoot.SetSiblingIndex(0);

            RectTransform frameRect = CreateRect("Frame", styleRoot, uiLayer);
            Stretch(frameRect, Vector2.zero, Vector2.zero);
            AddImage(frameRect, frame);

            TMP_FontAsset font = oldBody.font != null ? oldBody.font : oldName.font;

            RectTransform bodyRect = CreateRect("Body", styleRoot, uiLayer);
            Stretch(bodyRect, new Vector2(56f, 30f), new Vector2(-56f, PlateOffset.y - PlateSize.y - 8f));
            TMP_Text body = AddText(bodyRect, font, 30f, BodyColor, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 12f;

            RectTransform plateRect = CreateRect("Nameplate", styleRoot, uiLayer);
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 1f);
            plateRect.anchoredPosition = PlateOffset;
            plateRect.sizeDelta = PlateSize;
            AddImage(plateRect, plate);

            RectTransform nameRect = CreateRect("Name", plateRect, uiLayer);
            Stretch(nameRect, new Vector2(PlateSkew + 10f, 34f), new Vector2(-PlateSkew - 10f, -8f));
            TMP_Text name = AddText(nameRect, font, 38f, NameColor, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = 38f;

            RectTransform titleRect = CreateRect("Title", plateRect, uiLayer);
            Stretch(titleRect, new Vector2(PlateSkew + 10f, 8f), new Vector2(-PlateSkew - 10f, -PlateSize.y + 34f));
            TMP_Text title = AddText(titleRect, font, 20f, TitleColor, TextAlignmentOptions.Center);
            log.AppendLine("- 새 구성: Frame(양피지 테두리) / Nameplate(이름 + 칭호) / Body(본문)");

            foreach (TMP_Text old in new[] { oldName, oldBody })
            {
                if (old == null || old.transform.IsChildOf(styleRoot)) continue;
                Undo.RecordObject(old.gameObject, UndoName);
                old.gameObject.SetActive(false);
            }
            log.AppendLine($"- 예전 글자 끔: '{oldName.name}', '{oldBody.name}'");

            Undo.RecordObject(dialogueBox, UndoName);
            boxSo.FindProperty("speakerNameText").objectReferenceValue = name;
            boxSo.FindProperty("bodyText").objectReferenceValue = body;
            boxSo.FindProperty("speakerTitleText").objectReferenceValue = title;
            boxSo.FindProperty("speakerPlate").objectReferenceValue = plateRect.gameObject;
            var status = boxSo.FindProperty("statusText").objectReferenceValue as TMP_Text;
            boxSo.ApplyModifiedProperties();
            log.AppendLine("- DialogueBox 연결: 이름 / 칭호 / 이름표 / 본문");

            if (status != null)
            {
                Undo.RecordObject(status, UndoName);
                status.color = StatusColor;
                log.AppendLine("- AUTO·빨리감기 표시 색을 양피지에 맞게 변경");
            }

            if (portraitImage != null)
            {
                RectTransform portraitRect = portraitImage.rectTransform;
                if (portraitRect.IsChildOf(layoutRect))
                {
                    Undo.SetTransformParent(portraitRect, layoutRect.parent, UndoName);
                    portraitRect.SetSiblingIndex(layoutRect.GetSiblingIndex());
                    log.AppendLine("- 초상화를 대화창 밖(바로 뒤)으로 이동 — 마스크에 잘리지 않게");
                }

                Undo.RecordObject(portraitRect, UndoName);
                portraitRect.anchorMin = portraitRect.anchorMax = Vector2.zero;
                portraitRect.pivot = Vector2.zero;
                portraitRect.anchoredPosition = new Vector2(reference.x * 0.04f, 0f);
                portraitRect.sizeDelta = PortraitSize;
                portraitRect.localScale = Vector3.one;
                Undo.RecordObject(portraitImage, UndoName);
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
                log.AppendLine($"- 초상화: 왼쪽 하단 {PortraitSize.x:0}×{PortraitSize.y:0}");
            }

            if (motion != null)
            {
                Transform line = motion.transform.Find("Content/Line");
                if (line != null)
                {
                    var lineImage = line.GetComponent<Image>();
                    if (lineImage != null)
                    {
                        Undo.RecordObject(lineImage, UndoName);
                        lineImage.color = Gold;
                    }

                    var gradient = line.GetComponent<UIGradient>();
                    if (gradient != null)
                    {
                        Undo.RecordObject(gradient, UndoName);
                        gradient.enabled = false;
                    }
                    log.AppendLine("- MTP-2 상단 선: 금색");
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 대화로 확인. 초상화 그림은 DialoguePortrait의 목록에 넣어야 보입니다.");
            Debug.Log(log.ToString());
        }

        [MenuItem("Tools/Dialogue/하데스 스타일 이미지 다시 만들기")]
        private static void Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            DialogueBox dialogueBox = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            var boxObject = dialogueBox != null ? new SerializedObject(dialogueBox).FindProperty("box").objectReferenceValue as GameObject : null;
            Vector2 reference = boxObject != null ? ReferenceSize((RectTransform)boxObject.transform) : new Vector2(1920f, 1080f);

            BuildFrame(Mathf.RoundToInt(reference.x * (BoxAnchorMax.x - BoxAnchorMin.x)), Mathf.RoundToInt(reference.y * (BoxAnchorMax.y - BoxAnchorMin.y)));
            BuildPlate(Mathf.RoundToInt(PlateSize.x), Mathf.RoundToInt(PlateSize.y));
            Debug.Log($"[하데스 스타일] 이미지 다시 만듦: {FramePath}, {PlatePath} (씬 {scene.name})");
        }

        private static Vector2 ReferenceSize(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>(true);
            canvas = canvas != null ? canvas.rootCanvas : null;
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) return scaler.referenceResolution;
            if (canvas != null && canvas.transform is RectTransform canvasRect && canvasRect.rect.width > 0f) return canvasRect.rect.size;
            return new Vector2(1920f, 1080f);
        }

        private static Sprite BuildFrame(int width, int height)
        {
            var bands = new[]
            {
                new Band(3f, Outline),
                new Band(3f, Gold),
                new Band(8f, BandColor),
                new Band(2f, InnerGold),
            };
            return SaveSprite(FramePath, Paint(width, height, FrameSkew, bands, PaperTop, PaperBottom, true));
        }

        private static Sprite BuildPlate(int width, int height)
        {
            var bands = new[]
            {
                new Band(2f, Outline),
                new Band(2f, Gold),
                new Band(4f, PlateBottom),
                new Band(1f, PlateInner),
            };
            return SaveSprite(PlatePath, Paint(width, height, PlateSkew, bands, PlateTop, PlateBottom, false));
        }

        private struct Band
        {
            public float Width;
            public Color Color;

            public Band(float width, Color color)
            {
                Width = width;
                Color = color;
            }
        }

        private static Texture2D Paint(int width, int height, float skew, Band[] bands, Color top, Color bottom, bool vignette)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            float slant = Mathf.Sqrt(height * height + skew * skew);

            for (int y = 0; y < height; y++)
            {
                float py = y + 0.5f;
                float v = py / height;
                Color fill = Color.Lerp(bottom, top, v);

                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float left = (height * px - skew * py) / slant;
                    float right = (skew * py - height * (px - (width - skew))) / slant;
                    float d = Mathf.Min(Mathf.Min(py, height - py), Mathf.Min(left, right));

                    Color inner = fill;
                    if (vignette)
                    {
                        float edge = Mathf.Clamp01(1f - (d - 16f) / 60f);
                        inner = Color.Lerp(fill, fill * 0.88f, edge * edge);
                        inner.a = 1f;
                    }

                    Color color = inner;
                    float start = 0f;
                    for (int i = 0; i < bands.Length; i++)
                    {
                        float end = start + bands[i].Width;
                        if (d < end)
                        {
                            Color next = i + 1 < bands.Length ? bands[i + 1].Color : inner;
                            color = Color.Lerp(bands[i].Color, next, Mathf.Clamp01(d - end + 0.5f));
                            break;
                        }
                        start = end;
                    }

                    color.a = Mathf.Clamp01(d + 0.5f);
                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite SaveSprite(string path, Texture2D texture)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        private static Image AddImage(RectTransform rect, Sprite sprite)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
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
