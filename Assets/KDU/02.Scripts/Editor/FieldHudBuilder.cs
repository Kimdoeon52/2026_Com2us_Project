using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 씬에 고물상 필드 HUD를 만든다. 이름으로 찾아 재사용하므로 다시 실행해도 쌓이지 않는다
public static class FieldHudBuilder
{
    private const string Title = "고물상 HUD 생성";
    private const string CanvasName = "Canvas_FieldHUD";
    private const int SortingOrder = -1;
    private const int TimelineSegments = 20;
    private const int StatusSegments = 10;
    private const int MissionRows = 3;

    // 로봇 상태 줄: 부위, 라벨, 열, 행
    private static readonly (Slot slot, string label, int col, int row)[] StatusLayout =
    {
        (Slot.Head, "HEAD 머리", 0, 0),
        (Slot.Core, "CORE 코어", 1, 0),
        (Slot.LeftArm, "L.ARM 좌팔", 0, 1),
        (Slot.RightArm, "R.ARM 우팔", 1, 1),
        (Slot.LeftLeg, "L.LEG 좌다리", 0, 2),
        (Slot.RightLeg, "R.LEG 우다리", 1, 2),
    };

    // 키 안내: 키, 설명
    private static readonly (string key, string label)[] KeyHints =
    {
        ("WASD", "이동"),
        ("E", "상호작용"),
        ("TAB", "인벤토리"),
        ("I", "제작"),
        ("M", "지도"),
        ("ESC", "메뉴"),
    };

