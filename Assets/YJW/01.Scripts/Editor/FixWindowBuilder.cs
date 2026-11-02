using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// YJW 씬의 Canvas/FixWindow 안에 수리/복구 화면을 SYS/UI 리소스로 배치한다. 표시 값은 샘플 데이터다
public static class FixWindowBuilder
{
    private const string SceneName = "YJW";
    private const string UndoName = "FixWindow 배치";
    private const string RepairPanelName = "RepairPanel";
    private const string RestorePanelName = "RestorePanel";
    private const string FontPath = "Assets/KDU/05.Font/NeoDunggeunmoPro-Regular SDF.asset";
    private const string UiRoot = "Assets/SYS/UI";

    // SYS/UI 리소스에서 뽑은 색
    private static readonly Color Yellow = Hex("FFE800");
    private static readonly Color Cyan = Hex("35D0DE");
    private static readonly Color Bad = Hex("FF4D2E");
    private static readonly Color Good = Hex("52E08F");
    private static readonly Color Warning = Hex("FF8A1E");
    private static readonly Color Bone = Hex("EEEDE8");
    private static readonly Color Neutral = Hex("B4B9C0");
    private static readonly Color Ink = Hex("1A1B1D");

    internal static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static TMP_FontAsset _font;

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

        if (!EditorUtility.DisplayDialog(UndoName, $"FixWindow의 {RepairPanelName}, {RestorePanelName}을 지우고 SYS/UI 리소스로 다시 만듭니다.\n사용하는 SYS/UI 이미지는 Sprite(Single)와 9-slice 값으로 임포트합니다.\n씬은 자동 저장하지 않습니다. (씬 변경은 Ctrl+Z로 되돌리기)", "배치", "취소"))
            return;

