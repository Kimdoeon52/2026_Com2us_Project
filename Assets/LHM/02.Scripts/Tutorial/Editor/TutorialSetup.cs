using System.Collections.Generic;
using System.IO;
using System.Text;
using RealSteel.BossRace.EditorTools;
using RealSteel.Dialogue.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using static RealSteel.BossRace.EditorTools.RsOsStyleKit;
using Object = UnityEngine.Object;

namespace RealSteel.Tutorial.EditorTools
{
    public static class TutorialSetup
    {
        private const string Title = "튜토리얼 화면";
        private const string PrefabFolder = "Assets/LHM/05.Prefab/Tutorial";
        internal const string PrefabPath = PrefabFolder + "/TutorialCanvas.prefab";
        private const string GuideFolder = "Assets/LHM/03.Data/Tutorial";
        internal const string GuidePath = GuideFolder + "/Tutorial_Basic.asset";
        private const string TestName = "[Tutorial] Test";

        // 1920x1080 기준 창 크기와 영상 영역 (16:9)
        private const float W = 1480f;
        private const float H = 900f;
        private const float MediaW = 960f;
        private const float MediaH = 540f;
        private const float MediaX = (W - MediaW) * 0.5f;
        private const float MediaY = 140f;
        private const float ArrowSize = 72f;
        private const float DoneWidth = 160f;
        private const float SideGap = 24f;

        private static readonly Color TextMain = Hex("F0F1F3");
        private static readonly Color TextBody = Hex("E2E4E7");

        private static TMP_FontAsset font;
        private static int layer;

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!SetupKit.RequireLhmScene(scene, Title)) return;

            RsOsSprites sprites = LoadSprites(Title);
            if (sprites == null) return;

            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            bool guideExists = AssetDatabase.LoadAssetAtPath<TutorialGuide>(GuidePath) != null;
            var placed = Object.FindFirstObjectByType<TutorialPanel>(FindObjectsInactive.Include);

            if (!SetupKit.Dialog(Title,
                    $"'{scene.name}' 씬에 튜토리얼 화면 (영상 + 이전/다음 + 설명)을 만듭니다.\n" +
                    $"- 프리팹: {PrefabPath} ({(prefabExists ? "다시 만듦" : "새로 만듦")})\n" +
                    $"- 예시 데이터: {GuidePath} ({(guideExists ? "있음, 그대로 둠" : "새로 만듦")})\n" +
                    $"- 씬: 튜토리얼 화면 {(placed != null ? "이미 있음" : "배치")} + {TestName} (Insert로 열기)\n" +
                    "프리팹 · 데이터 파일은 Ctrl+Z로 되돌릴 수 없습니다. 씬은 자동 저장하지 않습니다.",
                    "만들기", "취소"))
            {
                return;
            }

            font = BossRaceSetup.FindFont() ?? TMP_Settings.defaultFontAsset;
            layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            var log = new StringBuilder($"[튜토리얼 화면] {scene.path}\n");

            EnsureFolder(PrefabFolder);
            EnsureFolder(GuideFolder);
            TutorialGuide guide = guideExists ? AssetDatabase.LoadAssetAtPath<TutorialGuide>(GuidePath) : CreateSampleGuide(log);
            BuildPrefab(sprites);
            log.AppendLine($"- 프리팹 {(prefabExists ? "다시 만듦" : "만듦")}: {PrefabPath}");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(Title);
            int group = Undo.GetCurrentGroup();

            TutorialPanel panel = placed;
            if (panel == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(instance, Title);
                panel = instance.GetComponent<TutorialPanel>();
                log.AppendLine("- 씬에 튜토리얼 화면 배치");
            }
            else
            {
                log.AppendLine($"- 씬에 이미 있음 ({panel.name}) — 프리팹 인스턴스면 바뀐 프리팹이 그대로 반영됨");
            }

            var test = Object.FindFirstObjectByType<TutorialTest>(FindObjectsInactive.Include);
            if (test == null)
            {
                var go = new GameObject(TestName);
                Undo.RegisterCreatedObjectUndo(go, Title);
                test = go.AddComponent<TutorialTest>();
                log.AppendLine($"- {TestName} 생성");
            }

