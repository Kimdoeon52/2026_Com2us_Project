// RE:AL STEEL - Z-파이팅 찾기 · 고치기 (에디터)
//
// 서로 다른 오브젝트의 면이 같은 평면 · 같은 방향으로 겹친 곳(깜빡이는 곳)을 찾아, 작은 쪽을 법선 방향으로 몇 mm 밀어 고친다.
//  · 조립품 안의 부품끼리 겹친 면은 조립품 메시가 알아서 밀어 낸다 (RSMeshBuilder.ZBiasStep) — 여기서는 오브젝트끼리만 본다
//  · 지형(RS 지형 · 외곽)은 움직이지 않는다. 지형과 겹친 바닥판 등은 바닥판 쪽을 민다
//  · 맵 프리팹(MAP_...) 안의 오브젝트를 밀면 그 프리팹에 바로 저장한다
//  · 반투명 · 빌보드 · 풀 · 물 · 젖은 바닥 덧칠 · 안개처럼 원래 겹쳐 그리는 것은 뺀다
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Common.EditorTools
{
    public class RSZFightWindow : EditorWindow
    {
        const float DefPlaneTol = 0.0005f, DefNudge = 0.004f, DefMinArea = 0.0004f;

        [SerializeField] float planeTol = DefPlaneTol;
        [SerializeField] float nudge = DefNudge;
        [SerializeField] float minArea = DefMinArea;

        class Pair
        {
            public Renderer ra, rb;
            public GameObject oa, ob;        // 실제로 움직일 오브젝트 (저장 안 되는 자동 생성 자식이면 그 주인)
            public bool fixA, fixB;          // 움직여도 되는지 (지형 X)
            public Vector3 n;                // 같은 법선 (월드)
            public Vector3 at;               // 겹친 곳 하나 (표시용)
            public float area;               // 겹친 넓이 합 (m²)
        }

        readonly List<Pair> pairs = new List<Pair>();
        string report = "";
        Vector2 scroll;
        bool showMarks = true;

        [MenuItem("Tools/RE_AL STEEL/Stage/Z-파이팅 찾기 · 고치기", false, 31)]
        static void Open() { GetWindow<RSZFightWindow>("Z-파이팅"); }

        void OnEnable() { SceneView.duringSceneGui += OnScene; }
        void OnDisable() { SceneView.duringSceneGui -= OnScene; }

        // ─────────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(
                "오브젝트끼리 같은 평면에 같은 방향으로 겹친 면(깜빡이는 곳)을 찾아 작은 쪽을 몇 mm 밀어 고칩니다.\n" +
                "조립품 안 부품끼리 겹친 면은 조립품이 알아서 밀어 냅니다 (씬을 열면 다시 만들어짐).\n" +
                "1) 찾기 → 씬 뷰에 노란 표시  2) 모두 고치기 (맵 프리팹 안이면 프리팹에 저장)",
                MessageType.Info);

            planeTol = EditorGUILayout.Slider(new GUIContent("같은 평면 기준 (m)", "두 면의 거리가 이보다 가까우면 겹친 것으로 본다 (0.0005 = 0.5 mm)"), planeTol, 0.0001f, 0.005f);
            nudge = EditorGUILayout.Slider(new GUIContent("밀어 내는 거리 (m)", "작은 쪽을 법선 방향으로 이만큼 민다. 1 픽셀 = 0.031 m 라 0.004 는 눈에 안 보인다"), nudge, 0.001f, 0.02f);
            minArea = EditorGUILayout.Slider(new GUIContent("최소 겹친 넓이 (m²)", "이보다 작게 겹친 건 무시 (모서리만 닿은 것 등)"), minArea, 0.00005f, 0.05f);
            if (GUILayout.Button("기본값으로", GUILayout.Width(90))) { planeTol = DefPlaneTol; nudge = DefNudge; minArea = DefMinArea; GUI.FocusControl(null); }
            EditorGUI.BeginChangeCheck();
            showMarks = EditorGUILayout.Toggle("씬 뷰 표시", showMarks);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("찾기 (열린 씬)", GUILayout.Height(28))) { Find(); SceneView.RepaintAll(); }
                using (new EditorGUI.DisabledScope(pairs.Count == 0))
                    if (GUILayout.Button("모두 고치기 (" + pairs.Count + ")", GUILayout.Height(28)))
                    {
                        int moved = FixAll(out string where);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        Find();
                        report = moved + "개를 밀었습니다 (" + where + ")\n다시 찾은 결과: 남은 곳 " + pairs.Count + "개\n" + report;
                        SceneView.RepaintAll();
                    }
            }
            if (GUILayout.Button("SYS 씬 전부 찾아서 고치기"))
            {
                if (EditorUtility.DisplayDialog("Z-파이팅", "SYS/Scenes 의 씬을 차례로 열어 찾고 · 고치고 · 저장합니다.\n열린 씬에 저장 안 한 변경이 있으면 먼저 물어봅니다.", "진행", "취소"))
                    AllScenes();
            }

            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);

            if (pairs.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("겹친 곳 (넓은 순)", EditorStyles.boldLabel);
                foreach (var p in pairs)
                {
                    if (p.ra == null || p.rb == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string mover = Mover(p) == null ? "  ※ 둘 다 지형 — 직접" : "";
                        EditorGUILayout.LabelField(Name(p.oa) + "  ↔  " + Name(p.ob) + "  (" + (p.area * 10000f).ToString("0") + " cm²)" + mover);
                        if (GUILayout.Button("보기", GUILayout.Width(40)))
                        {
                            Selection.objects = new Object[] { p.oa, p.ob };
                            var sv = SceneView.lastActiveSceneView;
                            if (sv != null) sv.LookAt(p.at, sv.rotation, 2.5f);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static string Name(GameObject g) { return g != null ? g.name : "(없음)"; }

        void OnScene(SceneView sv)
        {
            if (!showMarks || pairs.Count == 0) return;
            var z = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (var p in pairs)
            {
                Handles.color = Mover(p) == null ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.9f, 0.1f);
                float s = HandleUtility.GetHandleSize(p.at) * 0.08f;
                Handles.DrawWireDisc(p.at, p.n, s);
                Handles.DrawLine(p.at, p.at + p.n * s * 2f);
            }
            Handles.zTest = z;
        }

        // ─────────────────────────────────────────────────────────────
        // 찾기
        // ─────────────────────────────────────────────────────────────

        struct Tri
        {
            public int r;                 // 렌더러 번호
            public Vector3 a, b, c, n;
            public float d, area;
        }

        static readonly string[] SkipShader = { "Billboard", "Foliage", "Water", "Wet", "Fog", "GodRay", "Shaft", "Blob", "Sky", "Sprite", "Particle", "Nano" };
        static readonly string[] Immovable = { "RSTerrain", "RSTerrainOuter" };

        static bool HasType(GameObject g, string[] names)
        {
            foreach (var mb in g.GetComponentsInParent<MonoBehaviour>(true))
                if (mb != null && System.Array.IndexOf(names, mb.GetType().Name) >= 0) return true;
            return false;
        }

        static GameObject Owner(Renderer r)
        {
            var t = r.transform;
            while (t != null && (t.gameObject.hideFlags & HideFlags.DontSave) != 0) t = t.parent;
            return t != null ? t.gameObject : null;
        }

        void Find()
        {
            pairs.Clear();
            var scene = SceneManager.GetActiveScene();
            var rends = new List<Renderer>();
            var tris = new List<Tri>();
            var tmpV = new List<Vector3>();
            var tmpI = new List<int>();

            foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.gameObject.scene != scene || EditorUtility.IsPersistent(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                bool skip = false;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.renderQueue >= 2500) { skip = true; break; }
                    string sn = m.shader != null ? m.shader.name : "";
                    foreach (var k in SkipShader) if (sn.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) { skip = true; break; }
                    if (skip) break;
                }
                if (skip) continue;
                if (HasType(r.gameObject, new[] { "RSWetSurface", "RSBackdrop", "RSFogVolume", "RSWaterReflection" })) continue;

                var mesh = mf.sharedMesh;
                try
                {
                    tmpV.Clear(); mesh.GetVertices(tmpV);
                    if (tmpV.Count == 0) continue;
                    var m4 = r.transform.localToWorldMatrix;
                    for (int i = 0; i < tmpV.Count; i++) tmpV[i] = m4.MultiplyPoint3x4(tmpV[i]);
                    int ri = rends.Count;
                    rends.Add(r);
                    for (int sm = 0; sm < mesh.subMeshCount; sm++)
                    {
                        if (mesh.GetTopology(sm) != MeshTopology.Triangles) continue;
                        tmpI.Clear(); mesh.GetTriangles(tmpI, sm);
                        for (int i = 0; i + 2 < tmpI.Count; i += 3)
                        {
                            Vector3 a = tmpV[tmpI[i]], b = tmpV[tmpI[i + 1]], c = tmpV[tmpI[i + 2]];
                            Vector3 cr = Vector3.Cross(b - a, c - a);
                            float ar = cr.magnitude * 0.5f;
                            if (ar < 1e-6f) continue;
                            Vector3 n = cr / (ar * 2f);
                            tris.Add(new Tri { r = ri, a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), area = ar });
                        }
                    }
                }
                catch (System.Exception) { /* 읽을 수 없는 메시 */ }
            }

            // 평면(법선 · 거리) + 평면 위 1 m 칸으로 묶는다
            var buckets = new Dictionary<long, List<int>>();
            const float cell = 1f, dBin = 0.01f;
            for (int ti = 0; ti < tris.Count; ti++)
            {
                var t = tris[ti];
                int qx = Mathf.RoundToInt(t.n.x * 40f), qy = Mathf.RoundToInt(t.n.y * 40f), qz = Mathf.RoundToInt(t.n.z * 40f);
                Basis(qx, qy, qz, out Vector3 u, out Vector3 v);
                float minU = Mathf.Min(Vector3.Dot(t.a, u), Mathf.Min(Vector3.Dot(t.b, u), Vector3.Dot(t.c, u)));
                float maxU = Mathf.Max(Vector3.Dot(t.a, u), Mathf.Max(Vector3.Dot(t.b, u), Vector3.Dot(t.c, u)));
                float minV = Mathf.Min(Vector3.Dot(t.a, v), Mathf.Min(Vector3.Dot(t.b, v), Vector3.Dot(t.c, v)));
                float maxV = Mathf.Max(Vector3.Dot(t.a, v), Mathf.Max(Vector3.Dot(t.b, v), Vector3.Dot(t.c, v)));
                int u0 = Mathf.FloorToInt(minU / cell), u1 = Mathf.FloorToInt(maxU / cell);
                int v0 = Mathf.FloorToInt(minV / cell), v1 = Mathf.FloorToInt(maxV / cell);
                if ((long)(u1 - u0 + 1) * (v1 - v0 + 1) > 400) { u1 = u0 + 19; v1 = v0 + 19; }
                int d0 = Mathf.FloorToInt((t.d - planeTol) / dBin), d1 = Mathf.FloorToInt((t.d + planeTol) / dBin);
                for (int db = d0; db <= d1; db++)
                for (int iu = u0; iu <= u1; iu++)
                for (int iv = v0; iv <= v1; iv++)
                {
                    long key = Key(qx, qy, qz, db, iu, iv);
                    List<int> l; if (!buckets.TryGetValue(key, out l)) buckets[key] = l = new List<int>();
                    l.Add(ti);
                }
            }

            var tested = new HashSet<long>();
            var byPair = new Dictionary<long, Pair>();
            foreach (var l in buckets.Values)
            {
                if (l.Count < 2) continue;
                for (int x = 0; x < l.Count; x++)
                {
                    var A = tris[l[x]];
                    for (int y = x + 1; y < l.Count; y++)
                    {
                        var B = tris[l[y]];
                        if (A.r == B.r) continue;
                        if (Vector3.Dot(A.n, B.n) < 0.9995f) continue;
                        if (Mathf.Abs(A.d - B.d) > planeTol) continue;
                        int i0 = Mathf.Min(l[x], l[y]), i1 = Mathf.Max(l[x], l[y]);
                        if (!tested.Add(((long)i0 << 32) | (uint)i1)) continue;
                        float ov = Overlap(A, B, out Vector3 at);
                        if (ov <= 1e-6f) continue;
                        int r0 = Mathf.Min(A.r, B.r), r1 = Mathf.Max(A.r, B.r);
                        long pk = ((long)r0 << 32) | (uint)r1;
                        Pair p;
                        if (!byPair.TryGetValue(pk, out p))
                        {
                            p = new Pair { ra = rends[r0], rb = rends[r1], n = A.n, at = at };
                            byPair[pk] = p;
                        }
                        p.area += ov;
                    }
                }
            }

            foreach (var p in byPair.Values)
            {
                if (p.area < minArea) continue;
                p.oa = Owner(p.ra); p.ob = Owner(p.rb);
                if (p.oa == null || p.ob == null || p.oa == p.ob) continue;
                p.fixA = !HasType(p.oa, Immovable);
                p.fixB = !HasType(p.ob, Immovable);
                pairs.Add(p);
            }
            pairs.Sort((p, q) => q.area.CompareTo(p.area));

            int stuck = 0; foreach (var p in pairs) if (Mover(p) == null) stuck++;
            report = string.Format("{0}: 면 {1:n0}개 · 오브젝트 {2}개 검사 → 겹친 곳 {3}개{4}",
                scene.name, tris.Count, rends.Count, pairs.Count, stuck > 0 ? " (지형끼리 " + stuck + "개는 직접)" : "");
        }

        static long Key(int qx, int qy, int qz, int db, int iu, int iv)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                h = (h ^ qx) * 1099511628211L; h = (h ^ qy) * 1099511628211L; h = (h ^ qz) * 1099511628211L;
                h = (h ^ db) * 1099511628211L; h = (h ^ iu) * 1099511628211L; h = (h ^ iv) * 1099511628211L;
                return h;
            }
        }

        static void Basis(int qx, int qy, int qz, out Vector3 u, out Vector3 v)
        {
            Vector3 n = new Vector3(qx, qy, qz);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
            n.Normalize();
            Vector3 r = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(n, r).normalized;
            v = Vector3.Cross(n, u);
        }

        // 두 삼각형을 A 의 평면에 투영해 겹친 넓이 (모서리만 닿은 건 0)
        static float Overlap(Tri A, Tri B, out Vector3 at)
        {
            Vector3 r = Mathf.Abs(A.n.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(A.n, r).normalized, v = Vector3.Cross(A.n, u);
            var pa = new List<Vector2> { P(A.a, u, v), P(A.b, u, v), P(A.c, u, v) };
            var pb = new List<Vector2> { P(B.a, u, v), P(B.b, u, v), P(B.c, u, v) };
            if (Signed(pa) < 0f) pa.Reverse();
            if (Signed(pb) < 0f) pb.Reverse();
            var poly = pb;
            for (int i = 0; i < 3 && poly.Count > 0; i++) poly = Clip(poly, pa[i], pa[(i + 1) % 3]);
            at = A.a;
            if (poly.Count < 3) return 0f;
            float area = Mathf.Abs(Signed(poly));
            if (area < 1e-6f) return 0f;
            Vector2 c = Vector2.zero; foreach (var p in poly) c += p; c /= poly.Count;
            at = A.n * A.d + u * c.x + v * c.y;
            return area;
        }

        static Vector2 P(Vector3 p, Vector3 u, Vector3 v) { return new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)); }

        static float Signed(List<Vector2> p)
        {
            float s = 0f;
            for (int i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.x * b.y - b.x * a.y; }
            return s * 0.5f;
        }

        // 선 e0 → e1 의 왼쪽만 남긴다 (Sutherland–Hodgman)
        static List<Vector2> Clip(List<Vector2> poly, Vector2 e0, Vector2 e1)
        {
            var o = new List<Vector2>();
            Vector2 e = e1 - e0;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float sa = e.x * (a.y - e0.y) - e.y * (a.x - e0.x);
                float sb = e.x * (b.y - e0.y) - e.y * (b.x - e0.x);
                if (sa >= 0f) o.Add(a);
                if ((sa >= 0f) != (sb >= 0f)) o.Add(a + (b - a) * (sa / (sa - sb)));
            }
            return o;
        }

        // ─────────────────────────────────────────────────────────────
        // 고치기
        // ─────────────────────────────────────────────────────────────

        /// <summary>밀 쪽: 움직일 수 있는 쪽 중 작은 것 (덧댄 판 · 바닥판이 앞으로 나온다)</summary>
        static GameObject Mover(Pair p)
        {
            if (p.fixA && p.fixB)
            {
                float sa = p.ra.bounds.size.sqrMagnitude, sb = p.rb.bounds.size.sqrMagnitude;
                return sa <= sb ? p.oa : p.ob;
            }
            if (p.fixA) return p.oa;
            if (p.fixB) return p.ob;
            return null;
        }

        int FixAll(out string where)
        {
            var done = new HashSet<GameObject>();
            var prefabs = new HashSet<string>();
            int moved = 0;
            foreach (var p in pairs)
            {
                var g = Mover(p);
                if (g == null || done.Contains(g)) continue;   // 한 오브젝트는 한 번만 (여러 면이 겹쳐도 같은 방향으로 밀면 됨)
                done.Add(g);
                Undo.RecordObject(g.transform, "Z-파이팅 고치기");
                g.transform.position += p.n * nudge;
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
                moved++;
                if (PrefabUtility.IsPartOfPrefabInstance(g))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(g);
                    string path = root != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) : null;
                    if (!string.IsNullOrEmpty(path))
                    {
                        try { PrefabUtility.ApplyObjectOverride(g.transform, path, InteractionMode.AutomatedAction); prefabs.Add(Path.GetFileNameWithoutExtension(path)); }
                        catch (System.Exception e) { Debug.LogWarning("[Z-파이팅] " + g.name + " 프리팹 반영 실패 — 씬 오버라이드로 남김\n" + e.Message); }
                    }
                }
            }
            where = prefabs.Count > 0 ? "프리팹 " + string.Join(", ", prefabs) + " 에 저장" : "씬";
            return moved;
        }

        void AllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = SceneManager.GetActiveScene().path;
            var log = new System.Text.StringBuilder("[Z-파이팅] 씬 전부\n");
            foreach (var g in AssetDatabase.FindAssets("t:Scene", new[] { RSPaths.Root + "/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Find();
                int before = pairs.Count;
                int moved = 0; string where = "";
                if (before > 0)
                {
                    moved = FixAll(out where);
                    EditorSceneManager.MarkSceneDirty(sc);
                    EditorSceneManager.SaveScene(sc);
                    Find();
                }
                log.AppendLine("· " + sc.name + " — 겹친 곳 " + before + " → 밀기 " + moved + (moved > 0 ? " (" + where + ")" : "") + " → 남음 " + pairs.Count);
            }
            if (!string.IsNullOrEmpty(back) && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Find();
            report = log.ToString();
            Debug.Log(report);
            SceneView.RepaintAll();
        }
    }
}
