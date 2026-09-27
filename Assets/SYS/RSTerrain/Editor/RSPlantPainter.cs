// RE:AL STEEL - 풀꽃 배치 창 (시트에서 고른 풀 · 꽃을 씬 뷰에 클릭해서 하나씩 세운다) + 풀꽃 하나 인스펙터
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;

namespace RealSteel.Terrain.EditorTools
{
    public class RSPlantPainter : EditorWindow
    {
        [MenuItem("Tools/RE_AL STEEL/Stage/풀꽃 배치 (하나씩 찍기)", false, 24)]
        public static void Open()
        {
            var w = GetWindow<RSPlantPainter>(false, "풀꽃 배치", true);
            w.minSize = new Vector2(300f, 360f);
            w.Show();
        }

        const string LastSheetKey = "RS_Foliage_LastSheet";

        Texture2D sheet;
        List<RectInt> rects = new List<RectInt>();
        List<bool> flowers = new List<bool>();
        readonly HashSet<int> picked = new HashSet<int>();
        Vector2 scroll;

        bool painting;
        float ppu = 32f;
        int perClick = 1;
        float spread = 0f;
        float dragStep = 0.35f;
        Vector2 scaleRange = new Vector2(0.9f, 1.1f);
        bool randomFlip = true;
        float colorVar = 0.08f;
        float sink = 0.03f;
        float eraseRadius = 0.6f;

