using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 씬에 스킬 창을 만들고, 전투 씬의 플레이어에 파츠 스킬 로드아웃을 붙인다. 이름으로 찾아 재사용하므로 다시 실행해도 된다
public static class SkillWindowBuilder
{
    private const string CanvasName = "Canvas_SkillWindow";
    private const string CatalogPath = "Assets/KDU/03.SOData/03.LegSkill/PartSkillCatalog.asset";
    private const string PlayerControlType = "RSSimpleCharacter";
    private const string LoadoutTitle = "전투 로드아웃 연결";
    private const int SortingOrder = 6;
    private const int RowCount = 4;

    private const float WindowWidth = 1320f;
    private const float WindowHeight = 760f;
    private const float SectionTop = 130f;
    private const float SectionHeight = 590f;
    private const float ListX = 40f;
    private const float ListWidth = 680f;
    private const float DetailX = 740f;
    private const float DetailWidth = 540f;
    private const float RowLeft = 20f;
    private const float RowTop = 76f;
    private const float RowWidth = 640f;
    private const float RowHeight = 110f;
    private const float RowGap = 12f;

    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color ButtonDisabled = new Color(1f, 1f, 1f, 0.25f);

    [MenuItem("KDU/스킬/스킬 창 생성")]
    public static void BuildWindow()
    {
        string missing = Load();
        if (missing != null)
        {
            Debug.LogError($"에셋을 찾지 못했다: {missing}");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        // 이미 있으면 손으로 고친 배치·글자를 덮어쓰지 않는다
        if (FindRoot(scene, CanvasName) != null)
        {
            Debug.Log($"{CanvasName}이 이미 있어 건너뛴다. 다시 만들려면 지우고 실행할 것.");
            return;
        }

        GameObject canvas = BuildCanvas(scene);
        GameObject window = BuildFrame(canvas.transform);

        Transform list = BuildList(window.transform, out TextMeshProUGUI points, out GameObject empty, out RectTransform rows);
        BuildDetail(window.transform, out TextMeshProUGUI skillName, out TextMeshProUGUI description);
        BindOpenEffect(window, rows);

        SkillWindowView view = GetOrAdd<SkillWindowView>(canvas);
        var so = new SerializedObject(view);
        so.FindProperty("_panel").objectReferenceValue = window;
        so.FindProperty("_pointsLabel").objectReferenceValue = points;
        so.FindProperty("_emptyNotice").objectReferenceValue = empty;
        so.FindProperty("_skillNameLabel").objectReferenceValue = skillName;
        so.FindProperty("_descriptionLabel").objectReferenceValue = description;
        so.ApplyModifiedProperties();

        BindInputGate(canvas);
        window.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"스킬 창 생성 완료. {list.name} 줄 {RowCount}개. 씬을 저장하세요.", view);
    }

