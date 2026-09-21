using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 미니게임 오버레이 UI 프리팹을 만들고 참조까지 연결한다.
// 손으로 YAML을 쓰면 참조가 어긋나기 쉬워 에디터에서 생성한다
public static class ScrapMinigameUIBuilder
{
    private const string ParentFolder = "Assets/KDU/04.Prefab";
    private const string FolderName = "Minigame";
    private const string Folder = ParentFolder + "/" + FolderName;

    private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color PanelColor = new Color(0.16f, 0.15f, 0.14f, 1f);
    private static readonly Color TrackColor = new Color(0.28f, 0.27f, 0.25f, 1f);
    private static readonly Color ZoneColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    private static readonly Color CursorColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color DebrisColor = new Color(0.45f, 0.42f, 0.38f, 1f);

    [MenuItem("KDU/고물상/미니게임 UI 프리팹 생성")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder(ParentFolder, FolderName);

        DraggableDebris piece = BuildDebrisPiece();
        BuildTimingBar();
        BuildScratchCard();
        BuildDebrisClear(piece);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"미니게임 UI 프리팹 4개를 {Folder} 에 만들었다.");
    }

    // ---------- 개별 프리팹 ----------

    private static DraggableDebris BuildDebrisPiece()
    {
        GameObject root = NewUI("DebrisPiece", null);
        Center(Rect(root), new Vector2(96f, 96f), Vector2.zero);
        AddImage(root, DebrisColor, true);
        root.AddComponent<DraggableDebris>();

        GameObject saved = Save(root, "DebrisPiece");
        return saved.GetComponent<DraggableDebris>();
    }

    private static void BuildTimingBar()
    {
        GameObject root = NewUI("MinigameTimingBar", null);
        Stretch(Rect(root));
        var game = root.AddComponent<TimingBarMinigame>();

        GameObject overlay = BuildOverlay(root, new Vector2(640f, 360f), "SPACE 로 멈춰라", out GameObject panel, out TextMeshProUGUI timer);

        GameObject track = NewUI("Track", panel.transform);
        Center(Rect(track), new Vector2(520f, 44f), Vector2.zero);
        AddImage(track, TrackColor, false);

        GameObject zone = NewUI("Zone", track.transform);
        Center(Rect(zone), new Vector2(120f, 44f), Vector2.zero);
        AddImage(zone, ZoneColor, false);

        GameObject cursor = NewUI("Cursor", track.transform);
        Center(Rect(cursor), new Vector2(8f, 60f), Vector2.zero);
        AddImage(cursor, CursorColor, false);

        var so = new SerializedObject(game);
        so.FindProperty("_overlayRoot").objectReferenceValue = overlay;
        so.FindProperty("_timerLabel").objectReferenceValue = timer;
        so.FindProperty("_trackRect").objectReferenceValue = Rect(track);
        so.FindProperty("_zoneRect").objectReferenceValue = Rect(zone);
        so.FindProperty("_cursorRect").objectReferenceValue = Rect(cursor);
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "MinigameTimingBar");
    }

    private static void BuildScratchCard()
    {
        GameObject root = NewUI("MinigameScratchCard", null);
        Stretch(Rect(root));
        var game = root.AddComponent<ScratchCardMinigame>();

        GameObject overlay = BuildOverlay(root, new Vector2(480f, 480f), "드래그해서 긁어라", out GameObject panel, out TextMeshProUGUI timer);

        // 긁으면 드러나는 밑그림. 색은 판마다 바뀐다
        GameObject reward = NewUI("Reward", panel.transform);
        Center(Rect(reward), new Vector2(320f, 320f), Vector2.zero);
        Image rewardImage = AddImage(reward, new Color(0.85f, 0.66f, 0.28f, 1f), false);

        GameObject surface = NewUI("Surface", panel.transform);
        Center(Rect(surface), new Vector2(320f, 320f), Vector2.zero);
        var raw = surface.AddComponent<RawImage>();
        raw.raycastTarget = true;
        var scratch = surface.AddComponent<ScratchSurface>();

        var so = new SerializedObject(game);
        so.FindProperty("_overlayRoot").objectReferenceValue = overlay;
        so.FindProperty("_timerLabel").objectReferenceValue = timer;
        so.FindProperty("_surface").objectReferenceValue = scratch;
        so.FindProperty("_rewardImage").objectReferenceValue = rewardImage;
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "MinigameScratchCard");
    }

    private static void BuildDebrisClear(DraggableDebris piecePrefab)
    {
        GameObject root = NewUI("MinigameDebrisClear", null);
        Stretch(Rect(root));
        var game = root.AddComponent<DebrisClearMinigame>();

        GameObject overlay = BuildOverlay(root, new Vector2(720f, 480f), "창 밖으로 끌어내라", out GameObject panel, out TextMeshProUGUI timer);

        // 고물에 덮인 부품. 실제 아트로 교체한다
        GameObject buried = NewUI("Buried", panel.transform);
        Center(Rect(buried), new Vector2(200f, 200f), Vector2.zero);
        AddImage(buried, new Color(0.8f, 0.75f, 0.35f, 1f), false);

        GameObject spawnArea = NewUI("SpawnArea", panel.transform);
        Stretch(Rect(spawnArea));

        var so = new SerializedObject(game);
        so.FindProperty("_overlayRoot").objectReferenceValue = overlay;
        so.FindProperty("_timerLabel").objectReferenceValue = timer;
        so.FindProperty("_boundsRect").objectReferenceValue = Rect(panel);
        so.FindProperty("_spawnArea").objectReferenceValue = Rect(spawnArea);
        so.FindProperty("_debrisPrefab").objectReferenceValue = piecePrefab;
        so.FindProperty("_timeLimit").floatValue = 60f;
        so.ApplyModifiedPropertiesWithoutUndo();

        Save(root, "MinigameDebrisClear");
    }

    // ---------- 공통 조립 ----------

    // 배경 + 패널 + 상단 타이머 + 하단 안내. 오버레이는 꺼둔 채 저장한다
    private static GameObject BuildOverlay(GameObject root, Vector2 panelSize, string hint, out GameObject panel, out TextMeshProUGUI timer)
    {
        GameObject overlay = NewUI("Overlay", root.transform);
        Stretch(Rect(overlay));

        GameObject backdrop = NewUI("Backdrop", overlay.transform);
        Stretch(Rect(backdrop));
        AddImage(backdrop, Backdrop, true);

        panel = NewUI("Panel", overlay.transform);
        Center(Rect(panel), panelSize, Vector2.zero);
        AddImage(panel, PanelColor, true);

        GameObject timerGo = NewUI("TimerLabel", overlay.transform);
        TopCenter(Rect(timerGo), new Vector2(260f, 64f), -24f);
        timer = AddText(timerGo, "00.00", 44f);

        GameObject hintGo = NewUI("Hint", panel.transform);
        BottomCenter(Rect(hintGo), new Vector2(panelSize.x - 40f, 48f), 16f);
        AddText(hintGo, hint, 24f);

        overlay.SetActive(false);
        return overlay;
    }

    // ---------- 유틸 ----------

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        if (parent != null)
            go.transform.SetParent(parent, false);

        return go;
    }

    private static RectTransform Rect(GameObject go) => (RectTransform)go.transform;

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void TopCenter(RectTransform rect, Vector2 size, float offsetY)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, offsetY);
    }

    private static void BottomCenter(RectTransform rect, Vector2 size, float offsetY)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, offsetY);
    }

    private static Image AddImage(GameObject go, Color color, bool raycastTarget)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI AddText(GameObject go, string text, float fontSize)
    {
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject Save(GameObject instance, string fileName)
    {
        string path = $"{Folder}/{fileName}.prefab";
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return asset;
    }
}