        Vector3 lastStamp;
        bool hasLastStamp;

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            string g = EditorPrefs.GetString(LastSheetKey, "");
            if (!string.IsNullOrEmpty(g)) SetSheet(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)));
            if (sheet == null) SetSheet(RSFoliageTools.FindTexture("꽃", "flower", "foliage"));
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            painting = false;
        }

        void SetSheet(Texture2D tex)
        {
            sheet = tex;
            rects.Clear(); flowers.Clear(); picked.Clear();
            if (sheet == null) return;
            EditorPrefs.SetString(LastSheetKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sheet)));
            if (!RSFoliageTools.ImportOk(sheet, true)) RSFoliageTools.FixImport(sheet, true);
            rects = RSFoliageTools.Slice(sheet, RSFoliageTools.SliceGap, flowers);
        }

        // ─────────────────────────────────────────────────────────────
        // 창
        // ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            EditorGUILayout.HelpBox("시트에서 풀 · 꽃을 고르고 [찍기 켜기] → 씬 뷰에서 지면을 클릭(드래그)하면 하나씩 선다.\n" +
                                    "Shift + 클릭 = 지우기 · Esc = 찍기 끄기. 찍힌 건 '풀꽃 하나' 오브젝트라 옮기고 지우기 자유.", MessageType.None);

            var t = (Texture2D)EditorGUILayout.ObjectField("시트", sheet, typeof(Texture2D), false);
            if (t != sheet) SetSheet(t);
            if (sheet == null) { EditorGUILayout.HelpBox("풀 · 꽃 스프라이트 시트(배경 투명 PNG)를 넣으세요.", MessageType.Info); return; }

            using (new EditorGUILayout.HorizontalScope())
            {
                RSFoliageTools.SliceGap = EditorGUILayout.IntSlider(new GUIContent("자르기 틈", "그림 사이가 이 픽셀 수 이상 비면 다른 그림"), RSFoliageTools.SliceGap, 1, 12);
                if (GUILayout.Button("다시 자르기", GUILayout.Width(80f))) SetSheet(sheet);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부")) { picked.Clear(); for (int i = 0; i < rects.Count; i++) picked.Add(i); }
                if (GUILayout.Button("꽃만")) { picked.Clear(); for (int i = 0; i < rects.Count; i++) if (flowers[i]) picked.Add(i); }
                if (GUILayout.Button("풀만")) { picked.Clear(); for (int i = 0; i < rects.Count; i++) if (!flowers[i]) picked.Add(i); }
                if (GUILayout.Button("해제")) picked.Clear();
            }
            EditorGUILayout.LabelField($"{rects.Count}개 중 {picked.Count}개 고름 — 여러 개 고르면 그중에서 무작위로 찍는다", EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
            int clicked = DrawPalette(sheet, rects, picked, position.width - 24f);
            EditorGUILayout.EndScrollView();
            if (clicked >= 0)
            {
                if (Event.current.shift || Event.current.control) { if (!picked.Remove(clicked)) picked.Add(clicked); }
                else { picked.Clear(); picked.Add(clicked); }
            }

            EditorGUILayout.Space(4f);
            var col = GUI.backgroundColor;
            GUI.backgroundColor = painting ? new Color(0.5f, 1f, 0.5f) : col;
            using (new EditorGUI.DisabledScope(picked.Count == 0))
            {
                if (GUILayout.Button(painting ? "찍는 중 — 씬 뷰에서 클릭 (눌러서 끄기)" : "찍기 켜기", GUILayout.Height(30f)))
                {
                    painting = !painting;
                    SceneView.RepaintAll();
                }
            }
            GUI.backgroundColor = col;
            if (picked.Count == 0) EditorGUILayout.HelpBox("위에서 찍을 그림을 하나 이상 고르세요 (클릭 · Ctrl+클릭으로 여러 개).", MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("찍기 설정", EditorStyles.boldLabel);
            perClick = EditorGUILayout.IntSlider(new GUIContent("한 번에 개수", "클릭 한 번에 세울 포기 수"), perClick, 1, 20);
            spread = EditorGUILayout.Slider(new GUIContent("흩뿌림 반경 (m)", "0 = 클릭한 자리에 정확히 / 크면 그 반경 안에 흩어서"), spread, 0f, 4f);
            dragStep = EditorGUILayout.Slider(new GUIContent("드래그 간격 (m)", "드래그하면 이만큼 움직일 때마다 찍는다"), dragStep, 0.1f, 3f);
            EditorGUILayout.MinMaxSlider(new GUIContent($"크기 {scaleRange.x:0.00} ~ {scaleRange.y:0.00}"), ref scaleRange.x, ref scaleRange.y, 0.4f, 2f);
            randomFlip = EditorGUILayout.Toggle("좌우 무작위", randomFlip);
            colorVar = EditorGUILayout.Slider(new GUIContent("색 흔들기", "포기마다 밝기를 조금씩 다르게"), colorVar, 0f, 0.3f);
            ppu = EditorGUILayout.FloatField(new GUIContent("PPU (픽셀/m)", "지형 머티리얼 PPU 와 같게 (기본 32)"), ppu);
            sink = EditorGUILayout.Slider(new GUIContent("묻기 (m)", "밑동을 땅에 살짝 묻는다"), sink, 0f, 0.2f);
            eraseRadius = EditorGUILayout.Slider(new GUIContent("지우기 반경 (m)", "Shift + 클릭으로 지울 때"), eraseRadius, 0.1f, 4f);
        }

        /// <summary>시트 조각 썸네일 격자. 누른 칸 번호 (없으면 -1)</summary>
        public static int DrawPalette(Texture2D sheet, List<RectInt> rects, ICollection<int> selected, float width)
        {
            const float cell = 40f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt(width / cell));
            int rows = Mathf.CeilToInt(rects.Count / (float)perRow);
            var area = GUILayoutUtility.GetRect(perRow * cell, rows * cell);
            int clicked = -1;
            for (int i = 0; i < rects.Count; i++)
            {
                var it = rects[i];
                var r = new Rect(area.x + (i % perRow) * cell, area.y + (i / perRow) * cell, cell - 2f, cell - 2f);
                bool on = selected != null && selected.Contains(i);
                EditorGUI.DrawRect(r, on ? new Color(0.25f, 0.6f, 1f, 0.55f) : new Color(0f, 0f, 0f, 0.2f));
                var uv = new Rect(it.x / (float)sheet.width, it.y / (float)sheet.height, it.width / (float)sheet.width, it.height / (float)sheet.height);
                float s = Mathf.Min((cell - 6f) / it.width, (cell - 6f) / it.height);
                var d = new Rect(0, 0, it.width * s, it.height * s);
                d.center = new Vector2(r.center.x, r.yMax - 3f - d.height * 0.5f);
                GUI.DrawTextureWithTexCoords(d, sheet, uv);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
                {
                    clicked = i;
                    Event.current.Use();
                }
            }
            return clicked;
        }

        // ─────────────────────────────────────────────────────────────
        // 씬 뷰
        // ─────────────────────────────────────────────────────────────

        void OnSceneGUI(SceneView sv)
        {
            if (!painting || sheet == null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { painting = false; Repaint(); e.Use(); return; }
            if (e.alt) return;   // Alt = 카메라 돌리기

            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            bool hit = Ground(HandleUtility.GUIPointToWorldRay(e.mousePosition), out Vector3 p);
            if (hit)
            {
                Handles.color = e.shift ? new Color(1f, 0.4f, 0.3f, 0.9f) : new Color(0.4f, 1f, 0.4f, 0.9f);
                Handles.DrawWireDisc(p, Vector3.up, e.shift ? eraseRadius : Mathf.Max(0.15f, spread));
                sv.Repaint();
            }

            if (e.button != 0 || !hit) return;
            if (e.type == EventType.MouseDown)
            {
                GUIUtility.hotControl = id;
                if (e.shift) Erase(p); else Stamp(p);
                lastStamp = p; hasLastStamp = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                if (e.shift) Erase(p);
                else if (!hasLastStamp || (p - lastStamp).magnitude >= dragStep) { Stamp(p); lastStamp = p; hasLastStamp = true; }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                hasLastStamp = false;
                e.Use();
            }
        }

        /// <summary>지면 찾기: RS 지형(콜라이더 없어도) → 없으면 콜라이더</summary>
        public static bool Ground(Ray ray, out Vector3 p)
        {
            p = Vector3.zero;
            float best = float.MaxValue; bool any = false;
            foreach (var t in RSTerrain.All)
            {
                if (t == null || !t.HasHeights) continue;
                if (t.Raycast(ray, out Vector3 h))
                {
                    float d = (h - ray.origin).sqrMagnitude;
                    if (d < best) { best = d; p = h; any = true; }
                }
            }
            if (Physics.Raycast(ray, out RaycastHit rh, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                float d = (rh.point - ray.origin).sqrMagnitude;
                if (d < best) { p = rh.point; any = true; }
            }
            return any;
        }

        /// <summary>지면 높이 (월드 XZ). RS 지형 → 콜라이더 → 그대로</summary>
        public static float GroundY(Vector3 w)
        {
            if (Ground(new Ray(w + Vector3.up * 50f, Vector3.down), out Vector3 p)) return p.y;
            return w.y;
        }

        Transform Group()
        {
            const string name = "풀꽃_배치";
            RSTerrain terrain = null;
            foreach (var t in RSTerrain.All) if (t != null) { terrain = t; break; }
            Transform parent = terrain != null ? terrain.transform : null;
            Transform g = parent != null ? parent.Find(name) : null;
            if (g == null)
            {
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == name) { g = root.transform; break; }
            }
            if (g == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "풀꽃 배치");
                if (parent != null) go.transform.SetParent(parent, false);
                g = go.transform;
            }
            return g;
        }

        void Stamp(Vector3 center)
        {
            if (picked.Count == 0) return;
            var ids = new List<int>(picked);
            var mat = RSFoliageTools.MaterialFor(sheet);
            var group = Group();
            for (int n = 0; n < perClick; n++)
            {
                Vector3 p = center;
                if (spread > 0.001f || perClick > 1)
                {
                    Vector2 r = Random.insideUnitCircle * Mathf.Max(spread, 0.3f);
                    p = new Vector3(center.x + r.x, center.y, center.z + r.y);
                    p.y = GroundY(p);
                }
                p.y -= sink;
                int k = ids[Random.Range(0, ids.Count)];
                var go = new GameObject("풀꽃_" + k);
                Undo.RegisterCreatedObjectUndo(go, "풀꽃 찍기");
                go.transform.SetParent(group, true);
                go.transform.position = p;
                var pl = go.AddComponent<RSPlant>();
                pl.sheet = sheet;
                pl.rect = rects[k];
                pl.ppu = ppu;
                pl.material = mat;
                pl.scale = Random.Range(scaleRange.x, scaleRange.y);
                pl.flip = randomFlip && Random.value < 0.5f;
                float b = 1f + Random.Range(-colorVar, colorVar);
                pl.tint = new Color(b, b, b, 1f);
                pl.phase = Random.value;
                pl.Build();
            }
        }

        void Erase(Vector3 center)
        {
            foreach (var pl in Object.FindObjectsByType<RSPlant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var d = pl.transform.position - center; d.y = 0f;
                if (d.magnitude <= eraseRadius) Undo.DestroyObjectImmediate(pl.gameObject);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 풀꽃 하나 인스펙터 — 그림 바꾸기 · 지면에 붙이기
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSPlant)), CanEditMultipleObjects]
    public class RSPlantEditor : Editor
    {
        static readonly Dictionary<Texture2D, List<RectInt>> cache = new Dictionary<Texture2D, List<RectInt>>();

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var pl = (RSPlant)target;
            if (pl.sheet != null)
            {
                if (!cache.TryGetValue(pl.sheet, out var rects)) cache[pl.sheet] = rects = RSFoliageTools.Slice(pl.sheet, RSFoliageTools.SliceGap);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("다른 그림으로 바꾸기", EditorStyles.boldLabel);
                int cur = rects.IndexOf(pl.rect);
                int clicked = RSPlantPainter.DrawPalette(pl.sheet, rects, cur >= 0 ? new[] { cur } : null, EditorGUIUtility.currentViewWidth - 30f);
                if (clicked >= 0)
                {
                    foreach (var o in targets)
                    {
                        var p = (RSPlant)o;
                        Undo.RecordObject(p, "풀꽃 바꾸기");
                        p.rect = rects[clicked];
                        if (p.sheet != pl.sheet) p.sheet = pl.sheet;
                        p.Build();
                        EditorUtility.SetDirty(p);
                    }
                }
            }
            else EditorGUILayout.HelpBox("시트를 넣으세요.", MessageType.Info);

            if (pl.material == null && pl.sheet != null && GUILayout.Button("머티리얼 연결 (시트 옆 MAT_RS_Foliage_*)"))
                foreach (var o in targets) { var p = (RSPlant)o; Undo.RecordObject(p, "풀꽃 머티리얼"); p.material = RSFoliageTools.MaterialFor(p.sheet); p.Build(); }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("지면에 붙이기"))
                    foreach (var o in targets)
                    {
                        var p = (RSPlant)o;
                        Undo.RecordObject(p.transform, "지면에 붙이기");
                        var w = p.transform.position;
                        w.y = RSPlantPainter.GroundY(w) - 0.03f;
                        p.transform.position = w;
                    }
                if (GUILayout.Button("풀꽃 배치 창")) RSPlantPainter.Open();
            }
        }
    }
}