    [MenuItem("KDU/고물상/필드 HUD 생성")]
    public static void Build()
    {
        string missing = Load();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(Title, $"리소스를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName(Title);
        int group = Undo.GetCurrentGroup();

        GameObject canvas = BuildCanvas(scene);
        Transform root = canvas.transform;

        DummyFieldHudSource source = GetOrAdd<DummyFieldHudSource>(canvas);
        FieldHudView hud = GetOrAdd<FieldHudView>(canvas);
        var so = new SerializedObject(hud);
        so.FindProperty("_source").objectReferenceValue = source;

        BuildHeader(root, so);
        BuildClockCard(root, so);
        BuildBanner(root, so);
        BuildResources(root, so);
        BuildMinimap(root);
        BuildMissions(root, so);
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildRobotStatus(root);
        BuildKeyHints(root);
        ApplyFont(canvas);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvas;
        Debug.Log($"[{Title}] 완료. 씬을 저장하세요.");
    }

    private static GameObject BuildCanvas(Scene scene)
    {
        GameObject go = FindRoot(scene, CanvasName);
        if (go == null)
        {
            go = new GameObject(CanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, Title);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = LayerMask.NameToLayer("UI");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(go, Title);
        }

        Canvas canvas = GetOrAdd<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    //================================ 좌상단: 위치 ================================
    private static void BuildHeader(Transform root, SerializedObject hud)
    {
        GameObject header = FindOrCreate(root, "Header");
        PlaceRect(header, 40f, 24f, 380f, 104f);
        Transform t = header.transform;

        PlaceBox(t, "Mark", Yellow, 0f, 4f, 8f, 8f);
        PlaceLabel(t, "System", "RS-OS   FIELD HUD", 11, Bone, TextAlignmentOptions.Left, 14f, 0f, 160f, 16f);
        PlaceBox(t, "Line", Ghost, 140f, 8f, 100f, 1f);
        PlaceLabel(t, "Version", "//// v0.3", 11, Neutral, TextAlignmentOptions.Left, 248f, 0f, 100f, 16f);
        PlaceLabel(t, "Sector", "SEC-03  //  SCRAPYARD · OUTDOOR", 11, Neutral, TextAlignmentOptions.Left, 0f, 24f, 300f, 16f);
        PlaceDiamond(t, "Diamond", Yellow, 8f, 50f, 13f);
        TextMeshProUGUI title = PlaceLabel(t, "Location", "고물상", 36, Bone, TextAlignmentOptions.Left, 30f, 44f, 300f, 46f);
        PlaceBox(t, "Ruler", Ghost, 0f, 100f, 360f, 1f);
        for (int i = 0; i <= 9; i++)
        {
            PlaceBox(t, $"Tick_{i}", Ghost, i * 40f, 96f, 1f, i % 5 == 0 ? 8f : 5f);
        }

        hud.FindProperty("_locationTitle").objectReferenceValue = title;
    }

    //================================ 좌상단: 시간 카드 ================================
    private static void BuildClockCard(Transform root, SerializedObject hud)
    {
        GameObject card = PlacePanel(root, "ClockCard", "bg_panelWide_chamfer", Color.white, 4f, 40f, 150f, 380f, 116f);
        Overlay(card.transform, "frame_panelMedium_v8", 6f);
        Transform t = card.transform;

        GameObject chip = PlacePanel(t, "DayChip", "bg_button_primary_enabled", Color.white, 3f, 16f, 16f, 80f, 24f);
        TextMeshProUGUI dayChip = PlaceLabel(chip.transform, "Label", "DAY 12", 14, Ink, TextAlignmentOptions.Center, 0f, 0f, 80f, 24f);
        TextMeshProUGUI dayLabel = PlaceLabel(t, "DayLabel", "TUE · 화요일 · 맑음", 12, Bone, TextAlignmentOptions.Left, 106f, 20f, 240f, 18f);
        TextMeshProUGUI clock = PlaceLabel(t, "Clock", "14:00", 46, Bone, TextAlignmentOptions.Left, 14f, 42f, 200f, 50f);
        PlaceLabel(t, "RemainCaption", "REMAIN", 10, Neutral, TextAlignmentOptions.Left, 260f, 52f, 100f, 14f);
        TextMeshProUGUI remain = PlaceLabel(t, "Remain", "12H", 18, Yellow, TextAlignmentOptions.Left, 260f, 66f, 100f, 24f);

        GameObject track = FindOrCreate(t, "Timeline");
        PlaceRect(track, 16f, 98f, 348f, 5f);
        const float gap = 3f;
        float width = (348f - gap * (TimelineSegments - 1)) / TimelineSegments;
        for (int i = 0; i < TimelineSegments; i++)
        {
            PlaceBox(track.transform, $"Seg_{i}", Bone, i * (width + gap), 0f, width, 5f);
        }

        GameObject marker = PlaceDiamond(t, "Marker", Cyan, 16f, 94f, 10f);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(16f, -100f);

        hud.FindProperty("_dayChip").objectReferenceValue = dayChip;
        hud.FindProperty("_dayLabel").objectReferenceValue = dayLabel;
        hud.FindProperty("_clock").objectReferenceValue = clock;
        hud.FindProperty("_remain").objectReferenceValue = remain;
        hud.FindProperty("_timelineTrack").objectReferenceValue = track.GetComponent<RectTransform>();
        hud.FindProperty("_timelineMarker").objectReferenceValue = markerRect;
    }

    //================================ 상단 중앙: 도착 배너 ================================
    private static void BuildBanner(Transform root, SerializedObject hud)
    {
        GameObject banner = FindOrCreate(root, "ArrivalBanner");
        PlaceRect(banner, 632f, 32f, 656f, 120f);
        Overlay(banner.transform, "frame_panelMedium_v8", 8f);
        CanvasGroup group = GetOrAdd<CanvasGroup>(banner);
        group.blocksRaycasts = false;
        Transform t = banner.transform;

        PlacePanel(t, "Plate", "bg_picker_populated", Color.white, 1f, 8f, 8f, 640f, 104f);
        PlaceBox(t, "Accent", Orange, 8f, 8f, 8f, 104f);
        PlaceLabel(t, "Caption", "ARRIVED  //  SCRAPYARD", 12, Dim, TextAlignmentOptions.Left, 36f, 22f, 300f, 16f);
        TextMeshProUGUI title = PlaceLabel(t, "Title", "고물상", 34, Ink, TextAlignmentOptions.Left, 34f, 40f, 300f, 42f);
        TextMeshProUGUI omen = PlaceLabel(t, "Omen", "오늘의 징후: -", 13, Ink, TextAlignmentOptions.Left, 36f, 86f, 420f, 18f);
        TextMeshProUGUI clock = PlaceLabel(t, "Clock", "14:00", 40, Ink, TextAlignmentOptions.Right, 420f, 40f, 198f, 44f);
        TextMeshProUGUI travel = PlaceLabel(t, "Travel", "TRAVEL 25 MIN", 10, Dim, TextAlignmentOptions.Right, 420f, 88f, 180f, 14f);

        hud.FindProperty("_banner").objectReferenceValue = group;
        hud.FindProperty("_bannerTitle").objectReferenceValue = title;
        hud.FindProperty("_bannerOmen").objectReferenceValue = omen;
        hud.FindProperty("_bannerClock").objectReferenceValue = clock;
        hud.FindProperty("_bannerTravel").objectReferenceValue = travel;
    }

    //================================ 우상단: 재화 ================================
    private static void BuildResources(Transform root, SerializedObject hud)
    {
        GameObject bar = FindOrCreate(root, "Resources");
        PlaceRect(bar, 1590f, 40f, 290f, 46f);
        Transform t = bar.transform;

        hud.FindProperty("_credit").objectReferenceValue = Resource(t, "Credit", "CREDIT", Yellow, 0f, 100f);
        PlaceBox(t, "Divider_0", Ghost, 104f, 8f, 1f, 34f);
        hud.FindProperty("_scrap").objectReferenceValue = Resource(t, "Scrap", "SCRAP", Orange, 116f, 70f);
        PlaceBox(t, "Divider_1", Ghost, 190f, 8f, 1f, 34f);
        hud.FindProperty("_refined").objectReferenceValue = Resource(t, "Refined", "REFINED", Cyan, 202f, 80f);
        PlaceBox(t, "Divider_2", Ghost, 288f, 8f, 1f, 34f);
    }

    private static TextMeshProUGUI Resource(Transform parent, string name, string caption, Color mark, float x, float width)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, 0f, width, 46f);
        PlaceDiamond(go.transform, "Mark", mark, 4f, 2f, 6f);
        PlaceLabel(go.transform, "Caption", caption, 10, Neutral, TextAlignmentOptions.Left, 12f, 4f, width, 14f);
        return PlaceLabel(go.transform, "Value", "0", 22, Bone, TextAlignmentOptions.Left, 10f, 18f, width, 28f);
    }

