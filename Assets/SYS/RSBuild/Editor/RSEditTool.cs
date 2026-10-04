// RE:AL STEEL - 편집 메시 씬 도구 (RSEditMesh 를 고르면 씬 뷰 도구 막대에 '편집 메시' 가 생긴다)
//
//  만들기
//   D 그리기                   바닥이나 면 위를 끌어 밑면 → 마우스로 높이 → 클릭 (면 위에서 시작하면 그 면에서 솟는다)
//                             숫자 '1.5 0.5' Enter = 밑면 크기, 숫자 Enter = 높이, Shift+클릭 = 만들고 하나 더
//   Shift+A 도형 메뉴          상자 · 원기둥(각 수) · 판(양면) · 계단(칸 수) · 쐐기 · 지붕
//  편집 (Tab 으로 칠하기와 오가기)
//   1 점 · 2 선 · 3 면        고르는 단위.  클릭 = 고르기, Shift+클릭 = 더하기/빼기, 빈 곳 끌기 = 네모로 고르기
//   G 이동 · R 회전 · S 크기   누른 뒤 마우스. X/Y/Z = 축 고정, 숫자 = 정확한 값(m · 도 · 배), Ctrl = 스냅 끄기
//                             클릭 · Enter = 확정, 우클릭 · Esc = 취소
//   E 밀어내기                 고른 면을 법선 방향으로 뽑아낸다 (그 다음은 이동과 같다)
//   K 자르기                   마우스 높이에서 부품을 자른다 (X/Y/Z = 자르는 방향, Shift+클릭 = 전체)
//   X · Delete 지우기  M 합치기(점)  N 면 뒤집기  Shift+D 복제
//   A 전체 고르기/풀기  L 마우스 밑 부품 고르기  [ ] 스냅 간격  Shift+우클릭 = 3D 커서 놓기
//  칠하기 (P)
//   면 클릭 = 재질 붙이기, 끌면 연속, Shift+클릭 = 부품 전체, Alt+클릭 = 그 면 칠 집기
//   반복 = 재질 타일 (1m = 32px, 결 자동이면 긴 쪽으로 나뭇결)  ·  딱 맞게 = 그림 한 장을 그 면(들)에 꼭 맞게
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    public enum RSESelMode { 점, 선, 면 }
    public enum RSEToolMode { 편집, 칠하기 }

    [InitializeOnLoad]
    public static class RSEditCore
    {
        enum Modal { 없음, 이동, 회전, 크기, 자르기, 네모, 그리기 }

        public static RSEditMesh target;
        public static RSEToolMode toolMode = RSEToolMode.편집;
        public static RSESelMode selMode = RSESelMode.면;
        public static readonly HashSet<int> selV = new HashSet<int>();
        public static readonly HashSet<int> selF = new HashSet<int>();
        public static readonly HashSet<long> selE = new HashSet<long>();

        static readonly float[] snapSteps = { 1f / 32f, 0.125f, 0.25f, 0.5f, 1f };
        static int SnapIdx { get { return Mathf.Clamp(EditorPrefs.GetInt("RS_EditSnap", 0), 0, snapSteps.Length - 1); } set { EditorPrefs.SetInt("RS_EditSnap", Mathf.Clamp(value, 0, snapSteps.Length - 1)); } }
        public static float SnapStep { get { return snapSteps[SnapIdx]; } }

        // 3D 커서 (편집 메시 로컬)
        static Vector3 cursorL;
        static bool cursorSet;

        // 칠 붓
        public static RSEFace brush = new RSEFace();
        static bool grainAuto = true;
        static int fitBorder = 3;
        static int lastPaintFace = -1;
        static Vector2 paletteScroll;

        // 호버
        static int hoverFace = -1, hoverVert = -1;
        static Vector2Int hoverEdge = new Vector2Int(-1, -1);
        static Vector3 hoverPointW, hoverNormalW;
        static Vector2 mouse;

        // 모달 (G R S K)
        static Modal modal;
        static int axis = -1;                 // 0 X · 1 Y · 2 Z (로컬)
        static bool customAxis;               // 밀어내기: 법선 방향
        static Vector3 customAxisL;
        static string numeric = "";
        static readonly List<int> moving = new List<int>();
        static Vector3[] startPos;
        static Vector3 pivotL;
        static Vector2 startMouse;
        static bool ctrlHeld;
        static string modalInfo = "";
        static bool painting;
        static Vector2 boxStart;

        static readonly List<Vector2Int> edges = new List<Vector2Int>();
        static int edgesVersion = -1;
        static RSEditMesh edgesOwner;
        static string status = "";

        static RSEditCore()
        {
            Undo.undoRedoPerformed += () =>
            {
                CancelModal(false);
                foreach (var m in Object.FindObjectsByType<RSEditMesh>(FindObjectsSortMode.None)) m.MarkDirty();
                Sanitize();
                SceneView.RepaintAll();
            };
            RegisterContext();
            Selection.selectionChanged += () => { if (modal != Modal.없음) CancelModal(); };
        }

        /// <summary>다른 편집 메시로 바뀌면 선택 · 모달을 정리</summary>
        public static void SetTarget(RSEditMesh m)
        {
            if (target == m) return;
            CancelModal(false);
            target = m;
            ClearSel(); cursorSet = false; lastPaintFace = -1; seenVersion = -1;
            hoverFace = -1; hoverVert = -1; hoverEdge = new Vector2Int(-1, -1);
        }

        public static void Deactivated()
        {
            CancelModal();
            EndPaintStroke();
        }

        /// <summary>도구 밖에서 점 · 면 번호가 바뀌었을 때 (정리 · 가져오기)</summary>
        public static void ClearSelection()
        {
            CancelModal(false);
            ClearSel(); lastPaintFace = -1;
            hoverFace = -1; hoverVert = -1; hoverEdge = new Vector2Int(-1, -1);
            SceneView.RepaintAll();
        }

        static int seenVersion = -1;
        static bool eatContextClick;

        static bool registered;
        static void RegisterContext()
        {
            if (registered) return;
            try { ShortcutManager.RegisterContext(context); registered = true; }
            catch (System.Exception) { /* 단축키 시스템이 아직 준비 전 — 씬 GUI 에서 다시 */ }
        }

        static readonly RSEditShortcutContext context = new RSEditShortcutContext();

        public static bool ToolActive { get { return ToolManager.activeToolType == typeof(RSEditTool) && target != null; } }

        static long EKey(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
        static Vector2Int EUnpack(long k) { return new Vector2Int((int)(k >> 32), (int)(k & 0xffffffff)); }

        static void Sanitize()
        {
            if (target == null) { selV.Clear(); selF.Clear(); selE.Clear(); return; }
            selV.RemoveWhere(v => v < 0 || v >= target.verts.Count);
            selF.RemoveWhere(f => f < 0 || f >= target.faces.Count);
            selE.RemoveWhere(k => { var e = EUnpack(k); return e.x < 0 || e.y < 0 || e.x >= target.verts.Count || e.y >= target.verts.Count; });
        }

        static void ClearSel() { selV.Clear(); selF.Clear(); selE.Clear(); }

        static List<Vector2Int> Edges()
        {
            if (edgesOwner != target || edgesVersion != target.version) { RSEditOps.Edges(target, edges); edgesOwner = target; edgesVersion = target.version; }
            return edges;
        }

        /// <summary>고른 것 → 점 집합</summary>
        static HashSet<int> SelVerts()
        {
            var s = new HashSet<int>();
            switch (selMode)
            {
                case RSESelMode.점: s.UnionWith(selV); break;
                case RSESelMode.선: foreach (var k in selE) { var e = EUnpack(k); s.Add(e.x); s.Add(e.y); } break;
                default: foreach (int f in selF) if (target.ValidFace(f)) s.UnionWith(target.faces[f].v); break;
            }
            return s;
        }

        /// <summary>고른 것 → 면 집합 (점 · 선 모드는 점이 모두 고른 면)</summary>
        static HashSet<int> SelFaces()
        {
            if (selMode == RSESelMode.면) return new HashSet<int>(selF);
            var vs = SelVerts();
            var res = new HashSet<int>();
            for (int f = 0; f < target.faces.Count; f++)
            {
                if (!target.ValidFace(f)) continue;
                bool all = true;
                foreach (int vi in target.faces[f].v) if (!vs.Contains(vi)) { all = false; break; }
                if (all) res.Add(f);
            }
            return res;
        }

        // ─────────────────────────────────────────────────────────────
        // 씬 GUI
        // ─────────────────────────────────────────────────────────────

        public static void OnGUI(RSEditMesh m, EditorWindow window)
        {
            if (m == null || !(window is SceneView sv)) return;
            RegisterContext();
            SetTarget(m);
            if (seenVersion != target.version && modal == Modal.없음) { Sanitize(); if (!target.ValidFace(lastPaintFace)) lastPaintFace = -1; seenVersion = target.version; }
            if (drawPendingFor != null && drawPendingFor == target) { drawPendingFor = null; DoDraw(null); }
            if (shapeMenuPending && Event.current.type == EventType.Layout && sv == SceneView.lastActiveSceneView)
            {
                shapeMenuPending = false;
                mouse = Event.current.mousePosition;
                Handles.BeginGUI(); ShowShapeMenu(); Handles.EndGUI();
            }
            if (hoverFace >= target.faces.Count) hoverFace = -1;
            if (hoverVert >= target.verts.Count) hoverVert = -1;
            if (hoverEdge.x >= target.verts.Count || hoverEdge.y >= target.verts.Count) hoverEdge = new Vector2Int(-1, -1);
            var e = Event.current;
            mouse = e.mousePosition;
            ctrlHeld = e.control || e.command;

            if (painting && e.rawType == EventType.MouseUp) EndPaintStroke();
            if (e.type == EventType.ContextClick && modal != Modal.그리기 && (eatContextClick || modal != Modal.없음 || e.shift)) { eatContextClick = false; e.Use(); }

            Commands(e, sv);
            DrawPanel(sv);
            bool overPanel = (modal == Modal.없음 || (modal == Modal.그리기 && drawStep == DrawStep.대기)) && GUIUtility.hotControl == 0 && PanelRect(sv).Contains(e.mousePosition);
            int ctrl = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(ctrl);   // 씬 클릭이 다른 오브젝트를 고르지 않게
            if (overPanel)
            {
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag) e.Use();
                if (e.type == EventType.Repaint) Draw(sv);
                DrawStatus(sv);
                return;
            }

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.MouseDown) UpdateHover(sv);

            if (modal == Modal.네모) BoxGUI(e, sv);
            else if (modal == Modal.이동 || modal == Modal.회전 || modal == Modal.크기) ModalGUI(e, sv, ctrl);
            else if (modal == Modal.자르기) CutGUI(e, sv, ctrl);
            else if (modal == Modal.그리기) DrawGUI(e, sv, ctrl);
            else if (toolMode == RSEToolMode.칠하기) PaintGUI(e, ctrl);
            else SelectGUI(e, sv, ctrl);

            // 3D 커서: Shift + 우클릭
            if (e.type == EventType.MouseDown && e.button == 1 && e.shift && modal == Modal.없음)
            {
                if (hoverFace >= 0) cursorL = target.transform.InverseTransformPoint(hoverPointW);
                else if (GroundPoint(HandleUtility.GUIPointToWorldRay(e.mousePosition), out var gp)) cursorL = gp;
                cursorL = SnapV(cursorL);
                cursorSet = true;
                eatContextClick = true;
                e.Use();
            }

            if (e.type == EventType.Repaint) Draw(sv);
            DrawStatus(sv);
        }

        static void Commands(Event e, SceneView sv)
        {
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand) return;
            string c = e.commandName;
            bool mine = c == "SoftDelete" || c == "Delete" || c == "Duplicate" || c == "SelectAll" || c == "FrameSelected" || c == "DeselectAll";
            if (!mine) return;
            if (e.type == EventType.ValidateCommand) { e.Use(); return; }
            switch (c)
            {
                case "SoftDelete": case "Delete": DoDelete(); break;
                case "Duplicate": DoDuplicate(); break;
                case "SelectAll": SelectAll(true); break;
                case "DeselectAll": ClearSel(); break;
                case "FrameSelected": Frame(sv); break;
            }
            e.Use();
        }

        static void Frame(SceneView sv)
        {
            var vs = SelVerts();
            if (vs.Count == 0)
            {
                var mr = target.GetComponentInChildren<MeshRenderer>();
                var bb = mr != null && target.verts.Count > 0 ? mr.bounds : new Bounds(target.transform.position, Vector3.one * 2f);
                sv.Frame(bb, false);
                return;
            }
            var b = new Bounds(target.transform.TransformPoint(target.verts[First(vs)]), Vector3.zero);
            foreach (int v in vs) b.Encapsulate(target.transform.TransformPoint(target.verts[v]));
            b.Expand(0.2f);
            sv.Frame(b, false);
        }

        static int First(HashSet<int> s) { foreach (int x in s) return x; return -1; }

        // ─────────────────────────────────────────────────────────────
        // 호버
        // ─────────────────────────────────────────────────────────────

        static void UpdateHover(SceneView sv)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            hoverFace = -1; hoverVert = -1; hoverEdge = new Vector2Int(-1, -1);
            float hitDist = float.MaxValue;
            if (target.Raycast(ray, out int f, out var p, out var n, out float dist)) { hoverFace = f; hoverPointW = p; hoverNormalW = n; hitDist = dist; }
            if (toolMode != RSEToolMode.편집 || modal != Modal.없음) return;

            var tr = target.transform;
            Vector3 camPos = sv.camera.transform.position;
            bool Visible(Vector3 w)
            {
                float d = Vector3.Distance(camPos, w);
                return hitDist == float.MaxValue || d <= hitDist + Mathf.Max(0.01f, d * 0.01f);
            }
            if (selMode == RSESelMode.점)
            {
                float best = 12f;
                for (int i = 0; i < target.verts.Count; i++)
                {
                    Vector3 w = tr.TransformPoint(target.verts[i]);
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(w), mouse);
                    if (d < best && Visible(w)) { best = d; hoverVert = i; }
                }
            }
            else if (selMode == RSESelMode.선)
            {
                float best = 8f;
                foreach (var ed in Edges())
                {
                    Vector3 a = tr.TransformPoint(target.verts[ed.x]), b = tr.TransformPoint(target.verts[ed.y]);
                    float d = HandleUtility.DistancePointLine(mouse, HandleUtility.WorldToGUIPoint(a), HandleUtility.WorldToGUIPoint(b));
                    if (d < best && Visible((a + b) * 0.5f)) { best = d; hoverEdge = ed; }
                }
            }
        }

        static bool GroundPoint(Ray worldRay, out Vector3 local)
        {
            var tr = target.transform;
            Vector3 o = tr.InverseTransformPoint(worldRay.origin), d = tr.InverseTransformDirection(worldRay.direction);
            float planeY = cursorSet ? cursorL.y : 0f;
            local = Vector3.zero;
            if (Mathf.Abs(d.y) < 1e-6f) return false;
            float t = (planeY - o.y) / d.y;
            if (t < 0) return false;
            local = o + d * t;
            return true;
        }

        static float Snap(float v, float step) { return ctrlHeld ? v : Mathf.Round(v / step) * step; }
        static Vector3 SnapV(Vector3 v) { float s = SnapStep; return ctrlHeld ? v : new Vector3(Mathf.Round(v.x / s) * s, Mathf.Round(v.y / s) * s, Mathf.Round(v.z / s) * s); }

        // ─────────────────────────────────────────────────────────────
        // 고르기
        // ─────────────────────────────────────────────────────────────

        static void SelectGUI(Event e, SceneView sv, int ctrl)
        {
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    {
                        GUIUtility.keyboardControl = 0;
                        bool any = false;
                        if (selMode == RSESelMode.점 && hoverVert >= 0) { Toggle(selV, hoverVert, e.shift); any = true; }
                        else if (selMode == RSESelMode.선 && hoverEdge.x >= 0) { Toggle(selE, EKey(hoverEdge.x, hoverEdge.y), e.shift); any = true; }
                        else if (selMode == RSESelMode.면 && hoverFace >= 0) { Toggle(selF, hoverFace, e.shift); any = true; }
                        if (!any)
                        {
                            // 빈 곳: 네모로 고르기 시작
                            modal = Modal.네모; boxStart = e.mousePosition;
                            GUIUtility.hotControl = ctrl;
                        }
                        e.Use();
                    }
                    break;
            }
        }

        static void Toggle<T>(HashSet<T> set, T item, bool add)
        {
            if (!add) { set.Clear(); set.Add(item); return; }
            if (!set.Remove(item)) set.Add(item);
        }

        static void BoxGUI(Event e, SceneView sv)
        {
            if (modal != Modal.네모) return;
            if (e.type == EventType.MouseDrag) { sv.Repaint(); e.Use(); }
            else if ((e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) && e.button == 0)
            {
                var r = Rect.MinMaxRect(Mathf.Min(boxStart.x, mouse.x), Mathf.Min(boxStart.y, mouse.y), Mathf.Max(boxStart.x, mouse.x), Mathf.Max(boxStart.y, mouse.y));
                if (!e.shift) ClearSel();
                if (r.width > 3 || r.height > 3)
                {
                    var tr = target.transform;
                    bool In(Vector3 l) { return r.Contains(HandleUtility.WorldToGUIPoint(tr.TransformPoint(l))); }
                    if (selMode == RSESelMode.점) { for (int i = 0; i < target.verts.Count; i++) if (In(target.verts[i])) selV.Add(i); }
                    else if (selMode == RSESelMode.선) { foreach (var ed in Edges()) if (In(target.verts[ed.x]) && In(target.verts[ed.y])) selE.Add(EKey(ed.x, ed.y)); }
                    else for (int f = 0; f < target.faces.Count; f++) if (target.ValidFace(f) && In(target.FaceCenter(f))) selF.Add(f);
                }
                modal = Modal.없음;
                GUIUtility.hotControl = 0;
                e.Use();
            }
            else if (e.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                var r = Rect.MinMaxRect(Mathf.Min(boxStart.x, mouse.x), Mathf.Min(boxStart.y, mouse.y), Mathf.Max(boxStart.x, mouse.x), Mathf.Max(boxStart.y, mouse.y));
                EditorGUI.DrawRect(r, new Color(1f, 0.7f, 0.2f, 0.12f));
                Handles.color = new Color(1f, 0.7f, 0.2f, 0.9f);
                Handles.DrawAAPolyLine(1.5f, new Vector3(r.xMin, r.yMin), new Vector3(r.xMax, r.yMin), new Vector3(r.xMax, r.yMax), new Vector3(r.xMin, r.yMax), new Vector3(r.xMin, r.yMin));
                Handles.EndGUI();
            }
        }

        static void SelectAll(bool force)
        {
            bool anySel = selV.Count + selE.Count + selF.Count > 0;
            ClearSel();
            if (anySel && !force) return;
            if (selMode == RSESelMode.점) for (int i = 0; i < target.verts.Count; i++) selV.Add(i);
            else if (selMode == RSESelMode.선) foreach (var ed in Edges()) selE.Add(EKey(ed.x, ed.y));
            else for (int f = 0; f < target.faces.Count; f++) if (target.ValidFace(f)) selF.Add(f);
        }

        // ─────────────────────────────────────────────────────────────
        // 모달: 이동 · 회전 · 크기
        // ─────────────────────────────────────────────────────────────

        static bool StartModal(Modal kind, string undoName, HashSet<int> verts = null)
        {
            if (target == null) return false;
            var vs = verts ?? SelVerts();
            if (vs.Count == 0) { status = "고른 것이 없습니다 (클릭으로 고르거나 A = 전체)"; return false; }
            modalUndoName = undoName;
            modalStructural = verts != null;
            moving.Clear(); moving.AddRange(vs);
            startPos = new Vector3[moving.Count];
            pivotL = Vector3.zero;
            for (int i = 0; i < moving.Count; i++) { startPos[i] = target.verts[moving[i]]; pivotL += startPos[i]; }
            pivotL /= moving.Count;
            startMouse = mouse;
            axis = -1; numeric = ""; modal = kind; lastShortcutDigit = '\0';
            customAxis = false;
            status = "";
            return true;
        }

        // 모달 Undo: G R S 는 확정할 때 한 번 기록. 밀어내기 · 복제는 시작 전 그룹으로 묶고, 취소하면 통째로 되돌린다
        static string modalUndoName = "";
        static bool modalStructural;
        static int modalUndoGroup = -1;
        static int structVerts, structFaces;

        static void BeginStructural(string name)
        {
            modalUndoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(target, name);
            structVerts = target.verts.Count; structFaces = target.faces.Count;
        }

        static void CancelModal(bool repaint = true)
        {
            var was = modal;
            bool structural = modalStructural;
            modal = Modal.없음; modalStructural = false;   // 되돌리기가 부르는 undoRedoPerformed 에서 다시 들어오지 않게
            if (was == Modal.이동 || was == Modal.회전 || was == Modal.크기)
            {
                if (target != null && structural && modalUndoGroup >= 0)
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(modalUndoGroup);
                    target.skipCollider = false;
                    target.MarkDirty(); target.Rebuild();
                    ClearSel(); Sanitize();
                }
                else if (target != null && startPos != null && startPos.Length == moving.Count)
                {
                    for (int i = 0; i < moving.Count; i++) if (moving[i] < target.verts.Count) target.verts[moving[i]] = startPos[i];
                    target.skipCollider = false;
                    target.MarkDirty(); target.Rebuild();
                }
            }
            modal = Modal.없음; customAxis = false; numeric = ""; modalStructural = false; modalUndoGroup = -1;
            if (GUIUtility.hotControl != 0) GUIUtility.hotControl = 0;
            if (repaint) SceneView.RepaintAll();
        }

        static void ConfirmModal()
        {
            if (target != null && startPos != null && startPos.Length == moving.Count)
            {
                // 마지막 위치를 보관 → 처음 위치로 → 기록 → 마지막 위치 (그래야 Undo 가 이 변경을 담는다)
                var fin = new Vector3[moving.Count];
                for (int i = 0; i < moving.Count; i++) { fin[i] = target.verts[moving[i]]; target.verts[moving[i]] = startPos[i]; }
                Undo.RecordObject(target, modalUndoName);
                for (int i = 0; i < moving.Count; i++) target.verts[moving[i]] = fin[i];
                if (modalStructural && customAxis) RSEditOps.Compact(target);   // 밀어내기로 안 쓰이게 된 원래 점 정리
                if (modalStructural && modalUndoGroup >= 0) Undo.CollapseUndoOperations(modalUndoGroup);
                target.skipCollider = false;
                target.MarkDirty(); EditorUtility.SetDirty(target);
                Sanitize();
            }
            modal = Modal.없음; customAxis = false; numeric = ""; modalStructural = false; modalUndoGroup = -1;
            SceneView.RepaintAll();
        }

        static bool NumericValue(out float v)
        {
            v = 0;
            if (string.IsNullOrEmpty(numeric) || numeric == "-" || numeric == ".") return false;
            return float.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        static Vector3 AxisL(int a) { var v = Vector3.zero; v[a] = 1f; return v; }

        static float ClosestOnLine(Vector3 p, Vector3 dir, Ray ray)
        {
            Vector3 w0 = p - ray.origin;
            float a = Vector3.Dot(dir, dir), b = Vector3.Dot(dir, ray.direction), c = Vector3.Dot(ray.direction, ray.direction);
            float d = Vector3.Dot(dir, w0), e2 = Vector3.Dot(ray.direction, w0);
            float den = a * c - b * b;
            if (Mathf.Abs(den) < 1e-8f) return 0f;
            return (b * e2 - c * d) / den;
        }

        static void ModalGUI(Event e, SceneView sv, int ctrl)
        {
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(ctrl);
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) { ApplyModal(sv); e.Use(); }
            else if (e.type == EventType.MouseDown && e.button == 0) { ApplyModal(sv); ConfirmModal(); e.Use(); }
            else if (e.type == EventType.MouseDown && e.button == 1) { CancelModal(); eatContextClick = true; e.Use(); }
            else if (e.type == EventType.KeyDown)
            {
                var k = e.keyCode;
                char ch = e.character;
                if (k == KeyCode.Escape) { CancelModal(); e.Use(); }
                else if (k == KeyCode.Return || k == KeyCode.KeypadEnter) { ApplyModal(sv); ConfirmModal(); e.Use(); }
                else if (k == KeyCode.Backspace) { if (numeric.Length > 0) numeric = numeric.Substring(0, numeric.Length - 1); ApplyModal(sv); e.Use(); }
                else if (ch != 0 && ((ch >= '0' && ch <= '9') || ch == '.' || ch == '-'))
                {
                    // 1 · 2 · 3 은 단축키가 먼저 넣었다 (같은 키의 문자 이벤트는 건너뜀)
                    if (ch == lastShortcutDigit) lastShortcutDigit = '\0';
                    else if (ch != '-' || numeric.Length == 0) numeric += ch;
                    ApplyModal(sv); e.Use();
                }
                else if (k == KeyCode.LeftControl || k == KeyCode.RightControl || k == KeyCode.LeftCommand) { ApplyModal(sv); }
            }
            else if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl || e.keyCode == KeyCode.LeftCommand)) { ApplyModal(sv); }
        }

        /// <summary>숫자 키 (단축키 1 2 3 이 모달 중에는 숫자로)</summary>
        static char lastShortcutDigit;
        public static void TypeDigit(char c) { numeric += c; lastShortcutDigit = c; var sv = SceneView.lastActiveSceneView; if (sv != null) { if (modal != Modal.그리기) ApplyModal(sv); sv.Repaint(); } }

        static void ApplyModal(SceneView sv)
        {
            if (target == null || startPos == null) return;
            var tr = target.transform;
            var cam = sv.camera;
            bool hasNum = NumericValue(out float num);
            switch (modal)
            {
                case Modal.이동:
                    {
                        Vector3 delta;
                        if (axis >= 0 || customAxis)
                        {
                            Vector3 dirL = customAxis && axis < 0 ? customAxisL : AxisL(axis);
                            float dist;
                            if (hasNum) dist = num;
                            else
                            {
                                Vector3 pw = tr.TransformPoint(pivotL), dw = tr.TransformVector(dirL);
                                float len = Mathf.Max(1e-6f, dw.magnitude);
                                dw /= len;
                                float t = ClosestOnLine(pw, dw, HandleUtility.GUIPointToWorldRay(mouse)) - ClosestOnLine(pw, dw, HandleUtility.GUIPointToWorldRay(startMouse));
                                dist = Snap(t / len, SnapStep);
                            }
                            delta = dirL * dist;
                            modalInfo = (customAxis && axis < 0 ? "법선" : "XYZ".Substring(axis, 1) + "축") + " " + Len(dist);
                        }
                        else
                        {
                            if (hasNum) delta = new Vector3(num, 0, 0);
                            else
                            {
                                Vector3 pw = tr.TransformPoint(pivotL);
                                var plane = new Plane(-cam.transform.forward, pw);
                                var r0 = HandleUtility.GUIPointToWorldRay(startMouse); var r1 = HandleUtility.GUIPointToWorldRay(mouse);
                                if (!plane.Raycast(r0, out float t0) || !plane.Raycast(r1, out float t1)) return;
                                delta = tr.InverseTransformVector(r1.GetPoint(t1) - r0.GetPoint(t0));
                                delta = SnapV(delta);
                            }
                            modalInfo = Len(delta.x) + " , " + Len(delta.y) + " , " + Len(delta.z);
                        }
                        for (int i = 0; i < moving.Count; i++) target.verts[moving[i]] = startPos[i] + delta;
                    }
                    break;
                case Modal.회전:
                    {
                        Vector3 axL;
                        Vector2 pc = HandleUtility.WorldToGUIPoint(tr.TransformPoint(pivotL));
                        float ang;
                        if (axis >= 0)
                        {
                            axL = AxisL(axis);
                            Vector3 axW = tr.TransformDirection(axL);
                            ang = Vector2.SignedAngle(startMouse - pc, mouse - pc);
                            if (Vector3.Dot(axW, -cam.transform.forward) < 0) ang = -ang;
                        }
                        else
                        {
                            axL = tr.InverseTransformDirection(-cam.transform.forward).normalized;
                            ang = Vector2.SignedAngle(startMouse - pc, mouse - pc);
                        }
                        if (hasNum) ang = num;
                        else ang = ctrlHeld ? ang : Mathf.Round(ang / 15f) * 15f;
                        var q = Quaternion.AngleAxis(ang, axL);
                        for (int i = 0; i < moving.Count; i++) target.verts[moving[i]] = pivotL + q * (startPos[i] - pivotL);
                        modalInfo = (axis >= 0 ? "XYZ".Substring(axis, 1) + "축 " : "화면 축 ") + ang.ToString("0.#") + "°";
                    }
                    break;
                case Modal.크기:
                    {
                        Vector2 pc = HandleUtility.WorldToGUIPoint(tr.TransformPoint(pivotL));
                        float d0 = Mathf.Max(4f, (startMouse - pc).magnitude);
                        float s = hasNum ? num : (mouse - pc).magnitude / d0;
                        if (!hasNum && !ctrlHeld) s = Mathf.Round(s * 10f) / 10f;
                        Vector3 sv3 = axis >= 0 ? Vector3.one : new Vector3(s, s, s);
                        if (axis >= 0) sv3[axis] = s;
                        for (int i = 0; i < moving.Count; i++) target.verts[moving[i]] = pivotL + Vector3.Scale(startPos[i] - pivotL, sv3);
                        modalInfo = (axis >= 0 ? "XYZ".Substring(axis, 1) + "축 " : "") + "× " + s.ToString("0.###");
                    }
                    break;
            }
            target.skipCollider = true;
            target.MarkDirty();
            target.Rebuild();
            sv.Repaint();
        }

        static string Len(float m)
        {
            return m.ToString("0.###", CultureInfo.InvariantCulture) + "m (" + Mathf.RoundToInt(m * RSSurface.PixelsPerMeter) + "px)";
        }

        // ─────────────────────────────────────────────────────────────
        // 자르기 (K)
        // ─────────────────────────────────────────────────────────────

        static int cutAxis = 1;

        static void CutGUI(Event e, SceneView sv, int ctrl)
        {
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(ctrl);
            if (e.type == EventType.MouseMove) { sv.Repaint(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { modal = Modal.없음; e.Use(); sv.Repaint(); return; }
            if (e.type == EventType.MouseDown && e.button == 1) { modal = Modal.없음; eatContextClick = true; e.Use(); return; }
            if (e.type == EventType.MouseDown && e.button == 0 && hoverFace >= 0)
            {
                Vector3 pL = CutPoint();
                HashSet<int> set;
                if (e.shift) { set = new HashSet<int>(); for (int f = 0; f < target.faces.Count; f++) set.Add(f); }
                else if (selMode == RSESelMode.면 && selF.Count > 0 && selF.Contains(hoverFace)) set = new HashSet<int>(selF);
                else set = RSEditOps.Linked(target, hoverFace);
                Undo.RecordObject(target, "자르기");
                var made = RSEditOps.Cut(target, set, pL, AxisL(cutAxis));
                target.MarkDirty(); EditorUtility.SetDirty(target);
                status = made.Count > 0 ? "면 " + made.Count + "개를 잘랐습니다" : "자를 면이 없습니다 (그 높이를 지나는 면이 없음)";
                ClearSel(); Sanitize();
                modal = Modal.없음;
                e.Use();
            }
        }

        static Vector3 CutPoint()
        {
            Vector3 pL = target.transform.InverseTransformPoint(hoverPointW);
            pL[cutAxis] = Snap(pL[cutAxis], SnapStep);
            return pL;
        }

        // ─────────────────────────────────────────────────────────────
        // 그려서 만들기 (D · Shift+A 도형 메뉴)
        //  대기: 바닥이나 면 위에 마우스 → 놓일 자리 미리보기 (면 위면 그 면에서 솟는다)
        //  밑면: 끌어 놓기, 또는 클릭 → 마우스 → 클릭.  숫자 "1.2 0.8" Enter = 정확한 크기
        //  높이: 마우스 위아래, 숫자 Enter.  클릭 = 만들기 (Shift+클릭 = 하나 더)
        // ─────────────────────────────────────────────────────────────

        enum DrawStep { 대기, 밑면, 높이 }
        public static RSEShape drawShape = RSEShape.상자;
        static int drawSides = 8, drawSteps = 4;
        static DrawStep drawStep;
        static Vector3 dO, dN = Vector3.up, dA = Vector3.right, dB = Vector3.forward;   // 고정된 평면 (로컬)
        static Vector3 hO, hN = Vector3.up, hA = Vector3.right, hB = Vector3.forward;   // 마우스 밑 평면 (대기)
        static Vector2 dStart, dEnd, dHover;
        static bool dHoverOk, dDragging, dHoverOnFace, dOnFace;
        static float dHeight;
        static Vector2 dMouseDown;
        static bool shapeMenuPending;
        static RSEditMesh drawPendingFor;
        static readonly List<Vector3[]> previewPolys = new List<Vector3[]>();

        static readonly string[] shapeNames = { "상자", "원기둥", "판", "계단", "쐐기", "지붕" };

        public static void DoDraw(RSEShape? s)
        {
            if (target == null) return;
            if (modal != Modal.없음 && modal != Modal.그리기) return;
            if (painting) return;
            if (dDragging) { dDragging = false; GUIUtility.hotControl = 0; }
            if (s.HasValue) drawShape = s.Value;
            toolMode = RSEToolMode.편집;
            modal = Modal.그리기; drawStep = DrawStep.대기; numeric = ""; dDragging = false;
            status = "";
            SceneView.RepaintAll();
        }

        public static void ShapeMenu() { if (modal == Modal.없음 || modal == Modal.그리기) { shapeMenuPending = true; SceneView.RepaintAll(); } }

        /// <summary>새 편집 메시: 도구가 켜지면 바로 그리기</summary>
        public static void DrawWhenReady(RSEditMesh m) { drawPendingFor = m; }

        static void ShowShapeMenu()
        {
            var gm = new GenericMenu();
            for (int i = 0; i < shapeNames.Length; i++)
            {
                var sh = (RSEShape)i;
                gm.AddItem(new GUIContent(shapeNames[i] + " 그리기"), drawShape == sh && modal == Modal.그리기, () => DoDraw(sh));
            }
            gm.AddSeparator("");
            gm.AddItem(new GUIContent("1m 상자 바로 놓기 (3D 커서 자리)"), false, () => { if (modal == Modal.그리기) modal = Modal.없음; DoAddBox(); });
            gm.DropDown(new Rect(mouse, Vector2.zero));
        }

        static bool RayPlane(Ray worldRay, Vector3 O, Vector3 N, Vector3 A, Vector3 B, out Vector2 ab)
        {
            ab = Vector2.zero;
            var tr = target.transform;
            Vector3 o = tr.InverseTransformPoint(worldRay.origin), d = tr.InverseTransformDirection(worldRay.direction);
            float den = Vector3.Dot(d, N);
            if (Mathf.Abs(den) < 1e-6f) return false;
            float t = Vector3.Dot(O - o, N) / den;
            if (t < 0) return false;
            Vector3 p = o + d * t - O;
            ab = new Vector2(Snap(Vector3.Dot(p, A), SnapStep), Snap(Vector3.Dot(p, B), SnapStep));
            return true;
        }

        static void HoverPlane()
        {
            // 면 위면 그 면 평면, 아니면 바닥 (로컬 y = 0, 3D 커서가 있으면 그 높이)
            dHoverOnFace = hoverFace >= 0;
            if (dHoverOnFace)
            {
                hN = target.FaceNormal(hoverFace);
                Vector3 hp = target.transform.InverseTransformPoint(hoverPointW);
                hO = hN * Vector3.Dot(hp, hN);
            }
            else { hN = Vector3.up; hO = new Vector3(0, cursorSet ? cursorL.y : 0f, 0); }
            RSEditMesh.Frame(hN, out hA, out hB);
            if (Mathf.Abs(hN.y) > 0.98f)
            {
                // 거의 수평인 면: 로컬 X · Z 를 평면에 눕혀서 (기울어도 밑면이 면에 붙게)
                hA = Vector3.ProjectOnPlane(Vector3.right, hN).normalized;
                hB = Vector3.Cross(hA, hN).normalized;
                if (Vector3.Dot(hB, Vector3.forward) < 0) hB = -hB;
            }
            dHoverOk = RayPlane(HandleUtility.GUIPointToWorldRay(mouse), hO, hN, hA, hB, out dHover);
        }

        static List<float> NumList()
        {
            var res = new List<float>();
            foreach (var part in numeric.Split(new[] { ' ', ',', 'x', '*' }, System.StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) res.Add(v);
            return res;
        }

        /// <summary>숫자 입력을 반영한 밑면 끝점 · 높이</summary>
        static void DrawValues(out Vector2 end, out float h)
        {
            end = dEnd; h = dHeight;
            var nums = NumList();
            if (nums.Count == 0) return;
            if (drawStep == DrawStep.밑면)
            {
                Vector2 dir = dEnd - dStart;
                if (drawShape == RSEShape.판)
                {
                    Vector2 u = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.right;
                    end = dStart + u * nums[0];
                }
                else
                {
                    float w = nums[0], d = nums.Count > 1 ? nums[1] : nums[0];
                    end = dStart + new Vector2(dir.x < 0 ? -w : w, dir.y < 0 ? -d : d);
                }
            }
            else if (drawStep == DrawStep.높이) h = nums[0];
        }

        static void UpdateDraw()
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (drawStep == DrawStep.대기) HoverPlane();
            else if (drawStep == DrawStep.밑면) { if (RayPlane(ray, dO, dN, dA, dB, out var ab)) dEnd = ab; }
            else
            {
                Vector2 lo = Vector2.Min(dStart, dEnd), hi = Vector2.Max(dStart, dEnd);
                if (drawShape == RSEShape.판) { lo = (dStart + dEnd) * 0.5f; hi = lo; }
                Vector3 baseL = dO + dA * ((lo.x + hi.x) * 0.5f) + dB * ((lo.y + hi.y) * 0.5f);
                var tr = target.transform;
                Vector3 nW = tr.TransformVector(dN);
                float len = Mathf.Max(1e-6f, nW.magnitude);
                float t = ClosestOnLine(tr.TransformPoint(baseL), nW / len, ray) / len;
                float h = Snap(t, SnapStep);
                if (Mathf.Abs(h) < SnapStep * 0.5f) h = h < 0 ? -SnapStep : SnapStep;
                dHeight = h;
            }
        }

        static bool BaseOk(Vector2 end)
        {
            Vector2 d = end - dStart;
            float half = Mathf.Max(RSEditMesh.Pixel, SnapStep * 0.5f);
            if (drawShape == RSEShape.판) return d.magnitude >= half;
            return Mathf.Abs(d.x) >= half || Mathf.Abs(d.y) >= half;     // 한쪽만 있으면 얇은 벽
        }

        static void ConfirmBase()
        {
            DrawValues(out var end, out _);
            if (!BaseOk(end)) { status = "밑면이 너무 작습니다 — 끌거나 숫자로 (예: 1.5 0.5 Enter)"; return; }
            dEnd = end;
            drawStep = DrawStep.높이; numeric = "";
            dHeight = SnapStep;
            UpdateDraw();
        }

        static void BuildPreview(List<Vector3[]> into)
        {
            DrawValues(out var end, out float h);
            if (drawStep != DrawStep.높이) { into.Clear(); return; }
            RSEditOps.ShapePolys(drawShape, dO, dA, dB, dN, dStart, end, h, drawSides, drawSteps, RSEditMesh.Pixel, into);
        }

        static void CreateShape(bool again)
        {
            BuildPreview(previewPolys);
            if (previewPolys.Count == 0) { status = "만들 모양이 없습니다 (높이 0?)"; return; }
            Undo.RecordObject(target, shapeNames[(int)drawShape] + " 만들기");
            RSEFace paint = null;
            if (brush.surface != null) { paint = new RSEFace(); paint.CopyPaint(brush); paint.mode = RSEditPaintMode.반복; paint.fitGroup = 0; }
            var made = RSEditOps.AddPolys(target, previewPolys, paint);
            selMode = RSESelMode.면; ClearSel(); selF.UnionWith(made);
            numeric = "";
            if (again) drawStep = DrawStep.대기;
            else modal = Modal.없음;
            After(shapeNames[(int)drawShape] + "을(를) 만들었습니다 — G 이동 · S 크기 · E 밀어내기 · D 하나 더");
        }

        static void DrawGUI(Event e, SceneView sv, int ctrl)
        {
            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdateDraw();
                    sv.Repaint();
                    if (e.type == EventType.MouseDrag && dDragging) e.Use();
                    break;
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    GUIUtility.keyboardControl = 0;   // 패널 숫자칸이 키를 가져가지 않게
                    UpdateDraw();
                    if (drawStep == DrawStep.대기)
                    {
                        if (!dHoverOk) break;
                        dO = hO; dN = hN; dA = hA; dB = hB; dOnFace = dHoverOnFace;
                        dStart = dEnd = dHover;
                        drawStep = DrawStep.밑면; numeric = "";
                        dMouseDown = mouse; dDragging = true;
                        GUIUtility.hotControl = ctrl;
                    }
                    else if (drawStep == DrawStep.밑면) ConfirmBase();
                    else CreateShape(e.shift);
                    e.Use();
                    break;
                case EventType.MouseUp when e.button == 0:
                    if (dDragging)
                    {
                        dDragging = false; GUIUtility.hotControl = 0;
                        if (drawStep == DrawStep.밑면 && (mouse - dMouseDown).magnitude > 4f) ConfirmBase();
                        e.Use();
                    }
                    break;
                case EventType.ContextClick:
                    // 우클릭 끌기(시점 돌리기)는 그대로 두고, 끌지 않은 우클릭만 '뒤로'
                    if (eatContextClick) eatContextClick = false; else DrawBack();
                    e.Use();
                    break;
                case EventType.KeyDown:
                    {
                        var k = e.keyCode; char ch = e.character;
                        if (k == KeyCode.Escape) { DrawBack(); e.Use(); }
                        else if (k == KeyCode.Return || k == KeyCode.KeypadEnter)
                        {
                            if (drawStep == DrawStep.밑면) ConfirmBase();
                            else if (drawStep == DrawStep.높이) CreateShape(e.shift);
                            e.Use();
                        }
                        else if (k == KeyCode.Backspace) { if (numeric.Length > 0) numeric = numeric.Substring(0, numeric.Length - 1); e.Use(); }
                        else if (ch != 0 && drawStep != DrawStep.대기 && ((ch >= '0' && ch <= '9') || ch == '.' || ch == '-' || ch == ' ' || ch == ','))
                        {
                            if (ch == lastShortcutDigit) lastShortcutDigit = '\0';
                            else numeric += ch;
                            e.Use();
                        }
                        sv.Repaint();
                    }
                    break;
            }
        }

        static void DrawBack()
        {
            numeric = "";
            if (dDragging) { dDragging = false; GUIUtility.hotControl = 0; }
            if (drawStep == DrawStep.높이) drawStep = DrawStep.밑면;
            else if (drawStep == DrawStep.밑면) drawStep = DrawStep.대기;
            else modal = Modal.없음;
            SceneView.RepaintAll();
        }

        static void DrawDrawPreview()
        {
            var col = new Color(1f, 0.85f, 0.25f);
            if (drawStep == DrawStep.대기)
            {
                if (!dHoverOk) return;
                Vector3 p = hO + hA * dHover.x + hB * dHover.y;
                // 놓일 평면의 격자 + 십자
                float step = Mathf.Max(SnapStep, 0.125f);
                int n = Mathf.Clamp(Mathf.RoundToInt(1f / step), 2, 16);
                Vector3 c = hO + hA * (Mathf.Round(dHover.x / step) * step) + hB * (Mathf.Round(dHover.y / step) * step);
                Handles.color = new Color(1f, 1f, 1f, 0.12f);
                for (int i = -n; i <= n; i++)
                {
                    Handles.DrawLine(c + hA * (i * step) - hB * (n * step), c + hA * (i * step) + hB * (n * step));
                    Handles.DrawLine(c + hB * (i * step) - hA * (n * step), c + hB * (i * step) + hA * (n * step));
                }
                float s = HandleUtility.GetHandleSize(p) * 0.08f;
                Handles.color = col;
                Handles.DrawAAPolyLine(3f, p - hA * s, p + hA * s);
                Handles.DrawAAPolyLine(3f, p - hB * s, p + hB * s);
                Handles.DrawAAPolyLine(2f, p, p + hN * s * 2f);
                Handles.Label(p + hN * s * 2f, shapeNames[(int)drawShape] + (dHoverOnFace ? " · 이 면에서 솟기" : " · 바닥"), EditorStyles.whiteMiniLabel);
                return;
            }
            DrawValues(out var end, out float h);
            Vector3 P(float a, float b) { return dO + dA * a + dB * b; }
            if (drawStep == DrawStep.밑면)
            {
                Handles.color = col;
                if (drawShape == RSEShape.판)
                {
                    Handles.DrawAAPolyLine(4f, P(dStart.x, dStart.y), P(end.x, end.y));
                    Handles.Label(P(end.x, end.y), "길이 " + Len((end - dStart).magnitude), EditorStyles.whiteBoldLabel);
                }
                else
                {
                    Vector2 lo = Vector2.Min(dStart, end), hi = Vector2.Max(dStart, end);
                    var q = new[] { P(lo.x, lo.y), P(hi.x, lo.y), P(hi.x, hi.y), P(lo.x, hi.y) };
                    Handles.color = new Color(col.r, col.g, col.b, 0.15f);
                    Handles.DrawAAConvexPolygon(q);
                    Handles.color = col;
                    Handles.DrawAAPolyLine(3f, q[0], q[1], q[2], q[3], q[0]);
                    Handles.Label(P(hi.x, hi.y), Len(hi.x - lo.x) + " × " + Len(hi.y - lo.y), EditorStyles.whiteBoldLabel);
                }
                return;
            }
            BuildPreview(previewPolys);
            foreach (var poly in previewPolys)
            {
                Handles.color = new Color(col.r, col.g, col.b, 0.12f);
                var tris = new List<int>();
                var p2 = new Vector2[poly.Length];
                RSEditMesh.Frame(PolyNormal(poly), out var u, out var v);
                for (int i = 0; i < poly.Length; i++) p2[i] = new Vector2(Vector3.Dot(poly[i], u), Vector3.Dot(poly[i], v));
                RSEditOps.EarClip(p2, tris);
                for (int t = 0; t < tris.Count; t += 3) Handles.DrawAAConvexPolygon(poly[tris[t]], poly[tris[t + 1]], poly[tris[t + 2]]);
                Handles.color = col;
                var line = new Vector3[poly.Length + 1];
                for (int i = 0; i < poly.Length; i++) line[i] = poly[i];
                line[poly.Length] = poly[0];
                Handles.DrawAAPolyLine(2f, line);
            }
            Vector2 lo2 = Vector2.Min(dStart, end), hi2 = Vector2.Max(dStart, end);
            Handles.Label(P(hi2.x, hi2.y) + dN * h, "높이 " + Len(h), EditorStyles.whiteBoldLabel);
        }

        static Vector3 PolyNormal(Vector3[] p)
        {
            Vector3 n = Vector3.zero;
            for (int i = 0; i < p.Length; i++) n += Vector3.Cross(p[i], p[(i + 1) % p.Length]);
            return n.sqrMagnitude > 1e-14f ? n.normalized : Vector3.up;
        }

        static string DrawStatusText()
        {
            string nm = shapeNames[(int)drawShape];
            string extra = drawShape == RSEShape.원기둥 ? " (" + drawSides + "각)" : drawShape == RSEShape.계단 ? " (" + drawSteps + "칸)" : "";
            string num = numeric.Length > 0 ? "   [ 입력: " + numeric + " ]" : "";
            switch (drawStep)
            {
                case DrawStep.대기:
                    return "그리기 · " + nm + extra + ":  바닥이나 면 위를 끌기(또는 클릭 → 클릭) = 밑면  ·  면 위에서 시작하면 그 면에서 솟는다\n" +
                           "Shift+A 도형 바꾸기 · 스냅 " + StepName() + " ([ ]) · Ctrl = 스냅 끄기 · Esc 그만";
                case DrawStep.밑면:
                    return "밑면" + num + ":  끌어 놓기 / 클릭 = 확정  ·  숫자 '1.5 0.5' Enter = 정확한 크기" + (drawShape == RSEShape.판 ? " (판은 길이 하나)" : "") + "\n한쪽만 끌면 1px 얇은 벽 · 우클릭/Esc 뒤로";
                default:
                    return "높이" + num + ":  마우스 위아래 · 숫자 Enter  ·  클릭 = 만들기 (Shift+클릭 = 만들고 하나 더) · 우클릭/Esc 뒤로";
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 칠하기
        // ─────────────────────────────────────────────────────────────

        static void PaintGUI(Event e, int ctrl)
        {
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    if (hoverFace < 0) break;
                    if (e.alt)
                    {
                        var src = target.faces[hoverFace];
                        brush = new RSEFace(); brush.CopyPaint(src); brush.fitGroup = 0; brush.rotate = src.rotate;
                        e.Use(); break;
                    }
                    Undo.RecordObject(target, "칠하기");
                    PaintAt(hoverFace, e.shift);
                    painting = true;
                    GUIUtility.hotControl = ctrl;
                    e.Use();
                    break;
                case EventType.MouseDrag when painting:
                    if (hoverFace >= 0 && hoverFace != lastPaintFace && !e.shift) PaintAt(hoverFace, false);
                    e.Use();
                    break;
                case EventType.MouseUp when painting || GUIUtility.hotControl == ctrl:
                    EndPaintStroke();
                    e.Use();
                    break;
            }
        }

        static void EndPaintStroke()
        {
            if (!painting) return;
            painting = false;
            GUIUtility.hotControl = 0;
            Undo.IncrementCurrentGroup();
        }

        /// <summary>Shift: 부품 전체 (딱 맞게면 그 면과 같은 쪽을 보는 부품 면들)</summary>
        static HashSet<int> PaintTargets(int face, bool whole)
        {
            if (!whole) return new HashSet<int> { face };
            var part = RSEditOps.Linked(target, face);
            if (brush.mode != RSEditPaintMode.딱맞게) return part;
            Vector3 n0 = target.FaceNormal(face);
            var res = new HashSet<int>();
            foreach (int f in part) if (Vector3.Dot(target.FaceNormal(f), n0) > 0.7f) res.Add(f);
            return res;
        }

        static void PaintAt(int face, bool whole)
        {
            var set = PaintTargets(face, whole);
            int group = brush.mode == RSEditPaintMode.딱맞게 && set.Count > 1 ? target.NewGroup() : 0;
            Vector3 longAxis = Vector3.zero;
            if (grainAuto && brush.mode == RSEditPaintMode.반복) longAxis = PartLongAxis(face);
            foreach (int f in set)
            {
                if (!target.ValidFace(f)) continue;
                var fc = target.faces[f];
                fc.CopyPaint(brush);
                fc.fitGroup = group;
                int rot = brush.rotate;
                if (longAxis != Vector3.zero)
                {
                    RSEditMesh.Frame(target.FaceNormal(f), out var u, out var v);
                    if (Mathf.Abs(Vector3.Dot(v, longAxis)) > Mathf.Abs(Vector3.Dot(u, longAxis)) + 0.05f) rot += 1;
                }
                fc.rotate = rot & 3;
            }
            lastPaintFace = face;
            target.MarkDirty(); EditorUtility.SetDirty(target);
        }

        /// <summary>부품의 긴 방향 (점 상자 기준)</summary>
        static Vector3 PartLongAxis(int face)
        {
            var part = RSEditOps.Linked(target, face);
            var vs = new HashSet<int>();
            foreach (int f in part) vs.UnionWith(target.faces[f].v);
            if (vs.Count == 0) return Vector3.zero;
            var b = new Bounds(target.verts[First(vs)], Vector3.zero);
            foreach (int v in vs) b.Encapsulate(target.verts[v]);
            var s = b.size;
            return s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
        }

        // ─────────────────────────────────────────────────────────────
        // 연산 (단축키 · 패널 버튼이 부른다)
        // ─────────────────────────────────────────────────────────────

        static void After(string msg)
        {
            target.MarkDirty(); EditorUtility.SetDirty(target);
            Sanitize();
            if (msg != null) status = msg;
            SceneView.RepaintAll();
        }

        public static void DoMove() { if (Ready()) StartModal(Modal.이동, "이동"); SceneView.RepaintAll(); }
        public static void DoRotate() { if (Ready()) StartModal(Modal.회전, "회전"); SceneView.RepaintAll(); }
        public static void DoScale() { if (Ready()) StartModal(Modal.크기, "크기"); SceneView.RepaintAll(); }

        static bool Ready() { return target != null && toolMode == RSEToolMode.편집 && modal == Modal.없음; }

        public static void DoExtrude()
        {
            if (!Ready()) return;
            var faces = SelFaces();
            if (faces.Count == 0) { status = "밀어낼 면을 고르세요 (3 = 면)"; SceneView.RepaintAll(); return; }
            BeginStructural("밀어내기");
            var moved = RSEditOps.Extrude(target, faces, out var n);
            target.MarkDirty(); target.Rebuild();
            selMode = RSESelMode.면; selV.Clear(); selE.Clear(); selF.Clear(); selF.UnionWith(faces);
            if (StartModal(Modal.이동, "밀어내기", moved)) { customAxis = true; customAxisL = n; }
            SceneView.RepaintAll();
        }

        public static void DoCut()
        {
            if (!Ready()) return;
            modal = Modal.자르기;
            status = "자를 곳에 마우스 → 클릭 (X/Y/Z = 방향, Shift+클릭 = 전체, Esc = 그만)";
            SceneView.RepaintAll();
        }

        public static void DoDelete()
        {
            if (target == null || modal != Modal.없음 || toolMode != RSEToolMode.편집) return;
            if (selV.Count + selE.Count + selF.Count == 0) { status = "지울 것을 고르세요"; SceneView.RepaintAll(); return; }
            Undo.RecordObject(target, "지우기");
            if (selMode == RSESelMode.면) RSEditOps.DeleteFaces(target, selF);
            else if (selMode == RSESelMode.점) RSEditOps.DeleteVerts(target, selV);
            else
            {
                var fs = new HashSet<int>();
                foreach (var k in selE)
                {
                    var ed = EUnpack(k);
                    for (int f = 0; f < target.faces.Count; f++) if (RSEditOps.FaceHasEdge(target.faces[f].v, ed.x, ed.y)) fs.Add(f);
                }
                RSEditOps.DeleteFaces(target, fs);
            }
            ClearSel();
            hoverFace = -1; hoverVert = -1; hoverEdge = new Vector2Int(-1, -1);
            After("지웠습니다");
        }

        public static void DoMerge()
        {
            if (!Ready()) return;
            var vs = SelVerts();
            if (vs.Count < 2) { status = "합칠 점을 2개 이상 고르세요"; SceneView.RepaintAll(); return; }
            Vector3 c = Vector3.zero; foreach (int v in vs) c += target.verts[v]; c /= vs.Count;
            Undo.RecordObject(target, "점 합치기");
            RSEditOps.MergeVerts(target, vs, c);
            ClearSel();
            After("점 " + vs.Count + "개를 합쳤습니다");
        }

        public static void DoFlip()
        {
            if (!Ready()) return;
            var fs = SelFaces();
            if (fs.Count == 0) return;
            Undo.RecordObject(target, "면 뒤집기");
            RSEditOps.Flip(target, fs);
            After("면 " + fs.Count + "개를 뒤집었습니다");
        }

        public static void DoDuplicate()
        {
            if (!Ready()) return;
            var fs = SelFaces();
            if (fs.Count == 0) { status = "복제할 면을 고르세요"; SceneView.RepaintAll(); return; }
            BeginStructural("복제");
            var made = RSEditOps.Duplicate(target, fs);
            target.MarkDirty(); target.Rebuild();
            selMode = RSESelMode.면; ClearSel(); selF.UnionWith(made);
            var vs = SelVerts();
            StartModal(Modal.이동, "복제", vs);
            SceneView.RepaintAll();
        }

        public static void DoAddBox()
        {
            if (!Ready()) return;
            Vector3 at = cursorSet ? cursorL : (hoverFace >= 0 ? SnapV(target.transform.InverseTransformPoint(hoverPointW)) : Vector3.zero);
            Undo.RecordObject(target, "상자 추가");
            var paint = new RSEFace(); paint.CopyPaint(brush); paint.mode = RSEditPaintMode.반복; paint.fitGroup = 0;
            if (brush.surface == null) paint.surface = null;
            var made = RSEditOps.AddBox(target, at, Vector3.one, paint);
            selMode = RSESelMode.면; ClearSel(); selF.UnionWith(made);
            After("1m 상자를 놓았습니다 — S 크기 · G 이동 (숫자로 정확히)");
        }

        public static void DoLinked()
        {
            if (target == null || modal != Modal.없음 || hoverFace < 0) return;
            selMode = RSESelMode.면;
            selF.UnionWith(RSEditOps.Linked(target, hoverFace));
            SceneView.RepaintAll();
        }

        public static void DoSelectAll() { if (target != null && modal == Modal.없음) { SelectAll(false); SceneView.RepaintAll(); } }

        public static void SetSelMode(RSESelMode m, char digit)
        {
            if (modal == Modal.이동 || modal == Modal.회전 || modal == Modal.크기 || (modal == Modal.그리기 && drawStep != DrawStep.대기)) { if (digit != ' ') TypeDigit(digit); return; }
            if (modal != Modal.없음) return;
            if (selMode != m) { ConvertSelection(m); selMode = m; }
            toolMode = RSEToolMode.편집;
            SceneView.RepaintAll();
        }

        static void ConvertSelection(RSESelMode to)
        {
            var vs = SelVerts();
            var fs = SelFaces();
            ClearSel();
            if (to == RSESelMode.점) selV.UnionWith(vs);
            else if (to == RSESelMode.면) selF.UnionWith(fs);
            else foreach (var ed in Edges()) if (vs.Contains(ed.x) && vs.Contains(ed.y)) selE.Add(EKey(ed.x, ed.y));
        }

        public static void SetAxis(int a)
        {
            if (modal == Modal.자르기) { cutAxis = a; SceneView.RepaintAll(); return; }
            if (modal == Modal.이동 || modal == Modal.회전 || modal == Modal.크기)
            {
                axis = axis == a ? -1 : a;
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) ApplyModal(sv);
                return;
            }
            if (a == 0) DoDelete();   // X = 지우기
        }

        public static void ToggleTool()
        {
            if (modal != Modal.없음 || painting) return;
            toolMode = toolMode == RSEToolMode.편집 ? RSEToolMode.칠하기 : RSEToolMode.편집;
            SceneView.RepaintAll();
        }

        public static void Paint() { if (modal == Modal.없음 && !painting) { toolMode = toolMode == RSEToolMode.칠하기 ? RSEToolMode.편집 : RSEToolMode.칠하기; SceneView.RepaintAll(); } }

        public static void SnapStepChange(int d) { SnapIdx += d; status = "스냅 " + StepName(); SceneView.RepaintAll(); }

        static string StepName() { float s = SnapStep; return s < 0.05f ? "1px" : s.ToString("0.###", CultureInfo.InvariantCulture) + "m"; }

        // ─────────────────────────────────────────────────────────────
        // 그리기
        // ─────────────────────────────────────────────────────────────

        static readonly Color cSel = new Color(1f, 0.62f, 0.15f);
        static readonly Color cHover = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color cWire = new Color(1f, 1f, 1f, 0.22f);

        static void Draw(SceneView sv)
        {
            var tr = target.transform;
            using (new Handles.DrawingScope(tr.localToWorldMatrix))
            {
                // 선 전체
                if (toolMode == RSEToolMode.편집)
                {
                    var es = Edges();
                    var pts = new Vector3[es.Count * 2];
                    for (int i = 0; i < es.Count; i++) { pts[i * 2] = target.verts[es[i].x]; pts[i * 2 + 1] = target.verts[es[i].y]; }
                    Handles.color = cWire;
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
                    Handles.DrawLines(pts);
                    Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                }

                // 고른 면
                if (selF.Count > 0 && selMode == RSESelMode.면) FillFaces(selF, new Color(cSel.r, cSel.g, cSel.b, 0.28f), cSel);
                // 고른 선
                if (selMode == RSESelMode.선)
                {
                    Handles.color = cSel;
                    foreach (var k in selE) { var ed = EUnpack(k); Handles.DrawAAPolyLine(4f, target.verts[ed.x], target.verts[ed.y]); }
                }
                // 점
                if (selMode == RSESelMode.점 && toolMode == RSEToolMode.편집)
                {
                    for (int i = 0; i < target.verts.Count; i++)
                    {
                        bool s = selV.Contains(i);
                        Handles.color = s ? cSel : new Color(0.1f, 0.1f, 0.1f, 0.8f);
                        float size = HandleUtility.GetHandleSize(target.verts[i]) * (s ? 0.035f : 0.022f);
                        Handles.DotHandleCap(0, target.verts[i], Quaternion.identity, size, EventType.Repaint);
                    }
                }

                // 호버
                if (modal == Modal.없음 || modal == Modal.자르기)
                {
                    if (toolMode == RSEToolMode.칠하기 && hoverFace >= 0)
                    {
                        var set = PaintTargets(hoverFace, Event.current.shift);
                        FillFaces(set, new Color(1f, 0.6f, 0.2f, 0.18f), new Color(1f, 0.6f, 0.2f));
                    }
                    else if (selMode == RSESelMode.점 && hoverVert >= 0)
                    {
                        Handles.color = cHover;
                        Handles.DotHandleCap(0, target.verts[hoverVert], Quaternion.identity, HandleUtility.GetHandleSize(target.verts[hoverVert]) * 0.045f, EventType.Repaint);
                    }
                    else if (selMode == RSESelMode.선 && hoverEdge.x >= 0) { Handles.color = cHover; Handles.DrawAAPolyLine(5f, target.verts[hoverEdge.x], target.verts[hoverEdge.y]); }
                    else if (selMode == RSESelMode.면 && hoverFace >= 0 && modal == Modal.없음) FillFaces(new HashSet<int> { hoverFace }, new Color(1, 1, 1, 0.08f), new Color(1, 1, 1, 0.6f));
                }

                // 3D 커서
                if (cursorSet)
                {
                    float s = HandleUtility.GetHandleSize(cursorL) * 0.12f;
                    Handles.color = new Color(1f, 0.3f, 0.3f);
                    Handles.DrawWireDisc(cursorL, Vector3.up, s);
                    Handles.color = Color.white;
                    Handles.DrawLine(cursorL - Vector3.right * s * 1.4f, cursorL + Vector3.right * s * 1.4f);
                    Handles.DrawLine(cursorL - Vector3.forward * s * 1.4f, cursorL + Vector3.forward * s * 1.4f);
                }

                // 모달 축 · 격자
                if ((modal == Modal.이동 || modal == Modal.회전 || modal == Modal.크기))
                {
                    if (axis >= 0 || customAxis)
                    {
                        Vector3 dir = axis >= 0 ? AxisL(axis) : customAxisL;
                        Handles.color = axis == 0 ? new Color(1f, 0.3f, 0.3f) : axis == 1 ? new Color(0.4f, 1f, 0.4f) : axis == 2 ? new Color(0.4f, 0.6f, 1f) : new Color(1f, 0.85f, 0.3f);
                        Handles.DrawLine(pivotL - dir * 50f, pivotL + dir * 50f);
                    }
                    if (modal == Modal.이동) DrawGrid(pivotL);
                }

                if (modal == Modal.그리기) DrawDrawPreview();

                // 자르기 미리보기
                if (modal == Modal.자르기 && hoverFace >= 0)
                {
                    Vector3 p = CutPoint();
                    Vector3 n = AxisL(cutAxis);
                    var mb = target.BuiltMesh.bounds;
                    float r = Mathf.Max(0.5f, mb.extents.magnitude * 1.1f);
                    Vector3 a = cutAxis == 1 ? Vector3.right : Vector3.up, b = Vector3.Cross(n, a);
                    Vector3 c = Vector3.Scale(mb.center, Vector3.one - n) + Vector3.Scale(p, n);
                    var quad = new[] { c + (a + b) * r, c + (a - b) * r, c + (-a - b) * r, c + (-a + b) * r };
                    Handles.color = new Color(1f, 0.3f, 0.3f, 0.12f);
                    Handles.DrawAAConvexPolygon(quad);
                    Handles.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                    Handles.DrawAAPolyLine(2f, quad[0], quad[1], quad[2], quad[3], quad[0]);
                    Handles.Label(p, "자르기 " + "XYZ".Substring(cutAxis, 1) + " = " + Len(p[cutAxis]), EditorStyles.whiteBoldLabel);
                }

                // 치수
                if (modal == Modal.없음 && toolMode == RSEToolMode.편집)
                {
                    var vs = SelVerts();
                    if (vs.Count > 1)
                    {
                        var bb = new Bounds(target.verts[First(vs)], Vector3.zero);
                        foreach (int v in vs) bb.Encapsulate(target.verts[v]);
                        Handles.color = new Color(1f, 0.85f, 0.3f, 0.5f);
                        Handles.DrawWireCube(bb.center, bb.size);
                        Handles.Label(bb.max, Size(bb.size), EditorStyles.whiteMiniLabel);
                    }
                }
            }
        }

        static string Size(Vector3 s)
        {
            int px = RSSurface.PixelsPerMeter;
            return s.x.ToString("0.###", CultureInfo.InvariantCulture) + " × " + s.y.ToString("0.###", CultureInfo.InvariantCulture) + " × " + s.z.ToString("0.###", CultureInfo.InvariantCulture) + " m\n" +
                   Mathf.RoundToInt(s.x * px) + " × " + Mathf.RoundToInt(s.y * px) + " × " + Mathf.RoundToInt(s.z * px) + " px";
        }

        static readonly List<int> triTmp = new List<int>();

        static void FillFaces(HashSet<int> set, Color fill, Color line)
        {
            bool outlineOnly = set.Count > 300;   // 많으면 윤곽만 (그리기 부담)
            foreach (int f in set)
            {
                if (!target.ValidFace(f)) continue;
                var vv = target.faces[f].v;
                Handles.color = fill;
                if (!outlineOnly) target.Triangulate(vv, target.FaceNormal(f), triTmp); else triTmp.Clear();
                for (int t = 0; t < triTmp.Count; t += 3)
                    Handles.DrawAAConvexPolygon(target.verts[vv[triTmp[t]]], target.verts[vv[triTmp[t + 1]]], target.verts[vv[triTmp[t + 2]]]);
                Handles.color = line;
                var pts = new Vector3[vv.Length + 1];
                for (int i = 0; i < vv.Length; i++) pts[i] = target.verts[vv[i]];
                pts[vv.Length] = pts[0];
                Handles.DrawAAPolyLine(2.5f, pts);
            }
        }

        static void DrawGrid(Vector3 center)
        {
            float step = Mathf.Max(SnapStep, 0.125f);
            int n = Mathf.Clamp(Mathf.RoundToInt(2f / step), 4, 32);
            float y = center.y;
            Vector3 c = new Vector3(Mathf.Round(center.x / step) * step, y, Mathf.Round(center.z / step) * step);
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            for (int i = -n; i <= n; i++)
            {
                Handles.DrawLine(c + new Vector3(i * step, 0, -n * step), c + new Vector3(i * step, 0, n * step));
                Handles.DrawLine(c + new Vector3(-n * step, 0, i * step), c + new Vector3(n * step, 0, i * step));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 패널 · 상태 줄
        // ─────────────────────────────────────────────────────────────

        static RSEToolMode shownTool = RSEToolMode.편집;
        static RSEditPaintMode shownPaint = RSEditPaintMode.반복;

        static Rect PanelRect(SceneView sv)
        {
            float h = shownTool == RSEToolMode.칠하기 ? (shownPaint == RSEditPaintMode.딱맞게 ? 330 : 300) : 262;
            return new Rect(56, 8, 268, h);
        }

        static void DrawPanel(SceneView sv)
        {
            if (Event.current.type == EventType.Layout) { shownTool = toolMode; shownPaint = brush.mode; }
            Handles.BeginGUI();
            GUILayout.BeginArea(PanelRect(sv), GUI.skin.box);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("편집 메시: " + target.name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("[", EditorStyles.miniButtonLeft, GUILayout.Width(18))) SnapStepChange(-1);
                GUILayout.Label("스냅 " + StepName(), EditorStyles.miniLabel, GUILayout.Width(62));
                if (GUILayout.Button("]", EditorStyles.miniButtonRight, GUILayout.Width(18))) SnapStepChange(1);
            }
            var tm = (RSEToolMode)GUILayout.Toolbar((int)toolMode, new[] { "모양 (Tab)", "칠하기 (P)" });
            if (tm != toolMode && modal == Modal.없음 && !painting) toolMode = tm;
            if (shownTool == RSEToolMode.편집) EditPanel(); else PaintPanel();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static void EditPanel()
        {
            GUILayout.Label("만들기  (D = 그리기 · ⇧A = 도형 메뉴)", EditorStyles.miniBoldLabel);
            for (int row = 0; row < 2; row++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int i = row * 3 + c;
                        bool on = modal == Modal.그리기 && drawShape == (RSEShape)i;
                        var st = c == 0 ? EditorStyles.miniButtonLeft : c == 2 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                        if (GUILayout.Toggle(on, shapeNames[i], st) != on)
                        {
                            if (on) modal = Modal.없음; else DoDraw((RSEShape)i);
                        }
                    }
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("원기둥 각", EditorStyles.miniLabel, GUILayout.Width(48));
                drawSides = Mathf.Clamp(EditorGUILayout.IntField(drawSides, GUILayout.Width(28)), 3, 64);
                GUILayout.Label("계단 칸", EditorStyles.miniLabel, GUILayout.Width(40));
                drawSteps = Mathf.Clamp(EditorGUILayout.IntField(drawSteps, GUILayout.Width(28)), 1, 64);
            }
            GUILayout.Label("고치기", EditorStyles.miniBoldLabel);
            var sm = (RSESelMode)GUILayout.Toolbar((int)selMode, new[] { "점 1", "선 2", "면 3" }, EditorStyles.miniButton);
            if (sm != selMode) SetSelMode(sm, ' ');
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이동 G", EditorStyles.miniButtonLeft)) DoMove();
                if (GUILayout.Button("회전 R", EditorStyles.miniButtonMid)) DoRotate();
                if (GUILayout.Button("크기 S", EditorStyles.miniButtonRight)) DoScale();
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("밀어내기 E", EditorStyles.miniButtonLeft)) DoExtrude();
                if (GUILayout.Button("자르기 K", EditorStyles.miniButtonMid)) DoCut();
                if (GUILayout.Button("1m 상자", EditorStyles.miniButtonRight)) DoAddBox();
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("지우기 X", EditorStyles.miniButtonLeft)) DoDelete();
                if (GUILayout.Button("합치기 M", EditorStyles.miniButtonMid)) DoMerge();
                if (GUILayout.Button("뒤집기 N", EditorStyles.miniButtonMid)) DoFlip();
                if (GUILayout.Button("복제 ⇧D", EditorStyles.miniButtonRight)) DoDuplicate();
            }
            GUILayout.Label("G·R·S 뒤 X/Y/Z = 축, 숫자 = 값, Ctrl = 스냅 끄기\n클릭·Enter 확정 · 우클릭·Esc 취소\nA 전체 · L 부품 · ⇧+우클릭 3D 커서 · F 맞춰 보기", EditorStyles.wordWrappedMiniLabel);
        }

        static void PaintPanel()
        {
            brush.mode = (RSEditPaintMode)EditorGUILayout.Popup((int)brush.mode, new[] { "반복 (재질 타일, 1m = 32px)", "딱 맞게 (그림 한 장)" });
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("돌리기 " + brush.rotate * 90 + "°", EditorStyles.miniLabel, GUILayout.Width(70));
                if (GUILayout.Button("↻", EditorStyles.miniButtonLeft, GUILayout.Width(24))) brush.rotate = (brush.rotate + 1) & 3;
                brush.flip = GUILayout.Toggle(brush.flip, "뒤집기", EditorStyles.miniButtonMid, GUILayout.Width(48));
                if (shownPaint == RSEditPaintMode.반복) grainAuto = GUILayout.Toggle(grainAuto, "결 자동", EditorStyles.miniButtonRight, GUILayout.Width(56));
            }
            var set = target.palette;
            if (set == null)
            {
                GUILayout.Label("인스펙터 '팔레트' 에 재질 세트를 넣으세요", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("기본 세트 쓰기", EditorStyles.miniButton))
                {
                    var t = target;
                    EditorApplication.delayCall += () => { if (t == null) return; Undo.RecordObject(t, "팔레트"); t.palette = RSSurfaceTools.DefaultSet(); EditorUtility.SetDirty(t); SceneView.RepaintAll(); };
                }
            }
            else
            {
                paletteScroll = GUILayout.BeginScrollView(paletteScroll, GUILayout.Height(150));
                int cols = 5, i = 0;
                GUILayout.BeginHorizontal();
                foreach (var s in set.surfaces)
                {
                    if (s == null) continue;
                    var rr = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
                    RSSurfaceTools.DrawSwatch(rr, s, brush.surface == s);
                    if (Event.current.type == EventType.MouseDown && rr.Contains(Event.current.mousePosition)) { brush.surface = s; Event.current.Use(); }
                    if (++i % cols == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
            }
            GUILayout.Label((brush.surface != null ? brush.surface.Label : "(재질 없음 = 기본)") + "\n클릭 · 끌기 = 칠 · Shift = 부품 전체 · Alt = 집기", EditorStyles.wordWrappedMiniLabel);
            if (shownPaint == RSEditPaintMode.딱맞게)
            {
                using (new GUILayout.HorizontalScope())
                {
                    bool can = target.ValidFace(lastPaintFace) && target.faces[lastPaintFace].mode == RSEditPaintMode.딱맞게;
                    GUI.enabled = can;
                    var px = can ? target.FitPixels(lastPaintFace) : Vector2Int.zero;
                    if (GUILayout.Button(can ? "이 면 크기 그림 만들기 (" + px.x + "×" + px.y + "px)" : "칠한 면 크기 그림 만들기", EditorStyles.miniButton))
                    {
                        var t = target; int f = lastPaintFace; var srf = brush.surface; int b = fitBorder;
                        EditorApplication.delayCall += () => { if (t != null) RSEditMeshTools.MakeFitPicture(t, f, srf, b); };
                    }
                    GUI.enabled = true;
                    GUILayout.Label("테두리", EditorStyles.miniLabel, GUILayout.Width(34));
                    fitBorder = Mathf.Clamp(EditorGUILayout.IntField(fitBorder, GUILayout.Width(26)), 0, 32);
                }
            }
        }

        static void DrawStatus(SceneView sv)
        {
            string text;
            if (modal == Modal.이동 || modal == Modal.회전 || modal == Modal.크기)
                text = (modal == Modal.이동 ? (customAxis ? "밀어내기" : "이동") : modal == Modal.회전 ? "회전" : "크기") + "   " + modalInfo +
                       (numeric.Length > 0 ? "   [ 입력: " + numeric + " ]" : "") +
                       "\nX/Y/Z 축 · 숫자 입력 · Ctrl 스냅 끄기(" + StepName() + ") · 클릭/Enter 확정 · 우클릭/Esc 취소";
            else if (modal == Modal.그리기) text = DrawStatusText();
            else if (modal == Modal.자르기) text = "자르기: 클릭 = 마우스 밑 부품 자르기 · Shift+클릭 = 전체 · X/Y/Z 방향 · Esc 그만";
            else if (toolMode == RSEToolMode.칠하기)
            {
                text = "칠하기";
                if (hoverFace >= 0)
                {
                    var f = target.faces[hoverFace];
                    text += "   ·  면 " + hoverFace + ": " + (f.surface != null ? f.surface.Label : "기본") + " · " + (f.mode == RSEditPaintMode.반복 ? "반복" : "딱 맞게 " + target.FitPixels(hoverFace).x + "×" + target.FitPixels(hoverFace).y + "px");
                }
            }
            else
            {
                int n = selMode == RSESelMode.점 ? selV.Count : selMode == RSESelMode.선 ? selE.Count : selF.Count;
                text = selMode + " " + n + "개 고름" + (string.IsNullOrEmpty(status) ? "" : "   ·  " + status);
            }
            Handles.BeginGUI();
            var size = EditorStyles.helpBox.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(8, sv.position.height - size.y - 30, Mathf.Min(size.x + 8, sv.position.width - 16), size.y + 4), text, EditorStyles.helpBox);
            Handles.EndGUI();
        }
    }

    /// <summary>편집 메시 도구가 켜져 있고 씬 뷰에 초점이 있을 때만 단축키가 이긴다</summary>
    public class RSEditShortcutContext : IShortcutContext
    {
        public bool active
        {
            get
            {
                if (!RSEditCore.ToolActive || Tools.viewToolActive) return false;
                return EditorWindow.focusedWindow is SceneView;
            }
        }
    }

    static class RSEditShortcuts
    {
        const string P = "RE_AL STEEL 편집 메시/";
        [Shortcut(P + "점 고르기", typeof(RSEditShortcutContext), KeyCode.Alpha1)] static void K1() { RSEditCore.SetSelMode(RSESelMode.점, '1'); }
        [Shortcut(P + "선 고르기", typeof(RSEditShortcutContext), KeyCode.Alpha2)] static void K2() { RSEditCore.SetSelMode(RSESelMode.선, '2'); }
        [Shortcut(P + "면 고르기", typeof(RSEditShortcutContext), KeyCode.Alpha3)] static void K3() { RSEditCore.SetSelMode(RSESelMode.면, '3'); }
        [Shortcut(P + "이동", typeof(RSEditShortcutContext), KeyCode.G)] static void G() { RSEditCore.DoMove(); }
        [Shortcut(P + "회전", typeof(RSEditShortcutContext), KeyCode.R)] static void R() { RSEditCore.DoRotate(); }
        [Shortcut(P + "크기", typeof(RSEditShortcutContext), KeyCode.S)] static void S() { RSEditCore.DoScale(); }
        [Shortcut(P + "밀어내기", typeof(RSEditShortcutContext), KeyCode.E)] static void E() { RSEditCore.DoExtrude(); }
        [Shortcut(P + "자르기", typeof(RSEditShortcutContext), KeyCode.K)] static void K() { RSEditCore.DoCut(); }
        [Shortcut(P + "X 축 · 지우기", typeof(RSEditShortcutContext), KeyCode.X)] static void X() { RSEditCore.SetAxis(0); }
        [Shortcut(P + "Y 축", typeof(RSEditShortcutContext), KeyCode.Y)] static void Y() { RSEditCore.SetAxis(1); }
        [Shortcut(P + "Z 축", typeof(RSEditShortcutContext), KeyCode.Z)] static void Z() { RSEditCore.SetAxis(2); }
        [Shortcut(P + "점 합치기", typeof(RSEditShortcutContext), KeyCode.M)] static void M() { RSEditCore.DoMerge(); }
        [Shortcut(P + "면 뒤집기", typeof(RSEditShortcutContext), KeyCode.N)] static void N() { RSEditCore.DoFlip(); }
        [Shortcut(P + "전체 고르기", typeof(RSEditShortcutContext), KeyCode.A)] static void A() { RSEditCore.DoSelectAll(); }
        [Shortcut(P + "부품 고르기", typeof(RSEditShortcutContext), KeyCode.L)] static void L() { RSEditCore.DoLinked(); }
        [Shortcut(P + "도형 메뉴", typeof(RSEditShortcutContext), KeyCode.A, ShortcutModifiers.Shift)] static void ShiftA() { RSEditCore.ShapeMenu(); }
        [Shortcut(P + "그리기", typeof(RSEditShortcutContext), KeyCode.D)] static void Dk() { RSEditCore.DoDraw(null); }
        [Shortcut(P + "복제", typeof(RSEditShortcutContext), KeyCode.D, ShortcutModifiers.Shift)] static void ShiftD() { RSEditCore.DoDuplicate(); }
        [Shortcut(P + "모양 ↔ 칠하기", typeof(RSEditShortcutContext), KeyCode.Tab)] static void Tab() { RSEditCore.ToggleTool(); }
        [Shortcut(P + "칠하기", typeof(RSEditShortcutContext), KeyCode.P)] static void Pk() { RSEditCore.Paint(); }
        [Shortcut(P + "스냅 작게", typeof(RSEditShortcutContext), KeyCode.LeftBracket)] static void SnapDown() { RSEditCore.SnapStepChange(-1); }
        [Shortcut(P + "스냅 크게", typeof(RSEditShortcutContext), KeyCode.RightBracket)] static void SnapUp() { RSEditCore.SnapStepChange(1); }
    }

    [EditorTool("편집 메시 (RS)", typeof(RSEditMesh))]
    public class RSEditTool : EditorTool
    {
        GUIContent icon;
        public override GUIContent toolbarIcon { get { return icon ?? (icon = new GUIContent("편집", "RS 편집 메시: 점 · 선 · 면 고치기 + 칠하기")); } }
        public override void OnActivated() { RSEditCore.SetTarget(target as RSEditMesh); }
        public override void OnWillBeDeactivated() { RSEditCore.Deactivated(); }
        public override void OnToolGUI(EditorWindow window) { RSEditCore.OnGUI(target as RSEditMesh, window); }
    }
}
