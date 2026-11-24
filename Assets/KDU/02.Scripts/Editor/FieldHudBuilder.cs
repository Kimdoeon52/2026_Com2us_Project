using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 씬에 장소별 필드 HUD를 만든다. 이름으로 찾아 재사용하므로 다시 실행해도 쌓이지 않는다
public static class FieldHudBuilder
{
    // 장소마다 다른 문구·구성
    private sealed class Profile
    {
        public string Title;
        public string Location;
        public string Sector;
        public string Arrival;
        public bool Radar;
        public (string key, string label)[] KeyHints;
    }

    private static readonly Profile Scrapyard = new Profile
    {
        Title = "고물상 HUD 생성",
        Location = "고물상",
        Sector = "SEC-03  //  SCRAPYARD · OUTDOOR",
        Arrival = "ARRIVED  //  SCRAPYARD",
        Radar = true,
        KeyHints = new[] { ("WASD", "이동"), ("E", "상호작용"), ("TAB", "인벤토리"), ("M", "지도"), ("ESC", "메뉴") },
    };

    private static readonly Profile Workshop = new Profile
    {
        Title = "거점·정비소 HUD 생성",
        Location = "정비소",
        Sector = "SEC-01  //  HOME · WORKSHOP",
        Arrival = "ARRIVED  //  HOME",
        Radar = false,
        KeyHints = new[] { ("WASD", "이동"), ("E", "상호작용"), ("TAB", "인벤토리"), ("V", "스킬"), ("M", "지도"), ("ESC", "메뉴") },
    };

    private static readonly Profile GuildLobby = new Profile
    {
        Title = "길드 로비 HUD 생성",
        Location = "길드 로비",
        Sector = "SEC-02  //  LEAGUE · GUILD LOBBY",
        Arrival = "ARRIVED  //  GUILD LOBBY",
        Radar = false,
        KeyHints = new[] { ("WASD", "이동"), ("E", "대화"), ("O", "달력"), ("SHIFT+R", "랭킹"), ("M", "지도"), ("ESC", "메뉴") },
    };

    private const string CanvasName = "Canvas_FieldHUD";
    private const int SortingOrder = -1;
    private const int TimelineSegments = 20;
    private const int StatusSegments = 10;
    private const int MissionRows = 3;
    private const string CirclePath = "Assets/KDU/06.Image/Radar/radar_circle.png";
    private const string RingPath = "Assets/KDU/06.Image/Radar/radar_ring.png";
    private const float RadarSize = 190f;
    private const float SweepAngle = 60f;
    private const float SweepAlpha = 0.35f;

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

    [MenuItem("KDU/UI/필드 HUD/고물상")]
    public static void BuildScrapyard() => Build(Scrapyard);

    [MenuItem("KDU/UI/필드 HUD/거점·정비소")]
    public static void BuildWorkshop() => Build(Workshop);

    [MenuItem("KDU/UI/필드 HUD/길드 로비")]
    public static void BuildGuildLobby() => Build(GuildLobby);

    private static void Build(Profile profile)
    {
        string missing = Load();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(profile.Title, $"리소스를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName(profile.Title);
        int group = Undo.GetCurrentGroup();

        GameObject canvas = BuildCanvas(scene, profile.Title);
        Transform root = canvas.transform;

        DummyFieldHudSource source = GetOrAdd<DummyFieldHudSource>(canvas);
        FieldHudView hud = GetOrAdd<FieldHudView>(canvas);
        var so = new SerializedObject(hud);
        so.FindProperty("_source").objectReferenceValue = source;
        so.FindProperty("_locationName").stringValue = profile.Location;

        BuildHeader(root, so, profile);
        BuildClockCard(root, so);
        BuildBanner(root, so, profile);
        BuildResources(root, so);

        // 레이더가 없는 장소는 미션 패널을 그 자리로 올린다
        if (profile.Radar)
        {
            BuildMinimap(root);
            SetShown(root.Find("Minimap").gameObject, true);
        }
        else
            RemoveChild(root, "Minimap");

        BuildMissions(root, so, profile.Radar ? 366f : 100f);
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildRobotStatus(root);
        BuildKeyHints(root, profile.KeyHints);
        ApplyFont(canvas);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvas;
        Debug.Log($"[{profile.Title}] 완료. 씬을 저장하세요.");
    }

    // 다른 장소용으로 만들었던 요소를 지운다. 프리팹 인스턴스 안이면 지울 수 없어 숨긴다
    private static void RemoveChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
            return;

        if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
            SetShown(child.gameObject, false);
        else
            Undo.DestroyObjectImmediate(child.gameObject);
    }

