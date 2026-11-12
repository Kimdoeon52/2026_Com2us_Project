// RE:AL STEEL - 먼 배경 카드 만들기 (고정 평면 그림)
//
// 메뉴: GameObject → RE_AL STEEL → 먼 배경 카드 →
//  · 도시 세트 (6장, 층층이): 씬 뷰가 보고 있는 지점 뒤쪽으로 변두리 → 건물군 → 랜드마크(A사 타워 · 경기장 돔) → 스카이라인을
//    서로 다른 거리에 세운다. 만든 뒤 한 장씩 옮겨 맞추면 된다 (전부 그 자리에 고정)
//  · 선택한 그림으로 1장: Project 창에서 고른 그림(Texture)으로 카드 한 장. 같은 폴더에 '<이름>_불빛' 그림이 있으면 밤 불빛으로 연결
// 그림은 SYS/Textures/Backdrop. 가져오기 설정은 픽셀용(Point · 무압축)으로 맞춘다
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;
using RealSteel.Lighting;

public static class RSBackdropMenu
{
    struct Item
    {
        public string file; public float width, dx, dz;
        public Item(string f, float w, float x, float z) { file = f; width = w; dx = x; dz = z; }
    }

    // 가로 크기(m) · 좌우 위치 · 뒤로 거리 — 가까운 층부터
    static readonly Item[] CitySet =
    {
        new Item("도시카드_변두리", 100f, 0f, 34f),
        new Item("도시카드_건물군A", 110f, -50f, 46f),
        new Item("도시카드_건물군B", 110f, 55f, 48f),
        new Item("도시카드_경기장돔", 80f, 40f, 58f),
        new Item("도시카드_A사타워", 26f, -30f, 62f),
        new Item("도시카드_스카이라인", 260f, 0f, 70f),
    };

    static Camera MainCam()
    {
        Camera cam = null;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.CompareTag("MainCamera") && (cam == null || c.depth > cam.depth)) cam = c;
        return cam;
    }

    // 기준점 = 씬 뷰가 보고 있는 지점, 방향 = 메인 카메라가 보는 쪽 (수평 성분)
    static void Frame(out Vector3 anchor, out Quaternion yaw, out Camera cam)
    {
        cam = MainCam();
        var sv = SceneView.lastActiveSceneView;
        anchor = sv != null ? sv.pivot : Vector3.zero;
        Vector3 f = cam != null ? cam.transform.forward : Vector3.forward;
        f.y = 0f;
        yaw = f.sqrMagnitude > 0.001f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
    }

    static RSBackdrop MakeCard(Texture2D image, float width, Vector3 pos, Quaternion rot, Transform parent, Camera cam)
    {
        RSSurfaceTools.MakePixelTexture(image, false);
        string path = AssetDatabase.GetAssetPath(image);
        string lightsPath = path.Substring(0, path.Length - System.IO.Path.GetExtension(path).Length) + "_불빛" + System.IO.Path.GetExtension(path);
        var lights = AssetDatabase.LoadAssetAtPath<Texture2D>(lightsPath);
        if (lights != null) RSSurfaceTools.MakePixelTexture(lights, false);

        var go = new GameObject("CARD_" + image.name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = rot;
        var b = go.AddComponent<RSBackdrop>();
        b.image = image;
        b.lights = lights;
        b.width = width;
        b.cam = cam;
        return b;
    }

    [MenuItem("GameObject/RE_AL STEEL/먼 배경 카드/도시 세트 (6장, 층층이)", false, 15)]
    static void CreateCitySet()
    {
        string folder = RSPaths.Textures + "/Backdrop";
        AssetDatabase.Refresh();
        Vector3 anchor; Quaternion yaw; Camera cam;
        Frame(out anchor, out yaw, out cam);

        var root = new GameObject("BACKDROP_도시 카드 (고정 평면 · 한 장씩 옮겨 맞춘다)");
        Undo.RegisterCreatedObjectUndo(root, "먼 배경 카드");
        root.transform.position = anchor;
        int made = 0;
        float farthest = 0f;
        foreach (var it in CitySet)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + it.file + ".png");
            if (tex == null) continue;
            Vector3 pos = anchor + yaw * new Vector3(it.dx, 0f, it.dz);
            MakeCard(tex, it.width, pos, yaw, root.transform, cam);
            if (cam != null) farthest = Mathf.Max(farthest, Vector3.Distance(cam.transform.position, pos));
            made++;
        }
        if (made == 0)
        {
            Object.DestroyImmediate(root);
            EditorUtility.DisplayDialog("먼 배경 카드", folder + " 에서 도시카드_ 그림을 찾지 못했습니다.", "확인");
            return;
        }
        Selection.activeGameObject = root;
        string warn = cam != null && farthest > cam.farClipPlane
            ? " ⚠ 가장 먼 카드가 카메라에서 " + farthest.ToString("0") + " m 인데 카메라 Far 는 " + cam.farClipPlane.ToString("0") + " m 입니다 — 카드를 당기거나 Far 를 늘려야 보입니다."
            : "";
        Debug.Log("[먼 배경 카드] 도시 카드 " + made + "장을 세웠습니다. 전부 그 자리에 고정이니 한 장씩 옮겨 맞추세요." + warn, root);
    }

    [MenuItem("GameObject/RE_AL STEEL/먼 배경 카드/선택한 그림으로 1장", false, 16)]
    static void CreateFromSelection()
    {
        var tex = Selection.activeObject as Texture2D;
        if (tex == null) { EditorUtility.DisplayDialog("먼 배경 카드", "Project 창에서 그림(Texture)을 하나 고른 뒤 누르세요.", "확인"); return; }
        Vector3 anchor; Quaternion yaw; Camera cam;
        Frame(out anchor, out yaw, out cam);
        var b = MakeCard(tex, Mathf.Clamp(tex.width / 16f, 4f, 300f), anchor, yaw, null, cam);   // 처음 크기: 16 픽셀 = 1 m
        Undo.RegisterCreatedObjectUndo(b.gameObject, "먼 배경 카드");
        Selection.activeGameObject = b.gameObject;
    }
}
