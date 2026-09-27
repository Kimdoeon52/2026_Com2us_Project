// RE:AL STEEL - RS Terrain 에디터 (인스펙터 · 브러시 · 자동 갱신 · 만들기 메뉴 · ProBuilder 변환)
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// ProBuilder 는 이름만 따로 가져온다 — 통째로 using 하면 HandleUtility 등이 UnityEditor 것과 겹친다
using ProBuilderMesh = UnityEngine.ProBuilder.ProBuilderMesh;
using Face = UnityEngine.ProBuilder.Face;
using RealSteel.Common.EditorTools;

namespace RealSteel.Terrain.EditorTools
{
    // ═════════════════════════════════════════════════════════════════
    // 자동 갱신 — 요소를 옮기거나 값을 바꾸면 다시 만든다
    // ═════════════════════════════════════════════════════════════════

    [InitializeOnLoad]
    static class RSTerrainAutoRebuild
    {
        static RSTerrainAutoRebuild()
        {
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        static void OnUndoRedo()
        {
            foreach (var t in RSTerrain.All) if (t != null) t.MarkDirty();
        }

        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool dragging = GUIUtility.hotControl != 0;

            foreach (var t in RSTerrain.All.ToArray())
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                if (t.ConsumeTransformChanges()) t.MarkDirty();
                if (!t.dirty && t.HasMesh) continue;

                bool first = !t.HasMesh;
                bool allowed = first || (t.autoRebuild && (t.rebuildWhileDragging || !dragging));
                if (!allowed) continue;
                if (!first && Time.realtimeSinceStartup - t.dirtyTime < 0.05f) continue;

                t.Rebuild();
                SceneView.RepaintAll();
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 인스펙터 + 브러시
    // ═════════════════════════════════════════════════════════════════

    [CustomEditor(typeof(RSTerrain))]
    public class RSTerrainEditor : Editor
    {
        enum Tool { Off, Raise, Lower, Flatten, Smooth, ResetHeight, Paint, ErasePaint }

        static readonly string[] ToolLabels =
        {
            "끄기", "올리기", "내리기", "평탄화",
            "부드럽게", "높이 되돌리기", "칠하기", "칠 지우기",
        };

        static Tool tool = Tool.Off;
        static RSLayer brushLayer = RSLayer.R;
        static float radius = 1.5f;
        static float strength = 0.5f;
        static float hardness = 0.5f;
        static float flattenHeight = 0f;

        bool stroking;
        Vector3 lastDab;

        void OnEnable() { Tools.hidden = tool != Tool.Off; }
        void OnDisable() { Tools.hidden = false; }

        public override void OnInspectorGUI()
        {
            var t = (RSTerrain)target;
            RSHelpGUI.DrawSummary(target);

            // ── 요소 추가 ──
            EditorGUILayout.LabelField("요소 추가  (씬 뷰 가운데에 생긴다)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("둔덕 · 절벽")) RSTerrainMenu.AddFeature<RSPlateau>(t, "둔덕");
                if (GUILayout.Button("더미")) RSTerrainMenu.AddFeature<RSHeap>(t, "더미");
                if (GUILayout.Button("웅덩이")) RSTerrainMenu.AddFeature<RSPit>(t, "웅덩이");
                if (GUILayout.Button("폐자재")) RSTerrainMenu.AddFeature<RSScatter>(t, "폐자재");
                if (GUILayout.Button("풀꽃 놓기")) RSPlantPainter.Open();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("작업장 · 평탄")) RSTerrainMenu.AddFeature<RSPad>(t, "작업장");
                if (GUILayout.Button("길")) RSTerrainMenu.AddSpline(t, RSSpline.Kind.Path, false);
                if (GUILayout.Button("경사로")) RSTerrainMenu.AddSpline(t, RSSpline.Kind.Path, true);
                if (GUILayout.Button("배수로")) RSTerrainMenu.AddSpline(t, RSSpline.Kind.Trench, false);
            }

            EditorGUILayout.Space(6f);

            // ── 브러시 ──
            EditorGUILayout.LabelField("브러시  (씬 뷰에서 드래그)", EditorStyles.boldLabel);
            var newTool = (Tool)GUILayout.SelectionGrid((int)tool, ToolLabels, 4);
            if (newTool != tool)
            {
                tool = newTool;
                Tools.hidden = tool != Tool.Off;
                SceneView.RepaintAll();
            }
            if (tool != Tool.Off)
            {
                if (t.data == null)
                {
                    EditorGUILayout.HelpBox("손질한 결과를 담을 브러시 데이터 에셋이 필요합니다.", MessageType.Warning);
                    if (GUILayout.Button("브러시 데이터 만들기")) CreateData(t);
                }
                if (tool == Tool.Paint)
                    brushLayer = (RSLayer)EditorGUILayout.EnumPopup("칠할 레이어", brushLayer);
                if (tool == Tool.Flatten)
                    flattenHeight = EditorGUILayout.FloatField(new GUIContent("평탄화 높이", "Ctrl+클릭으로 지면에서 찍어 올 수 있다"), flattenHeight);
                radius   = EditorGUILayout.Slider("반경", radius, 0.2f, 12f);
                strength = EditorGUILayout.Slider("세기", strength, 0.01f, 1f);
                hardness = EditorGUILayout.Slider(new GUIContent("단단함", "가운데 꽉 찬 영역 비율. 1 = 경계가 딱 끊김"), hardness, 0f, 1f);
                EditorGUILayout.HelpBox(
                    "Shift = 반대로 (올리기↔내리기, 칠하기→칠 지우기)\n" +
                    "[ ] = 반경 줄이기 / 늘리기\n" +
                    "평탄화: Ctrl+클릭으로 높이 찍기\n" +
                    "칠하기 '바탕' = 요소가 칠한 레이어를 흙으로 덮기", MessageType.None);
            }

            EditorGUILayout.Space(6f);

            // ── 설정 ──
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            RSFoliageTools.DrawGrassLayerField(t);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("지금 다시 만들기", GUILayout.Height(26f))) { t.Rebuild(); SceneView.RepaintAll(); }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (t.data == null)
                {
                    if (GUILayout.Button("브러시 데이터 만들기")) CreateData(t);
                }
                else
                {
                    if (GUILayout.Button("높이 손질 전부 지우기") &&
                        EditorUtility.DisplayDialog("높이 손질 지우기", "브러시로 깎은 높이를 전부 지웁니다. (Ctrl+Z 가능)", "지우기", "취소"))
                    {
                        Undo.RecordObject(t.data, "RS Terrain 높이 지우기");
                        t.data.ClearHeight(); EditorUtility.SetDirty(t.data); t.RecomposeAll();
                    }
                    if (GUILayout.Button("칠 전부 지우기") &&
                        EditorUtility.DisplayDialog("칠 지우기", "브러시로 칠한 레이어를 전부 지웁니다. (Ctrl+Z 가능)", "지우기", "취소"))
                    {
                        Undo.RecordObject(t.data, "RS Terrain 칠 지우기");
                        t.data.ClearPaint(); EditorUtility.SetDirty(t.data); t.RecomposeAll();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("예제: 폐철장 40x40")) RSTerrainExamples.BuildJunkyard(t);
                if (GUILayout.Button("ProBuilder 로 변환")) RSTerrainMenu.ConvertToProBuilder(t);
            }
        }