        // 임포트가 Undo 그룹에 섞이지 않도록 먼저 끝낸다
        string missing = PrepareSprites();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(UndoName, $"스프라이트를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

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

    // 화면을 지우지 않고 FixWindowController만 붙이고 스프라이트를 채운다
    [MenuItem("Tools/YJW/FixWindow 데이터 연결")]
    private static void Connect()
    {
        const string title = "FixWindow 데이터 연결";

        Scene scene = SceneManager.GetActiveScene();
        Transform window = FindFixWindow(scene);
        if (scene.name != SceneName || window == null || window.Find(RepairPanelName) == null)
        {
            EditorUtility.DisplayDialog(title, "YJW 씬을 열고, FixWindow 화면을 먼저 배치한 상태에서 실행하세요.", "확인");
            return;
        }

        string missing = PrepareSprites();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(title, $"스프라이트를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

        FixWindowController controller = window.GetComponent<FixWindowController>();
        if (controller == null)
            controller = Undo.AddComponent<FixWindowController>(window.gameObject);

        var so = new SerializedObject(controller);
        so.FindProperty("_statusCool").objectReferenceValue = Sprites["ic_status_cool"];
        so.FindProperty("_statusWarm").objectReferenceValue = Sprites["ic_status_warm"];
        so.FindProperty("_statusHot").objectReferenceValue = Sprites["ic_status_hot"];
        so.FindProperty("_cellGood").objectReferenceValue = Sprites["bg_statusButton_good_enabled"];
        so.FindProperty("_cellWarning").objectReferenceValue = Sprites["bg_statusButton_warning_enabled"];
        so.FindProperty("_cellBad").objectReferenceValue = Sprites["bg_statusButton_bad_enabled"];
        so.FindProperty("_cellEmpty").objectReferenceValue = Sprites["swatch_v2"];
        so.FindProperty("_badgeNeutral").objectReferenceValue = Sprites["bg_statusButton_neutral_enabled"];
        so.FindProperty("_badgeCyan").objectReferenceValue = Sprites["bg_miniButton_secondary_enabled"];
        so.FindProperty("_badgeOrange").objectReferenceValue = Sprites["bg_statusButton_warning_enabled"];
        so.FindProperty("_badgeYellow").objectReferenceValue = Sprites["bg_miniButton_primary_pressed"];
        so.FindProperty("_badgeRed").objectReferenceValue = Sprites["bg_statusButton_bad_enabled"];
        so.FindProperty("_badgeGreen").objectReferenceValue = Sprites["bg_statusButton_good_enabled"];
        so.FindProperty("_rowNormal").objectReferenceValue = Sprites["bg_selectItem_enabled"];
        so.FindProperty("_rowSelected").objectReferenceValue = Sprites["bg_selectItem_hover"];
        so.FindProperty("_slotNormal").objectReferenceValue = Sprites["bg_button_secondary_enabled"];
        so.FindProperty("_slotSelected").objectReferenceValue = Sprites["bg_button_primary_enabled"];
        so.FindProperty("_localTimeSystem").objectReferenceValue = EnsureLocalTimeSystem(window);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[{title}] 완료. Ctrl+S로 저장하세요.");
    }

    // YJW 씬에는 시간 시스템이 없어 단독 실행용을 꺼둔 채로 둔다. 켜는 건 FixWindowController가 정한다
    private static GameObject EnsureLocalTimeSystem(Transform window)
    {
        const string name = "LocalTimeSystem";

        Transform found = window.Find(name);
        GameObject go = found != null ? found.gameObject : new GameObject(name);
        if (found == null)
        {
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            go.transform.SetParent(window, false);
        }

        TimeSystemManager time = go.GetComponent<TimeSystemManager>();
        if (time == null)
            time = Undo.AddComponent<TimeSystemManager>(go);

        Transform date = window.Find("Date");
        CanvasGroup group = date.GetComponent<CanvasGroup>();
        if (group == null)
            group = Undo.AddComponent<CanvasGroup>(date.gameObject);

        Undo.RecordObject(time, UndoName);
        time.timeText = date.GetComponentInChildren<TextMeshProUGUI>(true);
        time.timeCanvasGroup = group;
        EditorUtility.SetDirty(time);

        go.SetActive(false);
        return go;
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

    //================================ 스프라이트 준비 ================================
    // 쓰는 파일만 Single + 9-slice로 임포트한다. 경계값은 PNG 픽셀을 재서 정했다
    internal static string PrepareSprites()
    {
        var table = new Dictionary<string, Vector4>();

        AddStates(table, "BG/Button/button/bg_button_primary", 16, 34, "enabled", "hover", "pressed", "disabled");
        AddStates(table, "BG/Button/button/bg_button_secondary", 16, 34, "enabled", "hover", "pressed", "disabled");
        AddStates(table, "BG/Button/miniButton/bg_miniButton_default", 18, 22, "enabled", "hover", "pressed", "disabled");
        AddStates(table, "BG/Button/miniButton/bg_miniButton_primary", 18, 22, "enabled", "hover", "pressed");
        AddStates(table, "BG/Button/miniButton/bg_miniButton_secondary", 18, 22, "enabled", "hover", "pressed");
        AddStates(table, "BG/Button/selectItem/bg_selectItem", 4, 16, "enabled", "hover");
        AddStates(table, "BG/Button/statusButton/bg_statusButton_good", 6, 16, "enabled");
        AddStates(table, "BG/Button/statusButton/bg_statusButton_bad", 6, 16, "enabled");
        AddStates(table, "BG/Button/statusButton/bg_statusButton_warning", 6, 16, "enabled");
        AddStates(table, "BG/Button/statusButton/bg_statusButton_neutral", 6, 16, "enabled");
        AddStates(table, "BG/Button/iconButton/bg_iconButton_v1_sm", 20, 20, "enabled", "hover", "pressed");
        AddStates(table, "BG/Button/iconButton/bg_iconButton_v1_lg", 20, 20, "enabled", "disabled");
        AddStates(table, "BG/Button/picker/bg_picker", 24, 24, "populated");

        table["BG/Panel/bg_panelLarge"] = Uniform(6);
        table["BG/Panel/bg_panelSmall"] = Uniform(90);
        table["BG/Panel/bg_panelWide_chamfer"] = Uniform(66);
        table["BG/Swatch/swatch_v1"] = Vector4.zero;
        table["BG/Swatch/swatch_v2"] = Vector4.zero;
        table["BG/Swatch/swatch_v3"] = Uniform(6);

        table["Frame/Panel/panelLarge/frame_panelLarge_v2"] = new Vector4(410, 246, 410, 246);
        table["Frame/Panel/panelLarge/frame_panelLarge_v3"] = new Vector4(156, 148, 156, 148);
        table["Frame/Panel/panelMedium/frame_panelMedium_v3"] = new Vector4(290, 174, 290, 174);
        table["Frame/Panel/panelMedium/frame_panelMedium_v4"] = new Vector4(238, 243, 238, 243);
        table["Frame/Panel/panelMedium/frame_panelMedium_v8"] = new Vector4(220, 216, 220, 216);
        table["Frame/Panel/panelMedium/frame_panelMedium_v9"] = new Vector4(282, 267, 282, 267);

        foreach (string icon in new[] { "ic_done", "ic_center_focus_strong", "ic_notifications_none", "ic_cancel", "ic_delete_outline", "ic_keyboard_arrow_down" })
            table[$"Icon/Material/{icon}"] = Vector4.zero;
        foreach (string icon in new[] { "ic_status_cool", "ic_status_warm", "ic_status_hot" })
            table[$"Icon/Status/{icon}"] = Vector4.zero;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (KeyValuePair<string, Vector4> pair in table)
                ApplyImport($"{UiRoot}/{pair.Key}.png", pair.Value);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Sprites.Clear();
        foreach (string key in table.Keys)
        {
            string path = $"{UiRoot}/{key}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                return path;
            Sprites[Path.GetFileName(key)] = sprite;
        }
        return null;
    }

    private static void AddStates(Dictionary<string, Vector4> table, string prefix, float horizontal, float vertical, params string[] states)
    {
        foreach (string state in states)
            table[$"{prefix}_{state}"] = new Vector4(horizontal, vertical, horizontal, vertical);
    }

    private static Vector4 Uniform(float value) => new Vector4(value, value, value, value);

    private static void ApplyImport(string path, Vector4 border)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            return;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool same = importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == SpriteImportMode.Single
            && importer.spriteBorder == border
            && settings.spriteMeshType == SpriteMeshType.FullRect;
        if (same)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;

        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    //================================ 상단 헤더 ================================
    private static void StyleHeader(Transform window)
    {
        StyleHeaderBox(window, "Gold", Yellow);
        StyleHeaderBox(window, "Date", Bone);

        // Date에 Toggle이 남아 있으면 탭 선택을 가로챈다
        Transform date = window.Find("Date");
        Toggle dateToggle = date != null ? date.GetComponent<Toggle>() : null;
        if (dateToggle != null)
            Undo.DestroyObjectImmediate(dateToggle);

        // 줄무늬 자리는 노란 구분선으로 쓴다
        Transform strip = window.Find("Image");
        if (strip != null && strip.TryGetComponent(out Image stripImage))
        {
            Undo.RecordObject(stripImage, UndoName);
            stripImage.sprite = Sprites["swatch_v1"];
            stripImage.type = Image.Type.Simple;
            stripImage.color = Color.white;
            stripImage.raycastTarget = false;
            EditorUtility.SetDirty(stripImage);

            RectTransform rect = strip.GetComponent<RectTransform>();
            Undo.RecordObject(rect, UndoName);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 4);
            EditorUtility.SetDirty(rect);
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
            ApplySprite(image, "bg_miniButton_default_enabled", 1.5f);
            image.color = Color.white;
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

        Sprite onSprite = Sprites["bg_button_primary_enabled"];
        Sprite offSprite = Sprites["bg_button_secondary_enabled"];

        TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, UndoName);
            label.color = isOn ? Ink : Bone;
            label.fontSize = 32;
            EditorUtility.SetDirty(label);
        }

        if (tab.TryGetComponent(out Image image))
        {
            Undo.RecordObject(image, UndoName);
            ApplySprite(image, isOn ? "bg_button_primary_enabled" : "bg_button_secondary_enabled", 2);
            image.color = Color.white;
            EditorUtility.SetDirty(image);
        }

        TabColor tabColor = tab.GetComponent<TabColor>();
        if (tabColor == null)
            tabColor = Undo.AddComponent<TabColor>(tab.gameObject);

        var so = new SerializedObject(tabColor);
        so.FindProperty("onColor").colorValue = Color.white;
        so.FindProperty("offColor").colorValue = Color.white;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("onLabelColor").colorValue = Ink;
        so.FindProperty("offLabelColor").colorValue = Bone;
        so.FindProperty("onSprite").objectReferenceValue = onSprite;
        so.FindProperty("offSprite").objectReferenceValue = offSprite;
        so.ApplyModifiedProperties();
    }

    //================================ 1번 화면: 수리 ================================
    private static GameObject BuildRepairPanel(Transform window)
    {
        GameObject root = Node(window, RepairPanelName);
        Stretch(root, 20, 130, 20, 20);

        const float summaryHeight = 110;
        const float gap = 20;

        GameObject diagnosis = Panel(root.transform, "DiagnosisPanel", "frame_panelMedium_v3");
        StretchVertical(diagnosis, 0, 440, 0, summaryHeight + gap);
        BuildDiagnosis(diagnosis.transform);

        GameObject parts = Panel(root.transform, "PartsPanel", "frame_panelLarge_v2");
        Stretch(parts, 440 + gap, 0, 500 + gap, summaryHeight + gap);
        BuildPartsList(parts.transform);

        GameObject detail = Panel(root.transform, "DetailPanel", "frame_panelMedium_v4");
        StretchVerticalRight(detail, 0, 500, 0, summaryHeight + gap);
        BuildDetail(detail.transform);

        GameObject summary = Panel(root.transform, "SummaryBar", "frame_panelLarge_v3");
        StretchBottom(summary, summaryHeight);
        BuildSummary(summary.transform);

        return root;
    }

    private static void BuildDiagnosis(Transform parent)
    {
        Header(parent, "로봇 진단", "UNIT DIAGNOSTIC", "부위를 눌러 선택");

        DiagnosisSlot(parent, "Slot_Head", "패시브\n머리", "button_primary", Ink, "ic_status_warm", 150, 90, 140, 110);
        DiagnosisSlot(parent, "Slot_LeftArm", "Q\n왼팔", "button_secondary", Bone, "ic_status_cool", 28, 225, 84, 200);
        DiagnosisSlot(parent, "Slot_RightArm", "W\n오른팔", "button_secondary", Bone, "ic_status_hot", 328, 225, 84, 200);
        DiagnosisSlot(parent, "Slot_LeftLeg", "E\n왼다리", "button_secondary", Bone, "ic_status_warm", 126, 445, 84, 200);

        GameObject empty = Button(parent, "Slot_RightLeg", "R\n빈 슬롯", "button_secondary", Neutral, 22, 2);
        Place(empty, 230, 445, 84, 200);
        empty.GetComponent<Button>().interactable = false;

        GameObject core = Img(parent, "Slot_Core", "bg_panelSmall", Color.white, 2);
        Place(core, 120, 215, 200, 215);
        GameObject coreIcon = Img(core.transform, "CoreIcon", "ic_center_focus_strong", Cyan);
        Place(coreIcon, 64, 26, 72, 72);
        Label(core.transform, "CoreLevel", "CORE Lv.10", 26, Bone, TextAlignmentOptions.Center, 0, 108, 200, 40);
        Label(core.transform, "CoreState", "정상", 20, Good, TextAlignmentOptions.Center, 0, 150, 200, 30);

        GameObject line = Line(parent, "LegendLine");
        StretchBottom(line, 2, 90);

        GameObject legend = Node(parent, "Legend");
        StretchBottom(legend, 80, 5);
        LegendItem(legend.transform, "정상", "ic_status_cool", 28, 5);
        LegendItem(legend.transform, "손상", "ic_status_warm", 118, 5);
        LegendItem(legend.transform, "응급 복구 (내구도 1)", "ic_status_hot", 208, 5);
    }

    private static void DiagnosisSlot(Transform parent, string name, string label, string family, Color textColor, string status, float x, float y, float w, float h)
    {
        GameObject slot = Button(parent, name, label, family, textColor, 22, 2);
        Place(slot, x, y, w, h);

        GameObject mark = Img(slot.transform, "Status", status, Color.white);
        PlaceRight(mark, 6, 6, 22, 22);
    }

    private static void LegendItem(Transform parent, string text, string status, float x, float y)
    {
        GameObject mark = Img(parent, $"Mark_{text}", status, Color.white);
        Place(mark, x, y + 3, 22, 22);
        Label(parent, $"Text_{text}", text, 18, Neutral, TextAlignmentOptions.Left, x + 28, y, 240, 28);
    }

    private static void BuildPartsList(Transform parent)
    {
        Header(parent, "장착 파츠", "EQUIPPED PARTS", null);
        GameObject clear = Button(parent, "ClearAllButton", "전체 해제", "miniButton_default", Bone, 20, 2);
        PlaceRight(clear, 24, 12, 130, 44);

        Transform content = ScrollList(parent, "PartsScrollView", 72, false);

        PartRow(content, "패시브", "머리", "A사 에픽 머리", "에픽", 24, 60, "손상", "ic_status_warm", Warning, "60G", "1시간", true, true);
        PartRow(content, "Q", "왼팔", "B사 레어 왼팔", "레어", 100, 100, "정상", "ic_status_cool", Good, "0G", "수리 불필요", false, false);
        PartRow(content, "W", "오른팔", "B사 레어 오른팔", "레어", 1, 100, "응급 복구", "ic_status_hot", Bad, "45G", "1시간", true, false);
        PartRow(content, "E", "왼다리", "C사 보급형 왼다리", "일반", 52, 80, "손상", "ic_status_warm", Warning, "9G", "1시간", true, false);
        PartRow(content, "R", "오른다리", "빈 슬롯", null, 0, 0, "빈 슬롯", null, Neutral, "-", "잔해 보관 중", false, false);
    }

    private static void PartRow(Transform parent, string key, string slot, string partName, string grade, int current, int max, string state, string status, Color stateColor, string cost, string time, bool isChecked, bool selected)
    {
        GameObject row = Img(parent, $"Row_{slot}", selected ? "bg_selectItem_hover" : "bg_selectItem_enabled", Color.white);
        row.AddComponent<LayoutElement>().preferredHeight = 118;

        if (selected)
        {
            GameObject bar = Img(row.transform, "SelectedBar", "swatch_v1", Color.white);
            SetRect(bar, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0));
        }

        CheckBox(row.transform, isChecked);

        GameObject keyBox = Img(row.transform, "KeyBox", "swatch_v3", Color.white);
        Place(keyBox, 70, 16, key.Length > 1 ? 90 : 40, 40);
        FillLabel(keyBox.transform, key, 22, Cyan);

        float nameX = key.Length > 1 ? 172 : 122;
        Label(row.transform, "Slot", slot, 20, Neutral, TextAlignmentOptions.Left, nameX, 20, 80, 32);
        Label(row.transform, "Name", partName, 24, Bone, TextAlignmentOptions.Left, nameX + 80, 20, 260, 32);
        if (grade != null)
            Badge(row.transform, grade, nameX + 340, 18, 70, 36);

        Segments(row.transform, "Durability", 70, 72, 300, 16, 15, current, max, stateColor == Good ? "good" : stateColor == Bad ? "bad" : "warning");
        Label(row.transform, "DurabilityText", max > 0 ? $"{current}/{max}" : "-", 20, Bone, TextAlignmentOptions.Left, 385, 62, 90, 34);
        if (status != null)
        {
            GameObject mark = Img(row.transform, "StateMark", status, Color.white);
            Place(mark, 472, 68, 24, 24);
        }
        Label(row.transform, "State", state, 20, stateColor, TextAlignmentOptions.Left, 502, 62, 130, 34);

        LabelRight(row.transform, "Cost", cost, 26, Yellow, 16, 20, 110, 36);
        LabelRight(row.transform, "Time", time, 18, Neutral, 16, 66, 150, 28);
    }

