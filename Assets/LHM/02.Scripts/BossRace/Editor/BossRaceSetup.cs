using System.Collections.Generic;
using System.Linq;
using System.Text;
using RealSteel.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealSteel.BossRace.EditorTools
{
    public static class BossRaceSetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "보스 레이스 선택 UI 배치";
        private const string RootName = "[BossRace] Canvas";

        private static readonly Color PageColor = new Color32(0x13, 0x15, 0x18, 0xFF);
        private static readonly Color HeaderColor = new Color32(0x17, 0x19, 0x1C, 0xFF);
        private static readonly Color LineColor = new Color32(0x2A, 0x2D, 0x31, 0xFF);
        private static readonly Color BorderColor = new Color32(0x33, 0x36, 0x3B, 0xFF);
        private static readonly Color PanelColor = new Color32(0x1B, 0x1E, 0x22, 0xFF);
        private static readonly Color DividerColor = new Color32(0x2E, 0x31, 0x35, 0xFF);
        private static readonly Color Gold = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        private static readonly Color TextMain = new Color32(0xF0, 0xF1, 0xF3, 0xFF);
        private static readonly Color TextSub = new Color32(0x9A, 0x9E, 0xA4, 0xFF);
        private static readonly Color TextBody = new Color32(0xE2, 0xE4, 0xE7, 0xFF);
        private static readonly Color Dark = new Color32(0x14, 0x16, 0x19, 0xFF);

        internal static TMP_FontAsset font;
        internal static int uiLayer;

        [MenuItem("Tools/Dialogue/보스 레이스 선택 UI 배치")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var existing = Object.FindFirstObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"보스 레이스 선택 UI가 이미 있습니다. ({existing.name})\n다시 만들려면 지우고 실행하세요.", "확인");
                return;
            }

            var bossTest = Object.FindFirstObjectByType<Step11_BossEntryTest>(FindObjectsInactive.Include);
            var debug = Object.FindFirstObjectByType<Step09_DialogueDebug>(FindObjectsInactive.Include);

            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{scene.name}' 씬에 보스 레이스 · 지역 보스 선택 화면을 만듭니다.\n" +
                    $"- {RootName} (화면 전체, 평소에는 꺼짐)\n" +
                    $"- 이미지 생성: {BossRaceArt.Folder}\n" +
                    $"- {(bossTest != null ? "접수원 '토벌 신청' → 이 화면 → 보스 진입으로 연결" : "주의: Step11_BossEntryTest가 없어 연결 없이 화면만 만듦")}\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기, 이미지 파일은 남음)", "배치", "취소"))
            {
                return;
            }

            BossRaceArt.Generate();
            font = FindFont();
            uiLayer = LayerMask.NameToLayer("UI");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[BossRace 배치] {scene.path}\n");

            var canvasObject = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, UndoName);
            if (uiLayer >= 0) canvasObject.layer = uiLayer;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var ui = canvasObject.AddComponent<BossRaceSelectUI>();

            RectTransform root = Rect("Panel", canvasObject.transform);
            Stretch(root);
            AddImage(root, PageColor, true);

            RectTransform layout = Rect("Layout", root);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.sizeDelta = new Vector2(1280f, 720f);

            RectTransform header = Place("Header", layout, 0f, 0f, 1280f, 63f);
            AddImage(header, HeaderColor);
            AddImage(Place("Line", layout, 0f, 63f, 1280f, 1f), LineColor);

            RectTransform back = Place("BackButton", layout, 28f, 10f, 127f, 43f);
            Image backImage = AddImage(back, HeaderColor, true);
            Image backFrame = AddImage(Place("Frame", back, 0f, 0f, 127f, 43f), new Color32(0x3A, 0x3D, 0x42, 0xFF));
            Sliced(backFrame, 2f);
            var backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backImage;
            Text(back, "Label", 0f, 0f, 127f, 43f, "<   길드 접수처", 13f, TextBody, true, TextAlignmentOptions.Center);

            Text(layout, "Title", 175f, 12f, 600f, 40f, "보스 레이스 <color=#F2A900>·</color> 지역 보스 선택", 25f, TextMain, true, TextAlignmentOptions.MidlineLeft);

            Text(layout, "SeasonLabel", 897f, 11f, 150f, 18f, "시즌 종료까지", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text season = Text(layout, "SeasonValue", 897f, 30f, 150f, 24f, "D-[00]", 16f, TextMain, true, TextAlignmentOptions.Right);
            AddImage(Place("Separator", layout, 1076f, 15f, 1f, 33f), BorderColor);
            Text(layout, "RankLabel", 1022f, 11f, 150f, 18f, "내 길드 랭킹", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text rank = Text(layout, "RankValue", 1022f, 30f, 150f, 24f, "[순위]위", 16f, Gold, true, TextAlignmentOptions.Right);
            Text(layout, "ScoreLabel", 1101f, 11f, 150f, 18f, "시즌 점수", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text score = Text(layout, "ScoreValue", 1101f, 30f, 150f, 24f, "[점수]", 16f, TextMain, true, TextAlignmentOptions.Right);

            Text(layout, "RouteLabel", 28f, 86f, 90f, 18f, "레이스 경로", 12f, TextSub, true, TextAlignmentOptions.MidlineLeft);
            Image dash = AddImage(Place("RouteLine", layout, 100f, 94f, 945f, 2f), new Color32(0x3A, 0x3D, 0x42, 0xFF));
            dash.sprite = BossRaceArt.Dash;
            dash.type = Image.Type.Tiled;
            dash.pixelsPerUnitMultiplier = 2f;
            TMP_Text routeHint = Text(layout, "RouteHint", 652f, 86f, 600f, 18f, "시즌 1위 달성 시 다음 지역 레이스 개방", 12f, TextSub, false, TextAlignmentOptions.MidlineRight);

            List<BossRaceEntry> entries = DefaultEntries();
            var cards = new BossRaceCard[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                cards[i] = BuildCard(layout, i, 28f + i * 310f, 118f);
            }

            RectTransform detail = Place("Detail", layout, 28f, 470f, 908f, 226f);
            AddImage(detail, PanelColor);
            Sliced(AddImage(Place("Frame", detail, 0f, 0f, 908f, 226f), BorderColor), 2f);
            Text(detail, "Caption", 24f, 74f, 260f, 18f, "선택한 보스", 12f, TextSub, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text detailName = Text(detail, "Name", 24f, 90f, 270f, 46f, "호이루", 32f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text detailSub = Text(detail, "Sub", 24f, 138f, 270f, 18f, "지역 1 · 바퀴형", 12.5f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            AddImage(Place("Divider", detail, 291f, 18f, 2f, 190f), DividerColor);
            TMP_Text detailDescription = Text(detail, "Description", 321f, 70f, 560f, 52f, "", 13.5f, TextBody, true, TextAlignmentOptions.TopLeft);
            detailDescription.textWrappingMode = TextWrappingModes.Normal;
            detailDescription.lineSpacing = 18f;
            TMP_Text detailStats = Text(detail, "Stats", 321f, 128f, 560f, 22f, "", 13f, TextSub, false, TextAlignmentOptions.MidlineLeft);

            RectTransform confirm = Place("ConfirmButton", layout, 953f, 470f, 300f, 226f);
            Image confirmImage = AddImage(confirm, Gold, true);
            var confirmButton = confirm.gameObject.AddComponent<Button>();
            confirmButton.targetGraphic = confirmImage;
            confirmButton.transition = Selectable.Transition.None;
            TMP_Text confirmText = Text(confirm, "Label", 0f, 0f, 300f, 226f, "난이도 선택   >", 22f, Dark, true, TextAlignmentOptions.Center);

            var so = new SerializedObject(ui);
            SerializedProperty entriesProp = so.FindProperty("entries");
            entriesProp.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty e = entriesProp.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("bossId").stringValue = entries[i].bossId;
                e.FindPropertyRelative("regionLabel").intValue = entries[i].regionLabel;
                e.FindPropertyRelative("bossName").stringValue = entries[i].bossName;
                e.FindPropertyRelative("typeLabel").stringValue = entries[i].typeLabel;
                e.FindPropertyRelative("tags").stringValue = entries[i].tags;
                e.FindPropertyRelative("description").stringValue = entries[i].description;
                e.FindPropertyRelative("icon").objectReferenceValue = entries[i].icon;
            }
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            SerializedProperty cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++) cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("confirmButton").objectReferenceValue = confirmButton;
            so.FindProperty("confirmImage").objectReferenceValue = confirmImage;
            so.FindProperty("confirmText").objectReferenceValue = confirmText;
            so.FindProperty("seasonText").objectReferenceValue = season;
            so.FindProperty("rankText").objectReferenceValue = rank;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("routeHintText").objectReferenceValue = routeHint;
            so.FindProperty("detailNameText").objectReferenceValue = detailName;
            so.FindProperty("detailSubText").objectReferenceValue = detailSub;
            so.FindProperty("detailDescriptionText").objectReferenceValue = detailDescription;
            so.FindProperty("detailStatsText").objectReferenceValue = detailStats;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            log.AppendLine($"- {RootName} 생성 (카드 {cards.Length}장, 평소에는 Panel 꺼짐)");

            if (bossTest != null)
            {
                var testSo = new SerializedObject(bossTest);
                Undo.RecordObject(bossTest, UndoName);
                testSo.FindProperty("bossSelect").objectReferenceValue = ui;
                testSo.ApplyModifiedProperties();
                log.AppendLine("- Step11_BossEntryTest 연결: 접수원 '토벌 신청' → 보스 선택 → 보스 진입");
            }

            if (debug != null)
            {
                var debugSo = new SerializedObject(debug);
                SerializedProperty bosses = debugSo.FindProperty("regionBosses");
                var ids = new List<string>();
                for (int i = 0; i < bosses.arraySize; i++) ids.Add(bosses.GetArrayElementAtIndex(i).stringValue);
                int added = 0;
                foreach (BossRaceEntry entry in entries.Where(e => !ids.Contains(e.bossId)))
                {
                    bosses.arraySize++;
                    bosses.GetArrayElementAtIndex(bosses.arraySize - 1).stringValue = entry.bossId;
                    added++;
                }
                if (added > 0)
                {
                    Undo.RecordObject(debug, UndoName);
                    debugSo.ApplyModifiedProperties();
                    log.AppendLine($"- Step09 디버그 보스 목록에 {added}개 추가 (G로 전환, Shift+G로 지역 클리어)");
                }
            }

            if (font == null) log.AppendLine("- 주의: 한글 폰트를 찾지 못해 기본 폰트 사용 (글자가 네모로 나오면 폰트 교체)");

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 접수원 E → 토벌 신청.");
            Debug.Log(log.ToString());
        }

        private static BossRaceCard BuildCard(RectTransform parent, int index, float x, float y)
        {
            const float w = 295f;
            const float h = 330f;

            RectTransform card = Place($"Card{index + 1}", parent, x, y, w, h);
            AddImage(card, new Color(0f, 0f, 0f, 0f), true);
            var component = card.gameObject.AddComponent<BossRaceCard>();

            RectTransform content = Place("Content", card, 0f, 0f, w, h);
            var group = content.gameObject.AddComponent<CanvasGroup>();
            Image background = AddImage(Place("Background", content, 0f, 0f, w, h), PanelColor);
            Image frame = AddImage(Place("Frame", content, 0f, 0f, w, h), BorderColor);
            Sliced(frame, 2f);

            Image tag = AddImage(Place("RegionTag", content, 17f, 22f, 53f, 25f), Gold);
            TMP_Text tagText = Text(tag.rectTransform, "Text", 0f, 0f, 53f, 25f, "지역", 12f, Dark, true, TextAlignmentOptions.Center);
            TMP_Text status = Text(content, "Status", w - 145f, 22f, 128f, 25f, "진행 중", 12.5f, Gold, true, TextAlignmentOptions.MidlineRight);
            Image lockIcon = AddImage(Place("LockIcon", content, w - 63f, 27f, 15f, 15f), TextSub);
            lockIcon.sprite = BossRaceArt.Lock;
            lockIcon.preserveAspect = true;

            Image icon = AddImage(Place("Icon", content, (w - 120f) * 0.5f, 75f, 120f, 120f), new Color32(0x9A, 0x9E, 0xA4, 0xFF));
            icon.preserveAspect = true;

            TMP_Text name = Text(content, "Name", 17f, 238f, w - 34f, 38f, "보스", 24f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 24f;
            TMP_Text tags = Text(content, "Tags", 17f, 280f, w - 34f, 20f, "", 12.5f, TextSub, false, TextAlignmentOptions.MidlineLeft);

            var segments = new Image[5];
            float segmentWidth = (w - 34f - 4f * 4f) / 5f;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = AddImage(Place($"Segment{i + 1}", content, 17f + i * (segmentWidth + 4f), 308f, segmentWidth, 4f), DividerColor);
            }

            RectTransform overlay = Place("LockOverlay", card, 0f, 0f, w, h);
            AddImage(overlay, new Color(0.42f, 0.44f, 0.47f, 0.28f));
            overlay.gameObject.SetActive(false);

            var so = new SerializedObject(component);
            so.FindProperty("shakeTarget").objectReferenceValue = card;
            so.FindProperty("content").objectReferenceValue = group;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("regionTag").objectReferenceValue = tag;
            so.FindProperty("regionText").objectReferenceValue = tagText;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("lockIcon").objectReferenceValue = lockIcon;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("tagsText").objectReferenceValue = tags;
            SerializedProperty segmentsProp = so.FindProperty("segments");
            segmentsProp.arraySize = segments.Length;
            for (int i = 0; i < segments.Length; i++) segmentsProp.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
            so.FindProperty("lockOverlay").objectReferenceValue = overlay.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static List<BossRaceEntry> DefaultEntries()
        {
            return new List<BossRaceEntry>
            {
                new BossRaceEntry
                {
                    bossId = "boss.wheel", regionLabel = 1, bossName = "호이루", typeLabel = "바퀴형",
                    tags = "바퀴형 · 튀어오르기 / 돌진 / 내려찍기",
                    description = "바닥과 천장을 튀어 오르며 360도 투사체를 뿌리고, 맵 끝까지 돌진합니다. 돌진 전에 피해를 누적하면 스턴을 노릴 수 있습니다.",
                    icon = BossRaceArt.Wheel,
                },
                new BossRaceEntry
                {
                    bossId = "boss.kyodai", regionLabel = 2, bossName = "쿄다이", typeLabel = "거대 로봇형",
                    tags = "거대 로봇 · 본체는 배경, 팔·다리·렌즈만 피격",
                    description = "거대한 본체는 배경에 있고 팔·다리와 메인 렌즈만 공격할 수 있습니다. 냉각탑과 안테나를 부수면 틈이 생깁니다.",
                    icon = BossRaceArt.Robot,
                },
                new BossRaceEntry
                {
                    bossId = "boss.region3", regionLabel = 3, bossName = "[지역 3 보스명]", typeLabel = "상자형",
                    tags = "상자형 몸통 · 기물 파괴 2 + 부위 파괴 1",
                    description = "보스 기획 후 추가 예정입니다.",
                    icon = BossRaceArt.Box,
                },
                new BossRaceEntry
                {
                    bossId = "boss.final", regionLabel = BossRaceEntry.FinalRegion, bossName = "???", typeLabel = "미공개",
                    tags = "길드 레이스 최종 보스",
                    description = "정체가 알려지지 않은 길드 레이스의 최종 보스입니다.",
                    icon = BossRaceArt.Unknown,
                },
            };
        }

        internal static TMP_FontAsset FindFont()
        {
            var manager = Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            var box = manager != null ? new SerializedObject(manager).FindProperty("dialogueBox").objectReferenceValue as DialogueBox : null;
            if (box != null)
            {
                var boxSo = new SerializedObject(box);
                foreach (string field in new[] { "speakerNameText", "bodyText" })
                {
                    var text = boxSo.FindProperty(field)?.objectReferenceValue as TMP_Text;
                    if (text != null && text.font != null) return text.font;
                }
            }

            TMP_Text any = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.font != null);
            return any != null ? any.font : null;
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            if (uiLayer >= 0) go.layer = uiLayer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static RectTransform Place(string name, Transform parent, float x, float y, float w, float h)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal static Image AddImage(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static void Sliced(Image image, float multiplier)
        {
            image.sprite = BossRaceArt.Frame;
            image.type = Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        internal static TMP_Text Text(Transform parent, string name, float x, float y, float w, float h, string content, float size, Color color, bool bold, TextAlignmentOptions alignment)
        {
            RectTransform rect = Place(name, parent, x, y, w, h);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
