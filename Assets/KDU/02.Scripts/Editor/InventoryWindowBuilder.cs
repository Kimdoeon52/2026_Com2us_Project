using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 씬의 Tab 인벤토리 창을 장착 / 그리드 / 스펙·스킬 3단으로 배치하고 SYS/UI로 입힌다.
// 계층 이름으로 찾아 재사용하므로 다시 실행해도 쌓이지 않는다
public static class InventoryWindowBuilder
{
    private const string Title = "인벤토리 창 스타일 적용";
    private const string SkillCatalogPath = "Assets/KDU/03.SOData/03.LegSkill/PartSkillCatalog.asset";
    private const string GridCellPath = "Assets/KDU/04.Prefab/GridCell.prefab";

    private const float WindowWidth = 1800f;
    private const float WindowHeight = 960f;
    private const float SectionTop = 130f;
    private const float SectionHeight = 800f;
    private const float LoadoutX = 40f;
    private const float LoadoutWidth = 640f;
    private const float StorageX = 700f;
    private const float StorageWidth = 440f;
    private const float SpecX = 1160f;
    private const float SpecWidth = 600f;
    private const int MaxCostPips = 5;
    private const int InventorySortingOrder = 4;

    private static readonly Color CellColor = new Color(1f, 1f, 1f, 0.35f);

    // 장착 칸 이름, 라벨, 좌상단 위치
    private static readonly (string name, string label, Vector2 pos)[] SlotLayout =
    {
        ("Head", "HEAD", new Vector2(232f, 24f)),
        ("Core", "CORE", new Vector2(448f, 60f)),
        ("L_Arm", "L.ARM", new Vector2(16f, 200f)),
        ("R_Arm", "R.ARM", new Vector2(448f, 230f)),
        ("L_Leg", "L.LEG", new Vector2(16f, 480f)),
        ("R_Leg", "R.LEG", new Vector2(448f, 500f)),
    };

    private static readonly Vector2 SlotSize = new Vector2(176f, 140f);

    // 스킬 카드 부위와 라벨
    private static readonly (Slot slot, string label)[] SkillLayout =
    {
        (Slot.LeftArm, "L.ARM · Q"),
        (Slot.RightArm, "R.ARM · W"),
        (Slot.LeftLeg, "L.LEG · E"),
        (Slot.RightLeg, "R.LEG · R"),
        (Slot.Head, "HEAD"),
    };