    private static void CheckBox(Transform parent, bool isChecked)
    {
        GameObject box = Img(parent, "CheckBox", "bg_iconButton_v1_sm_enabled", Color.white, 2);
        Place(box, 20, 41, 36, 36);
        Image boxImage = box.GetComponent<Image>();
        boxImage.raycastTarget = true;

        GameObject mark = Img(box.transform, "CheckMark", "ic_done", Color.white);
        Stretch(mark, 4, 4, 4, 4);

        Toggle toggle = box.AddComponent<Toggle>();
        toggle.targetGraphic = boxImage;
        toggle.graphic = mark.GetComponent<Image>();
        toggle.isOn = isChecked;
        SetSpriteStates(toggle, "iconButton_v1_sm");
    }

    private static void BuildDetail(Transform parent)
    {
        Header(parent, "부위 상세", "PART DETAIL", "머리 · 패시브");

        Label(parent, "PartName", "A사 에픽 머리", 36, Bone, TextAlignmentOptions.Left, 24, 84, 450, 50);
        Badge(parent, "에픽", 24, 142, 70, 36);
        Badge(parent, "손상", 104, 142, 70, 36);

        Label(parent, "DurabilityLabel", "내구도", 20, Neutral, TextAlignmentOptions.Left, 24, 210, 150, 34);
        LabelRight(parent, "DurabilityValue", "24 / 60", 36, Bone, 24, 198, 220, 50);
        Segments(parent, "DurabilityBar", 24, 254, 452, 18, 20, 24, 60, "warning");

        GameObject box = DataBox(parent, "CostBox");
        PlaceStretchWidth(box, 24, 24, 292, 230);
        CostLine(box.transform, "BaseCost", "부위 기본 수리비 (머리)", "40G", 16);
        CostLine(box.transform, "GradeRate", "× 등급 배율 (에픽)", "2.5", 56);
        CostLine(box.transform, "LostDurability", "× 잃은 내구도", "60%", 96);
        GameObject line = Line(box.transform, "Line");
        PlaceStretchWidth(line, 16, 16, 140, 2);
        Label(box.transform, "TotalLabel", "= 수리비 (반올림)", 22, Bone, TextAlignmentOptions.Left, 18, 160, 280, 50);
        LabelRight(box.transform, "TotalValue", "60G", 34, Yellow, 18, 160, 150, 50);

        DetailLine(parent, "Time", "소요 시간", "1시간", 545);
        DetailLine(parent, "ZeroDurability", "전투 중 0이 되면", "패시브 스킬 해제", 590);
        DetailLine(parent, "BreakChance", "패배 시 파괴 확률", "20% (등급 기준)", 635);
    }