    //================================ 우상단: 미니맵 자리 ================================
    // 지도 UI는 다른 파트. 틀과 표시 자리만 둔다
    private static void BuildMinimap(Transform root)
    {
        GameObject map = PlacePanel(root, "Minimap", "bg_panelSmall", Color.white, 3f, 1615f, 100f, 265f, 235f);
        Overlay(map.transform, "frame_panelMedium_v8", 8f);

        GameObject view = FindOrCreate(map.transform, "MapView");
        PlaceRect(view, 24f, 24f, 217f, 187f);
        RawImage raw = GetOrAdd<RawImage>(view);
        raw.color = new Color(1f, 1f, 1f, 0.05f);
        raw.raycastTarget = false;

        PlaceLabel(map.transform, "Caption", "MAP", 10, Neutral, TextAlignmentOptions.Right, 180f, 210f, 60f, 14f);
    }

    //================================ 우측: 미션 ================================
    private static void BuildMissions(Transform root, SerializedObject hud)
    {
        GameObject panel = FindOrCreate(root, "Missions");
        PlaceRect(panel, 1560f, 366f, 320f, 230f);
        Transform t = panel.transform;

        PlaceDiamond(t, "Mark", Yellow, 18f, 6f, 9f);
        PlaceLabel(t, "Title", "MISSION", 14, Bone, TextAlignmentOptions.Left, 34f, 0f, 90f, 20f);
        PlaceLabel(t, "Caption", "일정 · 할 일", 12, Neutral, TextAlignmentOptions.Left, 106f, 2f, 120f, 18f);
        hud.FindProperty("_missionDay").objectReferenceValue = PlaceLabel(t, "Day", "DAY 12", 10, Yellow, TextAlignmentOptions.Right, 220f, 4f, 78f, 14f);

        GameObject plate = PlacePanel(t, "Plate", "bg_picker_populated", Color.white, 1f, 0f, 28f, 320f, 200f);
        SerializedProperty rows = hud.FindProperty("_missionRows");
        rows.arraySize = MissionRows;

        for (int i = 0; i < MissionRows; i++)
        {
            GameObject row = FindOrCreate(plate.transform, $"Row_{i}");
            PlaceRect(row, 14f, 14f + i * 60f, 292f, 50f);
            Image accent = PlaceBox(row.transform, "Accent", Cyan, 0f, 0f, 3f, 46f).GetComponent<Image>();
            TextMeshProUGUI tag = PlaceLabel(row.transform, "Tag", "TAG", 12, Ink, TextAlignmentOptions.Left, 14f, 2f, 270f, 16f);
            TextMeshProUGUI text = PlaceLabel(row.transform, "Text", "-", 14, Ink, TextAlignmentOptions.Left, 14f, 22f, 270f, 20f);

            SerializedProperty element = rows.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Root").objectReferenceValue = row;
            element.FindPropertyRelative("Accent").objectReferenceValue = accent;
            element.FindPropertyRelative("Tag").objectReferenceValue = tag;
            element.FindPropertyRelative("Text").objectReferenceValue = text;
        }
    }

