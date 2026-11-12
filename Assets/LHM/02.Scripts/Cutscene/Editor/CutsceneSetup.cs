using System.Text;
using RealSteel.Dialogue;
using RealSteel.Dialogue.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static RealSteel.BossRace.EditorTools.RsOsStyleKit;
using Object = UnityEngine.Object;

namespace RealSteel.Cutscene.EditorTools
{
    public static class CutsceneSetup
    {
        private const string Title = "오프닝 컷신";
        private const string PrefabFolder = "Assets/LHM/05.Prefab/Cutscene";
        internal const string PrefabPath = PrefabFolder + "/CutsceneCanvas.prefab";
        private const string DataPath = "Assets/LHM/03.Data/Cutscene/opening.json";
        private const string ScenePath = "Assets/LHM/01.Scene/OpeningCutscene.unity";
        private const string BoldFontPath = "Assets/LJS/03. Asset/Fonts/NanumBarunGothicBold SDF.asset";
        private const string RegularFontPath = "Assets/LJS/03. Asset/Fonts/NanumBarunGothic SDF.asset";
        private const int SortingOrder = 300;

        private const float ScreenW = 1920f;
        private const float ScreenH = 1080f;
        private const float TextLeft = 300f;
        private const float TextWidth = 1320f;

        private static readonly Color TextMain = Hex("F0F1F3");
        private static readonly Color TextBody = Hex("C9CCD1");

        private static TMP_FontAsset boldFont;
        private static TMP_FontAsset regularFont;
        private static int layer;

        internal static bool Exists()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        }

        internal static void Setup()
        {
            var data = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (data == null)
            {
                SetupKit.Dialog(Title, $"컷신 데이터가 없습니다.\n{DataPath}", "확인");
                return;
            }

            if (!CutsceneData.TryParse(data.text, out CutsceneData parsed, out string error))
            {
                SetupKit.Dialog(Title, $"컷신 데이터 오류 — {error}\n{DataPath}", "확인");
                return;
            }

            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (!SetupKit.Dialog(Title,
                    "오프닝 컷신 화면을 만듭니다.\n" +
                    $"- 프리팹: {PrefabPath} ({(Exists() ? "다시 만듦" : "새로 만듦")})\n" +
                    $"- 데이터: {DataPath} ({parsed.shots.Count}컷)\n" +
                    $"- 임시 지도: {CutsceneArt.MapPath} (없을 때만 만듦)\n" +
                    $"- 테스트 씬: {ScenePath} ({(sceneExists ? "있음, 그대로 둠" : "새로 만들고 엶")})\n" +
                    "프리팹 · 이미지 · 씬 파일은 Ctrl+Z로 되돌릴 수 없습니다.",
                    "만들기", "취소"))
            {
                return;
            }

            if (!sceneExists && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder($"[{Title}]\n");
            foreach (string problem in parsed.Check()) log.AppendLine($"- 데이터 경고: {problem}");

            LoadFonts(log);
            layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            CutsceneArt.EnsureImages(log);
            CutsceneArt.EnsureFolder(PrefabFolder);
            BuildPrefab(data);
            log.AppendLine($"- 프리팹 저장: {PrefabPath}");

            if (sceneExists) log.AppendLine($"- 테스트 씬 있음: {ScenePath} — 프리팹 인스턴스라 바뀐 프리팹이 그대로 반영됨");
            else CreateScene(log);

            log.AppendLine("완료. 테스트 씬에서 Play → Space 넘기기, Esc 두 번 건너뛰기");
            Debug.Log(log.ToString());
        }

        private static void LoadFonts(StringBuilder log)
        {
            boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);

            if (boldFont == null || regularFont == null)
            {
                TMP_FontAsset fallback = boldFont ?? regularFont ?? TMP_Settings.defaultFontAsset;
                boldFont ??= fallback;
                regularFont ??= fallback;
                log.AppendLine($"- 주의: 나눔바른고딕 폰트를 찾지 못해 '{(fallback != null ? fallback.name : "없음")}' 사용 — 한글이 안 보이면 텍스트의 Font Asset을 바꿔 주세요");
            }
        }

