using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// craftingWindow, ShopWindow 프리팹의 외형만 SYS/UI 리소스로 바꾼다. 계층과 스크립트 참조는 건드리지 않는다
public static class CraftShopPrefabSkinner
{
    private const string Title = "제작·상점 프리팹 스타일 적용";
    private const string CraftingPath = "Assets/YJW/02.Prefabs/craftingWindow.prefab";
    private const string ShopPath = "Assets/YJW/02.Prefabs/ShopWindow.prefab";
    private const string FontPath = "Assets/KDU/05.Font/NeoDunggeunmoPro-Regular SDF.asset";

    private static readonly Color Yellow = FromHex("FFE800");
    private static readonly Color Cyan = FromHex("35D0DE");
    private static readonly Color Bone = FromHex("EEEDE8");
    private static readonly Color Neutral = FromHex("B4B9C0");
    private static readonly Color Ink = FromHex("1A1B1D");
    private static readonly Color CannotSell = FromHex("737373");

    private static TMP_FontAsset _font;

    [MenuItem("Tools/YJW/제작·상점 프리팹 스타일 적용")]
    private static void Apply()
    {
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (_font == null)
        {
            EditorUtility.DisplayDialog(Title, $"폰트를 찾을 수 없습니다.\n{FontPath}", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog(Title, "craftingWindow, ShopWindow 프리팹의 이미지와 글자 스타일을 바꾸고 바로 저장합니다.\nCtrl+Z로 되돌릴 수 없습니다. (git으로 복구)", "적용", "취소"))
            return;

        string missing = FixWindowBuilder.PrepareSprites();
        if (missing != null)
        {
            EditorUtility.DisplayDialog(Title, $"스프라이트를 불러오지 못했습니다.\n{missing}", "확인");
            return;
        }

        EditPrefab(CraftingPath, SkinCrafting);
        EditPrefab(ShopPath, SkinShop);
        Debug.Log($"[{Title}] 완료.");
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    //================================ 제작 창 ================================
    private static void SkinCrafting(GameObject root)
    {
        ApplyFont(root);
        SetImage(root, "bg_panelLarge", Color.white);
        Overlay(root.transform, "frame_panelLarge_v3");

        foreach (string tab in new[] { "LeftA", "RightA", "LL", "RL" })
            SkinTab(root.transform.Find(tab), 22);

        Transform exit = root.transform.Find("ExitButton");
        SkinExit(exit);

        Transform recipe = root.transform.Find("RecipeBookPanel");
        SetImage(recipe.gameObject, "swatch_v2", Color.white);
        Overlay(recipe, "frame_panelMedium_v8");
        PlaceLabel(recipe, "Title", "레시피", 28, Bone, TextAlignmentOptions.Left, 24, 14, 200, 36);
        PlaceLabel(recipe, "Caption", "RECIPE BOOK", 14, Cyan, TextAlignmentOptions.Right, 276, 22, 200, 20);

        // RecipeSlot이 제작 가능할 때 초록을 입히므로 밝은 테두리 이미지를 쓴다
        foreach (RecipeSlot slot in root.GetComponentsInChildren<RecipeSlot>(true))
            SetImage(slot.gameObject, "bg_iconButton_v1_lg_enabled", Color.white);

        Transform crafting = root.transform.Find("CraftingPanel");
        SetImage(crafting.gameObject, "swatch_v2", Color.white);
        Overlay(crafting, "frame_panelMedium_v4");
        PlaceLabel(crafting, "Caption", "ASSEMBLY", 14, Cyan, TextAlignmentOptions.Center, 150, 10, 200, 20);

        // CombinationSlot이 재료가 없을 때 빨강을 입힌다
        foreach (CombinationSlot slot in root.GetComponentsInChildren<CombinationSlot>(true))
        {
            var so = new SerializedObject(slot);
            Image background = so.FindProperty("_BGImage").objectReferenceValue as Image;
            if (background == null)
                background = slot.GetComponent<Image>();
            SetImage(background.gameObject, "bg_iconButton_v1_lg_enabled", Color.white);
        }

        foreach (ResultSlot slot in root.GetComponentsInChildren<ResultSlot>(true))
            SetImage(slot.gameObject, "bg_iconButton_v1_lg_enabled", Yellow);

        PlaceIcon(crafting, "Arrow", "ic_keyboard_arrow_down", 226, 450, 48, 48);
        PlaceLabel(crafting, "ResultLabel", "제작 결과", 22, Bone, TextAlignmentOptions.Left, 40, 568, 130, 34);
    }

    //================================ 상점 창 ================================
    private static void SkinShop(GameObject root)
    {
        ApplyFont(root);
        SetImage(root, "bg_panelLarge", Color.white);
        Overlay(root.transform, "frame_panelLarge_v2");

        foreach (string tab in new[] { "Common", "Rare", "Epic" })
            SkinTab(root.transform.Find(tab), 36);

        SkinExit(root.transform.Find("BT_Exit"));

        Transform view = root.transform.Find("Scroll View");
        view.GetComponent<Image>().color = Color.clear;

        Transform bar = view.Find("Scrollbar Vertical");
        SetImage(bar.gameObject, "swatch_v2", Color.white);
        SetImage(bar.Find("Sliding Area/Handle").gameObject, "swatch_v1", Color.white);

        foreach (ShopSlot slot in root.GetComponentsInChildren<ShopSlot>(true))
            SkinShopSlot(slot);
    }

    private static void SkinShopSlot(ShopSlot slot)
    {
        Transform item = slot.transform;
        SetImage(slot.gameObject, "bg_selectItem_enabled", Color.white);

        Transform iconBG = item.Find("IconBG");
        SetImage(iconBG.gameObject, "swatch_v3", Color.white);
        iconBG.Find("ItemIcon").GetComponent<Image>().color = Color.white;

        SetText(item.Find("ItemName"), 28, Bone);
        SetText(item.Find("ItemGrade"), 22, Cyan);

        Transform sale = item.Find("SaleButton");
        SetImage(sale.gameObject, "bg_picker_populated", CannotSell, 2);
        SetText(sale.Find("Sale"), 20, Ink);
        SetText(sale.Find("Gold"), 26, Ink);

        // 판매 가능 여부를 어두운 뼈색/노랑으로 구분한다
        var so = new SerializedObject(slot);
        so.FindProperty("canSellColor").colorValue = Yellow;
        so.FindProperty("cannotSellColor").colorValue = CannotSell;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    //================================ 공용 ================================
    private static void SkinTab(Transform tab, float fontSize)
    {
        Toggle toggle = tab.GetComponent<Toggle>();
        Sprite onSprite = FixWindowBuilder.Sprites["bg_button_primary_enabled"];
        Sprite offSprite = FixWindowBuilder.Sprites["bg_button_secondary_enabled"];

        SetImage(tab.gameObject, toggle.isOn ? "bg_button_primary_enabled" : "bg_button_secondary_enabled", Color.white, 2);

        TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = fontSize;
        label.color = toggle.isOn ? Ink : Bone;

        var so = new SerializedObject(tab.GetComponent<TabColor>());
        so.FindProperty("onColor").colorValue = Color.white;
        so.FindProperty("offColor").colorValue = Color.white;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("onLabelColor").colorValue = Ink;
        so.FindProperty("offLabelColor").colorValue = Bone;
        so.FindProperty("onSprite").objectReferenceValue = onSprite;
        so.FindProperty("offSprite").objectReferenceValue = offSprite;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SkinExit(Transform exit)
    {
        SetImage(exit.gameObject, "bg_miniButton_default_enabled", Color.white, 1.5f);

        Button button = exit.GetComponent<Button>();
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = FixWindowBuilder.Sprites["bg_miniButton_default_hover"],
            selectedSprite = FixWindowBuilder.Sprites["bg_miniButton_default_hover"],
            pressedSprite = FixWindowBuilder.Sprites["bg_miniButton_default_pressed"],
            disabledSprite = FixWindowBuilder.Sprites["bg_miniButton_default_disabled"],
        };

        SetText(exit.GetComponentInChildren<TMP_Text>(true).transform, 30, Yellow);
    }

    private static void ApplyFont(GameObject root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            text.font = _font;
    }

    private static void SetImage(GameObject go, string sprite, Color color, float multiplier = 1)
    {
        Image image = go.GetComponent<Image>();
        image.sprite = FixWindowBuilder.Sprites[sprite];
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = color;
    }

    private static void SetText(Transform target, float size, Color color)
    {
        TMP_Text text = target.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.color = color;
    }

    // 다시 실행해도 쌓이지 않도록 이름으로 찾아 재사용한다
    private static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
            return found.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    // 맨 앞 자식으로 깔아 슬롯과 버튼을 가리지 않는다
    private static void Overlay(Transform parent, string frame)
    {
        GameObject go = FindOrCreate(parent, "SkinFrame");
        go.transform.SetAsFirstSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.raycastTarget = false;
        SetImage(go, frame, Color.white, 3);
    }

    private static void PlaceRect(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    private static void PlaceLabel(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, float x, float y, float w, float h)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, y, w, h);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static void PlaceIcon(Transform parent, string name, string sprite, float x, float y, float w, float h)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, y, w, h);

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.sprite = FixWindowBuilder.Sprites[sprite];
        image.raycastTarget = false;
    }

    private static Color FromHex(string rgb)
    {
        ColorUtility.TryParseHtmlString($"#{rgb}", out Color color);
        return color;
    }
}
