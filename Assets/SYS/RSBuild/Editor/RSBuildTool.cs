// RE:AL STEEL - 조립 씬 도구 (조립품 · 부품을 고르면 씬 뷰 도구 막대에 '조립' 이 생긴다)
//
//  고르기 : 부품 클릭 → 그 부품 선택 (유니티 이동 · 회전 · 복제는 그대로)
//  그리기 : 바닥(또는 부품 면)을 끌어 넓이 → 마우스를 올려 높이 → 클릭. Esc = 취소
//  밀기   : 면을 끌면 그 방향 크기만 바뀐다 (반대쪽 면은 제자리)
//  칠하기 : 팔레트 재질 · 방식을 골라 면 클릭. Shift+클릭 = 부품 전체, Alt+클릭 = 그 면 칠 집기
// 스냅: 조립품의 '스냅' (1 픽셀 = 1/32 m, 또는 0.25 · 0.5 · 1 m)
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    public enum RSBuildMode { 고르기, 그리기, 밀기, 칠하기 }

    /// <summary>두 도구(조립품용 · 부품용)가 같이 쓰는 본체</summary>
    public static class RSBuildToolCore
    {
        public static RSBuildMode mode = RSBuildMode.그리기;
        public static RSPartShape drawShape = RSPartShape.상자;
        public static RSFacePaint brush = new RSFacePaint();
        static Vector2 paletteScroll;

        // 그리기 상태
        enum DrawState { 없음, 넓이, 높이 }
        static DrawState drawState;
        static Vector3 planeOrigin, planeN, axisA, axisB;   // 조립품 로컬
        static Vector2 dragStart, dragEnd;
        static float drawHeight;

        // 밀기 상태
        static RSPart pushPart;
        static int pushAxis, pushSign;
        static Vector3 pushPointW, pushNormalW, pushStartPos, pushStartSize;
        static bool pushing;

        // 호버
        static RSPart hoverPart;
        static int hoverFace = -1;
        static Vector3 hoverPoint, hoverNormal;

        public static RSAssembly Resolve(Object target)
        {
            if (target is RSAssembly a) return a;
            if (target is RSPart p) return p.Owner;
            return null;
        }

        public static void OnGUI(RSAssembly asm, EditorWindow window)
        {
            if (asm == null || !(window is SceneView sv)) return;
            var e = Event.current;
            DrawPanel(asm, sv);
            if (GUIUtility.hotControl == 0 && PanelRect(sv).Contains(e.mousePosition))
            {
                // 패널 빈 곳 클릭이 씬의 다른 오브젝트를 고르지 않게
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag) e.Use();
                if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            int ctrl = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(ctrl);   // 씬 클릭이 다른 오브젝트를 고르지 않게

            // 호버
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Layout || e.type == EventType.MouseDown)
            {
                var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (!pushing && asm.Raycast(ray, out var hp, out var hf, out var pt, out var nn)) { hoverPart = hp; hoverFace = hf; hoverPoint = pt; hoverNormal = nn; }
                else if (!pushing) { hoverPart = null; hoverFace = -1; }
                if (e.type == EventType.MouseMove) sv.Repaint();
            }

            switch (mode)
            {
                case RSBuildMode.고르기: Pick(asm, e); break;
                case RSBuildMode.그리기: Draw(asm, e, ctrl, sv); break;
                case RSBuildMode.밀기: Push(asm, e, ctrl); break;
                case RSBuildMode.칠하기: Paint(asm, e); break;
            }

            if (e.type == EventType.Repaint) DrawHover(asm);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && (drawState != DrawState.없음 || pushing))
            {
                if (pushing && pushPart != null)
                {
                    pushPart.size = pushStartSize;
                    pushPart.transform.localPosition = pushStartPos;
                    pushPart.Touch();
                }
                drawState = DrawState.없음; pushing = false;
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 패널 (씬 뷰 왼쪽 위)
        // ─────────────────────────────────────────────────────────────

        static Rect PanelRect(SceneView sv)
        {
            float h = shownMode == RSBuildMode.칠하기 ? (shownBrushMode == RSPaintMode.늘리기 ? 322 : 300) : shownMode == RSBuildMode.그리기 ? 128 : 74;
            return new Rect(56, 8, 262, h);   // 왼쪽 유니티 도구 막대를 피해
        }

        static RSBuildMode shownMode = RSBuildMode.그리기;
        static RSPaintMode shownBrushMode = RSPaintMode.반복;

        static void DrawPanel(RSAssembly asm, SceneView sv)
        {
            // Layout 과 다른 이벤트에서 컨트롤 수가 달라지지 않게, 모드는 Layout 때 정한다
            if (Event.current.type == EventType.Layout) { shownMode = mode; shownBrushMode = brush.mode; }
            Handles.BeginGUI();
            var r = PanelRect(sv);
            GUILayout.BeginArea(r, GUI.skin.box);
            GUILayout.Label("조립: " + asm.name + "   (스냅 " + (asm.snap == RSSnap.픽셀 ? "1 픽셀" : asm.SnapStep + " m") + ")", EditorStyles.miniBoldLabel);
            mode = (RSBuildMode)GUILayout.Toolbar((int)mode, new[] { "고르기", "그리기", "밀기", "칠하기" });
            if (shownMode == RSBuildMode.그리기)
            {
                GUILayout.Space(2);
                var names = System.Enum.GetNames(typeof(RSPartShape));
                drawShape = (RSPartShape)GUILayout.SelectionGrid((int)drawShape, names, 3, EditorStyles.miniButton);
                GUILayout.Label(drawState == DrawState.높이 ? "마우스로 높이 → 클릭 (Esc 취소)" : "바닥이나 면을 끌어 넓이를 그린다", EditorStyles.miniLabel);
            }
            else if (shownMode == RSBuildMode.밀기) GUILayout.Label("면을 끌면 그쪽 크기만 바뀐다", EditorStyles.miniLabel);
            else if (shownMode == RSBuildMode.고르기) GUILayout.Label("부품 클릭 → 고르고 이동 도구로 (돌아오기: 도구 막대 '조립')", EditorStyles.miniLabel);
            else DrawPalette(asm);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        static void DrawPalette(RSAssembly asm)
        {
            var set = asm.palette;
            brush.mode = (RSPaintMode)EditorGUILayout.Popup((int)brush.mode, new[] { "반복 (재질 타일)", "도안 칸 (전용 그림)", "늘리기 (테두리 유지)" });
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("돌리기 " + brush.rotate * 90 + "°", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("↻", EditorStyles.miniButtonLeft, GUILayout.Width(24))) brush.rotate = (brush.rotate + 1) & 3;
                brush.flip = GUILayout.Toggle(brush.flip, "뒤집기", EditorStyles.miniButtonRight, GUILayout.Width(50));
            }
            if (shownBrushMode == RSPaintMode.늘리기)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("테두리 px", EditorStyles.miniLabel, GUILayout.Width(52));
                    int b = Mathf.Clamp(EditorGUILayout.IntField((int)brush.border.x, GUILayout.Width(36)), 0, 64);
                    brush.border = new Vector4(b, b, b, b);
                }
            }
            if (set == null)
            {
                GUILayout.Label("조립품 '팔레트' 에 재질 세트를 넣으세요", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("기본 세트 쓰기", EditorStyles.miniButton)) { Undo.RecordObject(asm, "팔레트"); asm.palette = RSSurfaceTools.DefaultSet(); EditorUtility.SetDirty(asm); }
                return;
            }
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
            GUILayout.Label((brush.surface != null ? brush.surface.Label : "(재질 없음 = 기본)") + " · Shift = 부품 전체 · Alt = 집기", EditorStyles.miniLabel);
        }

        // ─────────────────────────────────────────────────────────────
        // 고르기
        // ─────────────────────────────────────────────────────────────

        static void Pick(RSAssembly asm, Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hoverPart != null)
            {
                // 고른 부품은 유니티 이동 도구로 바로 옮길 수 있게 (조립 도구로 돌아오려면 도구 막대 '조립')
                Selection.activeGameObject = hoverPart.gameObject;
                Tools.current = Tool.Move;
                e.Use();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 그리기
        // ─────────────────────────────────────────────────────────────

        static float Snap(RSAssembly asm, float v) { float s = asm.SnapStep; return Mathf.Round(v / s) * s; }

        static bool PlanePoint(RSAssembly asm, Ray ray, out Vector2 ab)
        {
            ab = Vector2.zero;
            var w2l = asm.transform.worldToLocalMatrix;
            Vector3 o = w2l.MultiplyPoint3x4(ray.origin), d = w2l.MultiplyVector(ray.direction);
            float den = Vector3.Dot(d, planeN);
            if (Mathf.Abs(den) < 1e-6f) return false;
            float t = Vector3.Dot(planeOrigin - o, planeN) / den;
            if (t < 0) return false;
            Vector3 p = o + d * t - planeOrigin;
            ab = new Vector2(Snap(asm, Vector3.Dot(p, axisA)), Snap(asm, Vector3.Dot(p, axisB)));
            return true;
        }

        static void Draw(RSAssembly asm, Event e, int ctrl, SceneView sv)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && !e.alt:
                    if (drawState == DrawState.높이) { Create(asm); drawState = DrawState.없음; e.Use(); return; }
                    // 기준 평면: 닿은 면 또는 조립품 바닥 (로컬 y = 0)
                    if (hoverPart != null)
                    {
                        // 법선을 조립품 로컬로 (비균일 크기도 바르게: localToWorld 의 전치)
                        planeN = asm.transform.localToWorldMatrix.transpose.MultiplyVector(hoverNormal).normalized;
                        Vector3 hp = asm.transform.InverseTransformPoint(hoverPoint);
                        // 면 위 높이 = 법선 방향 성분, 평면 좌표는 조립품 원점 기준으로 스냅
                        planeOrigin = planeN * Vector3.Dot(hp, planeN);
                    }
                    else { planeN = Vector3.up; planeOrigin = Vector3.zero; }
                    RSShapes.Frame(planeN, Vector3.up, Vector3.right, out axisA, out axisB);
                    if (Mathf.Abs(planeN.y) > 0.999f) { axisA = Vector3.right; axisB = Vector3.Cross(axisA, planeN); if (Vector3.Dot(axisB, Vector3.forward) < 0) axisB = -axisB; }
                    if (PlanePoint(asm, ray, out dragStart)) { dragEnd = dragStart; drawState = DrawState.넓이; GUIUtility.hotControl = ctrl; e.Use(); }
                    break;
                case EventType.MouseDrag when drawState == DrawState.넓이:
                    if (PlanePoint(asm, ray, out var p2)) dragEnd = p2;
                    e.Use(); sv.Repaint();
                    break;
                case EventType.MouseUp when drawState == DrawState.넓이:
                    GUIUtility.hotControl = 0;
                    if (Mathf.Abs(dragEnd.x - dragStart.x) < asm.SnapStep * 0.5f || Mathf.Abs(dragEnd.y - dragStart.y) < asm.SnapStep * 0.5f)
                    {
                        // 판은 한 방향만 그어도 된다 (얇은 판)
                        if (drawShape == RSPartShape.판 && (Mathf.Abs(dragEnd.x - dragStart.x) >= asm.SnapStep * 0.5f || Mathf.Abs(dragEnd.y - dragStart.y) >= asm.SnapStep * 0.5f)) { }
                        else { drawState = DrawState.없음; e.Use(); break; }
                    }
                    drawState = DrawState.높이; drawHeight = asm.SnapStep;
                    e.Use();
                    break;
                case EventType.MouseMove when drawState == DrawState.높이:
                    {
                        // 넓이 가운데에서 법선 방향 선과 마우스 광선의 가장 가까운 점
                        Vector2 c2 = (dragStart + dragEnd) * 0.5f;
                        Vector3 baseL = planeOrigin + axisA * c2.x + axisB * c2.y;
                        Vector3 baseW = asm.transform.TransformPoint(baseL), nW = asm.transform.TransformVector(planeN).normalized;
                        float t = ClosestOnLine(baseW, nW, ray);
                        drawHeight = Mathf.Max(asm.SnapStep, Snap(asm, t / Mathf.Max(1e-5f, asm.transform.TransformVector(planeN).magnitude)));
                        sv.Repaint();
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    if (drawState != DrawState.없음) DrawPreview(asm);
                    break;
            }
        }

        static float ClosestOnLine(Vector3 p, Vector3 dir, Ray ray)
        {
            Vector3 w0 = p - ray.origin;
            float a = Vector3.Dot(dir, dir), b = Vector3.Dot(dir, ray.direction), c = Vector3.Dot(ray.direction, ray.direction);
            float d = Vector3.Dot(dir, w0), e2 = Vector3.Dot(ray.direction, w0);
            float den = a * c - b * b;
            if (Mathf.Abs(den) < 1e-6f) return 0f;
            return (b * e2 - c * d) / den;
        }

        static void DrawPreview(RSAssembly asm)
        {
            Vector2 lo = Vector2.Min(dragStart, dragEnd), hi = Vector2.Max(dragStart, dragEnd);
            float h = drawState == DrawState.높이 ? drawHeight : 0f;
            using (new Handles.DrawingScope(new Color(1f, 0.85f, 0.25f), asm.transform.localToWorldMatrix))
            {
                Vector3 P(float a, float b, float y) { return planeOrigin + axisA * a + axisB * b + planeN * y; }
                var c = new[] { P(lo.x, lo.y, 0), P(hi.x, lo.y, 0), P(hi.x, hi.y, 0), P(lo.x, hi.y, 0) };
                Handles.DrawAAPolyLine(3f, c[0], c[1], c[2], c[3], c[0]);
                if (h > 0)
                {
                    var t = new[] { P(lo.x, lo.y, h), P(hi.x, lo.y, h), P(hi.x, hi.y, h), P(lo.x, hi.y, h) };
                    Handles.DrawAAPolyLine(3f, t[0], t[1], t[2], t[3], t[0]);
                    for (int i = 0; i < 4; i++) Handles.DrawAAPolyLine(2f, c[i], t[i]);
                }
                Vector3 mid = P((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, h);
                Handles.Label(mid, drawShape + "  " + Px(hi.x - lo.x) + " × " + Px(h) + " × " + Px(hi.y - lo.y), EditorStyles.whiteBoldLabel);
            }
        }

        static string Px(float m)
        {
            int px = Mathf.RoundToInt(m * RSSurface.PixelsPerMeter);
            return m.ToString("0.###") + "m(" + px + "px)";
        }

        static void Create(RSAssembly asm)
        {
            Vector2 lo = Vector2.Min(dragStart, dragEnd), hi = Vector2.Max(dragStart, dragEnd);
            float w = Mathf.Max(RSAssembly.Pixel, hi.x - lo.x), d = Mathf.Max(RSAssembly.Pixel, hi.y - lo.y);
            Vector2 c = (lo + hi) * 0.5f;
            var go = new GameObject(drawShape.ToString());
            Undo.RegisterCreatedObjectUndo(go, "부품 그리기");
            go.transform.SetParent(asm.transform, false);
            go.transform.localPosition = planeOrigin + axisA * c.x + axisB * c.y;
            Vector3 fwd = Vector3.Cross(axisA, planeN);
            go.transform.localRotation = Quaternion.LookRotation(fwd, planeN);
            var p = go.AddComponent<RSPart>();
            p.shape = drawShape;
            p.size = new Vector3(w, drawHeight, d);
            if (drawShape == RSPartShape.판)
            {
                // 판: 긴 쪽이 가로, 짧은 쪽 방향을 바라본다
                if (d > w) { go.transform.localRotation = Quaternion.LookRotation(axisA, planeN) ; p.size = new Vector3(d, drawHeight, RSAssembly.Pixel); }
                else p.size = new Vector3(w, drawHeight, RSAssembly.Pixel);
            }
            if (drawShape == RSPartShape.지붕) p.overhang = 0f;
            if (brush.surface != null && brush.mode == RSPaintMode.반복) p.paint = new RSFacePaint { surface = brush.surface };
            asm.MarkDirty();
        }

        // ─────────────────────────────────────────────────────────────
        // 밀기
        // ─────────────────────────────────────────────────────────────

        static void Push(RSAssembly asm, Event e, int ctrl)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && !e.alt && hoverPart != null:
                    {
                        pushPart = hoverPart;
                        Vector3 nl = pushPart.transform.InverseTransformDirection(hoverNormal);
                        Vector3 an = new Vector3(Mathf.Abs(nl.x), Mathf.Abs(nl.y), Mathf.Abs(nl.z));
                        pushAxis = an.x >= an.y && an.x >= an.z ? 0 : (an.y >= an.z ? 1 : 2);
                        pushSign = nl[pushAxis] >= 0 ? 1 : -1;
                        Vector3 axisL = Vector3.zero; axisL[pushAxis] = pushSign;
                        pushNormalW = pushPart.transform.TransformDirection(axisL).normalized;
                        pushPointW = hoverPoint;
                        pushStartPos = pushPart.transform.localPosition;
                        pushStartSize = pushPart.size;
                        Undo.RecordObjects(new Object[] { pushPart, pushPart.transform }, "면 밀기");
                        pushing = true;
                        GUIUtility.hotControl = ctrl;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag when pushing && pushPart != null:
                    {
                        float t = ClosestOnLine(pushPointW, pushNormalW, ray);
                        float sc = Mathf.Max(1e-5f, pushPart.transform.lossyScale[pushAxis]);
                        float dlt = Snap(asm, t / sc);
                        Vector3 size = pushStartSize;
                        size[pushAxis] = Mathf.Max(RSAssembly.Pixel, pushStartSize[pushAxis] + dlt);
                        float real = size[pushAxis] - pushStartSize[pushAxis];
                        Vector3 axisL = Vector3.zero; axisL[pushAxis] = 1f;
                        Vector3 shiftL;
                        if (pushAxis == 1) shiftL = pushSign > 0 ? Vector3.zero : -axisL * real;   // 기준점이 바닥: 아래로 밀면 위치도 내려감
                        else shiftL = axisL * (real * 0.5f * pushSign);
                        pushPart.size = size;
                        var tr = pushPart.transform;
                        tr.localPosition = pushStartPos + (tr.localRotation * Vector3.Scale(shiftL, tr.localScale));
                        EditorUtility.SetDirty(pushPart);
                        pushPart.Touch();
                        e.Use();
                    }
                    break;
                case EventType.MouseUp when pushing:
                    pushing = false; GUIUtility.hotControl = 0; e.Use();
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 칠하기
        // ─────────────────────────────────────────────────────────────

        static void Paint(RSAssembly asm, Event e)
        {
            if (e.type != EventType.MouseDown || e.button != 0 || hoverPart == null) return;
            if (e.alt)
            {
                var got = hoverPart.PaintFor(hoverFace);
                brush = got.Clone(); brush.face = -1;
                e.Use();
                return;
            }
            Undo.RecordObject(hoverPart, "면 칠하기");
            hoverPart.SetPaint(e.shift ? -1 : hoverFace, brush);
            EditorUtility.SetDirty(hoverPart);
            e.Use();
        }

        // ─────────────────────────────────────────────────────────────
        // 강조
        // ─────────────────────────────────────────────────────────────

        static void DrawHover(RSAssembly asm)
        {
            if (hoverPart == null || mode == RSBuildMode.그리기 && drawState != DrawState.없음) return;
            int pi = asm.builtParts.IndexOf(hoverPart);
            if (pi < 0) return;
            bool whole = mode == RSBuildMode.고르기 || (mode == RSBuildMode.칠하기 && Event.current.shift);
            Color c = mode == RSBuildMode.밀기 ? new Color(0.3f, 0.9f, 1f) : mode == RSBuildMode.칠하기 ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 1f, 1f, 0.8f);
            using (new Handles.DrawingScope(c, asm.transform.localToWorldMatrix))
            {
                foreach (var o in asm.outlines)
                {
                    if (o.part != pi || (!whole && o.face != hoverFace)) continue;
                    var pts = new Vector3[o.poly.Length + 1];
                    for (int i = 0; i < o.poly.Length; i++) pts[i] = o.poly[i];
                    pts[o.poly.Length] = o.poly[0];
                    Handles.DrawAAPolyLine(4f, pts);
                    if (!whole)
                    {
                        Handles.color = new Color(c.r, c.g, c.b, 0.18f);
                        Handles.DrawAAConvexPolygon(o.poly);
                        Handles.color = c;
                    }
                }
            }
            var paint = hoverPart.PaintFor(hoverFace);
            string label = hoverPart.name + " · " + hoverPart.FaceName(hoverFace) + "\n" +
                (paint.mode == RSPaintMode.반복 ? "반복" : paint.mode == RSPaintMode.도안칸 ? "도안 칸" : "늘리기") + " · " + (paint.surface != null ? paint.surface.Label : "기본 재질");
            Handles.Label(hoverPoint + hoverNormal * 0.05f, label, EditorStyles.helpBox);
        }
    }

    [EditorTool("조립 (RS 조립품)", typeof(RSAssembly))]
    public class RSBuildToolAssembly : EditorTool
    {
        GUIContent icon;
        public override GUIContent toolbarIcon { get { return icon ?? (icon = new GUIContent("조립", "RS 조립: 부품 그리기 · 면 밀기 · 칠하기")); } }
        public override void OnToolGUI(EditorWindow window) { RSBuildToolCore.OnGUI(RSBuildToolCore.Resolve(target), window); }
    }

    [EditorTool("조립 (RS 부품)", typeof(RSPart))]
    public class RSBuildToolPart : EditorTool
    {
        GUIContent icon;
        public override GUIContent toolbarIcon { get { return icon ?? (icon = new GUIContent("조립", "RS 조립: 부품 그리기 · 면 밀기 · 칠하기")); } }
        public override void OnToolGUI(EditorWindow window) { RSBuildToolCore.OnGUI(RSBuildToolCore.Resolve(target), window); }
    }
}