    [MenuItem("KDU/인벤토리/인벤토리 창 스타일 적용")]
    public static void Apply()
    {
        string missing = Load();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(Title, $"리소스를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject equipmentCanvas = FindRoot(scene, "Canvas_Equipment");
        GameObject inventoryCanvas = FindRoot(scene, "Canvas_Inventory");
        Transform window = equipmentCanvas != null ? equipmentCanvas.transform.Find("Equipment_background") : null;
        Transform grid = inventoryCanvas != null ? inventoryCanvas.transform.Find("InventoryPanel") : null;

        if (window == null || grid == null)
        {
            EditorUtility.DisplayDialog(Title, "Canvas_Equipment/Equipment_background 또는 Canvas_Inventory/InventoryPanel을 찾지 못했습니다.", "확인");
            return;
        }

        Undo.SetCurrentGroupName(Title);
        int group = Undo.GetCurrentGroup();
        Undo.RegisterFullObjectHierarchyUndo(equipmentCanvas, Title);
        Undo.RegisterFullObjectHierarchyUndo(inventoryCanvas, Title);

        BuildWindow(window);
        Transform loadout = BuildLoadout(window);
        BuildStorage(window);
        BuildSpec(window);
        MoveSlots(window, loadout);

        SkinGrid(grid);
        RaiseInventoryCanvas(inventoryCanvas);
        SetTabPosition(inventoryCanvas, grid, StorageCenter());
        ApplyFont(equipmentCanvas);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[{Title}] 완료. 씬을 저장하세요.");
    }

    //================================ 창 ================================
    private static void BuildWindow(Transform window)
    {
        RectTransform rect = (RectTransform)window;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        SetImage(window.gameObject, "bg_panelLarge", Color.white, 1f);
        Overlay(window, "frame_panelLarge_v3", 2f);

        PlaceLabel(window, "Caption", "■ RS-OS   STORAGE", 14, Neutral, TextAlignmentOptions.Left, 60f, 28f, 400f, 20f);
        PlaceLabel(window, "Title", "INVENTORY", 56, Bone, TextAlignmentOptions.Left, 56f, 46f, 340f, 64f);
        PlaceLabel(window, "SubTitle", "인벤토리", 28, Bone, TextAlignmentOptions.Left, 400f, 70f, 200f, 36f);
        PlaceBox(window, "Divider", Ghost, 40f, 118f, WindowWidth - 80f, 2f);
    }

    //================================ 왼쪽: 장착 ================================
    private static Transform BuildLoadout(Transform window)
    {
        Transform panel = Section(window, "LoadoutPanel", LoadoutX, LoadoutWidth);
        PlaceLabel(panel, "Caption", "LOADOUT  //  6 PARTS", 14, Cyan, TextAlignmentOptions.Right, LoadoutWidth - 236f, 760f, 200f, 20f);

        Transform body = FindOrCreate(panel, "Silhouette").transform;
        Stretch((RectTransform)body);
        body.SetSiblingIndex(1);
        PlaceBox(body, "Head", Ghost, 290f, 150f, 60f, 44f);
        PlaceBox(body, "Torso", Ghost, 250f, 206f, 140f, 150f);
        PlaceBox(body, "Core", Yellow, 300f, 250f, 40f, 40f);
        PlaceBox(body, "ArmL", Ghost, 204f, 212f, 40f, 170f);
        PlaceBox(body, "ArmR", Ghost, 396f, 212f, 40f, 170f);
        PlaceBox(body, "HandL", Bone, 196f, 382f, 56f, 36f);
        PlaceBox(body, "HandR", Bone, 388f, 382f, 56f, 36f);
        PlaceBox(body, "LegL", Ghost, 262f, 366f, 52f, 200f);
        PlaceBox(body, "LegR", Ghost, 326f, 366f, 52f, 200f);
        return panel;
    }

    // 기존 장착 칸을 장착 패널로 옮기고 카드 모양으로 입힌다
    private static void MoveSlots(Transform window, Transform loadout)
    {
        for (int i = 0; i < SlotLayout.Length; i++)
        {
            (string name, string label, Vector2 pos) = SlotLayout[i];
            Transform slot = window.Find(name);
            if (slot == null)
                slot = loadout.Find(name);

            if (slot == null)
            {
                Debug.LogWarning($"[{Title}] 장착 칸 {name} 없음");
                continue;
            }

            if (slot.parent != loadout)
                Undo.SetTransformParent(slot, loadout, Title);

            PlaceRect(slot.gameObject, pos.x, pos.y, SlotSize.x, SlotSize.y);
            SkinSlot(slot, name, label);
        }
    }

    private static void SkinSlot(Transform slot, string name, string label)
    {
        Transform card = slot.Find($"{name}_Part");
        if (card == null)
            return;

        Stretch((RectTransform)card);
        SetImage(card.gameObject, "bg_selectItem_enabled", Color.white, 1f);

        // 아이콘이 배경에 가려지지 않도록 배경을 맨 뒤로
        Transform background = card.Find($"{name}_Background_Image");
        if (background != null)
        {
            background.SetAsFirstSibling();
            PlaceRect(background.gameObject, 12f, 36f, 92f, 92f);
            SetImage(background.gameObject, "swatch_v3", Color.white, 1f);
        }

        Transform icon = card.Find($"{name}_Image");
        if (icon != null)
            PlaceRect(icon.gameObject, 16f, 40f, 84f, 84f);

        Transform text = card.Find($"{name}_PartLabel");
        if (text != null && text.TryGetComponent(out TMP_Text partLabel))
        {
            PlaceRect(text.gameObject, 12f, 8f, SlotSize.x - 24f, 22f);
            partLabel.text = label;
            partLabel.fontSize = 14;
            partLabel.color = Cyan;
            partLabel.alignment = TextAlignmentOptions.Left;
            partLabel.raycastTarget = false;
        }
    }

    //================================ 가운데: 그리드 자리 ================================
    private static void BuildStorage(Transform window)
    {
        Transform panel = Section(window, "StoragePanel", StorageX, StorageWidth, "frame_panelMedium_v4");
        PlaceLabel(window, "StorageCaption", "STORAGE  //  6 x 12", 14, Cyan, TextAlignmentOptions.Right, StorageX + StorageWidth - 220f, 100f, 220f, 20f);
        panel.GetComponent<Image>().raycastTarget = false;
    }

    // 그리드 바탕과 칸 프리팹. 제작 모드에서도 혼자 뜨므로 바탕은 그리드 패널이 직접 가진다
    private static void SkinGrid(Transform grid)
    {
        Image background = grid.GetComponent<Image>();
        if (background != null)
        {
            bool raycast = background.raycastTarget;
            SetImage(grid.gameObject, "swatch_v2", Color.white, 1f);
            background.raycastTarget = raycast;
        }

        GameObject cell = PrefabUtility.LoadPrefabContents(GridCellPath);
        try
        {
            Image image = cell.GetComponent<Image>();
            image.sprite = Sprites["bg_iconButton_v1_sm_enabled"];
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = CellColor;
            PrefabUtility.SaveAsPrefabAsset(cell, GridCellPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(cell);
        }
    }

    // 그리드 패널이 올라갈 위치. 두 캔버스의 기준 해상도가 같다는 전제
    private static Vector2 StorageCenter()
    {
        float x = -WindowWidth * 0.5f + StorageX + StorageWidth * 0.5f;
        float y = WindowHeight * 0.5f - SectionTop - SectionHeight * 0.5f;
        return new Vector2(x, y);
    }

    //================================ 오른쪽: 스펙·스킬 ================================
    private static void BuildSpec(Transform window)
    {
        Transform panel = Section(window, "SpecPanel", SpecX, SpecWidth);
        PlaceLabel(panel, "Caption", "PLAYER  //  SPEC", 12, Cyan, TextAlignmentOptions.Left, 104f, 32f, 300f, 16f);
        PlaceLabel(panel, "Title", "스펙", 28, Bone, TextAlignmentOptions.Left, 32f, 20f, 70f, 36f);
        PlaceBox(panel, "Divider", Ghost, 32f, 64f, SpecWidth - 64f, 2f);

        MoveStat(window, panel);

        PlaceBox(panel, "SkillDivider", Ghost, 32f, 360f, SpecWidth - 64f, 2f);
        TextMeshProUGUI header = PlaceLabel(panel, "SkillHeader", "SKILLS // 0", 24, Bone, TextAlignmentOptions.Left, 32f, 372f, 260f, 32f);
        PlaceLabel(panel, "SkillCaption", "장착 파츠 스킬", 16, Neutral, TextAlignmentOptions.Left, 220f, 380f, 200f, 22f);

        GameObject list = FindOrCreate(panel, "SkillList");
        PlaceRect(list, 32f, 414f, SpecWidth - 64f, 366f);

        PlayerSkillView view = list.GetComponent<PlayerSkillView>();
        if (view == null)
            view = Undo.AddComponent<PlayerSkillView>(list);

        var so = new SerializedObject(view);
        so.FindProperty("_header").objectReferenceValue = header;
        if (so.FindProperty("_catalog").objectReferenceValue == null)
            so.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PartSkillCatalog>(SkillCatalogPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < SkillLayout.Length; i++)
        {
            BuildSkillCard(list.transform, i, SkillLayout[i].slot, SkillLayout[i].label);
        }
    }

    // 기존 스탯 표시를 스펙 패널로 옮긴다. 제목은 패널 제목이 대신한다
    private static void MoveStat(Transform window, Transform panel)
    {
        Transform stat = window.Find("PlayerStat");
        if (stat == null)
            stat = panel.Find("PlayerStat");

        if (stat == null)
        {
            Debug.LogWarning($"[{Title}] PlayerStat 없음");
            return;
        }

        if (stat.parent != panel)
            Undo.SetTransformParent(stat, panel, Title);

        PlaceRect(stat.gameObject, 32f, 76f, SpecWidth - 64f, 276f);

        PlayerStatView view = stat.GetComponent<PlayerStatView>();
        if (view == null)
            return;

        var so = new SerializedObject(view);
        so.FindProperty("_title").stringValue = "";
        so.FindProperty("_lineHeight").floatValue = 1.35f;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (so.FindProperty("_text").objectReferenceValue is TextMeshProUGUI text)
        {
            text.fontSize = 20;
            text.color = Bone;
            text.lineSpacing = 0f;
            text.raycastTarget = false;
        }
    }

    private static void BuildSkillCard(Transform list, int index, Slot slot, string slotLabel)
    {
        const float height = 66f;
        const float gap = 6f;
        float width = SpecWidth - 64f;

        GameObject card = FindOrCreate(list, $"SkillCard_{index}");
        PlaceRect(card, 0f, index * (height + gap), width, height);
        SetImage(card, "bg_panelWide_chamfer", Color.white, 3f);
        card.GetComponent<Image>().raycastTarget = false;

        PlaceBox(card.transform, "Accent", Yellow, 0f, 14f, 4f, height - 28f);
        TextMeshProUGUI skillName = PlaceLabel(card.transform, "SkillName", "비어 있음", 24, Bone, TextAlignmentOptions.Left, 20f, 6f, 300f, 30f);
        PlaceLabel(card.transform, "SlotLabel", slotLabel, 12, Cyan, TextAlignmentOptions.Right, width - 220f, 44f, 200f, 16f);
        TextMeshProUGUI partName = PlaceLabel(card.transform, "PartName", "", 14, Neutral, TextAlignmentOptions.Left, 140f, 40f, 220f, 20f);
        PlaceLabel(card.transform, "CostLabel", "COST", 12, Neutral, TextAlignmentOptions.Left, 20f, 42f, 44f, 16f);

        var pips = new Image[MaxCostPips];
        for (int i = 0; i < MaxCostPips; i++)
        {
            GameObject pip = PlaceBox(card.transform, $"Pip_{i}", Yellow, 66f + i * 14f, 44f, 9f, 9f);
            pip.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 45f);
            pip.SetActive(false);
            pips[i] = pip.GetComponent<Image>();
        }

        // 종류 태그: 카드 오른쪽 위 칩
        GameObject tag = FindOrCreate(card.transform, "Tag");
        PlaceRect(tag, width - 196f, 8f, 180f, 28f);
        SetImage(tag, "bg_statusButton_warning_enabled", Color.white, 2f);
        tag.GetComponent<Image>().raycastTarget = false;
        TextMeshProUGUI tagLabel = PlaceLabel(tag.transform, "Label", "", 14, Ink, TextAlignmentOptions.Center, 0f, 0f, 180f, 28f);
        tag.SetActive(false);

        SkillCardView view = card.GetComponent<SkillCardView>();
        if (view == null)
            view = Undo.AddComponent<SkillCardView>(card);

        var so = new SerializedObject(view);
        so.FindProperty("_slot").enumValueIndex = (int)slot;
        so.FindProperty("_nameLabel").objectReferenceValue = skillName;
        so.FindProperty("_partLabel").objectReferenceValue = partName;
        so.FindProperty("_tagRoot").objectReferenceValue = tag;
        so.FindProperty("_tagLabel").objectReferenceValue = tagLabel;

        SerializedProperty pipArray = so.FindProperty("_costPips");
        pipArray.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++)
        {
            pipArray.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    //================================ 캔버스·토글 ================================
    // 그리드가 창 배경 위에 그려지도록 인벤토리 캔버스를 위로
    private static void RaiseInventoryCanvas(GameObject inventoryCanvas)
    {
        Canvas canvas = inventoryCanvas.GetComponent<Canvas>();
        if (canvas != null && canvas.sortingOrder < InventorySortingOrder)
            canvas.sortingOrder = InventorySortingOrder;
    }

    // 씬에서도 그리드를 Tab 창 가운데에 둔다. 다른 모드는 기존 위치를 모드 값으로 옮겨 유지한다
    private static void SetTabPosition(GameObject inventoryCanvas, Transform grid, Vector2 position)
    {
        InventoryToggle toggle = inventoryCanvas.GetComponent<InventoryToggle>();
        if (toggle == null)
        {
            Debug.LogWarning($"[{Title}] InventoryToggle 없음");
            return;
        }

        RectTransform gridRect = (RectTransform)grid;
        Vector2 home = gridRect.anchoredPosition;

        var so = new SerializedObject(toggle);
        SerializedProperty modes = so.FindProperty("_modes");
        for (int i = 0; i < modes.arraySize; i++)
        {
            SerializedProperty mode = modes.GetArrayElementAtIndex(i);
            SerializedProperty move = mode.FindPropertyRelative("MoveRoot");
            SerializedProperty target = mode.FindPropertyRelative("RootPosition");

            if (mode.FindPropertyRelative("Key").intValue == (int)KeyCode.Tab)
            {
                move.boolValue = true;
                target.vector2Value = position;
            }
            else if (!move.boolValue)
            {
                move.boolValue = true;
                target.vector2Value = home;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        gridRect.anchoredPosition = position;
    }

    // 섹션 패널: 어두운 바탕 + 모서리 프레임
    private static Transform Section(Transform window, string name, float x, float width, string frame = "frame_panelMedium_v8")
    {
        GameObject panel = FindOrCreate(window, name);
        PlaceRect(panel, x, SectionTop, width, SectionHeight);
        SetImage(panel, "swatch_v2", Color.white, 1f);
        Overlay(panel.transform, frame, 3f);
        return panel.transform;
    }
}
