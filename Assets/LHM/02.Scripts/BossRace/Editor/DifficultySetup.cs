using System.Collections.Generic;
using System.Text;
using RealSteel.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ui = RealSteel.BossRace.EditorTools.BossRaceSetup;

namespace RealSteel.BossRace.EditorTools
{
    public static class DifficultySetup
    {
        private const string AllowedFolder = "Assets/LHM/";
        private const string UndoName = "난이도 선택 UI 배치";
        private const string RootName = "[Difficulty] Canvas";

        private static readonly Color PageColor = new Color32(0x13, 0x15, 0x18, 0xFF);
        private static readonly Color HeaderColor = new Color32(0x17, 0x19, 0x1C, 0xFF);
        private static readonly Color LineColor = new Color32(0x2A, 0x2D, 0x31, 0xFF);
        private static readonly Color BorderColor = new Color32(0x33, 0x36, 0x3B, 0xFF);
        private static readonly Color PanelColor = new Color32(0x1B, 0x1E, 0x22, 0xFF);
        private static readonly Color IllustColor = new Color32(0x1F, 0x22, 0x26, 0xFF);
        private static readonly Color BoxColor = new Color32(0x26, 0x29, 0x2E, 0xFF);
        private static readonly Color DividerColor = new Color32(0x2E, 0x31, 0x35, 0xFF);
        private static readonly Color Gold = new Color32(0xF2, 0xA9, 0x00, 0xFF);
        private static readonly Color TextMain = new Color32(0xF0, 0xF1, 0xF3, 0xFF);
        private static readonly Color TextSub = new Color32(0x9A, 0x9E, 0xA4, 0xFF);
        private static readonly Color TextBody = new Color32(0xE2, 0xE4, 0xE7, 0xFF);
        private static readonly Color Dark = new Color32(0x14, 0x16, 0x19, 0xFF);

        [MenuItem("Tools/Dialogue/난이도 선택 UI 배치")]
        private static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(AllowedFolder))
            {
                EditorUtility.DisplayDialog(UndoName, $"Assets/LHM 안의 씬에서만 실행할 수 있습니다.\n현재 씬: {(string.IsNullOrEmpty(scene.path) ? "(저장 안 된 씬)" : scene.path)}", "확인");
                return;
            }