        static void CreateData(RSTerrain t)
        {
            string path = EditorUtility.SaveFilePanelInProject("브러시 데이터 저장", t.name + "_BrushData", "asset",
                                                               "손으로 깎고 칠한 결과를 담을 에셋", "Assets");
            if (string.IsNullOrEmpty(path)) return;
            var d = ScriptableObject.CreateInstance<RSTerrainData>();
            AssetDatabase.CreateAsset(d, path);
            AssetDatabase.SaveAssets();
            Undo.RecordObject(t, "RS Terrain 브러시 데이터");
            t.data = d;
            EditorUtility.SetDirty(t);
            t.Rebuild();
        }

        // ── 씬 뷰 브러시 ──
        void OnSceneGUI()
        {
            if (tool == Tool.Off) return;
            var t = (RSTerrain)target;
            if (!t.HasHeights) return;

            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            // 반경 단축키
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.LeftBracket)  { radius = Mathf.Max(0.2f, radius / 1.15f); e.Use(); Repaint(); }
                if (e.keyCode == KeyCode.RightBracket) { radius = Mathf.Min(12f, radius * 1.15f); e.Use(); Repaint(); }
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 hit;
            if (!t.Raycast(ray, out hit)) return;

            Tool active = tool;
            if (e.shift)
            {
                if (tool == Tool.Raise) active = Tool.Lower;
                else if (tool == Tool.Lower) active = Tool.Raise;
                else if (tool == Tool.Paint) active = Tool.ErasePaint;
            }

