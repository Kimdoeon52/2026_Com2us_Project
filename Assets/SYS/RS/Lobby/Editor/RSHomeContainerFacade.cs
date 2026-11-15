// RE:AL STEEL - 고물상 주인공 집(빨간 컨테이너 2개) 바깥에 문 · 창 붙이기
//
// 메뉴: Tools → RE_AL STEEL → Stage → 고물상 집 컨테이너에 문 · 창 붙이기
//  · 대상: 씬에 3D_Models/Cont.obj 로 놓은 것 전부 (지금은 Cont · Cont (1)). 하나도 없으면 고른 오브젝트
//  · 집 안 씬(Home_Workshop)과 비슷한 자리:
//      문 = 화면 왼쪽 컨테이너의 앞(카메라 쪽) 끝면 가운데   (집 안: 왼벽 앞 칸의 출입문)
//      창 3개 = 화면 오른쪽 컨테이너의 바깥 긴 벽, 집 안 뒷벽 창과 같은 비율 (앞에서 12.5% · 46% · 79%)
//    집 안 그대로 돌리면 창이 카메라 반대쪽(서쪽) 벽으로 가서 안 보이므로, 보이는 동쪽 벽에 붙였다
//  · 문 위 차양 · 벽등 · 문 앞 디딤돌 포함. 밤(시간대 17.8 ~ 6.5시)에는 창 · 문 유리 · 벽등이 켜진다 (RSNightLamp)
//  · 각 컨테이너의 자식 '집_바깥 문 · 창 (자동)' 으로 들어간다 → 컨테이너를 옮기거나 키우면 같이 따라간다
//  · 다시 누르면 그 자식만 지우고 새로 만든다 (컨테이너 자체는 안 건드림). 입구 기능(문 열기 · 씬 이동)은 없음
//  · 도트 밀도는 컨테이너 텍스처와 같다 (끝면 한 변 = 88 px). 그림: Textures/Build/고물상_집 (같은 이름으로 덮어쓰면 바뀜)
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using RealSteel.Common.EditorTools;
using RealSteel.Lighting;
using RealSteel.Terrain;

public static class RSHomeContainerFacade
{
    public const string HolderName = "집_바깥 문 · 창 (자동)";
    const float EndPixels = 88f;                                   // 컨테이너 끝면 한 변의 텍스처 픽셀
    static readonly float[] WindowAt = { 0.125f, 0.458f, 0.792f };  // 집 안 뒷벽 창 자리 (-2.5 · 1.5 · 5.5 / 12 m)

    class Box
    {
        public Transform root;
        public Bounds b;            // 컨테이너 로컬
        public Vector3 longAxis, sideAxis, front;
        public Material src;
        public Vector3 WorldCenter { get { return root.TransformPoint(b.center); } }
        public Vector3 Face(Vector3 n) { return b.center + Vector3.Scale(n, b.extents); }
        public float Extent(Vector3 axis) { return Mathf.Abs(Vector3.Dot(b.size, axis)); }
    }

    static Dictionary<string, Material> mats;
    static string texDir, matDir;