            var existing = Object.FindFirstObjectByType<BossDifficultySelectUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorUtility.DisplayDialog(UndoName, $"난이도 선택 UI가 이미 있습니다. ({existing.name})\n다시 만들려면 지우고 실행하세요.", "확인");
                return;
            }

            var bossSelect = Object.FindFirstObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            var bossTest = Object.FindFirstObjectByType<Step11_BossEntryTest>(FindObjectsInactive.Include);
            if (bossSelect == null)
            {
                EditorUtility.DisplayDialog(UndoName, "보스 레이스 선택 UI가 없습니다. '보스 레이스 선택 UI 배치'를 먼저 실행하세요.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{scene.name}' 씬에 보스 레이스 · 난이도 선택 화면을 만듭니다.\n" +
                    $"- {RootName} (화면 전체, 평소에는 꺼짐)\n" +
                    $"- 이미지 추가 생성: {BossRaceArt.Folder} (slant, hazard, icon_alert)\n" +
                    "- 호이루의 '주로 노리는 부위'가 비어 있으면 시안 값(오른팔, 왼다리, 머리)으로 채움\n" +
                    $"- {(bossTest != null ? "보스 선택 → 난이도 선택 → 레이스 시작으로 연결" : "주의: Step11_BossEntryTest가 없어 연결 없이 화면만 만듦")}\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기, 이미지 파일은 남음)", "배치", "취소"))
            {
                return;
            }

            BossRaceArt.Generate();
            Ui.font = Ui.FindFont();
            Ui.uiLayer = LayerMask.NameToLayer("UI");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[Difficulty 배치] {scene.path}\n");

            var canvasObject = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, UndoName);
            if (Ui.uiLayer >= 0) canvasObject.layer = Ui.uiLayer;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            var ui = canvasObject.AddComponent<BossDifficultySelectUI>();

            RectTransform root = Ui.Rect("Panel", canvasObject.transform);
            Ui.Stretch(root);
            Ui.AddImage(root, PageColor, true);
            RectTransform layout = Ui.Rect("Layout", root);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.sizeDelta = new Vector2(1280f, 720f);

            Ui.AddImage(Ui.Place("Header", layout, 0f, 0f, 1280f, 63f), HeaderColor);
            Ui.AddImage(Ui.Place("Line", layout, 0f, 63f, 1280f, 1f), LineColor);
            RectTransform back = Ui.Place("BackButton", layout, 28f, 10f, 115f, 43f);
            Image backImage = Ui.AddImage(back, HeaderColor, true);
            Ui.Sliced(Ui.AddImage(Ui.Place("Frame", back, 0f, 0f, 115f, 43f), new Color32(0x3A, 0x3D, 0x42, 0xFF)), 2f);
            var backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = backImage;
            Ui.Text(back, "Label", 0f, 0f, 115f, 43f, "<   보스 선택", 13f, TextBody, true, TextAlignmentOptions.Center);
            Ui.Text(layout, "Title", 162f, 12f, 600f, 40f, "보스 레이스 <color=#F2A900>·</color> 난이도 선택", 25f, TextMain, true, TextAlignmentOptions.MidlineLeft);

            Ui.Text(layout, "SeasonLabel", 897f, 11f, 150f, 18f, "시즌 종료까지", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text season = Ui.Text(layout, "SeasonValue", 897f, 30f, 150f, 24f, "D-[00]", 16f, TextMain, true, TextAlignmentOptions.Right);
            Ui.AddImage(Ui.Place("Separator", layout, 1076f, 15f, 1f, 33f), BorderColor);
            Ui.Text(layout, "RankLabel", 1022f, 11f, 150f, 18f, "내 길드 랭킹", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text rank = Ui.Text(layout, "RankValue", 1022f, 30f, 150f, 24f, "[순위]위", 16f, Gold, true, TextAlignmentOptions.Right);
            Ui.Text(layout, "ScoreLabel", 1101f, 11f, 150f, 18f, "시즌 점수", 12.5f, TextSub, false, TextAlignmentOptions.Right);
            TMP_Text score = Ui.Text(layout, "ScoreValue", 1101f, 30f, 150f, 24f, "[점수]", 16f, TextMain, true, TextAlignmentOptions.Right);

            RectTransform bossPanel = Ui.Place("Boss", layout, 28f, 84f, 382f, 612f);
            Ui.AddImage(bossPanel, PanelColor);
            Ui.AddImage(Ui.Place("Illustration", bossPanel, 0f, 0f, 382f, 294f), IllustColor);
            Image regionTag = Ui.AddImage(Ui.Place("RegionTag", bossPanel, 16f, 15f, 77f, 26f), Gold);
            TMP_Text regionText = Ui.Text(regionTag.rectTransform, "Text", 0f, 0f, 77f, 26f, "지역 1 보스", 12f, Dark, true, TextAlignmentOptions.Center);
            Ui.Text(bossPanel, "IllustHint", 206f, 14f, 160f, 20f, "[보스 일러스트]", 12f, TextSub, false, TextAlignmentOptions.MidlineRight);
            Image icon = Ui.AddImage(Ui.Place("Icon", bossPanel, 110f, 70f, 162f, 162f), new Color32(0x6E, 0x72, 0x78, 0xFF));
            icon.preserveAspect = true;
            Image hazard = Ui.AddImage(Ui.Place("Hazard", bossPanel, 0f, 294f, 382f, 8f), Color.white);
            hazard.sprite = BossRaceArt.Hazard;
            hazard.type = Image.Type.Tiled;
            hazard.pixelsPerUnitMultiplier = 2f;
            TMP_Text bossName = Ui.Text(bossPanel, "Name", 20f, 316f, 340f, 46f, "호이루", 34f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text bossTags = Ui.Text(bossPanel, "Tags", 20f, 370f, 340f, 20f, "", 13f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            TMP_Text partsLabel = Ui.Text(bossPanel, "TargetPartsLabel", 20f, 403f, 200f, 18f, "주로 노리는 부위", 12.5f, TextBody, true, TextAlignmentOptions.MidlineLeft);
            var chips = new RectTransform[4];
            var chipTexts = new TMP_Text[4];
            for (int i = 0; i < chips.Length; i++)
            {
                chips[i] = Ui.Place($"Chip{i + 1}", bossPanel, 20f + i * 66f, 428f, 60f, 30f);
                Ui.Sliced(Ui.AddImage(chips[i], new Color32(0x4A, 0x4D, 0x52, 0xFF)), 2f);
                chipTexts[i] = Ui.Text(chips[i], "Text", 0f, 0f, 60f, 30f, "", 13f, TextMain, true, TextAlignmentOptions.Center);
                RectTransform chipText = chipTexts[i].rectTransform;
                chipText.anchorMin = Vector2.zero;
                chipText.anchorMax = Vector2.one;
                chipText.offsetMin = chipText.offsetMax = Vector2.zero;
            }
            RectTransform loseBox = Ui.Place("LoseBox", bossPanel, 20f, 527f, 340f, 64f);
            Ui.AddImage(loseBox, BoxColor);
            Image alert = Ui.AddImage(Ui.Place("Alert", loseBox, 16f, 22f, 20f, 20f), Gold);
            alert.sprite = BossRaceArt.Alert;
            alert.preserveAspect = true;
            TMP_Text loseText = Ui.Text(loseBox, "Lose", 48f, 12f, 285f, 20f, "", 13f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text loseHint = Ui.Text(loseBox, "Hint", 48f, 34f, 285f, 18f, "", 12.5f, TextSub, false, TextAlignmentOptions.MidlineLeft);

            Ui.Text(layout, "DifficultyLabel", 434f, 86f, 100f, 16f, "난이도", 12f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            Ui.Text(layout, "DifficultyHint", 700f, 86f, 228f, 16f, "위험도 · 획득 랭킹 점수", 12f, TextSub, false, TextAlignmentOptions.MidlineRight);

            List<DifficultyEntry> entries = DefaultDifficulties();
            var rows = new DifficultyRow[entries.Count];
            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(layout, i, 430f, 112f + i * 119f);

            RectTransform detail = Ui.Place("Selected", layout, 953f, 84f, 300f, 220f);
            Ui.AddImage(detail, PanelColor);
            Ui.Sliced(Ui.AddImage(Ui.Place("Frame", detail, 0f, 0f, 300f, 220f), Gold), 1f);
            Ui.Text(detail, "Caption", 18f, 20f, 200f, 16f, "선택한 난이도", 12f, TextSub, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text selectedName = Ui.Text(detail, "Name", 18f, 38f, 264f, 42f, "보통", 30f, Gold, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text selectedDescription = Ui.Text(detail, "Description", 18f, 90f, 264f, 44f, "", 13f, TextBody, true, TextAlignmentOptions.TopLeft);
            selectedDescription.textWrappingMode = TextWrappingModes.Normal;
            Ui.AddImage(Ui.Place("Divider", detail, 18f, 142f, 264f, 1f), DividerColor);
            Ui.Text(detail, "ClearScoreLabel", 18f, 154f, 160f, 18f, "클리어 시 랭킹 점수", 13f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            TMP_Text clearScore = Ui.Text(detail, "ClearScore", 150f, 154f, 132f, 18f, "[점수]", 13f, TextMain, true, TextAlignmentOptions.MidlineRight);
            Ui.Text(detail, "BestLabel", 18f, 182f, 160f, 18f, "내 최고 기록", 13f, TextSub, false, TextAlignmentOptions.MidlineLeft);
            TMP_Text best = Ui.Text(detail, "Best", 150f, 182f, 132f, 18f, "기록 없음", 13f, TextMain, true, TextAlignmentOptions.MidlineRight);

            RectTransform partsPanel = Ui.Place("Parts", layout, 953f, 319f, 300f, 184f);
            Ui.AddImage(partsPanel, PanelColor);
            Ui.Sliced(Ui.AddImage(Ui.Place("Frame", partsPanel, 0f, 0f, 300f, 184f), BorderColor), 2f);
            Ui.Text(partsPanel, "Caption", 18f, 16f, 170f, 18f, "내 로봇 파츠 내구도", 12.5f, TextBody, true, TextAlignmentOptions.MidlineLeft);
            RectTransform maintenance = Ui.Place("Maintenance", partsPanel, 196f, 14f, 86f, 22f);
            Image maintenanceHit = Ui.AddImage(maintenance, new Color(1f, 1f, 1f, 0f), true);
            var maintenanceButton = maintenance.gameObject.AddComponent<Button>();
            maintenanceButton.targetGraphic = maintenanceHit;
            maintenanceButton.transition = Selectable.Transition.None;
            Ui.Text(maintenance, "Label", 0f, 0f, 86f, 22f, "<u>정비하기</u>", 12.5f, Gold, false, TextAlignmentOptions.MidlineRight);

            string[] partNames = { "머리", "왼팔", "오른팔", "왼다리", "오른다리" };
            var fills = new RectTransform[partNames.Length];
            var values = new TMP_Text[partNames.Length];
            for (int i = 0; i < partNames.Length; i++)
            {
                float y = 44f + i * 27f;
                Ui.Text(partsPanel, $"Part{i + 1}", 18f, y, 62f, 18f, partNames[i], 13f, TextMain, true, TextAlignmentOptions.MidlineLeft);
                RectTransform bar = Ui.Place($"Bar{i + 1}", partsPanel, 80f, y + 6f, 156f, 7f);
                Ui.AddImage(bar, DividerColor);
                RectTransform fill = Ui.Rect("Fill", bar);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0.5f, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Ui.AddImage(fill, Gold);
                fills[i] = fill;
                values[i] = Ui.Text(partsPanel, $"Value{i + 1}", 238f, y, 44f, 18f, "-", 13f, TextSub, false, TextAlignmentOptions.MidlineRight);
            }

            RectTransform start = Ui.Place("StartButton", layout, 953f, 632f, 300f, 64f);
            Image startImage = Ui.AddImage(start, Gold, true);
            var startButton = start.gameObject.AddComponent<Button>();
            startButton.targetGraphic = startImage;
            Ui.Text(start, "Label", 0f, 0f, 300f, 64f, "레이스 시작   >", 22f, Dark, true, TextAlignmentOptions.Center);

            var so = new SerializedObject(ui);
            SerializedProperty list = so.FindProperty("difficulties");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = entries[i].id;
                e.FindPropertyRelative("label").stringValue = entries[i].label;
                e.FindPropertyRelative("subtitle").stringValue = entries[i].subtitle;
                e.FindPropertyRelative("description").stringValue = entries[i].description;
                e.FindPropertyRelative("risk").intValue = entries[i].risk;
                e.FindPropertyRelative("scoreLabel").stringValue = entries[i].scoreLabel;
            }
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            SetArray(so.FindProperty("rows"), rows);
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("maintenanceButton").objectReferenceValue = maintenanceButton;
            so.FindProperty("seasonText").objectReferenceValue = season;
            so.FindProperty("rankText").objectReferenceValue = rank;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("regionTagText").objectReferenceValue = regionText;
            so.FindProperty("bossIcon").objectReferenceValue = icon;
            so.FindProperty("bossNameText").objectReferenceValue = bossName;
            so.FindProperty("bossTagsText").objectReferenceValue = bossTags;
            so.FindProperty("targetPartsLabel").objectReferenceValue = partsLabel.gameObject;
            SetArray(so.FindProperty("partChips"), chips);
            SetArray(so.FindProperty("partChipTexts"), chipTexts);
            so.FindProperty("loseText").objectReferenceValue = loseText;
            so.FindProperty("loseHintText").objectReferenceValue = loseHint;
            so.FindProperty("selectedNameText").objectReferenceValue = selectedName;
            so.FindProperty("selectedDescriptionText").objectReferenceValue = selectedDescription;
            so.FindProperty("clearScoreText").objectReferenceValue = clearScore;
            so.FindProperty("bestRecordText").objectReferenceValue = best;
            SetArray(so.FindProperty("partFills"), fills);
            SetArray(so.FindProperty("partValueTexts"), values);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            log.AppendLine($"- {RootName} 생성 (난이도 {rows.Length}줄, 평소에는 Panel 꺼짐)");

            var bossSo = new SerializedObject(bossSelect);
            SerializedProperty bossEntries = bossSo.FindProperty("entries");
            for (int i = 0; i < bossEntries.arraySize; i++)
            {
                SerializedProperty e = bossEntries.GetArrayElementAtIndex(i);
                SerializedProperty parts = e.FindPropertyRelative("targetParts");
                if (e.FindPropertyRelative("bossId").stringValue != "boss.wheel" || !string.IsNullOrEmpty(parts.stringValue)) continue;
                Undo.RecordObject(bossSelect, UndoName);
                parts.stringValue = "오른팔, 왼다리, 머리";
                bossSo.ApplyModifiedProperties();
                log.AppendLine("- 호이루 '주로 노리는 부위' = 오른팔, 왼다리, 머리 (시안 값, BossRaceSelectUI Entries에서 수정)");
            }

            if (bossTest != null)
            {
                var testSo = new SerializedObject(bossTest);
                Undo.RecordObject(bossTest, UndoName);
                testSo.FindProperty("difficultySelect").objectReferenceValue = ui;
                testSo.ApplyModifiedProperties();
                log.AppendLine("- Step11_BossEntryTest 연결: 보스 선택 → 난이도 선택 → 레이스 시작");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play → 접수원 E → 토벌 신청 → 보스 선택 → 난이도 선택.");
            Debug.Log(log.ToString());
        }

        private static DifficultyRow BuildRow(RectTransform parent, int index, float x, float y)
        {
            const float w = 502f;
            const float h = 108f;

            RectTransform row = Ui.Place($"Row{index + 1}", parent, x, y, w, h);
            Image background = Ui.AddImage(row, PanelColor, true);
            Image frame = Ui.AddImage(Ui.Place("Frame", row, 0f, 0f, w, h), BorderColor);
            Ui.Sliced(frame, 2f);
            Image stripe = Ui.AddImage(Ui.Place("Stripe", row, 0f, 0f, 4f, h), Gold);
            TMP_Text label = Ui.Text(row, "Label", 24f, 28f, 260f, 32f, "", 22f, TextMain, true, TextAlignmentOptions.MidlineLeft);
            TMP_Text subtitle = Ui.Text(row, "Subtitle", 24f, 62f, 260f, 18f, "", 12.5f, TextSub, false, TextAlignmentOptions.MidlineLeft);

            var bars = new Image[5];
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = Ui.AddImage(Ui.Place($"Risk{i + 1}", row, 302f + i * 14f, 43f, 12f, 22f), DividerColor);
                bars[i].sprite = BossRaceArt.Slant;
            }

            TMP_Text score = Ui.Text(row, "Score", 400f, 40f, 82f, 26f, "[점수]", 16f, TextMain, true, TextAlignmentOptions.MidlineRight);
            var component = row.gameObject.AddComponent<DifficultyRow>();

            var so = new SerializedObject(component);
            so.FindProperty("shiftTarget").objectReferenceValue = row;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("stripe").objectReferenceValue = stripe;
            so.FindProperty("labelText").objectReferenceValue = label;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            SetArray(so.FindProperty("riskBars"), bars);
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static List<DifficultyEntry> DefaultDifficulties()
        {
            return new List<DifficultyEntry>
            {
                new DifficultyEntry { id = "veryEasy", label = "아주 쉬움", subtitle = "처음 만나는 보스 연습용", risk = 1, description = "패턴 속도와 피해가 크게 줄어든 연습 난이도입니다. 보스의 기본 움직임을 익히기 좋습니다." },
                new DifficultyEntry { id = "easy", label = "쉬움", subtitle = "패턴 예고 넉넉함", risk = 2, description = "패턴 예고 시간이 길어 대응할 여유가 있습니다." },
                new DifficultyEntry { id = "normal", label = "보통", subtitle = "기준 난이도", risk = 3, description = "기획 의도대로의 기준 난이도입니다. 모든 기믹과 부위 공격이 등장합니다." },
                new DifficultyEntry { id = "hard", label = "어려움", subtitle = "예고 단축 · 연속 패턴", risk = 4, description = "예고 시간이 짧아지고 패턴이 연속으로 이어집니다." },
                new DifficultyEntry { id = "hell", label = "헬", subtitle = "1위를 노린다면", risk = 5, description = "가장 높은 랭킹 점수를 주는 최고 난이도입니다. 작은 실수도 파츠 파괴로 이어집니다." },
            };
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
