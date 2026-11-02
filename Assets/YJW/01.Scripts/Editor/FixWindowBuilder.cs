using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// YJW 씬의 Canvas/FixWindow 안에 수리/복구 화면을 배치한다. 표시 값은 시안의 샘플 데이터다
public static class FixWindowBuilder
{
    private const string SceneName = "YJW";
    private const string UndoName = "FixWindow 배치";
    private const string RepairPanelName = "RepairPanel";
    private const string RestorePanelName = "RestorePanel";
    private const string FontPath = "Assets/KDU/05.Font/NeoDunggeunmoPro-Regular SDF.asset";
    private const string ImageFolder = "Assets/YJW/03.Images/FixWindow";

    private static readonly Color PanelColor = Hex("15191F");
    private static readonly Color CardColor = Hex("1B2028");
    private static readonly Color SelectedColor = Hex("262B35");
    private static readonly Color LineColor = Hex("3A414C");
    private static readonly Color TextColor = Hex("ECEFF3");
    private static readonly Color SubTextColor = Hex("A4ABB5");
    private static readonly Color Orange = Hex("E98335");
    private static readonly Color Yellow = Hex("EBC045");
    private static readonly Color Red = Hex("E8604C");
    private static readonly Color Blue = Hex("6FB0E8");
    private static readonly Color Purple = Hex("B48CF0");
    private static readonly Color EmptyColor = Hex("2B3039");
    private static readonly Color DarkText = Hex("15191F");

    private static TMP_FontAsset _font;
    private static Sprite _hazardSprite;
    private static Sprite _dashedSprite;