    private static void CostLine(Transform parent, string name, string left, string right, float y)
    {
        Label(parent, $"{name}Label", left, 20, Neutral, TextAlignmentOptions.Left, 18, y, 320, 32);
        LabelRight(parent, $"{name}Value", right, 20, Bone, 18, y, 110, 32);
    }

    private static void DetailLine(Transform parent, string name, string left, string right, float y)
    {
        Label(parent, $"{name}Label", left, 20, Neutral, TextAlignmentOptions.Left, 24, y, 220, 34);
        LabelRight(parent, $"{name}Value", right, 20, Bone, 24, y, 240, 34);
    }

    private static void BuildSummary(Transform parent)
    {
        SummaryItem(parent, "Selected", "선택", "3", "부위", Bone, 30);
        SummaryItem(parent, "Cost", "수리비", "114", "G", Yellow, 170);
        SummaryItem(parent, "Time", "소요 시간", "3", "시간", Bone, 330);
        SummaryItem(parent, "Balance", "수리 후 잔액", "1,126", "G", Bone, 490);

        LabelRight(parent, "FinishTime", "완료 예정 04월 12일 12시", 20, Neutral, 260, 38, 320, 34);
        GameObject start = Button(parent, "StartRepairButton", "수리 시작", "button_primary", Ink, 30, 2);
        PlaceRight(start, 24, 16, 220, 78);
    }

