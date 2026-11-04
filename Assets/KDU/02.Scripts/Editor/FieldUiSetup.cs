using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UiBuildKit;

// 열린 필드 씬에 인벤토리 연결 복구, 로그 캔버스, 스킬 창, 창 매니저를 한 번에 적용한다. 다시 실행해도 된다
public static class FieldUiSetup
{
    private const string LogCanvasName = "Canvas_Log";
    private const string LogPrefabPath = "Assets/KDU/04.Prefab/Canvas/Canvas_Log.prefab";
    private const string EquipmentPanelName = "Equipment_background";
    private const string BridgeType = "FieldInputBridge";
    private const string AnchorName = "StoragePanel";

    [MenuItem("KDU/UI/현재 씬에 필드 UI 전부 적용")]
    private static void ApplyAll()
    {
        if (!SetupLogCanvas(out GameObject log))
            return;

        RepairInventoryLinks(log);
        SkillWindowBuilder.BuildWindow();
        ExclusiveWindowBuilder.Build();
        Debug.Log("필드 UI 적용 완료. 씬을 저장하세요.");
    }

    //================================ 로그 ================================
    private static bool SetupLogCanvas(out GameObject log)
    {
        log = null;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"에셋을 찾지 못했다: {LogPrefabPath}");
            return false;
        }

        RemoveRaycaster();

        Scene scene = SceneManager.GetActiveScene();
        log = FindRoot(scene, LogCanvasName);
        if (log == null)
        {
            log = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(log, UndoName);
        }

        BindScrapMessages(log);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    // 로그는 클릭을 받지 않는다. 프리팹에서 지워야 모든 씬에 적용된다
    private static void RemoveRaycaster()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(LogPrefabPath);
        try
        {
            GraphicRaycaster raycaster = contents.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                return;

            Object.DestroyImmediate(raycaster);
            PrefabUtility.SaveAsPrefabAsset(contents, LogPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // 고철 수집이 쓰는 문구·안내를 로그 캔버스로 다시 연결한다
    private static void BindScrapMessages(GameObject log)
    {
        ScrapInteractor interactor = Find<ScrapInteractor>();
        if (interactor != null)
        {
            var so = new SerializedObject(interactor);
            Set(so, "_message", log.GetComponentInChildren<ScrapMessageLog>(true));
            so.ApplyModifiedProperties();
        }

        ScrapInteractPrompt prompt = Find<ScrapInteractPrompt>();
        Transform promptRoot = log.transform.Find("InteractPrompt");
        if (prompt != null && promptRoot != null)
        {
            var so = new SerializedObject(prompt);
            Set(so, "_root", promptRoot.gameObject);
            Set(so, "_label", promptRoot.GetComponentInChildren<TMP_Text>(true));
            so.ApplyModifiedProperties();
        }
    }

    //================================ 인벤토리 연결 복구 ================================
    // Canvas_Inventory 프리팹 ID가 바뀌면 씬의 참조가 끊긴다. 이름으로 찾아 다시 잇는다
    private static void RepairInventoryLinks(GameObject log)
    {
        InventoryToggle toggle = Find<InventoryToggle>();
        Transform panel = toggle != null ? toggle.transform.Find("InventoryPanel") : null;
        if (panel == null)
        {
            Debug.LogWarning("Canvas_Inventory/InventoryPanel을 찾지 못해 인벤토리 연결을 복구하지 않았다.");
            return;
        }

        InventoryView view = Find<InventoryView>();
        RepairInventoryView(view, panel);
        RepairPanelViews(panel, view);

        CraftingUIManager crafting = Find<CraftingUIManager>();
        GameObject craftingWindow = null;
        if (crafting != null)
        {
            var so = new SerializedObject(crafting);
            craftingWindow = so.FindProperty("craftingWindow").objectReferenceValue as GameObject;
            Set(so, "inventoryRoot", panel);
            so.ApplyModifiedProperties();
        }

        RepairToggle(toggle, crafting, craftingWindow);
        RepairTooltips(toggle.transform, panel);
        RepairMinigames();
        RepairInputBridge(toggle, log);
    }

    private static void RepairInventoryView(InventoryView view, Transform panel)
    {
        if (view == null)
            return;

        var so = new SerializedObject(view);
        Set(so, "_gridRoot", panel.Find("GridRoot"));
        Set(so, "_partsLayer", panel.Find("PartsLayer"));
        Set(so, "_componentLayer", panel.Find("ComponentLayer"));
        so.ApplyModifiedProperties();
    }

    // 드래그·툴팁은 씬의 InventoryView를 본다
    private static void RepairPanelViews(Transform panel, InventoryView view)
    {
        foreach (MonoBehaviour behaviour in new MonoBehaviour[] { panel.GetComponent<InventoryDragController>(), panel.GetComponent<InventoryTooltip>() })
        {
            if (behaviour == null)
                continue;

            var so = new SerializedObject(behaviour);
            Set(so, "_view", view);
            so.ApplyModifiedProperties();
        }
    }

    // 0번 = Tab(장비창), 1번 = 제작창
    private static void RepairToggle(InventoryToggle toggle, CraftingUIManager crafting, GameObject craftingWindow)
    {
        EquipmentView equipment = Find<EquipmentView>();
        Transform equipmentPanel = equipment != null ? equipment.transform.Find(EquipmentPanelName) : null;

        var so = new SerializedObject(toggle);
        SerializedProperty modes = so.FindProperty("_modes");
        if (modes.arraySize > 0 && equipmentPanel != null)
            SetPanel(modes.GetArrayElementAtIndex(0), equipmentPanel.gameObject);

        if (modes.arraySize > 1 && craftingWindow != null)
        {
            SerializedProperty mode = modes.GetArrayElementAtIndex(1);
            SetPanel(mode, craftingWindow);
            mode.FindPropertyRelative("RootAnchor").objectReferenceValue = FindAnchor(craftingWindow);

            SerializedProperty calls = mode.FindPropertyRelative("OnClose.m_PersistentCalls.m_Calls");
            if (calls.arraySize > 0)
                calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Target").objectReferenceValue = crafting;
        }

        so.ApplyModifiedProperties();
    }

    private static void SetPanel(SerializedProperty mode, GameObject panel)
    {
        SerializedProperty panels = mode.FindPropertyRelative("Panels");
        if (panels.arraySize == 0)
            panels.arraySize = 1;

        panels.GetArrayElementAtIndex(0).objectReferenceValue = panel;
    }

    // 제작창에서 인벤토리를 맞춰 넣는 칸
    private static RectTransform FindAnchor(GameObject craftingWindow)
    {
        foreach (RectTransform rect in craftingWindow.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == AnchorName)
                return rect;
        }

        Debug.LogWarning($"제작창에서 {AnchorName}을 찾지 못했다.");
        return null;
    }

    private static void RepairTooltips(Transform inventoryCanvas, Transform panel)
    {
        Transform box = inventoryCanvas.Find("Tooltip");
        TextMeshProUGUI text = box != null ? box.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        foreach (TooltipController tooltip in Object.FindObjectsByType<TooltipController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(tooltip);
            Set(so, "toolTipBox", box != null ? box.gameObject : null);
            Set(so, "tmp", text);
            so.ApplyModifiedProperties();
        }

        foreach (EquipmentSlotView slot in Object.FindObjectsByType<EquipmentSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(slot);
            Set(so, "_drag", panel.GetComponent<InventoryDragController>());
            Set(so, "_tooltip", panel.GetComponent<InventoryTooltip>());
            so.ApplyModifiedProperties();
        }
    }

    private static void RepairMinigames()
    {
        ScrapMinigameRouter router = Find<ScrapMinigameRouter>();
        if (router == null)
            return;

        ScrapMinigameBase[] minigames = Object.FindObjectsByType<ScrapMinigameBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var so = new SerializedObject(router);
        SerializedProperty list = so.FindProperty("_minigames");
        list.arraySize = minigames.Length;
        for (int i = 0; i < minigames.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = minigames[i];
        }

        so.ApplyModifiedProperties();
    }

    // 대화 중 숨기고 끄는 목록에서 끊긴 칸을 채운다
    private static void RepairInputBridge(InventoryToggle toggle, GameObject log)
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name != BridgeType)
                continue;

            var so = new SerializedObject(behaviour);
            Transform prompt = log.transform.Find("InteractPrompt");
            if (prompt != null)
                AddUnique(so.FindProperty("hiddenPrompts"), prompt.gameObject);

            AddUnique(so.FindProperty("uiHotkeys"), toggle);
            so.ApplyModifiedProperties();
            return;
        }
    }

    // 비어 있는 칸을 먼저 쓰고, 없으면 끝에 붙인다
    private static void AddUnique(SerializedProperty list, Object value)
    {
        var empty = new List<int>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object current = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (current == value)
                return;

            if (current == null)
                empty.Add(i);
        }

        int index = empty.Count > 0 ? empty[0] : list.arraySize++;
        list.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }

    //================================ 공용 ================================
    private static T Find<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"{so.targetObject.GetType().Name}: {field} 필드가 없다.");
            return;
        }

        property.objectReferenceValue = value;
    }
}