            var testSo = new SerializedObject(test);
            testSo.FindProperty("panel").objectReferenceValue = panel;
            if (testSo.FindProperty("guide").objectReferenceValue == null) testSo.FindProperty("guide").objectReferenceValue = guide;
            testSo.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 → Play → Insert (Shift+Insert = 본 기록 지우기)");
            Debug.Log(log.ToString());
        }

        private static TutorialGuide CreateSampleGuide(StringBuilder log)
        {
            var guide = ScriptableObject.CreateInstance<TutorialGuide>();
            guide.guideId = "basic";
            guide.title = "기본 조작";
            guide.steps = new List<TutorialStep>
            {
                new TutorialStep
                {
                    title = "필드 이동",
                    description = "<color=#FFE500>WASD</color>로 이동하고 <color=#FFE500>Shift</color>를 누른 채 달립니다.\n" +
                                  "<color=#FFE500>Q / E</color>로 카메라를 45°씩 돌릴 수 있어요.",
                },
                new TutorialStep
                {
                    title = "NPC와 대화",
                    description = "NPC 앞에서 <color=#FFE500>E</color>를 누르면 대화가 시작됩니다.\n" +
                                  "<color=#FFE500>Space</color> 다음 대사 · <color=#FFE500>Esc</color> 스킵 · <color=#FFE500>L</color> 지난 대사 보기",
                },
                new TutorialStep
                {
                    title = "고철 수집",
                    description = "고철 더미 앞에서 <color=#FFE500>G</color>로 뒤집니다.\n" +
                                  "미니게임에서는 <color=#FFE500>Space</color>로 타이밍을 맞춰요.",
                },
                new TutorialStep
                {
                    title = "보스 레이스 신청",
                    description = "길드 접수원에게 <color=#FFE500>E</color> → '토벌 신청'을 고르면 난이도를 정하고 레이스에 나갑니다.\n" +
                                  "<color=#FFE500>Home</color>을 누르면 언제든 단축키 안내를 볼 수 있어요.",
                },
            };

            AssetDatabase.CreateAsset(guide, GuidePath);
            AssetDatabase.SaveAssets();
            log.AppendLine($"- 예시 데이터 만듦: {GuidePath} (4단계, 영상 칸은 비어 있음)");
            return guide;
        }

        private static void BuildPrefab(RsOsSprites sprites)
        {
            var canvasObject = new GameObject("TutorialCanvas", typeof(RectTransform));
            try
            {
                canvasObject.layer = layer;
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 250;
                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
                var panel = canvasObject.AddComponent<TutorialPanel>();

                // 화면 전체를 어둡게 덮고 뒤쪽 클릭을 막음
                RectTransform root = Stretch(Rect("Root", canvasObject.transform, layer));
                var rootGroup = root.gameObject.AddComponent<CanvasGroup>();
                AddImage(root, null, new Color(Page.r, Page.g, Page.b, 0.9f)).raycastTarget = true;

                RectTransform window = Rect("Window", root, layer);
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
                window.sizeDelta = new Vector2(W, H);
                Fill(window.gameObject.AddComponent<Image>(), sprites, Panel);
                AddOutline(window, sprites, Edge);
                AddBrackets(window);

                // 상단: 마름모 + 제목, 밑줄, 시스템 표시, 단계 번호, 진행 막대
                RectTransform diamond = Rect("Diamond", window, layer);
                diamond.anchorMin = diamond.anchorMax = new Vector2(0f, 1f);
                diamond.pivot = new Vector2(0.5f, 0.5f);
                diamond.anchoredPosition = new Vector2(52f, -56f);
                diamond.sizeDelta = new Vector2(12f, 12f);
                diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
                AddImage(diamond, null, Yellow);

                TMP_Text guideTitle = Text(window, "GuideTitle", 72f, 34f, 900f, 44f, "이용 방법", 34f, TextMain, true, TextAlignmentOptions.MidlineLeft);
                AddImage(Place("Underline", window, 40f, 86f, 140f, 3f, layer), null, Yellow);
                TMP_Text tag = Text(window, "SystemTag", 196f, 80f, 700f, 16f, "RS-OS  //  GUIDE  ·  TUTORIAL  <color=#FFE500>////</color>", 11f, Dim, false, TextAlignmentOptions.MidlineLeft);
                tag.characterSpacing = 6f;
                TMP_Text counter = Text(window, "Counter", W - 280f, 34f, 240f, 44f, "01 / 04", 32f, Yellow, true, TextAlignmentOptions.MidlineRight);

                RectTransform track = Place("ProgressTrack", window, 40f, 112f, W - 80f, 4f, layer);
                AddImage(track, null, Box);
                RectTransform progress = Rect("Fill", track, layer);
                progress.anchorMin = Vector2.zero;
                progress.anchorMax = new Vector2(0.25f, 1f);
                progress.offsetMin = progress.offsetMax = Vector2.zero;
                AddImage(progress, null, Yellow);

                // 가운데: 영상 영역
                RectTransform media = Place("Media", window, MediaX, MediaY, MediaW, MediaH, layer);
                var mediaGroup = media.gameObject.AddComponent<CanvasGroup>();
                Fill(media.gameObject.AddComponent<Image>(), sprites, Box);
                TMP_Text placeholder = Text(media, "Placeholder", 0f, 0f, MediaW, MediaH, "영상 준비 중", 40f, Dim, true, TextAlignmentOptions.Center);

                RectTransform videoRect = Rect("Video", media, layer);
                videoRect.anchorMin = videoRect.anchorMax = videoRect.pivot = new Vector2(0.5f, 0.5f);
                videoRect.sizeDelta = new Vector2(MediaW - 8f, MediaH - 8f);
                var videoImage = videoRect.gameObject.AddComponent<RawImage>();
                videoImage.raycastTarget = false;
                videoRect.gameObject.SetActive(false);

                var player = media.gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.isLooping = true;

                RectTransform framesRect = Stretch(Rect("Frames", media, layer));
                framesRect.offsetMin = new Vector2(4f, 4f);
                framesRect.offsetMax = new Vector2(-4f, -4f);
                Image frameImage = AddImage(framesRect, null, Color.white);
                frameImage.preserveAspect = true;
                framesRect.gameObject.SetActive(false);

                Outline(AddImage(Stretch(Rect("Frame", media, layer)), null, Edge), sprites, Edge);

                // 좌우: 이전 · 다음 · 완료 버튼
                float buttonY = MediaY + (MediaH - ArrowSize) * 0.5f;
                float rightX = MediaX + MediaW + SideGap;
                Button prev = ArrowButton(window, "PrevButton", MediaX - SideGap - ArrowSize, buttonY, sprites, true);
                Button next = ArrowButton(window, "NextButton", rightX, buttonY, sprites, false);
                Button done = DoneButton(window, rightX, buttonY, sprites);
                done.gameObject.SetActive(false);

                // 아래: 단계 제목 + 설명
                RectTransform textRect = Place("Text", window, MediaX, MediaY + MediaH + 20f, MediaW, 130f, layer);
                var textGroup = textRect.gameObject.AddComponent<CanvasGroup>();
                TMP_Text stepTitle = Text(textRect, "StepTitle", 0f, 0f, MediaW, 40f, "", 30f, Yellow, true, TextAlignmentOptions.Center);
                TMP_Text description = Text(textRect, "Description", 0f, 48f, MediaW, 82f, "", 24f, TextBody, false, TextAlignmentOptions.Top);
                description.textWrappingMode = TextWrappingModes.Normal;

                // 맨 아래: 단계 점 + 키 안내
                RectTransform dotsRect = Place("Dots", window, 40f, H - 44f, W - 80f, 14f, layer);
                var dotsLayout = dotsRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                dotsLayout.childAlignment = TextAnchor.MiddleCenter;
                dotsLayout.spacing = 10f;
                dotsLayout.childControlWidth = dotsLayout.childControlHeight = false;
                dotsLayout.childForceExpandWidth = dotsLayout.childForceExpandHeight = false;
                RectTransform dotRect = Rect("DotTemplate", dotsRect, layer);
                dotRect.sizeDelta = new Vector2(14f, 6f);
                Image dotImage = AddImage(dotRect, null, Edge);
                dotImage.raycastTarget = true;
                var dot = dotRect.gameObject.AddComponent<Button>();
                dot.targetGraphic = dotImage;
                dot.transition = Selectable.Transition.None;
                dot.navigation = new Navigation { mode = Navigation.Mode.None };

                Text(window, "KeyHint", W - 560f, H - 50f, 520f, 24f,
                    "<color=#FFE500>← →</color> 이동    <color=#FFE500>Space</color> 다음    <color=#FFE500>Esc</color> 닫기",
                    16f, Dim, false, TextAlignmentOptions.MidlineRight);

                var so = new SerializedObject(panel);
                so.FindProperty("root").objectReferenceValue = root.gameObject;
                so.FindProperty("rootGroup").objectReferenceValue = rootGroup;
                so.FindProperty("window").objectReferenceValue = window;
                so.FindProperty("guideTitleText").objectReferenceValue = guideTitle;
                so.FindProperty("counterText").objectReferenceValue = counter;
                so.FindProperty("progressFill").objectReferenceValue = progress;
                so.FindProperty("mediaGroup").objectReferenceValue = mediaGroup;
                so.FindProperty("mediaArea").objectReferenceValue = media;
                so.FindProperty("videoImage").objectReferenceValue = videoImage;
                so.FindProperty("videoPlayer").objectReferenceValue = player;
                so.FindProperty("frameImage").objectReferenceValue = frameImage;
                so.FindProperty("placeholderText").objectReferenceValue = placeholder;
                so.FindProperty("textGroup").objectReferenceValue = textGroup;
                so.FindProperty("stepTitleText").objectReferenceValue = stepTitle;
                so.FindProperty("descriptionText").objectReferenceValue = description;
                so.FindProperty("prevButton").objectReferenceValue = prev;
                so.FindProperty("nextButton").objectReferenceValue = next;
                so.FindProperty("doneButton").objectReferenceValue = done;
                so.FindProperty("dotTemplate").objectReferenceValue = dot;
                so.ApplyModifiedPropertiesWithoutUndo();

                // 평소에는 꺼 둠 (열 때 TutorialPanel이 켬)
                root.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }

        private static Button ArrowButton(Transform parent, string name, float x, float y, RsOsSprites sprites, bool pointLeft)
        {
            RectTransform rect = Place(name, parent, x, y, ArrowSize, ArrowSize, layer);
            Fill(rect.gameObject.AddComponent<Image>(), sprites, HeaderColor);

            // 테두리가 버튼 상태색을 받음: 평소 회색, 마우스를 올리면 노란색
            Image outline = AddImage(Stretch(Rect("Frame", rect, layer)), null, Color.white);
            Outline(outline, sprites, Color.white);
            outline.raycastTarget = true;

            // 막대 두 개로 그린 '>' (왼쪽은 좌우 반전)
            RectTransform chevron = Rect("Chevron", rect, layer);
            chevron.anchorMin = chevron.anchorMax = chevron.pivot = new Vector2(0.5f, 0.5f);
            chevron.sizeDelta = new Vector2(24f, 24f);
            chevron.localScale = new Vector3(pointLeft ? -1f : 1f, 1f, 1f);
            ChevronBar(chevron, 5.5f, -45f);
            ChevronBar(chevron, -5.5f, 45f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = outline;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Edge;
            colors.highlightedColor = Yellow;
            colors.pressedColor = Yellow * new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Edge;
            colors.disabledColor = new Color(Edge.r, Edge.g, Edge.b, 0.3f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static void ChevronBar(RectTransform chevron, float y, float angle)
        {
            RectTransform bar = Rect("Bar", chevron, layer);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = new Vector2(0f, y);
            bar.sizeDelta = new Vector2(16f, 4f);
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
            AddImage(bar, null, TextMain);
        }

        private static Button DoneButton(Transform parent, float x, float y, RsOsSprites sprites)
        {
            RectTransform rect = Place("DoneButton", parent, x, y, DoneWidth, ArrowSize, layer);
            var chip = rect.gameObject.AddComponent<Image>();
            Chip(chip, sprites);
            chip.raycastTarget = true;
            Text(rect, "Label", 0f, 0f, DoneWidth, ArrowSize, "완료", 28f, Page, true, TextAlignmentOptions.Center);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, float x, float y, float w, float h, string content, float size, Color color, bool bold, TextAlignmentOptions alignment)
        {
            RectTransform rect = Place(name, parent, x, y, w, h, layer);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