    private static void SummaryItem(Transform parent, string name, string title, string value, string unit, Color valueColor, float x)
    {
        Label(parent, $"{name}Title", title, 18, Neutral, TextAlignmentOptions.Left, x, 14, 150, 28);
        Label(parent, $"{name}Value", $"{value} <size=60%>{unit}</size>", 36, valueColor, TextAlignmentOptions.Left, x, 46, 170, 50);
    }

    //================================ 2번 화면: 복구 ================================
    private static GameObject BuildRestorePanel(Transform window)
    {
        GameObject root = Node(window, RestorePanelName);
        Stretch(root, 20, 130, 20, 20);

        const float bannerHeight = 70;
        const float gap = 20;

        GameObject banner = Img(root.transform, "WarningBanner", "swatch_v2", Color.white);
        StretchTop(banner, bannerHeight);
        GameObject bannerBar = Img(banner.transform, "AccentBar", "swatch_v1", Color.white);
        SetRect(bannerBar, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 0));
        GameObject bell = Img(banner.transform, "Icon", "ic_notifications_none", Color.white);
        Place(bell, 28, 17, 36, 36);
        Label(banner.transform, "WarningTitle", "패배 후 정비", 24, Yellow, TextAlignmentOptions.Left, 76, 18, 170, 34);
        Label(banner.transform, "WarningText", "코어가 불안정합니다. 코어를 복구해야 로봇을 다시 조립할 수 있습니다.", 22, Bone, TextAlignmentOptions.Left, 250, 18, 1100, 34);