            // 원 그리기
            Color col = active == Tool.Lower || active == Tool.ErasePaint || active == Tool.ResetHeight
                ? new Color(1f, 0.45f, 0.35f) : new Color(0.4f, 0.9f, 1f);
            if (active == Tool.Paint) col = LayerGizmoColor(brushLayer);
            Handles.color = col;
            float scale = t.transform.lossyScale.x;
            Handles.DrawWireDisc(hit, t.transform.up, radius * scale);
            Handles.color = new Color(col.r, col.g, col.b, 0.5f);
            Handles.DrawWireDisc(hit, t.transform.up, radius * hardness * scale);

            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            if (e.alt) return;   // 궤도 회전 중

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (t.data == null) { CreateData(t); e.Use(); return; }

                Vector3 local = t.transform.InverseTransformPoint(hit);
                if (tool == Tool.Flatten && e.control)
                {
                    flattenHeight = local.y;
                    Repaint();
                    e.Use();
                    return;
                }
                Undo.RecordObject(t.data, "RS Terrain 브러시");
                stroking = true;
                GUIUtility.hotControl = id;
                Dab(t, active, local);
                lastDab = local;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && stroking && GUIUtility.hotControl == id)
            {
                Vector3 local = t.transform.InverseTransformPoint(hit);
                if (Vector3.Distance(local, lastDab) > radius * 0.2f)
                {
                    Dab(t, active, local);
                    lastDab = local;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && stroking)
            {
                stroking = false;
                if (GUIUtility.hotControl == id) GUIUtility.hotControl = 0;
                EditorUtility.SetDirty(t.data);
                t.RebuildScatter();   // 고친 지면에 폐자재를 다시 앉힌다
                e.Use();
            }
        }

        void Dab(RSTerrain t, Tool active, Vector3 local)
        {
            RSTerrain.BrushOp op;
            switch (active)
            {
                case Tool.Raise:       op = RSTerrain.BrushOp.Raise; break;
                case Tool.Lower:       op = RSTerrain.BrushOp.Lower; break;
                case Tool.Flatten:     op = RSTerrain.BrushOp.Flatten; break;
                case Tool.Smooth:      op = RSTerrain.BrushOp.Smooth; break;
                case Tool.ResetHeight: op = RSTerrain.BrushOp.ResetHeight; break;
                case Tool.Paint:       op = RSTerrain.BrushOp.Paint; break;
                case Tool.ErasePaint:  op = RSTerrain.BrushOp.ErasePaint; break;
                default: return;
            }
            t.ApplyBrush(op, local, radius, strength, hardness, brushLayer, flattenHeight);
        }

