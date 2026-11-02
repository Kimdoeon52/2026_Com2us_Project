using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 빌더 공용. SYS/UI 스프라이트 로드와 좌상단 기준 배치
public static class UiBuildKit
{
    public const string UndoName = "KDU UI 빌드";
    private const string UiRoot = "Assets/SYS/UI/";
    private const string FontPath = "Assets/KDU/05.Font/NeoDunggeunmoPro-Regular SDF.asset";

    public static readonly Color Yellow = FromHex("FFE800");
    public static readonly Color Orange = FromHex("FF8A1E");
    public static readonly Color Red = FromHex("FF4B3A");
    public static readonly Color Cyan = FromHex("35D0DE");
    public static readonly Color Bone = FromHex("EEEDE8");
    public static readonly Color Neutral = FromHex("B4B9C0");
    public static readonly Color Ink = FromHex("1A1B1D");
    public static readonly Color Dim = FromHex("5A5D62");
    public static readonly Color Ghost = new Color(0.71f, 0.73f, 0.75f, 0.3f);

    private static readonly Dictionary<string, string> SpritePaths = new Dictionary<string, string>
    {
        { "bg_panelLarge", "BG/Panel/bg_panelLarge.png" },
        { "bg_panelSmall", "BG/Panel/bg_panelSmall.png" },
        { "bg_panelWide_chamfer", "BG/Panel/bg_panelWide_chamfer.png" },
        { "swatch_v2", "BG/Swatch/swatch_v2.png" },
        { "swatch_v3", "BG/Swatch/swatch_v3.png" },
        { "bg_selectItem_enabled", "BG/Button/selectItem/bg_selectItem_enabled.png" },
        { "bg_button_primary_enabled", "BG/Button/button/bg_button_primary_enabled.png" },
        { "bg_picker_populated", "BG/Button/picker/bg_picker_populated.png" },
        { "bg_statusButton_warning_enabled", "BG/Button/statusButton/bg_statusButton_warning_enabled.png" },
        { "bg_iconButton_v1_sm_enabled", "BG/Button/iconButton/bg_iconButton_v1_sm_enabled.png" },
        { "frame_panelLarge_v3", "Frame/Panel/panelLarge/frame_panelLarge_v3.png" },
        { "frame_panelMedium_v4", "Frame/Panel/panelMedium/frame_panelMedium_v4.png" },
        { "frame_panelMedium_v8", "Frame/Panel/panelMedium/frame_panelMedium_v8.png" },
    };

    public static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    public static TMP_FontAsset Font { get; private set; }

    // 실패하면 못 찾은 경로
    public static string Load()
    {
        Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (Font == null)
            return FontPath;

        Sprites.Clear();
        foreach (KeyValuePair<string, string> pair in SpritePaths)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiRoot + pair.Value);
            if (sprite == null)
                return UiRoot + pair.Value;

            Sprites[pair.Key] = sprite;
        }

        return null;
    }

    public static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    // 다시 실행해도 쌓이지 않도록 이름으로 찾아 재사용한다
    public static GameObject FindOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
            return found.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    // 맨 앞 자식으로 깔아 내용을 가리지 않는다
    public static void Overlay(Transform parent, string frame, float multiplier)
    {
        GameObject go = FindOrCreate(parent, "SkinFrame");
        go.transform.SetAsFirstSibling();
        Stretch(go.GetComponent<RectTransform>());
        SetImage(go, frame, Color.white, multiplier);
        go.GetComponent<Image>().raycastTarget = false;
    }

    public static Image SetImage(GameObject go, string sprite, Color color, float multiplier)
    {
        Image image = GetOrAdd<Image>(go);
        image.sprite = Sprites[sprite];
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = color;
        return image;
    }

    public static GameObject PlacePanel(Transform parent, string name, string sprite, Color color, float multiplier, float x, float y, float w, float h)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, y, w, h);
        SetImage(go, sprite, color, multiplier).raycastTarget = false;
        return go;
    }

    public static GameObject PlaceBox(Transform parent, string name, Color color, float x, float y, float w, float h)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, y, w, h);

        Image image = GetOrAdd<Image>(go);
        image.sprite = null;
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    // 45도 돌린 사각형. 폰트에 없는 ◆ 대신 쓴다
    public static GameObject PlaceDiamond(Transform parent, string name, Color color, float x, float y, float size)
    {
        GameObject go = PlaceBox(parent, name, color, x, y, size, size);
        go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 45f);
        return go;
    }

    public static TextMeshProUGUI PlaceLabel(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, float x, float y, float w, float h)
    {
        GameObject go = FindOrCreate(parent, name);
        PlaceRect(go, x, y, w, h);

        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(go);
        text.font = Font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    // 부모 좌상단 기준 배치
    public static void PlaceRect(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void ApplyFont(GameObject root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = Font;
        }
    }

    public static Color FromHex(string rgb)
    {
        ColorUtility.TryParseHtmlString($"#{rgb}", out Color color);
        return color;
    }
}