        GameObject parts = Panel(root.transform, "DestroyedPartsPanel", "frame_panelLarge_v3");
        Stretch(parts, 0, bannerHeight + gap, 520 + gap, 0);
        Header(parts.transform, "파괴된 파츠 (잔해)", "WRECKAGE", "복구 가능 2 · 재료 부족 1 · 복구 불가 1 — 복구 시 4시간, 내구도 100%");
        Transform content = ScrollList(parts.transform, "DestroyedScrollView", 72, true);

        WreckCard(content, "패시브", "머리 · 잔해", "A사 전설 머리", "전설", "600G", "전설 부품 1개 <color=#FF4D2E>보유 0</color>", "재료 부족", false);
        WreckCard(content, "W", "오른팔 · 잔해", "B사 레어 오른팔", "레어", "120G", "없음", "복구 · 120G", true);
        WreckCard(content, "R", "오른다리 · 잔해", "C사 에픽 오른다리", "에픽", "200G", "에픽 부품 1개 <color=#52E08F>보유 2</color>", "복구 · 200G + 부품 1", true);
        WreckCard(content, "E", "왼다리 · 잔해", "C사 보급형 왼다리", "일반", null, null, null, false);

        GameObject core = Img(root.transform, "CoreRestorePanel", "bg_panelWide_chamfer", Color.white, 2);
        StretchVerticalRight(core, bannerHeight + gap, 520, 0, 0);
        GameObject coreFrame = Img(core.transform, "Frame", "frame_panelMedium_v9", Bad, 3);
        Stretch(coreFrame, 0, 0, 0, 0);
        BuildCoreRestore(core.transform);

