using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 씬의 경매장 위탁 창(Shop)에 ConsignmentView를 붙이고 UI를 연결한다. 다시 실행해도 된다
public static class ConsignmentBuilder
{
    private const string DefinitionPath = "Assets/KDU/03.SOData/04.Consignment/ConsignmentDefinition.asset";
    private const string Base = "BG/BGImage/";
    private const float RowHeight = 64f;

    [MenuItem("KDU/위탁/위탁 창 연결")]
    private static void Build()
    {
        string missing = Load();
        if (missing != null)
        {
            Debug.LogError($"에셋을 찾지 못했다: {missing}");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Transform shop = FindShop(scene);
        if (shop == null)
        {
            Debug.LogError("Shop 오브젝트를 찾지 못했다.");
            return;
        }

        Transform content = Need(shop, "MidleBlockLeft/Scroll View/Viewport/Content");
        if (content == null)
            return;

        SetupContent(content.gameObject);
        ConsignmentRow template = BuildRow(content);

        ConsignmentView view = GetOrAdd<ConsignmentView>(shop.gameObject);
        var so = new SerializedObject(view);
        so.FindProperty("_definition").objectReferenceValue = LoadOrCreateDefinition();
        so.FindProperty("_rowRoot").objectReferenceValue = content;
        so.FindProperty("_rowTemplate").objectReferenceValue = template;

        BindText(so, "_sellableLabel", shop, "MidleBlockLeft/Buttons/Tiltle (1)");
        BindButton(so, "_allButton", shop, "MidleBlockLeft/Buttons/All");
        BindButton(so, "_commonButton", shop, "MidleBlockLeft/Buttons/Normal");
        BindButton(so, "_rareButton", shop, "MidleBlockLeft/Buttons/Rare");
        BindButton(so, "_epicButton", shop, "MidleBlockLeft/Buttons/Epic");

        Transform image = Need(shop, "MidleBlockMid/AboutParts/PartImage");
        so.FindProperty("_partImage").objectReferenceValue = image != null ? image.GetComponent<Image>() : null;
        BindText(so, "_gradeLabel", shop, "MidleBlockMid/AboutParts/partsmallBG/PartGrade");
        BindText(so, "_slotLabel", shop, "MidleBlockMid/AboutParts/partsmallBG (2)/WhichPart");
        BindText(so, "_nameLabel", shop, "MidleBlockMid/AboutParts/PartName");
        BindText(so, "_durabilityLabel", shop, "MidleBlockMid/AboutParts/Negudo");
        BindText(so, "_baseCostLabel", shop, "MidleBlockMid/AboutParts/BaseCost");
        BindText(so, "_priceLabel", shop, "MidleBlockMid/PredictCost/Gold");
        BindButton(so, "_registerButton", shop, "MidleBlockMid/RegistButton");

        BindSlots(so, shop);
        BindText(so, "_countLabel", shop, "MidleBlockRight/HowManyPart");
        BindText(so, "_totalLabel", shop, "MidleBlockRight/Gold");
        BindText(so, "_goldLabel", shop, "UpBar/Gold");

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("위탁 창 연결 완료.", view);
    }

    private static Transform FindShop(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Shop" && t.Find(Base + "MidleBlockLeft") != null)
                    return t;
            }
        }

        return null;
    }

    private static Transform Need(Transform shop, string path)
    {
        Transform found = shop.Find(Base + path);
        if (found == null)
            Debug.LogWarning($"경로를 찾지 못했다: {path}");

        return found;
    }

    private static void BindText(SerializedObject so, string field, Transform shop, string path)
    {
        Transform t = Need(shop, path);
        so.FindProperty(field).objectReferenceValue = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static void BindButton(SerializedObject so, string field, Transform shop, string path)
    {
        Transform t = Need(shop, path);
        so.FindProperty(field).objectReferenceValue = t != null ? t.GetComponent<Button>() : null;
    }

    private static void BindSlots(SerializedObject so, Transform shop)
    {
        SerializedProperty slots = so.FindProperty("_slots");
        slots.arraySize = 7;

        for (int i = 0; i < slots.arraySize; i++)
        {
            Transform slot = Need(shop, $"MidleBlockRight/Slot/EmptySlot {i + 1}");
            SerializedProperty element = slots.GetArrayElementAtIndex(i);
            Transform label = slot != null ? slot.Find("Text (TMP)") : null;
            Transform cancel = slot != null ? slot.Find("CancleButton") : null;

            element.FindPropertyRelative("Label").objectReferenceValue = label != null ? label.GetComponent<TextMeshProUGUI>() : null;
            element.FindPropertyRelative("Cancel").objectReferenceValue = cancel != null ? cancel.GetComponent<Button>() : null;
        }
    }

    // 줄을 위에서부터 쌓고 스크롤 높이를 맞춘다
    private static void SetupContent(GameObject content)
    {
        var layout = GetOrAdd<VerticalLayoutGroup>(content);
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = GetOrAdd<ContentSizeFitter>(content);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    private static ConsignmentRow BuildRow(Transform content)
    {
        GameObject row = FindOrCreate(content, "RowTemplate");
        row.transform.SetAsFirstSibling();
        GetOrAdd<LayoutElement>(row).preferredHeight = RowHeight;

        Image background = GetOrAdd<Image>(row);
        background.sprite = null;
        background.color = new Color(1f, 1f, 1f, 0.06f);

        Button button = GetOrAdd<Button>(row);
        button.targetGraphic = background;

        TextMeshProUGUI name = RowLabel(row.transform, "Name", 22f, Bone, 8f, 30f);
        TextMeshProUGUI info = RowLabel(row.transform, "Info", 16f, Neutral, 38f, 22f);

        ConsignmentRow view = GetOrAdd<ConsignmentRow>(row);
        var so = new SerializedObject(view);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_background").objectReferenceValue = background;
        so.FindProperty("_nameLabel").objectReferenceValue = name;
        so.FindProperty("_infoLabel").objectReferenceValue = info;
        so.ApplyModifiedProperties();

        row.SetActive(false);
        return view;
    }

    // 가로는 줄 폭에 맞춰 늘어난다
    private static TextMeshProUGUI RowLabel(Transform row, string name, float size, Color color, float top, float height)
    {
        TextMeshProUGUI text = PlaceLabel(row, name, name, size, color, TextAlignmentOptions.Left, 0f, top, 0f, height);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(14f, -top - height);
        rect.offsetMax = new Vector2(-14f, -top);
        return text;
    }

    private static ConsignmentDefinition LoadOrCreateDefinition()
    {
        var definition = AssetDatabase.LoadAssetAtPath<ConsignmentDefinition>(DefinitionPath);
        if (definition != null)
            return definition;

        string folder = System.IO.Path.GetDirectoryName(DefinitionPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/KDU/03.SOData", "04.Consignment");

        definition = ScriptableObject.CreateInstance<ConsignmentDefinition>();
        AssetDatabase.CreateAsset(definition, DefinitionPath);
        AssetDatabase.SaveAssets();
        return definition;
    }
}