    private static void SetShown(GameObject go, bool shown)
    {
        if (go.activeSelf == shown)
            return;

        Undo.RecordObject(go, "HUD");
        go.SetActive(shown);
    }

    private static GameObject BuildCanvas(Scene scene, string undoName)
    {
        GameObject go = FindRoot(scene, CanvasName);
        if (go == null)
        {
            go = new GameObject(CanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, undoName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = LayerMask.NameToLayer("UI");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(go, undoName);
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
    private static void BuildHeader(Transform root, SerializedObject hud, Profile profile)
    {
        GameObject header = FindOrCreate(root, "Header");
        PlaceRect(header, 40f, 24f, 380f, 104f);
        Transform t = header.transform;

        PlaceBox(t, "Mark", Yellow, 0f, 4f, 8f, 8f);
        PlaceLabel(t, "System", "RS-OS   FIELD HUD", 11, Bone, TextAlignmentOptions.Left, 14f, 0f, 160f, 16f);
        PlaceBox(t, "Line", Ghost, 140f, 8f, 100f, 1f);
        PlaceLabel(t, "Version", "//// v0.3", 11, Neutral, TextAlignmentOptions.Left, 248f, 0f, 100f, 16f);
        PlaceLabel(t, "Sector", profile.Sector, 11, Neutral, TextAlignmentOptions.Left, 0f, 24f, 300f, 16f);
        PlaceDiamond(t, "Diamond", Yellow, 8f, 50f, 13f);
        TextMeshProUGUI title = PlaceLabel(t, "Location", profile.Location, 36, Bone, TextAlignmentOptions.Left, 30f, 44f, 300f, 46f);
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
    private static void BuildBanner(Transform root, SerializedObject hud, Profile profile)
    {
        GameObject banner = FindOrCreate(root, "ArrivalBanner");
        PlaceRect(banner, 632f, 32f, 656f, 120f);
        Overlay(banner.transform, "frame_panelMedium_v8", 8f);
        CanvasGroup group = GetOrAdd<CanvasGroup>(banner);
        group.blocksRaycasts = false;
        Transform t = banner.transform;

        PlacePanel(t, "Plate", "bg_picker_populated", Color.white, 1f, 8f, 8f, 640f, 104f);
        PlaceBox(t, "Accent", Orange, 8f, 8f, 8f, 104f);
        PlaceLabel(t, "Caption", profile.Arrival, 12, Dim, TextAlignmentOptions.Left, 36f, 22f, 300f, 16f);
        TextMeshProUGUI title = PlaceLabel(t, "Title", profile.Location, 34, Ink, TextAlignmentOptions.Left, 34f, 40f, 300f, 42f);
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

    //================================ 우상단: 고물 레이더 ================================
    private static void BuildMinimap(Transform root)
    {
        GameObject map = PlacePanel(root, "Minimap", "bg_panelSmall", Color.white, 3f, 1615f, 100f, 265f, 235f);
        Overlay(map.transform, "frame_panelMedium_v8", 8f);
        Transform t = map.transform;

        Transform oldView = t.Find("MapView");
        if (oldView != null)
            Undo.DestroyObjectImmediate(oldView.gameObject);

        Sprite circle = EnsureCircleSprite(CirclePath, 0f);
        Sprite ring = EnsureCircleSprite(RingPath, 3f);

        GameObject radar = FindOrCreate(t, "Radar");
        PlaceRect(radar, 37f, 22f, RadarSize, RadarSize);
        Transform r = radar.transform;

        PlaceSprite(r, "Base", circle, new Color(1f, 1f, 1f, 0.06f), 0f);
        PlaceBox(r, "AxisH", Ghost, 0f, RadarSize * 0.5f, RadarSize, 1f);
        PlaceBox(r, "AxisV", Ghost, RadarSize * 0.5f, 0f, 1f, RadarSize);
        PlaceSprite(r, "RingInner", ring, Ghost, RadarSize * 0.2f);
        PlaceSprite(r, "RingOuter", ring, new Color(0.71f, 0.73f, 0.75f, 0.6f), 0f);

        Image sweep = PlaceSprite(r, "Sweep", circle, new Color(Yellow.r, Yellow.g, Yellow.b, SweepAlpha), 0f);
        sweep.type = Image.Type.Filled;
        sweep.fillMethod = Image.FillMethod.Radial360;
        sweep.fillOrigin = (int)Image.Origin360.Top;
        sweep.fillClockwise = false;
        sweep.fillAmount = SweepAngle / 360f;

        GameObject blips = FindOrCreate(r, "Blips");
        Stretch(blips.GetComponent<RectTransform>());

        GameObject template = PlaceBox(r, "BlipTemplate", Orange, 0f, 0f, 8f, 8f);
        Center(template.GetComponent<RectTransform>(), 8f);
        template.SetActive(false);

        GameObject player = PlaceDiamond(r, "Player", Yellow, 0f, 0f, 10f);
        Center(player.GetComponent<RectTransform>(), 10f);
        player.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 45f);

        PlaceLabel(t, "Caption", "SCRAP RADAR", 10, Neutral, TextAlignmentOptions.Right, 140f, 212f, 100f, 14f);

        ScrapRadarView view = GetOrAdd<ScrapRadarView>(map);
        var so = new SerializedObject(view);
        so.FindProperty("_sweep").objectReferenceValue = sweep;
        so.FindProperty("_blipRoot").objectReferenceValue = blips.GetComponent<RectTransform>();
        so.FindProperty("_blipTemplate").objectReferenceValue = template;
        if (so.FindProperty("_detector").objectReferenceValue == null)
            so.FindProperty("_detector").objectReferenceValue = Object.FindAnyObjectByType<ScrapNodeDetector>(FindObjectsInactive.Include);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 레이더 영역을 inset만큼 줄여 채운다. 회전 중심이 가운데가 되도록 피벗도 가운데
    private static Image PlaceSprite(Transform parent, string name, Sprite sprite, Color color, float inset)
    {
        GameObject go = FindOrCreate(parent, name);
        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;

        Image image = GetOrAdd<Image>(go);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Center(RectTransform rect, float size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
    }

    // 원(thickness 0) 또는 링 스프라이트를 없을 때만 만든다
    private static Sprite EnsureCircleSprite(string path, float thickness)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        const int size = 256;
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f - 1f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - d + 0.5f);
                if (thickness > 0f)
                    alpha = Mathf.Min(alpha, Mathf.Clamp01(d - (radius - thickness) + 0.5f));

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));

        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    //================================ 우측: 미션 ================================
    private static void BuildMissions(Transform root, SerializedObject hud, float y)
    {
        GameObject panel = FindOrCreate(root, "Missions");
        PlaceRect(panel, 1560f, y, 320f, 230f);
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
    private static void BuildKeyHints(Transform root, (string key, string label)[] hints)
    {
        GameObject bar = FindOrCreate(root, "KeyHints");
        PlaceRect(bar, 1100f, 1012f, 780f, 32f);

        // 장소마다 항목 수가 달라 남는 칸은 숨긴다. 프리팹 인스턴스여도 되도록 지우지 않는다
        foreach (Transform child in bar.transform)
        {
            SetShown(child.gameObject, false);
        }

        // 오른쪽 끝에서부터 채운다
        float right = 780f;
        for (int i = hints.Length - 1; i >= 0; i--)
        {
            (string key, string label) = hints[i];
            float labelWidth = label.Length * 15f + 4f;
            float keyWidth = Mathf.Max(30f, key.Length * 11f + 16f);

            right -= labelWidth;
            TextMeshProUGUI text = PlaceLabel(bar.transform, $"Label_{i}", label, 14, Bone, TextAlignmentOptions.Left, right, 4f, labelWidth, 24f);
            SetShown(text.gameObject, true);

            right -= keyWidth + 8f;
            GameObject chip = PlacePanel(bar.transform, $"Key_{i}", "bg_picker_populated", Color.white, 4f, right, 2f, keyWidth, 28f);
            PlaceLabel(chip.transform, "Label", key, 12, Ink, TextAlignmentOptions.Center, 0f, 0f, keyWidth, 28f);
            SetShown(chip, true);

            right -= 22f;
        }
    }
}