    [MenuItem("Tools/RE_AL STEEL/Stage/고물상 집 컨테이너에 문 · 창 붙이기", false, 9)]
    static void Menu()
    {
        var targets = FindContainers();
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("집 문 · 창", "컨테이너를 못 찾았습니다.\n씬에서 컨테이너(Cont)를 골라 놓고 다시 누르세요.", "확인");
            return;
        }
        Undo.SetCurrentGroupName("집 컨테이너 문 · 창");
        int group = Undo.GetCurrentGroup();
        foreach (var t in targets)
        {
            var old = t.Find(HolderName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }
        if (!LoadAssets(targets[0])) return;

        var boxes = new List<Box>();
        foreach (var t in targets)
        {
            var bx = Measure(t);
            if (bx != null) boxes.Add(bx);
        }
        if (boxes.Count == 0) { Debug.LogWarning("[집 문 · 창] 컨테이너 메시를 못 읽었습니다"); return; }

        // 카메라 기준 앞 · 오른쪽 (없으면 +Z 를 보는 것으로)
        var cam = FindCamera();
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        foreach (var bx in boxes)
        {
            Vector3 e = bx.longAxis;
            bx.front = Vector3.Dot(bx.root.TransformDirection(e), -fwd) >= Vector3.Dot(bx.root.TransformDirection(-e), -fwd) ? e : -e;
        }
        boxes.Sort((a, c) => Vector3.Dot(a.WorldCenter, right).CompareTo(Vector3.Dot(c.WorldCenter, right)));
        var doorBox = boxes[0];
        var winBox = boxes[boxes.Count - 1];

        // 창 벽: 컨테이너가 둘 이상이면 문 컨테이너 반대쪽 바깥 벽, 하나뿐이면 카메라를 더 보는 옆벽
        Vector3 wn = winBox.sideAxis;
        if (winBox != doorBox)
        {
            Vector3 away = winBox.WorldCenter - doorBox.WorldCenter;
            if (Vector3.Dot(winBox.root.TransformDirection(wn), away) < 0f) wn = -wn;
        }
        else if (Vector3.Dot(winBox.root.TransformDirection(wn), -fwd) < 0f) wn = -wn;

        float doorTopWorld;
        var h1 = BuildDoor(doorBox, out doorTopWorld);
        var h2 = winBox == doorBox ? h1 : NewHolder(winBox.root);
        var glows2 = BuildWindows(winBox, wn, h2.transform, doorTopWorld);
        var lamp2 = h2.GetComponent<RSNightLamp>();
        if (lamp2 == null) { lamp2 = h2.AddComponent<RSNightLamp>(); lamp2.lights = new Light[0]; lamp2.intensity = new float[0]; lamp2.glows = new Renderer[0]; lamp2.dayGlow = 0.2f; }
        var all = new List<Renderer>(lamp2.glows); all.AddRange(glows2); lamp2.glows = all.ToArray();

        Undo.RegisterCreatedObjectUndo(h1, "집 문");
        if (h2 != h1) Undo.RegisterCreatedObjectUndo(h2, "집 창");
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = h1;
        Debug.Log("[집 문 · 창] 문 → '" + doorBox.root.name + "' 앞 끝면 · 창 3개 → '" + winBox.root.name + "' 바깥 옆벽" +
                  (boxes.Count > 2 ? " (컨테이너 " + boxes.Count + "개 중 양 끝 두 개만 썼습니다)" : ""), h1);
    }

    // ─────────────────────────────────────────────────────────────
    // 찾기 · 재기
    // ─────────────────────────────────────────────────────────────