    [MenuItem("Tools/YJW/FixWindow 수리·복구 화면 배치")]
    private static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform window = FindFixWindow(scene);
        if (scene.name != SceneName || window == null)
        {
            EditorUtility.DisplayDialog(UndoName, "YJW 씬을 열고, Canvas/FixWindow가 있는 상태에서 실행하세요.", "확인");
            return;
        }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null)
        {
            EditorUtility.DisplayDialog(UndoName, $"폰트를 찾을 수 없습니다.\n{FontPath}", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog(UndoName, $"FixWindow에 {RepairPanelName}, {RestorePanelName}을 배치합니다.\n같은 이름의 패널이 있으면 지우고 다시 만듭니다.\n씬은 자동 저장하지 않습니다. (Ctrl+Z로 되돌리기)", "배치", "취소"))
            return;

        // 스프라이트 임포트가 Undo 그룹에 섞이지 않도록 먼저 만든다
        _hazardSprite = CreateHazardSprite();
        _dashedSprite = CreateDashedSprite();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveOld(window, RepairPanelName);
        RemoveOld(window, RestorePanelName);

        GameObject repair = BuildRepairPanel(window);
        GameObject restore = BuildRestorePanel(window);
        restore.SetActive(false);
        Undo.RegisterCreatedObjectUndo(repair, UndoName);
        Undo.RegisterCreatedObjectUndo(restore, UndoName);

        StyleHeader(window);
        SetupTab(window, "FixToggle", repair, true);
        SetupTab(window, "RestoreToggle", restore, false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = window.gameObject;
        Debug.Log($"[{UndoName}] 완료. 확인 후 Ctrl+S로 저장하세요.");
    }

    private static Transform FindFixWindow(Scene scene)
    {
        foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == "FixWindow" && rect.gameObject.scene == scene)
                return rect;
        }
        return null;
    }

    private static void RemoveOld(Transform window, string childName)
    {
        Transform old = window.Find(childName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);
    }

    //================================ 상단 헤더 ================================
    private static void StyleHeader(Transform window)
    {
        StyleHeaderBox(window, "Gold", Yellow);
        StyleHeaderBox(window, "Date", TextColor);

        // Date에 붙은 Toggle이 같은 ToggleGroup에 들어 있어 탭 선택을 가로챈다
        Transform date = window.Find("Date");
        Toggle dateToggle = date != null ? date.GetComponent<Toggle>() : null;
        if (dateToggle != null)
            Undo.DestroyObjectImmediate(dateToggle);

        Transform strip = window.Find("Image");
        if (strip != null && strip.TryGetComponent(out Image stripImage))
        {
            Undo.RecordObject(stripImage, UndoName);
            stripImage.sprite = _hazardSprite;
            stripImage.type = Image.Type.Tiled;
            stripImage.color = Color.white;
            stripImage.raycastTarget = false;
            EditorUtility.SetDirty(stripImage);
        }
    }

    private static void StyleHeaderBox(Transform window, string childName, Color textColor)
    {
        Transform box = window.Find(childName);
        if (box == null)
            return;

        if (box.TryGetComponent(out Image image))
        {
            Undo.RecordObject(image, UndoName);
            image.color = PanelColor;
            EditorUtility.SetDirty(image);
        }

        TMP_Text text = box.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            Undo.RecordObject(text, UndoName);
            text.color = textColor;
            text.fontSize = 28;
            EditorUtility.SetDirty(text);
        }
    }

    private static void SetupTab(Transform window, string toggleName, GameObject panel, bool isOn)
    {
        Transform tab = window.Find(toggleName);
        if (tab == null || !tab.TryGetComponent(out Toggle toggle))
        {
            Debug.LogWarning($"[{UndoName}] {toggleName}을 찾지 못해 탭 연결을 건너뜁니다.");
            return;
        }

        Undo.RecordObject(toggle, UndoName);
        toggle.SetIsOnWithoutNotify(isOn);

        // 다시 실행해도 패널 전환 리스너가 쌓이지 않게 지운다
        for (int i = toggle.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (toggle.onValueChanged.GetPersistentMethodName(i) == nameof(GameObject.SetActive))
                UnityEventTools.RemovePersistentListener(toggle.onValueChanged, i);
        }
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, new UnityAction<bool>(panel.SetActive));
        EditorUtility.SetDirty(toggle);

        TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, UndoName);
            label.color = isOn ? DarkText : TextColor;
            label.fontSize = 32;
            EditorUtility.SetDirty(label);
        }

        if (tab.TryGetComponent(out Image image))
        {
            Undo.RecordObject(image, UndoName);
            image.color = isOn ? Orange : PanelColor;
            EditorUtility.SetDirty(image);
        }

        TabColor tabColor = tab.GetComponent<TabColor>();
        if (tabColor == null)
            tabColor = Undo.AddComponent<TabColor>(tab.gameObject);

        var so = new SerializedObject(tabColor);
        so.FindProperty("onColor").colorValue = Orange;
        so.FindProperty("offColor").colorValue = PanelColor;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("onLabelColor").colorValue = DarkText;
        so.FindProperty("offLabelColor").colorValue = TextColor;
        so.ApplyModifiedProperties();
    }

    //================================ 1번 화면: 수리 ================================
    private static GameObject BuildRepairPanel(Transform window)
    {
        GameObject root = Node(window, RepairPanelName);
        Stretch(root, 20, 130, 20, 20);

        const float summaryHeight = 110;
        const float gap = 20;

        GameObject diagnosis = Box(root.transform, "DiagnosisPanel", PanelColor, LineColor);
        StretchVertical(diagnosis, 0, 440, 0, summaryHeight + gap);
        BuildDiagnosis(diagnosis.transform);

        GameObject parts = Box(root.transform, "PartsPanel", PanelColor, LineColor);
        Stretch(parts, 440 + gap, 0, 500 + gap, summaryHeight + gap);
        BuildPartsList(parts.transform);

        GameObject detail = Box(root.transform, "DetailPanel", PanelColor, LineColor);
        StretchVerticalRight(detail, 0, 500, 0, summaryHeight + gap);
        BuildDetail(detail.transform);

        GameObject summary = Box(root.transform, "SummaryBar", PanelColor, LineColor);
        StretchBottom(summary, summaryHeight);
        BuildSummary(summary.transform);

        return root;
    }

    private static void BuildDiagnosis(Transform parent)
    {
        Header(parent, "로봇 진단", "부위를 눌러 선택");

        DiagnosisSlot(parent, "Slot_Head", "패시브\n머리", Yellow, 150, 90, 140, 110, true);
        DiagnosisSlot(parent, "Slot_LeftArm", "Q\n왼팔", Blue, 30, 225, 80, 200, false);
        DiagnosisSlot(parent, "Slot_RightArm", "W\n오른팔", Red, 330, 225, 80, 200, false);
        DiagnosisSlot(parent, "Slot_LeftLeg", "E\n왼다리", Yellow, 130, 445, 80, 200, false);

        GameObject core = Button(parent, "Slot_Core", "", CardColor, TextColor, Blue, 26);
        Place(core, 120, 215, 200, 215);
        GameObject coreIcon = Box(core.transform, "CoreIcon", Blue, Color.clear);
        Place(coreIcon, 72, 22, 56, 56);
        Label(core.transform, "CoreLevel", "CORE Lv.10", 26, TextColor, TextAlignmentOptions.Center, 0, 100, 200, 40);
        Label(core.transform, "CoreState", "정상", 20, Blue, TextAlignmentOptions.Center, 0, 145, 200, 30);

        GameObject empty = Button(parent, "Slot_RightLeg", "R\n빈 슬롯", CardColor, SubTextColor, Color.clear, 22);
        Place(empty, 230, 445, 80, 200);
        Dashed(empty.transform, "Dash", SubTextColor);

        GameObject line = Box(parent, "LegendLine", LineColor, Color.clear);
        StretchBottom(line, 2, 90);

        GameObject legend = Node(parent, "Legend");
        StretchBottom(legend, 80, 5);
        LegendItem(legend.transform, "정상", Blue, 20, 5);
        LegendItem(legend.transform, "손상", Yellow, 110, 5);
        LegendItem(legend.transform, "응급 복구 (내구도 1)", Red, 200, 5);
        LegendItem(legend.transform, "빈 슬롯", SubTextColor, 20, 40);
    }

    private static void DiagnosisSlot(Transform parent, string name, string label, Color border, float x, float y, float w, float h, bool selected)
    {
        GameObject slot = Button(parent, name, label, CardColor, TextColor, border, 22);
        Place(slot, x, y, w, h);
        if (selected)
        {
            GameObject frame = Box(slot.transform, "Selected", Color.clear, TextColor);
            Stretch(frame, -8, -8, -8, -8);
        }
    }

    private static void LegendItem(Transform parent, string text, Color color, float x, float y)
    {
        GameObject mark = Box(parent, $"Mark_{text}", color, Color.clear);
        Place(mark, x, y + 7, 14, 14);
        Label(parent, $"Text_{text}", text, 18, SubTextColor, TextAlignmentOptions.Left, x + 20, y, 240, 28);
    }

    private static void BuildPartsList(Transform parent)
    {
        Header(parent, "장착 파츠", null);
        GameObject clear = Button(parent, "ClearAllButton", "전체 해제", DarkText, TextColor, TextColor, 20);
        PlaceRight(clear, 16, 12, 120, 40);

        Transform content = ScrollList(parent, "PartsScrollView", 64, false);

        PartRow(content, "패시브", "머리", "A사 에픽 머리", "에픽", Purple, 24, 60, "손상", Yellow, "60G", "1시간", true, true);
        PartRow(content, "Q", "왼팔", "B사 레어 왼팔", "레어", Blue, 100, 100, "정상", Blue, "0G", "수리 불필요", false, false);
        PartRow(content, "W", "오른팔", "B사 레어 오른팔", "레어", Blue, 1, 100, "응급 복구", Red, "45G", "1시간", true, false);
        PartRow(content, "E", "왼다리", "C사 보급형 왼다리", "일반", TextColor, 52, 80, "손상", Yellow, "9G", "1시간", true, false);
        PartRow(content, "R", "오른다리", "빈 슬롯", null, Color.clear, 0, 0, "빈 슬롯", SubTextColor, "-", "잔해 보관 중", false, false);
    }

    private static void PartRow(Transform parent, string key, string slot, string partName, string grade, Color gradeColor, int current, int max, string state, Color stateColor, string cost, string time, bool isChecked, bool selected)
    {
        GameObject row = Box(parent, $"Row_{slot}", selected ? SelectedColor : PanelColor, selected ? TextColor : LineColor);
        row.AddComponent<LayoutElement>().preferredHeight = 118;

        CheckBox(row.transform, isChecked);

        GameObject keyBox = Box(row.transform, "KeyBox", DarkText, SubTextColor);
        Place(keyBox, 70, 16, key.Length > 1 ? 90 : 40, 40);
        FillLabel(keyBox.transform, key, 22, TextColor);

        float nameX = key.Length > 1 ? 172 : 122;
        Label(row.transform, "Slot", slot, 20, SubTextColor, TextAlignmentOptions.Left, nameX, 20, 80, 32);
        Label(row.transform, "Name", partName, 24, TextColor, TextAlignmentOptions.Left, nameX + 80, 20, 260, 32);
        if (grade != null)
            Badge(row.transform, grade, gradeColor, nameX + 340, 18, 60, 34);

        Segments(row.transform, "Durability", 70, 72, 300, 16, 15, current, max, stateColor);
        Label(row.transform, "DurabilityText", max > 0 ? $"{current}/{max}" : "-", 20, TextColor, TextAlignmentOptions.Left, 385, 62, 90, 34);
        GameObject mark = Box(row.transform, "StateMark", stateColor, Color.clear);
        Place(mark, 480, 73, 12, 12);
        Label(row.transform, "State", state, 20, stateColor, TextAlignmentOptions.Left, 498, 62, 130, 34);

        LabelRight(row.transform, "Cost", cost, 26, TextColor, 16, 20, 110, 36);
        LabelRight(row.transform, "Time", time, 18, SubTextColor, 16, 66, 150, 28);
    }

    private static void CheckBox(Transform parent, bool isChecked)
    {
        GameObject box = Box(parent, "CheckBox", EmptyColor, SubTextColor);
        Place(box, 20, 42, 32, 32);
        box.GetComponent<Image>().raycastTarget = true;

        GameObject mark = Box(box.transform, "CheckMark", Orange, Color.clear);
        Stretch(mark, 4, 4, 4, 4);

        Toggle toggle = box.AddComponent<Toggle>();
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = mark.GetComponent<Image>();
        toggle.isOn = isChecked;
    }

    private static void BuildDetail(Transform parent)
    {
        Header(parent, "부위 상세", "머리 · 패시브");

        Label(parent, "PartName", "A사 에픽 머리", 36, TextColor, TextAlignmentOptions.Left, 24, 84, 450, 50);
        Badge(parent, "에픽", Purple, 24, 142, 64, 34);
        Badge(parent, "손상", Yellow, 98, 142, 64, 34);

        Label(parent, "DurabilityLabel", "내구도", 20, SubTextColor, TextAlignmentOptions.Left, 24, 210, 150, 34);
        LabelRight(parent, "DurabilityValue", "24 / 60", 36, TextColor, 24, 198, 220, 50);
        Segments(parent, "DurabilityBar", 24, 254, 452, 18, 20, 24, 60, Yellow);

        GameObject box = Dashed(parent, "CostBox", SubTextColor);
        PlaceStretchWidth(box, 24, 24, 292, 230);
        CostLine(box.transform, "BaseCost", "부위 기본 수리비 (머리)", "40G", 16);
        CostLine(box.transform, "GradeRate", "× 등급 배율 (에픽)", "2.5", 56);
        CostLine(box.transform, "LostDurability", "× 잃은 내구도", "60%", 96);
        GameObject line = Box(box.transform, "Line", LineColor, Color.clear);
        PlaceStretchWidth(line, 16, 16, 140, 2);
        Label(box.transform, "TotalLabel", "= 수리비 (반올림)", 22, TextColor, TextAlignmentOptions.Left, 18, 160, 280, 50);
        LabelRight(box.transform, "TotalValue", "60G", 34, Orange, 18, 160, 150, 50);

        DetailLine(parent, "Time", "소요 시간", "1시간", 545);
        DetailLine(parent, "ZeroDurability", "전투 중 0이 되면", "패시브 스킬 해제", 590);
        DetailLine(parent, "BreakChance", "패배 시 파괴 확률", "20% (등급 기준)", 635);
    }

    private static void CostLine(Transform parent, string name, string left, string right, float y)
    {
        Label(parent, $"{name}Label", left, 20, SubTextColor, TextAlignmentOptions.Left, 18, y, 320, 32);
        LabelRight(parent, $"{name}Value", right, 20, TextColor, 18, y, 110, 32);
    }

    private static void DetailLine(Transform parent, string name, string left, string right, float y)
    {
        Label(parent, $"{name}Label", left, 20, SubTextColor, TextAlignmentOptions.Left, 24, y, 220, 34);
        LabelRight(parent, $"{name}Value", right, 20, TextColor, 24, y, 240, 34);
    }

    private static void BuildSummary(Transform parent)
    {
        SummaryItem(parent, "Selected", "선택", "3", "부위", TextColor, 30);
        SummaryItem(parent, "Cost", "수리비", "114", "G", Orange, 170);
        SummaryItem(parent, "Time", "소요 시간", "3", "시간", TextColor, 330);
        SummaryItem(parent, "Balance", "수리 후 잔액", "1,126", "G", TextColor, 490);

        LabelRight(parent, "FinishTime", "완료 예정 04월 12일 12시", 20, SubTextColor, 240, 38, 320, 34);
        GameObject start = Button(parent, "StartRepairButton", "수리 시작", Orange, DarkText, Color.clear, 30);
        PlaceRight(start, 20, 18, 200, 74);
    }

    private static void SummaryItem(Transform parent, string name, string title, string value, string unit, Color valueColor, float x)
    {
        Label(parent, $"{name}Title", title, 18, SubTextColor, TextAlignmentOptions.Left, x, 14, 150, 28);
        Label(parent, $"{name}Value", $"{value} <size=60%>{unit}</size>", 36, valueColor, TextAlignmentOptions.Left, x, 46, 170, 50);
    }

    //================================ 2번 화면: 복구 ================================
    private static GameObject BuildRestorePanel(Transform window)
    {
        GameObject root = Node(window, RestorePanelName);
        Stretch(root, 20, 130, 20, 20);

        const float bannerHeight = 70;
        const float gap = 20;

        GameObject banner = Box(root.transform, "WarningBanner", Hex("2A2413"), Yellow);
        StretchTop(banner, bannerHeight);
        Label(banner.transform, "WarningTitle", "패배 후 정비", 24, Yellow, TextAlignmentOptions.Left, 24, 18, 170, 34);
        Label(banner.transform, "WarningText", "코어가 불안정합니다. 코어를 복구해야 로봇을 다시 조립할 수 있습니다.", 22, TextColor, TextAlignmentOptions.Left, 200, 18, 1100, 34);

        GameObject parts = Box(root.transform, "DestroyedPartsPanel", PanelColor, LineColor);
        Stretch(parts, 0, bannerHeight + gap, 520 + gap, 0);
        Header(parts.transform, "파괴된 파츠 (잔해)", "복구 가능 2 · 재료 부족 1 · 복구 불가 1 — 복구 시 4시간, 내구도 100%");
        Transform content = ScrollList(parts.transform, "DestroyedScrollView", 64, true);

        WreckCard(content, "패시브", "머리 · 잔해", "A사 전설 머리", "전설", Orange, "600G", "전설 부품 1개 <color=#E8604C>보유 0</color>", "재료 부족", false);
        WreckCard(content, "W", "오른팔 · 잔해", "B사 레어 오른팔", "레어", Blue, "120G", "없음", "복구 · 120G", true);
        WreckCard(content, "R", "오른다리 · 잔해", "C사 에픽 오른다리", "에픽", Purple, "200G", "에픽 부품 1개 <color=#6FB0E8>보유 2</color>", "복구 · 200G + 부품 1", true);
        WreckCard(content, "E", "왼다리 · 잔해", "C사 보급형 왼다리", "일반", TextColor, null, null, null, false);

        GameObject core = Box(root.transform, "CoreRestorePanel", PanelColor, Red);
        StretchVerticalRight(core, bannerHeight + gap, 520, 0, 0);
        BuildCoreRestore(core.transform);

        return root;
    }

    private static void WreckCard(Transform parent, string key, string slot, string partName, string grade, Color gradeColor, string cost, string material, string actionLabel, bool canRestore)
    {
        GameObject card = Box(parent, $"Card_{partName}", CardColor, LineColor);

        GameObject icon = Dashed(card.transform, "Icon", SubTextColor);
        Place(icon, 22, 22, 90, 90);
        FillLabel(icon.transform, key, key.Length > 1 ? 20 : 32, TextColor);

        Label(card.transform, "Slot", slot, 18, SubTextColor, TextAlignmentOptions.Left, 130, 22, 300, 28);
        Label(card.transform, "Name", partName, 24, TextColor, TextAlignmentOptions.Left, 130, 58, 290, 34);
        Badge(card.transform, grade, gradeColor, 425, 56, 64, 36);

        // 일반 등급은 복구할 수 없어 안내 문구와 버리기 버튼만 둔다
        if (cost == null)
        {
            GameObject guide = Label(card.transform, "Guide", "일반 등급은 복구할 수 없습니다. 고물상에서 부품을 모아 새로 제작하세요.", 20, SubTextColor, TextAlignmentOptions.TopLeft, 22, 135, 590, 90);
            guide.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;

            GameObject discardOnly = Button(card.transform, "DiscardButton", "버리기", PanelColor, TextColor, SubTextColor, 22);
            Place(discardOnly, 22, 255, 596, 70);
            return;
        }

        Label(card.transform, "CostLabel", "복구비", 20, SubTextColor, TextAlignmentOptions.Left, 22, 130, 180, 32);
        LabelRight(card.transform, "CostValue", cost, 22, TextColor, 22, 130, 200, 32);
        Label(card.transform, "MaterialLabel", "추가 재료", 20, SubTextColor, TextAlignmentOptions.Left, 22, 175, 180, 32);
        LabelRight(card.transform, "MaterialValue", material, 20, TextColor, 22, 175, 400, 32);

        GameObject action = Button(card.transform, "RestoreButton", actionLabel, canRestore ? PanelColor : EmptyColor, canRestore ? TextColor : SubTextColor, canRestore ? Orange : LineColor, 22);
        Place(action, 22, 255, 460, 70);
        action.GetComponent<Button>().interactable = canRestore;

        GameObject discard = Button(card.transform, "DiscardButton", "버리기", PanelColor, TextColor, SubTextColor, 20);
        Place(discard, 498, 255, 120, 70);
    }

    private static void BuildCoreRestore(Transform parent)
    {
        Header(parent, "코어 복구", null);
        Badge(parent, "불안정한 코어", Red, 340, 14, 160, 36);

        GameObject icon = Box(parent, "CoreIcon", Hex("3A1B18"), Red);
        Place(icon, 24, 82, 110, 110);
        GameObject iconInner = Box(icon.transform, "Inner", Red, Color.clear);
        Stretch(iconInner, 28, 28, 28, 28);

        Label(parent, "CoreLevel", "CORE Lv.10", 36, TextColor, TextAlignmentOptions.Left, 152, 94, 340, 50);
        Label(parent, "CoreGuide", "복구 전에는 로봇을 조립할 수 없습니다.", 18, SubTextColor, TextAlignmentOptions.Left, 152, 148, 350, 30);

        GameObject box = Dashed(parent, "CostBox", SubTextColor);
        PlaceStretchWidth(box, 24, 24, 215, 180);
        CostLine(box.transform, "BaseCost", "기본 복구비", "100G", 14);
        CostLine(box.transform, "LevelCost", "+ 코어 레벨 10 × 50G", "500G", 54);
        GameObject line = Box(box.transform, "Line", LineColor, Color.clear);
        PlaceStretchWidth(line, 16, 16, 98, 2);
        Label(box.transform, "TotalLabel", "= 복구비", 22, TextColor, TextAlignmentOptions.Left, 18, 112, 200, 50);
        LabelRight(box.transform, "TotalValue", "600G", 36, Orange, 18, 112, 180, 50);

        DetailLine(parent, "Time", "소요 시간", "4시간 · 완료 04월 12일 13시", 412);
        DetailLine(parent, "Balance", "복구 후 잔액", "640G", 452);

        GameObject start = Button(parent, "StartCoreRestoreButton", "코어 복구 시작", Orange, DarkText, Color.clear, 30);
        PlaceStretchWidth(start, 24, 24, 505, 84);

        GameObject line2 = Box(parent, "RouteLine", LineColor, Color.clear);
        PlaceStretchWidth(line2, 24, 24, 630, 2);
        GameObject route = Label(parent, "RebirthRoute", "<color=#ECEFF3>회생 루트</color> · 아주 쉬움 토벌은 신청비 0, 파괴 판정 없음. 빈 슬롯은 기본 파츠로 채워집니다.", 18, SubTextColor, TextAlignmentOptions.TopLeft, 24, 648, 470, 90);
        route.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
    }

    //================================ 공용 UI 부품 ================================
    private static void Header(Transform parent, string title, string description)
    {
        Label(parent, "Title", title, 28, TextColor, TextAlignmentOptions.Left, 24, 14, 300, 38);
        if (description != null)
            LabelRight(parent, "Description", description, 18, SubTextColor, 18, 20, 800, 28);

        GameObject line = Box(parent, "HeaderLine", LineColor, Color.clear);
        PlaceStretchWidth(line, 0, 0, 62, 2);
    }

    private static void Badge(Transform parent, string text, Color color, float x, float y, float w, float h)
    {
        GameObject badge = Box(parent, $"Badge_{text}", Color.clear, color);
        Place(badge, x, y, w, h);
        FillLabel(badge.transform, text, 18, color);
    }

    private static void Segments(Transform parent, string name, float x, float y, float w, float h, int count, int current, int max, Color color)
    {
        GameObject bar = Node(parent, name);
        Place(bar, x, y, w, h);

        // 값이 1이라도 있으면 한 칸은 보이게 한다
        int filled = max > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)current / max * count), 0, count) : 0;
        float cell = (w - (count - 1) * 3) / count;
        for (int i = 0; i < count; i++)
        {
            GameObject segment = Box(bar.transform, $"Cell{i}", i < filled ? color : EmptyColor, Color.clear);
            Place(segment, i * (cell + 3), 0, cell, h);
        }
    }

    // 스크롤 뷰를 만들고 항목을 넣을 Content를 돌려준다
    private static Transform ScrollList(Transform parent, string name, float top, bool grid)
    {
        GameObject root = Node(parent, name);
        Stretch(root, 0, top, 0, 0);

        GameObject viewport = Node(root.transform, "Viewport");
        Stretch(viewport, 0, 0, 0, 0);
        viewport.AddComponent<RectMask2D>();

        GameObject content = Node(viewport.transform, "Content");
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        if (grid)
        {
            GridLayoutGroup layout = content.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = new Vector2(16, 16);
            layout.cellSize = new Vector2(640, 345);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
        }
        else
        {
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject track = Box(root.transform, "Scrollbar Vertical", EmptyColor, Color.clear);
        StretchVerticalRight(track, 0, 12, 0, 0);
        track.GetComponent<Image>().raycastTarget = true;

        GameObject area = Node(track.transform, "Sliding Area");
        Stretch(area, 0, 0, 0, 0);
        GameObject handle = Box(area.transform, "Handle", SubTextColor, Color.clear);
        Stretch(handle, 0, 0, 0, 0);
        handle.GetComponent<Image>().raycastTarget = true;

        Scrollbar scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // 드래그를 받을 투명 배경
        Image raycast = root.AddComponent<Image>();
        raycast.color = Color.clear;

        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4;

        return content.transform;
    }

    private static GameObject Button(Transform parent, string name, string label, Color fill, Color textColor, Color border, float fontSize)
    {
        GameObject go = Box(parent, name, fill, border);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(label))
            FillLabel(go.transform, label, fontSize, textColor);

        return go;
    }

    private static GameObject Dashed(Transform parent, string name, Color color)
    {
        GameObject go = Node(parent, name);
        Stretch(go, 0, 0, 0, 0);

        Image image = go.AddComponent<Image>();
        image.sprite = _dashedSprite;
        image.type = Image.Type.Tiled;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static GameObject Box(Transform parent, string name, Color fill, Color border)
    {
        GameObject go = Node(parent, name);
        Image image = go.AddComponent<Image>();
        image.color = fill;
        image.raycastTarget = false;

        if (border.a > 0)
        {
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2, -2);
        }

        return go;
    }

    private static void FillLabel(Transform parent, string text, float size, Color color)
    {
        GameObject go = Label(parent, "Label", text, size, color, TextAlignmentOptions.Center, 0, 0, 0, 0);
        Stretch(go, 0, 0, 0, 0);
    }

    private static GameObject Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment, float x, float y, float w, float h)
    {
        GameObject go = Node(parent, name);
        Place(go, x, y, w, h);
        SetText(go, text, size, color, alignment);
        return go;
    }

    private static GameObject LabelRight(Transform parent, string name, string text, float size, Color color, float right, float y, float w, float h)
    {
        GameObject go = Node(parent, name);
        PlaceRight(go, right, y, w, h);
        SetText(go, text, size, color, TextAlignmentOptions.Right);
        return go;
    }

    private static void SetText(GameObject go, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
    }

    private static GameObject Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    //================================ 배치 도우미 ================================
    private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 왼쪽 위 기준. y는 아래로 갈수록 커진다
    private static void Place(GameObject go, float x, float y, float w, float h)
    {
        SetRect(go, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
    }

    private static void PlaceRight(GameObject go, float right, float y, float w, float h)
    {
        SetRect(go, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -y), new Vector2(w, h));
    }

    private static void PlaceStretchWidth(GameObject go, float left, float right, float y, float h)
    {
        SetRect(go, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(left, -y), new Vector2(-left - right, h));
    }

    private static void Stretch(GameObject go, float left, float top, float right, float bottom)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchVertical(GameObject go, float left, float width, float top, float bottom)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(left + width, -top);
    }

    private static void StretchVerticalRight(GameObject go, float top, float width, float right, float bottom)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, 0.5f);
        rect.offsetMin = new Vector2(-right - width, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchTop(GameObject go, float height)
    {
        SetRect(go, new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, height));
    }

    private static void StretchBottom(GameObject go, float height, float bottom = 0)
    {
        SetRect(go, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, bottom), new Vector2(0, height));
    }

    private static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString($"#{rgb}", out Color color);
        return color;
    }

    //================================ 스프라이트 생성 ================================
    private static Sprite CreateHazardSprite()
    {
        const int width = 40;
        const int height = 20;
        Color32 stripe = Orange;
        Color32 clear = new Color32(0, 0, 0, 0);

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = (x + y) % width < 20 ? stripe : clear;
        }

        return SaveSprite("Hazard.png", width, height, pixels, Vector4.zero);
    }

    private static Sprite CreateDashedSprite()
    {
        const int size = 16;
        const int border = 2;
        const int dash = 8;
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool horizontalEdge = (y < border || y >= size - border) && x % size < dash;
                bool verticalEdge = (x < border || x >= size - border) && y % size < dash;
                pixels[y * size + x] = horizontalEdge || verticalEdge ? white : clear;
            }
        }

        return SaveSprite("DashedFrame.png", size, size, pixels, new Vector4(border, border, border, border));
    }

    private static Sprite SaveSprite(string fileName, int width, int height, Color32[] pixels, Vector4 border)
    {
        Directory.CreateDirectory(ImageFolder);
        string path = $"{ImageFolder}/{fileName}";

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100;   // 캔버스 기준과 맞춰 1픽셀로 표시
        importer.spriteBorder = border;

        // Tiled 이미지는 FullRect 메시여야 경고 없이 반복된다
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