    //================================ 좌하단: 수리 경고·로봇 상태 ================================
    private static void BuildRobotStatus(Transform root)
    {
        GameObject group = FindOrCreate(root, "RobotStatus");
        PlaceRect(group, 40f, 758f, 470f, 290f);
        Transform t = group.transform;

        GameObject repair = FindOrCreate(t, "RepairAlert");
        PlaceRect(repair, 0f, 0f, 470f, 44f);
        GameObject chip = PlacePanel(repair.transform, "Chip", "bg_statusButton_warning_enabled", Color.white, 1f, 0f, 0f, 236f, 44f);
        PlaceDiamond(chip.transform, "MarkA", Ink, 18f, 15f, 8f);
        PlaceDiamond(chip.transform, "MarkB", Ink, 30f, 15f, 8f);
        PlaceLabel(chip.transform, "Label", "REPAIR REQ.", 18, Ink, TextAlignmentOptions.Left, 46f, 0f, 180f, 44f);
        TextMeshProUGUI detail = PlaceLabel(repair.transform, "Detail", "", 9, Orange, TextAlignmentOptions.TopLeft, 244f, 6f, 226f, 34f);
        detail.textWrappingMode = TextWrappingModes.Normal;

        GameObject card = PlacePanel(t, "Card", "bg_panelWide_chamfer", Color.white, 4f, 0f, 54f, 460f, 228f);
        Overlay(card.transform, "frame_panelMedium_v8", 6f);
        TextMeshProUGUI title = PlaceLabel(card.transform, "Title", "R-07", 30, Bone, TextAlignmentOptions.Left, 20f, 16f, 100f, 36f);
        TextMeshProUGUI subtitle = PlaceLabel(card.transform, "Subtitle", "코어 없음", 14, Neutral, TextAlignmentOptions.Left, 108f, 28f, 300f, 20f);

        for (int i = 0; i < StatusLayout.Length; i++)
        {
            (Slot slot, string label, int col, int row) = StatusLayout[i];
            BuildStatusRow(card.transform, i, slot, label, 20f + col * 220f, 72f + row * 50f);
        }

        RobotStatusView view = GetOrAdd<RobotStatusView>(group);
        var so = new SerializedObject(view);
        so.FindProperty("_title").objectReferenceValue = title;
        so.FindProperty("_subtitle").objectReferenceValue = subtitle;
        so.FindProperty("_repairRoot").objectReferenceValue = repair;
        so.FindProperty("_repairDetail").objectReferenceValue = detail;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildStatusRow(Transform card, int index, Slot slot, string label, float x, float y)
    {
        const float width = 200f;
        const float gap = 2f;

        GameObject row = FindOrCreate(card, $"Row_{index}");
        PlaceRect(row, x, y, width, 46f);
        Transform t = row.transform;

        PlaceLabel(t, "Caption", label, 10, Neutral, TextAlignmentOptions.Left, 0f, 0f, width, 14f);
        TextMeshProUGUI name = PlaceLabel(t, "Name", "미장착", 13, Bone, TextAlignmentOptions.Left, 0f, 14f, 150f, 18f);
        TextMeshProUGUI value = PlaceLabel(t, "Value", "-", 13, Bone, TextAlignmentOptions.Right, 150f, 14f, 50f, 18f);

        var segments = new Image[StatusSegments];
        float segment = (width - gap * (StatusSegments - 1)) / StatusSegments;
        for (int i = 0; i < StatusSegments; i++)
        {
            segments[i] = PlaceBox(t, $"Seg_{i}", Ghost, i * (segment + gap), 36f, segment, 5f).GetComponent<Image>();
        }

        RobotStatusRow view = GetOrAdd<RobotStatusRow>(row);
        var so = new SerializedObject(view);
        so.FindProperty("_slot").enumValueIndex = (int)slot;
        so.FindProperty("_nameLabel").objectReferenceValue = name;
        so.FindProperty("_valueLabel").objectReferenceValue = value;

        SerializedProperty array = so.FindProperty("_segments");
        array.arraySize = segments.Length;
        for (int i = 0; i < segments.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    //================================ 우하단: 키 안내 ================================
    private static void BuildKeyHints(Transform root)
    {
        GameObject bar = FindOrCreate(root, "KeyHints");
        PlaceRect(bar, 1100f, 1012f, 780f, 32f);

        // 오른쪽 끝에서부터 채운다
        float right = 780f;
        for (int i = KeyHints.Length - 1; i >= 0; i--)
        {
            (string key, string label) = KeyHints[i];
            float labelWidth = label.Length * 15f + 4f;
            float keyWidth = Mathf.Max(30f, key.Length * 11f + 16f);

            right -= labelWidth;
            PlaceLabel(bar.transform, $"Label_{i}", label, 14, Bone, TextAlignmentOptions.Left, right, 4f, labelWidth, 24f);

            right -= keyWidth + 8f;
            GameObject chip = PlacePanel(bar.transform, $"Key_{i}", "bg_picker_populated", Color.white, 4f, right, 2f, keyWidth, 28f);
            PlaceLabel(chip.transform, "Label", key, 12, Ink, TextAlignmentOptions.Center, 0f, 0f, keyWidth, 28f);

            right -= 22f;
        }
    }
}