        return root;
    }

    private static void WreckCard(Transform parent, string key, string slot, string partName, string grade, string cost, string material, string actionLabel, bool canRestore)
    {
        GameObject card = Img(parent, $"Card_{partName}", "bg_selectItem_enabled", Color.white);

        GameObject icon = Img(card.transform, "Icon", "bg_iconButton_v1_lg_disabled", Color.white, 1);
        Place(icon, 22, 22, 90, 90);
        FillLabel(icon.transform, key, key.Length > 1 ? 20 : 32, Bone);

        Label(card.transform, "Slot", slot, 18, Neutral, TextAlignmentOptions.Left, 130, 22, 300, 28);
        Label(card.transform, "Name", partName, 24, Bone, TextAlignmentOptions.Left, 130, 58, 290, 34);
        Badge(card.transform, grade, 425, 56, 70, 36);

        // 일반 등급은 복구할 수 없어 안내 문구와 버리기 버튼만 둔다
        if (cost == null)
        {
            GameObject guide = Label(card.transform, "Guide", "일반 등급은 복구할 수 없습니다. 고물상에서 부품을 모아 새로 제작하세요.", 20, Neutral, TextAlignmentOptions.TopLeft, 22, 135, 590, 90);
            guide.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;

            GameObject discardOnly = Button(card.transform, "DiscardButton", "버리기", "miniButton_default", Bone, 22, 1.5f);
            Place(discardOnly, 22, 255, 596, 70);
            return;
        }

        Label(card.transform, "CostLabel", "복구비", 20, Neutral, TextAlignmentOptions.Left, 22, 130, 180, 32);
        LabelRight(card.transform, "CostValue", cost, 22, Yellow, 22, 130, 200, 32);
        Label(card.transform, "MaterialLabel", "추가 재료", 20, Neutral, TextAlignmentOptions.Left, 22, 175, 180, 32);
        LabelRight(card.transform, "MaterialValue", material, 20, Bone, 22, 175, 400, 32);

        GameObject action = Button(card.transform, "RestoreButton", actionLabel, "button_secondary", canRestore ? Cyan : Neutral, 22, 2);
        Place(action, 22, 255, 460, 70);
        action.GetComponent<Button>().interactable = canRestore;

        GameObject discard = Button(card.transform, "DiscardButton", "버리기", "miniButton_default", Bone, 20, 1.5f);
        Place(discard, 498, 255, 120, 70);
    }

    private static void BuildCoreRestore(Transform parent)
    {
        Header(parent, "코어 복구", "CORE RECOVERY", null);
        Badge(parent, "불안정한 코어", 330, 16, 166, 38);

        GameObject icon = Img(parent, "CoreIcon", "bg_iconButton_v1_lg_disabled", Bad, 1);
        Place(icon, 24, 82, 110, 110);
        GameObject iconInner = Img(icon.transform, "Inner", "ic_cancel", Bad);
        Stretch(iconInner, 24, 24, 24, 24);

        Label(parent, "CoreLevel", "CORE Lv.10", 36, Bone, TextAlignmentOptions.Left, 152, 94, 340, 50);
        Label(parent, "CoreGuide", "복구 전에는 로봇을 조립할 수 없습니다.", 18, Neutral, TextAlignmentOptions.Left, 152, 148, 350, 30);

        GameObject box = DataBox(parent, "CostBox");
        PlaceStretchWidth(box, 24, 24, 215, 180);
        CostLine(box.transform, "BaseCost", "기본 복구비", "100G", 14);
        CostLine(box.transform, "LevelCost", "+ 코어 레벨 10 × 50G", "500G", 54);
        GameObject line = Line(box.transform, "Line");
        PlaceStretchWidth(line, 16, 16, 98, 2);
        Label(box.transform, "TotalLabel", "= 복구비", 22, Bone, TextAlignmentOptions.Left, 18, 112, 200, 50);
        LabelRight(box.transform, "TotalValue", "600G", 36, Yellow, 18, 112, 180, 50);

        DetailLine(parent, "Time", "소요 시간", "4시간 · 완료 04월 12일 13시", 412);
        DetailLine(parent, "Balance", "복구 후 잔액", "640G", 452);

        GameObject start = Button(parent, "StartCoreRestoreButton", "코어 복구 시작", "button_primary", Ink, 30, 2);
        PlaceStretchWidth(start, 24, 24, 505, 84);

        GameObject routeLine = Line(parent, "RouteLine");
        PlaceStretchWidth(routeLine, 24, 24, 630, 2);
        GameObject route = Label(parent, "RebirthRoute", "<color=#EEEDE8>회생 루트</color> · 아주 쉬움 토벌은 신청비 0, 파괴 판정 없음. 빈 슬롯은 기본 파츠로 채워집니다.", 18, Neutral, TextAlignmentOptions.TopLeft, 24, 648, 470, 90);
        route.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;
    }

    //================================ 공용 UI 부품 ================================
    private static GameObject Panel(Transform parent, string name, string frame)
    {
        GameObject panel = Img(parent, name, "bg_panelLarge", Color.white);
        GameObject overlay = Img(panel.transform, "Frame", frame, Color.white, 3);
        Stretch(overlay, 0, 0, 0, 0);
        return panel;
    }

    private static GameObject DataBox(Transform parent, string name)
    {
        GameObject box = Img(parent, name, "swatch_v2", Color.white);
        GameObject overlay = Img(box.transform, "Frame", "frame_panelMedium_v8", Color.white, 4);
        Stretch(overlay, 0, 0, 0, 0);
        return box;
    }

    private static void Header(Transform parent, string title, string caption, string description)
    {
        Label(parent, "Title", title, 28, Bone, TextAlignmentOptions.Left, 24, 12, 300, 34);
        Label(parent, "Caption", caption, 14, Cyan, TextAlignmentOptions.Left, 24, 44, 300, 20);
        if (description != null)
            LabelRight(parent, "Description", description, 18, Neutral, 24, 22, 800, 28);

        GameObject line = Line(parent, "HeaderLine");
        PlaceStretchWidth(line, 24, 24, 68, 2);
    }

    private static GameObject Line(Transform parent, string name)
    {
        return Img(parent, name, "swatch_v1", new Color(1, 1, 1, 0.25f));
    }

    private static void Badge(Transform parent, string text, float x, float y, float w, float h)
    {
        string sprite;
        Color color = Ink;
        switch (text)
        {
            case "전설": sprite = "bg_miniButton_primary_pressed"; break;
            case "에픽": sprite = "bg_statusButton_warning_enabled"; break;
            case "레어": sprite = "bg_miniButton_secondary_enabled"; color = Cyan; break;
            case "일반": sprite = "bg_statusButton_neutral_enabled"; break;
            case "손상": sprite = "bg_statusButton_warning_enabled"; break;
            default: sprite = "bg_statusButton_bad_enabled"; break;
        }

        GameObject badge = Img(parent, $"Badge_{text}", sprite, Color.white, 2);
        Place(badge, x, y, w, h);
        FillLabel(badge.transform, text, 18, color);
    }

    private static void Segments(Transform parent, string name, float x, float y, float w, float h, int count, int current, int max, string state)
    {
        GameObject bar = Node(parent, name);
        Place(bar, x, y, w, h);

        // 값이 1이라도 있으면 한 칸은 보이게 한다
        int filled = max > 0 ? Mathf.Clamp(Mathf.CeilToInt((float)current / max * count), 0, count) : 0;
        float cell = (w - (count - 1) * 3) / count;
        for (int i = 0; i < count; i++)
        {
            GameObject segment = Img(bar.transform, $"Cell{i}", i < filled ? $"bg_statusButton_{state}_enabled" : "swatch_v2", Color.white);
            segment.GetComponent<Image>().type = Image.Type.Simple;
            Place(segment, i * (cell + 3), 0, cell, h);
        }
    }

    // 스크롤 뷰를 만들고 항목을 넣을 Content를 돌려준다
    private static Transform ScrollList(Transform parent, string name, float top, bool grid)
    {
        GameObject root = Node(parent, name);
        Stretch(root, 0, top, 0, 8);

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
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = new Vector2(16, 16);
            layout.cellSize = new Vector2(640, 345);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
        }
        else
        {
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject track = Img(root.transform, "Scrollbar Vertical", "swatch_v2", Color.white);
        StretchVerticalRight(track, 0, 12, 0, 0);
        track.GetComponent<Image>().raycastTarget = true;

        GameObject area = Node(track.transform, "Sliding Area");
        Stretch(area, 0, 0, 0, 0);
        GameObject handle = Img(area.transform, "Handle", "swatch_v1", Color.white);
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

    private static GameObject Button(Transform parent, string name, string label, string family, Color textColor, float fontSize, float multiplier)
    {
        GameObject go = Img(parent, name, $"bg_{family}_enabled", Color.white, multiplier);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        SetSpriteStates(button, family);

        if (!string.IsNullOrEmpty(label))
            FillLabel(go.transform, label, fontSize, textColor);

        return go;
    }

    // hover/pressed/disabled 리소스를 상태 이미지로 쓴다
    private static void SetSpriteStates(Selectable selectable, string family)
    {
        Sprites.TryGetValue($"bg_{family}_hover", out Sprite hover);
        Sprites.TryGetValue($"bg_{family}_pressed", out Sprite pressed);
        Sprites.TryGetValue($"bg_{family}_disabled", out Sprite disabled);

        selectable.transition = Selectable.Transition.SpriteSwap;
        selectable.spriteState = new SpriteState
        {
            highlightedSprite = hover,
            selectedSprite = hover,
            pressedSprite = pressed,
            disabledSprite = disabled,
        };
    }

    private static GameObject Img(Transform parent, string name, string sprite, Color color, float multiplier = 1)
    {
        GameObject go = Node(parent, name);
        Image image = go.AddComponent<Image>();
        ApplySprite(image, sprite, multiplier);
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    // multiplier가 클수록 모서리 장식이 작게 보인다
    private static void ApplySprite(Image image, string sprite, float multiplier)
    {
        image.sprite = Sprites[sprite];
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
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
}