        private static void CreateScene(StringBuilder log)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.AppendLine($"- 테스트 씬 만듦: {ScenePath} (카메라 + 컷신 프리팹)");
        }

        private static void BuildPrefab(TextAsset data)
        {
            var canvasObject = new GameObject("CutsceneCanvas", typeof(RectTransform));
            try
            {
                canvasObject.layer = layer;
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(ScreenW, ScreenH);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
                DialogueVoice voice = AddVoice(canvasObject);
                var player = canvasObject.AddComponent<CutscenePlayer>();

                RectTransform root = Stretch(Rect("Root", canvasObject.transform, layer));
                RectTransform stage = Stretch(Rect("Stage", root, layer));
                AddImage(Stretch(Rect("Backdrop", stage, layer)), null, Page).raycastTarget = true;

                CutsceneMapView mapView = BuildMap(stage);
                BuildShades(stage);
                (CanvasGroup titleGroup, TMP_Text title, TMP_Text subtitle) = BuildTitleCard(stage);
                (CanvasGroup narrationGroup, TMP_Text speaker, TMP_Text body, CanvasGroup nextMark) = BuildNarration(stage);

                Text(stage, "KeyHint", ScreenW - 600f, ScreenH - 44f, 560f, 24f,
                    "<color=#FFE500>Space</color> 넘기기    <color=#FFE500>Esc</color> 건너뛰기", 16f, Dim, false, TextAlignmentOptions.MidlineRight);

                TMP_Text skipText = Text(stage, "SkipHint", ScreenW - 600f, 40f, 560f, 36f,
                    "<color=#FFE500>Esc</color>를 한 번 더 누르면 건너뜁니다", 22f, TextMain, false, TextAlignmentOptions.MidlineRight);
                var skipHint = skipText.gameObject.AddComponent<CanvasGroup>();

                RectTransform fadeRect = Stretch(Rect("Fade", root, layer));
                AddImage(fadeRect, null, Color.black);
                var fade = fadeRect.gameObject.AddComponent<CanvasGroup>();
                fade.blocksRaycasts = false;

                var so = new SerializedObject(player);
                so.FindProperty("cutsceneJson").objectReferenceValue = data;
                so.FindProperty("root").objectReferenceValue = root.gameObject;
                so.FindProperty("stage").objectReferenceValue = stage.gameObject;
                so.FindProperty("fade").objectReferenceValue = fade;
                so.FindProperty("mapView").objectReferenceValue = mapView;
                so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
                so.FindProperty("titleText").objectReferenceValue = title;
                so.FindProperty("subtitleText").objectReferenceValue = subtitle;
                so.FindProperty("narrationGroup").objectReferenceValue = narrationGroup;
                so.FindProperty("speakerText").objectReferenceValue = speaker;
                so.FindProperty("bodyText").objectReferenceValue = body;
                so.FindProperty("nextMark").objectReferenceValue = nextMark;
                so.FindProperty("voice").objectReferenceValue = voice;
                so.FindProperty("skipHint").objectReferenceValue = skipHint;
                so.ApplyModifiedPropertiesWithoutUndo();

                root.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static DialogueVoice AddVoice(GameObject target)
        {
            var source = target.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var voice = target.AddComponent<DialogueVoice>();

            var so = new SerializedObject(voice);
            so.FindProperty("audioSource").objectReferenceValue = source;
            SerializedProperty voices = so.FindProperty("voices");
            AddSpeakerVoice(voices, "chairman", 0.8f);
            AddSpeakerVoice(voices, "father", 0.95f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return voice;
        }

        private static void AddSpeakerVoice(SerializedProperty voices, string speakerId, float pitch)
        {
            voices.arraySize++;
            SerializedProperty entry = voices.GetArrayElementAtIndex(voices.arraySize - 1);
            entry.FindPropertyRelative("speakerId").stringValue = speakerId;
            entry.FindPropertyRelative("clip").objectReferenceValue = null;
            entry.FindPropertyRelative("pitch").floatValue = pitch;
            entry.FindPropertyRelative("pitchVariance").floatValue = 0.05f;
        }

        private static CutsceneMapView BuildMap(RectTransform stage)
        {
            RectTransform viewport = Stretch(Rect("MapViewport", stage, layer));
            viewport.gameObject.AddComponent<RectMask2D>();
            var mapView = viewport.gameObject.AddComponent<CutsceneMapView>();

            RectTransform frame = Stretch(Rect("MapFrame", viewport, layer));
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ScreenW / ScreenH;

            RectTransform map = Stretch(Rect("Map", frame, layer));
            Sprite mapSprite = CutsceneArt.LoadMap();
            AddImage(map, mapSprite, mapSprite != null ? Color.white : Box);

            RectTransform pin = Rect("Pin", map, layer);
            pin.anchorMin = pin.anchorMax = pin.pivot = new Vector2(0.5f, 0.5f);
            pin.sizeDelta = Vector2.zero;
            var pinGroup = pin.gameObject.AddComponent<CanvasGroup>();
            Diamond(pin, "Halo", 40f, new Color(Yellow.r, Yellow.g, Yellow.b, 0.25f));
            Diamond(pin, "Dot", 16f, Yellow);
            TMP_Text pinLabel = BuildPinLabel(pin);

            var so = new SerializedObject(mapView);
            so.FindProperty("viewport").objectReferenceValue = viewport;
            so.FindProperty("map").objectReferenceValue = map;
            so.FindProperty("pin").objectReferenceValue = pin;
            so.FindProperty("pinGroup").objectReferenceValue = pinGroup;
            so.FindProperty("pinLabel").objectReferenceValue = pinLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return mapView;
        }

        private static TMP_Text BuildPinLabel(RectTransform pin)
        {
            RectTransform plate = Rect("Label", pin, layer);
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
            plate.pivot = new Vector2(0f, 0.5f);
            plate.anchoredPosition = new Vector2(30f, 0f);
            AddImage(plate, null, new Color(Page.r, Page.g, Page.b, 0.85f));

            var layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = plate.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return Text(plate, "Text", 0f, 0f, 200f, 32f, "지명", 24f, TextMain, true, TextAlignmentOptions.MidlineLeft);
        }

        private static void BuildShades(RectTransform stage)
        {
            Sprite shade = CutsceneArt.LoadShade();

            RectTransform top = Rect("ShadeTop", stage, layer);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 220f);
            top.localScale = new Vector3(1f, -1f, 1f);
            top.anchoredPosition = new Vector2(0f, -220f);
            AddImage(top, shade, Color.white);

            RectTransform bottom = Rect("ShadeBottom", stage, layer);
            bottom.anchorMin = Vector2.zero;
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(0f, 440f);
            AddImage(bottom, shade, Color.white);
        }

        private static (CanvasGroup, TMP_Text, TMP_Text) BuildTitleCard(RectTransform stage)
        {
            RectTransform card = Stretch(Rect("TitleCard", stage, layer));
            var group = card.gameObject.AddComponent<CanvasGroup>();

            TMP_Text title = Text(card, "Title", 160f, 450f, ScreenW - 320f, 120f, "제목", 88f, TextMain, true, TextAlignmentOptions.Center);
            title.characterSpacing = 14f;
            AddImage(Place("Line", card, (ScreenW - 160f) * 0.5f, 584f, 160f, 3f, layer), null, Yellow);
            TMP_Text subtitle = Text(card, "Subtitle", 160f, 606f, ScreenW - 320f, 50f, "부제", 32f, TextBody, false, TextAlignmentOptions.Center);
            subtitle.characterSpacing = 6f;
            return (group, title, subtitle);
        }

        private static (CanvasGroup, TMP_Text, TMP_Text, CanvasGroup) BuildNarration(RectTransform stage)
        {
            RectTransform narration = Stretch(Rect("Narration", stage, layer));
            var group = narration.gameObject.AddComponent<CanvasGroup>();

            TMP_Text speaker = Text(narration, "Speaker", TextLeft, 808f, TextWidth, 40f, "화자", 28f, Yellow, true, TextAlignmentOptions.MidlineLeft);
            AddImage(Place("Accent", speaker.transform, -22f, 6f, 6f, 28f, layer), null, Yellow);

            TMP_Text body = Text(narration, "Body", TextLeft, 860f, TextWidth, 150f, "", 34f, TextMain, false, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 10f;

            RectTransform mark = Place("NextMark", narration, TextLeft + TextWidth - 8f, 994f, 16f, 16f, layer);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(mark, null, Yellow);
            var nextMark = mark.gameObject.AddComponent<CanvasGroup>();
            return (group, speaker, body, nextMark);
        }

        private static void Diamond(RectTransform parent, string name, float size, Color color)
        {
            RectTransform rect = Rect(name, parent, layer);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(rect, null, color);
        }

        private static TMP_Text Text(Transform parent, string name, float x, float y, float w, float h, string content, float size, Color color, bool bold, TextAlignmentOptions alignment)
        {
            RectTransform rect = Place(name, parent, x, y, w, h, layer);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = bold ? boldFont : regularFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