        static Color LayerGizmoColor(RSLayer l)
        {
            switch (l)
            {
                case RSLayer.R: return new Color(1f, 0.85f, 0.35f);
                case RSLayer.G: return new Color(0.85f, 0.85f, 0.85f);
                case RSLayer.B: return new Color(1f, 0.5f, 0.2f);
                case RSLayer.A: return new Color(0.35f, 0.5f, 1f);
                case RSLayer.Grass: return new Color(0.4f, 0.95f, 0.3f);
                default:        return new Color(0.6f, 0.45f, 0.3f);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════
    // 만들기 메뉴 · 요소 추가 · ProBuilder 변환
    // ═════════════════════════════════════════════════════════════════

    public static class RSTerrainMenu
    {
        const string SplatShader = "RE_AL STEEL/Terrain Splat Pixel Lit";

        [MenuItem("GameObject/RE_AL STEEL/RS 지형", false, 10)]
        static void CreateTerrainFromHierarchy() { CreateTerrain(); }

        [MenuItem("Tools/RE_AL STEEL/Stage/RS 지형 만들기 (범용)", false, 16)]
        public static void CreateTerrain()
        {
            var go = new GameObject("RS_Terrain");
            Undo.RegisterCreatedObjectUndo(go, "RS 지형 만들기");
            var t = go.AddComponent<RSTerrain>();
            t.material = FindOrCreateSplatMaterial();
            t.waterMaterial = FindMaterial("MAT_Stage_Water");
            if (Selection.activeTransform != null && Selection.activeTransform.GetComponentInParent<RSTerrain>() == null)
                go.transform.SetParent(Selection.activeTransform, false);
            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        public static Material FindMaterial(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        public static Material FindOrCreateSplatMaterial()
        {
            var m = FindMaterial("MAT_Stage_TerrainSplat");
            if (m == null) m = FindMaterial("MAT_RSTerrain_Splat");
            if (m != null) return m;

            var sh = Shader.Find(SplatShader);
            if (sh == null)
            {
                Debug.LogWarning("[RS Terrain] 셰이더 '" + SplatShader + "' 를 못 찾았습니다. TerrainSplatPixelLit.shader 가 있는지 확인하세요.");
                return null;
            }
            m = new Material(sh);
            // 기존 MAT_Stage_* 의 텍스처·색을 레이어로 옮긴다. 길(R)·진흙(A)은 전용 텍스처가 없으면 흙을 밝게/어둡게.
            FillLayer(m, "_BaseTop",  "_BaseTopColor",  FindMaterial("MAT_Stage_Ground"),   1f);
            FillLayer(m, "_BaseSide", "_BaseSideColor", FindMaterial("MAT_Stage_Stone"),    1f);
            FillLayer(m, "_LayerR",   "_ColorR",        FindMaterial("MAT_Stage_Ground"),   1.35f);
            FillLayer(m, "_LayerG",   "_ColorG",        FindMaterial("MAT_Stage_Concrete"), 1f);
            FillLayer(m, "_LayerB",   "_ColorB",        FindMaterial("MAT_Stage_Rust"),     1f);
            FillLayer(m, "_LayerA",   "_ColorA",        FindMaterial("MAT_Stage_Ground"),   0.55f);
            string dir = "Assets/SYS/RSTerrain";
            if (!AssetDatabase.IsValidFolder(dir)) dir = "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath(dir + "/MAT_RSTerrain_Splat.mat");
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[RS Terrain] 스플랫 머티리얼 생성 → " + path + "  (레이어 텍스처를 채워 넣으세요)");
            return m;
        }

        static void FillLayer(Material dst, string texProp, string colProp, Material src, float brightness)
        {
            if (dst == null || src == null || !dst.HasProperty(texProp)) return;
            Texture tex = null;
            foreach (var p in new[] { "_BaseMap", "_MainTex", "_TopMap", "_SideMap" })
                if (src.HasProperty(p) && src.GetTexture(p) != null) { tex = src.GetTexture(p); break; }
            dst.SetTexture(texProp, tex);
            Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                    : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            c = new Color(Mathf.Clamp01(c.r * brightness), Mathf.Clamp01(c.g * brightness), Mathf.Clamp01(c.b * brightness), 1f);
            if (dst.HasProperty(colProp)) dst.SetColor(colProp, c);
        }

        public static void FillScatterMaterials(RSScatter s)
        {
            if (s.concrete == null) s.concrete = FindMaterial("MAT_Stage_Concrete");
            if (s.metal == null)    s.metal    = FindMaterial("MAT_Stage_Metal");
            if (s.rust == null)     s.rust     = FindMaterial("MAT_Stage_Rust");
            if (s.tire == null)     s.tire     = FindMaterial("MAT_Stage_Trim");
        }

        // ── 선택한 오브젝트를 지면에 붙이기 (다리 · 펜스 · 소품) ──

        [MenuItem("Tools/RE_AL STEEL/Stage/선택한 오브젝트를 지면에 붙이기", false, 21)]
        static void SnapSelection() { SnapSelectionToGround(false); }

        [MenuItem("Tools/RE_AL STEEL/Stage/선택한 오브젝트를 지면에 붙이기 (기울기 맞춤)", false, 22)]
        static void SnapSelectionAligned() { SnapSelectionToGround(true); }

        /// <summary>RS 지형 위로 선택한 오브젝트를 내려 앉힌다. 콜라이더 없이 지형 높이로 계산.</summary>
        public static void SnapSelectionToGround(bool alignToNormal)
        {
            int moved = 0;
            foreach (var tr in Selection.transforms)
            {
                if (tr.GetComponentInParent<RSTerrain>() != null) continue;   // 지형 자신·요소는 제외
                Vector3 hit = Vector3.zero; RSTerrain on = null; float best = float.MaxValue;
                var ray = new Ray(tr.position + Vector3.up * 200f, Vector3.down);
                foreach (var t in RSTerrain.All)
                {
                    Vector3 h;
                    if (t == null || !t.HasHeights || !t.Raycast(ray, out h)) continue;
                    float d = Mathf.Abs(h.y - tr.position.y);
                    if (d < best) { best = d; hit = h; on = t; }
                }
                if (on == null) continue;

                Undo.RecordObject(tr, "지면에 붙이기");
                tr.position = new Vector3(tr.position.x, hit.y, tr.position.z);
                if (alignToNormal)
                {
                    var l = on.transform.InverseTransformPoint(hit);
                    Vector3 nW = on.transform.TransformDirection(on.SampleNormal(l.x, l.z));
                    tr.rotation = Quaternion.FromToRotation(tr.up, nW) * tr.rotation;
                }
                moved++;
            }
            Debug.Log("[RS Terrain] 지면에 붙인 오브젝트 " + moved + "개" + (moved == 0 ? " (RS 지형 위에 있는 오브젝트를 선택하세요)" : ""));
        }

        /// <summary>씬 뷰 가운데를 지면에 쏜 위치 (지형 로컬). 못 맞추면 지형 가운데.</summary>
        static Vector3 SpawnLocal(RSTerrain t)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                Vector3 hit;
                var ray = sv.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (t.Raycast(ray, out hit)) return t.transform.InverseTransformPoint(hit);
                var l = t.transform.InverseTransformPoint(sv.pivot);
                if (t.InsideXZ(l.x, l.z)) return new Vector3(l.x, t.SampleHeight(l.x, l.z), l.z);
            }
            return new Vector3(0f, t.SampleHeight(0f, 0f), 0f);
        }

        public static T AddFeature<T>(RSTerrain t, string name) where T : RSTerrainFeature
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "RS 지형 요소 추가");
            go.transform.SetParent(t.transform, false);
            Vector3 p = SpawnLocal(t);
            if (typeof(T) != typeof(RSPad)) p.y = 0f;   // 작업장만 Y 가 윗면 높이
            go.transform.localPosition = p;
            var f = go.AddComponent<T>();
            f.salt = Random.Range(1, 999999);
            f.order = f.DefaultOrder;
            var sc = f as RSScatter;
            if (sc != null) FillScatterMaterials(sc);
            var fo = f as RSFoliage;
            if (fo != null) RSFoliageTools.SetupNew(fo);
            Selection.activeGameObject = go;
            t.MarkDirty();
            return f;
        }

        public static RSSpline AddSpline(RSTerrain t, RSSpline.Kind kind, bool ramp)
        {
            var s = AddFeature<RSSpline>(t, ramp ? "경사로" : kind == RSSpline.Kind.Trench ? "배수로" : "길");
            s.kind = kind;
            s.order = s.DefaultOrder;
            if (ramp)
            {
                s.heightMode = RSSpline.PathHeight.Points;
                s.halfWidth = 1.15f; s.feather = 0.3f; s.ruts = false; s.pathPaint = RSLayer.None;
                s.points = new List<Vector3> { new Vector3(0f, 0f, -2.5f), new Vector3(0f, 2f, 2.5f) };
                s.smooth = false;
            }
            else if (kind == RSSpline.Kind.Trench)
            {
                s.points = new List<Vector3> { new Vector3(-6f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(6f, 0f, 0f) };
            }
            t.MarkDirty();
            return s;
        }

        // ── ProBuilder 변환 ──

        public static void ConvertToProBuilder(RSTerrain t)
        {
            if (!t.HasHeights) t.Rebuild();
            if (!EditorUtility.DisplayDialog("ProBuilder 로 변환",
                "현재 지형을 ProBuilder 메시로 복사합니다 (정점 직접 편집용).\n\n" +
                "· 복사본은 원본과 연결이 끊깁니다 — 이후 요소를 옮겨도 복사본은 안 바뀝니다.\n" +
                "· 원본 RS 지형은 꺼 둡니다 (컴포넌트 비활성). 다시 켜면 되돌릴 수 있습니다.",
                "변환", "취소")) return;

            int vxN = t.VertexCountX, vzN = t.VertexCountZ;
            int cxN = vxN - 1, czN = vzN - 1;
            const int per = 80;   // 조각당 칸 (정점 65k 이하)

            var root = new GameObject(t.name + "_ProBuilder");
            Undo.RegisterCreatedObjectUndo(root, "RS 지형 ProBuilder 변환");
            root.transform.SetParent(t.transform.parent, false);
            root.transform.localPosition = t.transform.localPosition;
            root.transform.localRotation = t.transform.localRotation;
            root.transform.localScale = t.transform.localScale;

            for (int a = 0; a * per < cxN; a++)
                for (int b = 0; b * per < czN; b++)
                {
                    int i0 = a * per, i1 = Mathf.Min(cxN, i0 + per);
                    int j0 = b * per, j1 = Mathf.Min(czN, j0 + per);
                    var pos = new List<Vector3>();
                    var col = new List<Color>();
                    var faces = new List<Face>();
                    for (int i = i0; i < i1; i++)
                        for (int j = j0; j < j1; j++)
                        {
                            Vector3 va = t.GetVertex(i, j), vb = t.GetVertex(i + 1, j), vc = t.GetVertex(i + 1, j + 1), vd = t.GetVertex(i, j + 1);
                            int v0 = pos.Count;
                            pos.Add(va); pos.Add(vb); pos.Add(vc); pos.Add(vd);
                            col.Add(t.GetVertexColor(i, j)); col.Add(t.GetVertexColor(i + 1, j));
                            col.Add(t.GetVertexColor(i + 1, j + 1)); col.Add(t.GetVertexColor(i, j + 1));
                            bool diagAC = Mathf.Abs(va.y - vc.y) <= Mathf.Abs(vb.y - vd.y);
                            int[] idx = diagAC
                                ? new[] { v0, v0 + 3, v0 + 2, v0, v0 + 2, v0 + 1 }
                                : new[] { v0, v0 + 3, v0 + 1, v0 + 1, v0 + 3, v0 + 2 };
                            var f = new Face(idx);
                            f.smoothingGroup = 0;
                            faces.Add(f);
                        }

                    var pb = ProBuilderMesh.Create(pos, faces);
                    pb.name = "PB_" + a + "_" + b;
                    pb.transform.SetParent(root.transform, false);
                    pb.colors = col;
                    pb.GetComponent<MeshRenderer>().sharedMaterial = t.material;
                    pb.ToMesh();
                    pb.Refresh();
                    if (t.generateCollider)
                    {
                        var mc = pb.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = pb.GetComponent<MeshFilter>().sharedMesh;
                    }
                    Undo.RegisterCreatedObjectUndo(pb.gameObject, "RS 지형 ProBuilder 변환");
                }

            Undo.RecordObject(t, "RS 지형 끄기");
            t.enabled = false;
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Debug.Log("[RS Terrain] ProBuilder 변환 완료 → " + root.name + " (스커트·수면은 제외. 원본은 꺼 둠)");
        }
    }
}
