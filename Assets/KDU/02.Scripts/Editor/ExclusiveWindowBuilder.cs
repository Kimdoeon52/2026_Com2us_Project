using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static UiBuildKit;

// 열린 씬에 있는 창을 찾아 ExclusiveWindowManager에 등록한다. 목록을 새로 만들므로 다시 실행해도 된다
public static class ExclusiveWindowBuilder
{
    private const string ObjectName = "ExclusiveWindowManager";

    [MenuItem("KDU/UI/창 매니저 연결")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject go = FindRoot(scene, ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        ExclusiveWindowManager manager = GetOrAdd<ExclusiveWindowManager>(go);
        Undo.RecordObject(manager, UndoName);

        List<ExclusiveWindowManager.Entry> entries = manager.EditorEntries;
        entries.Clear();

        GameObject grid = AddInventory(entries);
        AddCrafting(entries, grid);
        AddSkillWindow(entries);
        AddTimeWindows(entries);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);

        var names = new StringBuilder();
        foreach (ExclusiveWindowManager.Entry entry in entries)
        {
            names.Append(entry.Name).Append(' ');
        }

        Debug.Log($"창 매니저 연결 완료. {entries.Count}개: {names}씬을 저장하세요.", manager);
    }

    // 그리드는 제작창과 함께 쓰므로 장비 패널을 인벤토리 창의 기준으로 본다. 반환: 그리드
    private static GameObject AddInventory(List<ExclusiveWindowManager.Entry> entries)
    {
        InventoryToggle toggle = FindInScene<InventoryToggle>();
        if (toggle == null)
            return null;

        var so = new SerializedObject(toggle);
        GameObject grid = so.FindProperty("_inventoryRoot").objectReferenceValue as GameObject;

        SerializedProperty modes = so.FindProperty("_modes");
        if (modes.arraySize > 0)
        {
            SerializedProperty panels = modes.GetArrayElementAtIndex(0).FindPropertyRelative("Panels");
            GameObject equipment = panels.arraySize > 0 ? panels.GetArrayElementAtIndex(0).objectReferenceValue as GameObject : null;
            Add(entries, "인벤토리", equipment, grid, toggle.CloseAll);
        }

        return grid;
    }

    private static void AddCrafting(List<ExclusiveWindowManager.Entry> entries, GameObject grid)
    {
        CraftingUIManager crafting = FindInScene<CraftingUIManager>();
        if (crafting == null)
            return;

        var so = new SerializedObject(crafting);
        Add(entries, "제작", Field(so, "craftingWindow"), grid, crafting.CloseCraftingWindow);
        Add(entries, "상점", Field(so, "shopWindow"), null, crafting.CloseShopWindow);
        Add(entries, "수리", Field(so, "fixWindow"), null, crafting.CloseFixWindow);
    }

    private static void AddSkillWindow(List<ExclusiveWindowManager.Entry> entries)
    {
        SkillWindowView skill = FindInScene<SkillWindowView>();
        if (skill == null)
            return;

        Add(entries, "스킬", Field(new SerializedObject(skill), "_panel"), null, skill.Close);
    }

    // 닫을 때 시간 정지를 풀어야 하는 창. 토글 함수로만 닫는다
    private static void AddTimeWindows(List<ExclusiveWindowManager.Entry> entries)
    {
        IsoMapSystemManager map = FindInScene<IsoMapSystemManager>();
        if (map != null)
            Add(entries, "지도", map.mapCanvas, null, map.ToggleMapUI);

        CalendarUiManager calendar = FindInScene<CalendarUiManager>();
        if (calendar != null)
            Add(entries, "달력", calendar.calendarCanvas, null, calendar.ToggleCalendarUI);

        RankingUiManager ranking = FindInScene<RankingUiManager>();
        if (ranking != null)
            Add(entries, "랭킹", ranking.rankingCanvas, null, ranking.ToggleRankingUI);
    }

    private static void Add(List<ExclusiveWindowManager.Entry> entries, string name, GameObject root, GameObject companion, UnityAction close)
    {
        if (root == null)
        {
            Debug.LogWarning($"[창 매니저] {name}: 창이 연결되어 있지 않아 건너뛴다.");
            return;
        }

        var entry = new ExclusiveWindowManager.Entry { Name = name, Root = root, Companion = companion };
        UnityEventTools.AddPersistentListener(entry.OnForceClose, close);
        entries.Add(entry);
    }

    private static GameObject Field(SerializedObject so, string name)
    {
        return so.FindProperty(name).objectReferenceValue as GameObject;
    }

    private static T FindInScene<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