    [MenuItem("KDU/스킬/전투 로드아웃 연결")]
    private static void BindBattleLoadout()
    {
        PlayerSkillLoadout fallback = Object.FindFirstObjectByType<PlayerSkillLoadout>(FindObjectsInactive.Include);
        if (fallback == null)
        {
            EditorUtility.DisplayDialog(LoadoutTitle, "이 씬에 PlayerSkillLoadout이 없습니다.\n전투 씬을 연 상태로 실행하세요.", "확인");
            return;
        }

        GameObject player = fallback.gameObject;
        PartSkillLoadout loadout = GetOrAdd<PartSkillLoadout>(player);

        var so = new SerializedObject(loadout);
        so.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartSkillCatalog>(CatalogPath);
        so.FindProperty("_fallback").objectReferenceValue = fallback;
        so.ApplyModifiedProperties();

        // GetComponent<ISkillLoadout>가 이쪽을 먼저 찾도록 앞으로 옮긴다
        while (IndexOf(player, loadout) > IndexOf(player, fallback))
        {
            if (!ComponentUtility.MoveComponentUp(loadout))
            {
                Debug.LogError("PartSkillLoadout을 PlayerSkillLoadout 앞으로 옮기지 못했다.", player);
                return;
            }
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorUtility.DisplayDialog(LoadoutTitle, $"{player.name}에 연결했습니다. 씬을 저장하세요.", "확인");
    }

    private static int IndexOf(GameObject go, Component target)
    {
        return System.Array.IndexOf(go.GetComponents<Component>(), target);
    }

    private static GameObject BuildCanvas(Scene scene)
    {
        GameObject go = FindRoot(scene, CanvasName);
        if (go == null)
        {
            go = new GameObject(CanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = LayerMask.NameToLayer("UI");
        }

        Canvas canvas = GetOrAdd<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = GetOrAdd<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(go);
        return go;
    }

    //================================ 창 ================================
    private static GameObject BuildFrame(Transform canvas)
    {
        GameObject window = FindOrCreate(canvas, "SkillWindow");
        RectTransform rect = window.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        SetImage(window, "bg_panelLarge", Color.white, 1f);
        Overlay(window.transform, "frame_panelLarge_v3", 2f);

        PlaceLabel(window.transform, "Caption", "■ RS-OS   SKILLS", 14, Neutral, TextAlignmentOptions.Left, 60f, 28f, 400f, 20f);
        PlaceLabel(window.transform, "Title", "SKILLS", 56, Bone, TextAlignmentOptions.Left, 56f, 46f, 240f, 64f);
        PlaceLabel(window.transform, "SubTitle", "스킬", 28, Bone, TextAlignmentOptions.Left, 300f, 70f, 200f, 36f);
        PlaceBox(window.transform, "Divider", Ghost, 40f, 118f, WindowWidth - 80f, 2f);
        return window;
    }

    private static Transform Section(Transform window, string name, float x, float width)
    {
        GameObject panel = FindOrCreate(window, name);
        PlaceRect(panel, x, SectionTop, width, SectionHeight);
        SetImage(panel, "swatch_v2", Color.white, 1f);
        Overlay(panel.transform, "frame_panelMedium_v8", 3f);
        return panel.transform;
    }

    //================================ 왼쪽: 스킬 목록 ================================
    private static Transform BuildList(Transform window, out TextMeshProUGUI points, out GameObject empty, out RectTransform rows)
    {
        Transform panel = Section(window, "ListPanel", ListX, ListWidth);
        PlaceLabel(panel, "Caption", "SKILL LEVEL", 14, Cyan, TextAlignmentOptions.Left, 28f, 28f, 200f, 20f);
        points = PlaceLabel(panel, "Points", "스킬 포인트 0", 26, Yellow, TextAlignmentOptions.Right, ListWidth - 328f, 20f, 300f, 36f);

        TextMeshProUGUI notice = PlaceLabel(panel, "Empty", "레벨을 올릴 수 있는 스킬이 없습니다", 22, Neutral, TextAlignmentOptions.Center, 0f, 260f, ListWidth, 40f);
        empty = notice.gameObject;

        GameObject group = FindOrCreate(panel, "Rows");
        PlaceRect(group, RowLeft, RowTop, RowWidth, (RowHeight + RowGap) * RowCount);
        rows = group.GetComponent<RectTransform>();

        for (int i = 0; i < RowCount; i++)
        {
            BuildRow(group.transform, i);
        }

        return panel;
    }

    private static void BuildRow(Transform parent, int index)
    {
        GameObject row = PlacePanel(parent, $"Row_{index}", "bg_selectItem_enabled", RowColor, 2f, 0f, (RowHeight + RowGap) * index, RowWidth, RowHeight);
        Image background = row.GetComponent<Image>();
        background.raycastTarget = true;

        Button select = GetOrAdd<Button>(row);
        select.targetGraphic = background;

        GameObject highlight = PlacePanel(row.transform, "Highlight", "frame_panelMedium_v4", Yellow, 3f, 0f, 0f, RowWidth, RowHeight);

        TextMeshProUGUI slot = PlaceLabel(row.transform, "SlotLabel", "L.LEG · E", 14, Cyan, TextAlignmentOptions.Left, 24f, 18f, 240f, 20f);
        TextMeshProUGUI name = PlaceLabel(row.transform, "SkillName", "스킬", 30, Bone, TextAlignmentOptions.Left, 24f, 44f, 330f, 42f);

        Button minus = BuildStepButton(row.transform, "Minus", "-", 390f, out TextMeshProUGUI minusGlyph);
        TextMeshProUGUI level = PlaceLabel(row.transform, "Level", "1", 30, Bone, TextAlignmentOptions.Center, 450f, 20f, 70f, 44f);
        Button plus = BuildStepButton(row.transform, "Plus", "+", 530f, out TextMeshProUGUI plusGlyph);
        PlaceLabel(row.transform, "LevelCaption", "스킬 레벨", 14, Neutral, TextAlignmentOptions.Center, 390f, 74f, 192f, 20f);

        SkillRowView view = GetOrAdd<SkillRowView>(row);
        var so = new SerializedObject(view);
        so.FindProperty("_nameLabel").objectReferenceValue = name;
        so.FindProperty("_slotLabel").objectReferenceValue = slot;
        so.FindProperty("_levelLabel").objectReferenceValue = level;
        so.FindProperty("_selectButton").objectReferenceValue = select;
        so.FindProperty("_minusButton").objectReferenceValue = minus;
        so.FindProperty("_plusButton").objectReferenceValue = plus;
        so.FindProperty("_minusGlyph").objectReferenceValue = minusGlyph;
        so.FindProperty("_plusGlyph").objectReferenceValue = plusGlyph;
        so.FindProperty("_highlight").objectReferenceValue = highlight;
        so.FindProperty("_glyphEnabled").colorValue = Bone;
        so.FindProperty("_glyphDisabled").colorValue = Dim;
        so.ApplyModifiedProperties();
    }

    private static Button BuildStepButton(Transform row, string name, string label, float x, out TextMeshProUGUI glyph)
    {
        GameObject go = PlacePanel(row, name, "bg_iconButton_v1_sm_enabled", Color.white, 2f, x, 20f, 52f, 44f);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = true;

        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = ButtonDisabled;
        button.colors = colors;

        glyph = PlaceLabel(go.transform, "Glyph", label, 30, Bone, TextAlignmentOptions.Center, 0f, 0f, 52f, 44f);
        return button;
    }

    //================================ 오른쪽: GIF·설명 자리 ================================
    private static void BuildDetail(Transform window, out TextMeshProUGUI skillName, out TextMeshProUGUI description)
    {
        Transform panel = Section(window, "DetailPanel", DetailX, DetailWidth);
        skillName = PlaceLabel(panel, "Caption", "SKILL NAME", 32, Cyan, TextAlignmentOptions.Left, 28f, 20f, DetailWidth - 56f, 44f);

        GameObject preview = PlacePanel(panel, "PreviewArea", "swatch_v3", Color.white, 1f, 28f, 72f, DetailWidth - 56f, 270f);
        PlaceLabel(preview.transform, "Placeholder", "스킬 GIF", 22, Dim, TextAlignmentOptions.Center, 0f, 115f, DetailWidth - 56f, 40f);

        description = PlaceLabel(panel, "Description", "설명", 22, Bone, TextAlignmentOptions.TopLeft, 28f, 360f, DetailWidth - 56f, 200f);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Overflow;
    }

    //================================ 입력 차단 ================================
    // 창이 열린 동안 플레이어 조작을 끈다. 이미 연결된 씬이면 건드리지 않는다
    private static void BindInputGate(GameObject canvas)
    {
        if (Object.FindFirstObjectByType<ScrapInputGateRelay>(FindObjectsInactive.Include) != null)
            return;

        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name != PlayerControlType)
                continue;

            var so = new SerializedObject(GetOrAdd<ScrapInputGateRelay>(canvas));
            SerializedProperty targets = so.FindProperty("_targets");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = behaviour;
            so.ApplyModifiedProperties();
            return;
        }

        Debug.LogWarning($"{PlayerControlType}를 찾지 못해 플레이어 입력 차단을 연결하지 않았다.");
    }

    //================================ 열림 연출 ================================
    private static void BindOpenEffect(GameObject window, RectTransform rows)
    {
        WindowOpenEffect effect = GetOrAdd<WindowOpenEffect>(window);
        var so = new SerializedObject(effect);

        SerializedProperty groups = so.FindProperty("_rowGroups");
        groups.arraySize = 1;
        groups.GetArrayElementAtIndex(0).objectReferenceValue = rows;

        SerializedProperty lines = so.FindProperty("_lines");
        lines.arraySize = 1;
        lines.GetArrayElementAtIndex(0).objectReferenceValue = window.transform.Find("Divider");

        so.ApplyModifiedProperties();
    }
}