    static List<Transform> FindContainers()
    {
        // 1) 씬의 Cont.obj 들 (지금 고른 것과 상관없이) → 2) 없으면 고른 오브젝트
        var list = new List<Transform>();
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                string p = src != null ? AssetDatabase.GetAssetPath(src) : "";
                if (p.EndsWith("/Cont.obj")) list.Add(t);
            }
        if (list.Count > 0) return list;
        foreach (var go in Selection.gameObjects)
        {
            if (!go.scene.IsValid() || go.name == HolderName) continue;
            var t = go.transform;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root != null) t = root.transform;
            if (!list.Contains(t) && t.GetComponentInChildren<MeshFilter>() != null) list.Add(t);
        }
        return list;
    }

    static Box Measure(Transform root)
    {
        var holder = root.Find(HolderName);
        bool any = false;
        var b = new Bounds();
        Material src = null;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || (holder != null && mf.transform.IsChildOf(holder))) continue;
            var mb = mf.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            var mr = mf.GetComponent<MeshRenderer>();
            if (src == null && mr != null) src = mr.sharedMaterial;
        }
        if (!any) return null;
        var bx = new Box { root = root, b = b, src = src };
        bx.longAxis = b.size.z >= b.size.x ? Vector3.forward : Vector3.right;
        bx.sideAxis = b.size.z >= b.size.x ? Vector3.right : Vector3.forward;
        return bx;
    }

    static Camera FindCamera()
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "CAM_Stage") return c;
        return Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
    }

    /// <summary>월드 지면 높이 (지형이 없으면 null)</summary>
    static float? Ground(Vector3 world)
    {
        foreach (var t in RSTerrain.All)
            if (t != null && t.isActiveAndEnabled)
            {
                var l = t.transform.InverseTransformPoint(world);
                if (t.InsideXZ(l.x, l.z)) return t.SampleHeightWorld(world);
            }
        RaycastHit hit;
        if (Physics.Raycast(world + Vector3.up * 20f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // 문 (+ 디딤돌 · 차양 · 벽등)
    // ─────────────────────────────────────────────────────────────

    static GameObject NewHolder(Transform root)
    {
        var h = new GameObject(HolderName);
        h.transform.SetParent(root, false);
        h.transform.localPosition = Vector3.zero;
        h.transform.localRotation = Quaternion.identity;
        h.transform.localScale = Vector3.one;
        return h;
    }

    static GameObject BuildDoor(Box bx, out float doorTopWorld)
    {
        var h = NewHolder(bx.root);
        Vector3 e = bx.front;
        Vector3 t = Vector3.Cross(Vector3.up, -e);                 // 끝면을 밖에서 볼 때 오른쪽
        float u = bx.Extent(bx.sideAxis) / EndPixels;              // 1 px = 컨테이너 로컬 몇 단위
        Vector3 fc = bx.Face(e);

        // 바닥: 문 앞 0.5 단위 지점의 지면 + 디딤돌 6 px (컨테이너 아래 끝 ~ 위 끝 사이로 묶는다)
        float groundY = 0f;
        Vector3 probe = bx.root.TransformPoint(fc + e * (16f * u));
        var g = Ground(probe);
        if (g.HasValue) groundY = bx.root.InverseTransformPoint(new Vector3(probe.x, g.Value, probe.z)).y;
        groundY = Mathf.Clamp(groundY, bx.b.min.y, bx.b.min.y + 24f * u);
        float floorY = groundY + 6f * u;
        float doorTop = floorY + 64f * u;
        float limit = bx.b.max.y - 10f * u;
        if (doorTop > limit) { floorY -= doorTop - limit; doorTop = limit; groundY = floorY - 6f * u; }

        Vector3 doorC = new Vector3(fc.x, floorY + 32f * u, fc.z);
        Quad(h.transform, "문", doorC + e * (0.6f * u), e, 30f * u, 64f * u, M("문"), true);
        var glass = Quad(h.transform, "문 유리 (밤에 켜짐)", doorC + Vector3.up * (20f * u) + e * (0.3f * u), e, 10f * u, 10f * u, M("문창불빛"), false);

        // 디딤돌: 위 = 문 바닥, 아래는 땅속으로 4 px
        var step = Cube(h.transform, "디딤돌", new Vector3(fc.x, floorY - 5f * u, fc.z) + e * (5f * u + 0.6f * u), Quaternion.LookRotation(e, Vector3.up),
            new Vector3(36f * u, 10f * u, 10f * u), M("계단"));
        step.shadowCastingMode = ShadowCastingMode.On;

        // 차양: 문 위 6 px 에서 밖으로 14 px, 12° 숙임 + 받침 2개
        Quaternion ar = Quaternion.LookRotation(e, Vector3.up) * Quaternion.Euler(12f, 0f, 0f);
        Vector3 hinge = new Vector3(fc.x, doorTop + 6f * u, fc.z) + e * (0.6f * u);
        Cube(h.transform, "차양", hinge + ar * new Vector3(0f, 1f * u, 7f * u), ar, new Vector3(40f * u, 2f * u, 14f * u), M("차양"));
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 a = new Vector3(fc.x, doorTop - 4f * u, fc.z) + t * (s * 17f * u) + e * (0.6f * u);
            Vector3 b = hinge + t * (s * 17f * u) + ar * new Vector3(0f, -0.5f * u, 11f * u);
            Vector3 d = b - a;
            Cube(h.transform, "차양 받침", (a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up), new Vector3(1f * u, 1f * u, d.magnitude), M("쇠"));
        }

        // 벽등: 문 오른쪽, 문 위 끝보다 10 px 아래
        Vector3 lampC = new Vector3(fc.x, doorTop - 10f * u, fc.z) + t * (24f * u) + e * (1.5f * u + 0.6f * u);
        Cube(h.transform, "벽등", lampC, Quaternion.LookRotation(e, Vector3.up), new Vector3(4f * u, 6f * u, 3f * u), M("쇠"));
        var bulb = Quad(h.transform, "벽등 불빛 (밤에 켜짐)", lampC + Vector3.down * (0.5f * u) + e * (1.6f * u), e, 2f * u, 3f * u, M("등불빛"), false);

        var lamp = h.AddComponent<RSNightLamp>();
        lamp.lights = new Light[0];
        lamp.intensity = new float[0];
        lamp.glows = new Renderer[] { glass, bulb };
        lamp.dayGlow = 0.2f;

        doorTopWorld = bx.root.TransformPoint(new Vector3(fc.x, doorTop, fc.z)).y;
        return h;
    }

    // ─────────────────────────────────────────────────────────────
    // 창 3개
    // ─────────────────────────────────────────────────────────────

    static List<Renderer> BuildWindows(Box bx, Vector3 n, Transform h, float doorTopWorld)
    {
        var glows = new List<Renderer>();
        float u = bx.Extent(bx.sideAxis) / EndPixels;
        float len = bx.Extent(bx.longAxis);
        Vector3 fc = bx.Face(n);
        // 창 위 끝 = 문 위 끝 (같은 높이 선). 컨테이너 안으로 묶는다
        float top = bx.root.InverseTransformPoint(new Vector3(bx.root.position.x, doorTopWorld, bx.root.position.z)).y;
        top = Mathf.Clamp(top, bx.b.min.y + 40f * u, bx.b.max.y - 8f * u);
        float cy = top - 15f * u;                       // 창 그림 30 px 의 가운데
        for (int i = 0; i < WindowAt.Length; i++)
        {
            Vector3 c = fc + bx.front * (len * 0.5f - WindowAt[i] * len);
            c.y = cy;
            Quad(h, "창 " + (i + 1), c + n * (0.6f * u), n, 34f * u, 30f * u, M("창"), true);
            glows.Add(Quad(h, "창 " + (i + 1) + " 불빛 (밤에 켜짐)", c + Vector3.up * (4f * u) + n * (0.3f * u), n, 30f * u, 18f * u, M("창불빛"), false));
        }
        return glows;
    }

    // ─────────────────────────────────────────────────────────────
    // 조각 만들기
    // ─────────────────────────────────────────────────────────────

    /// <summary>벽에 붙는 판. n = 벽 바깥 방향 (컨테이너 로컬), 판은 n 쪽에서 보인다</summary>
    static Renderer Quad(Transform parent, string name, Vector3 pos, Vector3 n, float w, float hgt, Material m, bool lit)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.LookRotation(-n, Vector3.up);
        g.transform.localScale = new Vector3(w, hgt, 1f);
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = lit;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return r;
    }

    static MeshRenderer Cube(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = size;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.On;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return r;
    }

    // ─────────────────────────────────────────────────────────────
    // 텍스처 · 머티리얼 (Materials/고물상/MAT_집_*)
    // ─────────────────────────────────────────────────────────────

    static Material M(string key) { Material m; return mats.TryGetValue(key, out m) ? m : null; }

    static bool LoadAssets(Transform anyContainer)
    {
        mats = new Dictionary<string, Material>();
        AssetDatabase.Refresh();
        texDir = RSPaths.Textures + "/Build/고물상_집";
        matDir = RSPaths.Ensure(RSPaths.Materials + "/고물상");
        if (!AssetDatabase.IsValidFolder(texDir))
        {
            EditorUtility.DisplayDialog("집 문 · 창", "그림 폴더가 없습니다: " + texDir, "확인");
            return false;
        }
        Material src = null;
        var mr = anyContainer.GetComponentInChildren<MeshRenderer>();
        if (mr != null) src = mr.sharedMaterial;      // 컨테이너와 같은 셰이더 · 조명 값 (URP Lit)
        Lit("문", "집_문", src, true);
        Lit("창", "집_창", src, true);
        Lit("계단", "집_계단", src, false);
        Lit("차양", "집_차양", src, false);
        Lit("쇠", "집_쇠", src, false);
        Glow("창불빛", "집_창_불빛", new Color(1.35f, 1.22f, 1.05f));
        Glow("문창불빛", "집_문창_불빛", new Color(1.35f, 1.22f, 1.05f));
        Glow("등불빛", "집_등_불빛", new Color(2.4f, 1.9f, 1.3f));
        AssetDatabase.SaveAssets();
        foreach (var kv in mats) if (kv.Value == null) { Debug.LogWarning("[집 문 · 창] 머티리얼을 못 만듦: " + kv.Key); return false; }
        return true;
    }

    static Texture2D Tex(string name)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/" + name + ".png");
        if (t == null) { Debug.LogWarning("[집 문 · 창] 그림이 없습니다: " + texDir + "/" + name + ".png"); return null; }
        RSSurfaceTools.MakePixelTexture(t, false);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/" + name + ".png");
    }

    static void Lit(string key, string tex, Material src, bool cutout)
    {
        var shader = src != null ? src.shader : Shader.Find("Universal Render Pipeline/Lit");
        var m = src != null ? new Material(src) : new Material(shader);
        var tx = Tex(tex);
        m.SetTexture("_BaseMap", tx);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tx);
        m.SetTextureScale("_BaseMap", Vector2.one);
        m.SetTextureOffset("_BaseMap", Vector2.zero);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
        m.DisableKeyword("_NORMALMAP");
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = -1;
        }
        mats[key] = Save(m, "MAT_집_" + key);
    }

    static void Glow(string key, string tex, Color hdr)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) { mats[key] = null; return; }
        var m = new Material(sh);
        m.SetTexture("_BaseMap", Tex(tex));
        m.SetColor("_BaseColor", hdr);
        mats[key] = Save(m, "MAT_집_" + key);
    }

    /// <summary>같은 이름이 있으면 값만 덮어쓴다 (씬의 다른 참조가 안 끊기게)</summary>
    static Material Save(Material m, string name)
    {
        string path = matDir + "/" + name + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        m.name = name;
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        old.shader = m.shader;
        old.CopyPropertiesFromMaterial(m);
        old.shaderKeywords = m.shaderKeywords;
        old.SetOverrideTag("RenderType", m.GetTag("RenderType", false, "Opaque"));
        old.renderQueue = m.renderQueue;
        EditorUtility.SetDirty(old);
        Object.DestroyImmediate(m);
        return old;
    }
}
