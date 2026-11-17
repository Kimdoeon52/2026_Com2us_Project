// RE:AL STEEL - 이동 막기 벽 인스펙터 · 씬 뷰 편집
//
// 씬 뷰: 벽마다 가운데 점(빨강 = 자동, 하늘색 = 수동). 점을 누르면 선택 →
//   화살표 = 옮기기, 상자 면의 점 = 크기, 노란 원 = 방향(Y 회전). Delete 키 = 지우기, Esc = 선택 해제
// 자동 벽을 건드리면 수동 벽으로 바뀌고, 원래 자리는 '숨긴 자동 벽 자리' 로 기록돼 다시 분석해도 거기엔 안 생긴다.
// 고친 내용은 씬의 프리팹 오버라이드로 남는다 → 인스펙터의 '프리팹에 반영' 을 누르면 MAP_ 프리팹에 저장.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RealSteel.Terrain.EditorTools
{
    [CustomEditor(typeof(RSBlockWalls))]
    public class RSBlockWallsEditor : Editor
    {
        static RSBlockWalls selOwner;
        static int selList = -1;     // 0 = 자동, 1 = 수동
        static int selIdx = -1;
        readonly BoxBoundsHandle bounds = new BoxBoundsHandle();

        static List<RSBlockWalls.Box> ListOf(RSBlockWalls w, int k) { return k == 0 ? w.boxes : w.manual; }

        static bool Valid(RSBlockWalls w)
        {
            if (selOwner != w || selList < 0) return false;
            var l = ListOf(w, selList);
            return l != null && selIdx >= 0 && selIdx < l.Count;
        }

        static void Select(RSBlockWalls w, int k, int i) { selOwner = w; selList = k; selIdx = i; }
        static void Deselect() { selList = -1; selIdx = -1; }

        void OnDisable() { if (selOwner == target) Deselect(); }

        // ─────────────────────────────────────────────────────────────
        // 인스펙터
        // ─────────────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            var w = (RSBlockWalls)target;
            if (w.manual == null) w.manual = new List<RSBlockWalls.Box>();
            if (w.suppressed == null) w.suppressed = new List<RSBlockWalls.Box>();

            EditorGUILayout.HelpBox(
                "씬 뷰에서 벽 가운데 점을 누르면 선택됩니다.\n" +
                "· 화살표 = 옮기기 · 상자 면의 점 = 크기 · 노란 원 = 방향 · Delete = 지우기 · Esc = 선택 해제\n" +
                "· 자동 벽(빨강)을 고치면 수동 벽(하늘색)이 되고, 다시 분석해도 그 자리엔 자동 벽이 안 생깁니다.",
                MessageType.Info);
            EditorGUILayout.LabelField("자동 벽 " + w.boxes.Count + "개 · 수동 벽 " + w.manual.Count + "개 · 숨긴 자동 벽 자리 " + w.suppressed.Count + "개");

            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.Toggle(new GUIContent("Always Show", "선택하지 않아도 씬 뷰에 보이기"), w.alwaysShow);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(w, "벽 보이기"); w.alwaysShow = show; Changed(w); }

            EditorGUILayout.Space();
            if (Valid(w))
            {
                var b = ListOf(w, selList)[selIdx];
                EditorGUILayout.LabelField((selList == 0 ? "선택: 자동 벽 #" : "선택: 수동 벽 #") + selIdx, EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 c = EditorGUILayout.Vector3Field("위치", b.center);
                Vector3 sz = EditorGUILayout.Vector3Field("크기", b.size);
                float yaw = EditorGUILayout.FloatField("방향 (Y 회전)", b.yaw);
                if (EditorGUI.EndChangeCheck())
                    Edit(w, x => { x.center = c; x.size = new Vector3(Mathf.Max(0.02f, sz.x), Mathf.Max(0.02f, sz.y), Mathf.Max(0.02f, sz.z)); x.yaw = yaw; return x; });
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("복제")) Duplicate(w);
                    if (GUILayout.Button("지우기")) Delete(w);
                    if (GUILayout.Button("선택 해제")) { Deselect(); SceneView.RepaintAll(); }
                }
            }
            else EditorGUILayout.LabelField("선택한 벽 없음 — 씬 뷰에서 벽 가운데 점을 누르세요", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            if (GUILayout.Button("수동 벽 추가 (씬 뷰 가운데 바닥에)")) AddAtView(w);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(w.manual.Count == 0))
                    if (GUILayout.Button("수동 벽 전부 지우기") && EditorUtility.DisplayDialog("이동 막기", "수동 벽 " + w.manual.Count + "개를 지울까요?", "지우기", "취소"))
                    { Undo.RecordObject(w, "수동 벽 지우기"); w.manual.Clear(); Deselect(); Changed(w); }
                using (new EditorGUI.DisabledScope(w.suppressed.Count == 0))
                    if (GUILayout.Button(new GUIContent("숨긴 자리 초기화", "다음 분석 때 손으로 옮기거나 지운 자리에도 자동 벽을 다시 세운다")))
                    { Undo.RecordObject(w, "숨긴 자리 초기화"); w.suppressed.Clear(); Changed(w); }
            }

            if (PrefabUtility.IsPartOfPrefabInstance(w))
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("프리팹에 반영 (MAP_ 프리팹에 저장)", GUILayout.Height(24)))
                    Debug.Log("[이동 막기] " + RSBlockWallsUtil.ApplyToPrefab(w));
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 씬 뷰
        // ─────────────────────────────────────────────────────────────

        void OnSceneGUI()
        {
            var w = (RSBlockWalls)target;
            if (w.manual == null) w.manual = new List<RSBlockWalls.Box>();
            var e = Event.current;

            if (Valid(w))
            {
                if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && (e.commandName == "SoftDelete" || e.commandName == "Delete"))
                {
                    if (e.type == EventType.ExecuteCommand) Delete(w);
                    e.Use();
                    return;
                }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Deselect(); e.Use(); Repaint(); return; }
            }

            Matrix4x4 m = w.transform.localToWorldMatrix;

            // 선택용 점
            for (int k = 0; k < 2; k++)
            {
                var l = ListOf(w, k);
                if (l == null) continue;
                Handles.color = k == 0 ? RSBlockWalls.AutoColor : RSBlockWalls.ManualColor;
                for (int i = 0; i < l.Count; i++)
                {
                    if (selOwner == w && k == selList && i == selIdx) continue;
                    Vector3 wc = m.MultiplyPoint3x4(l[i].center);
                    float s = HandleUtility.GetHandleSize(wc) * 0.05f;
                    if (Handles.Button(wc, Quaternion.identity, s, s * 1.6f, Handles.DotHandleCap)) { Select(w, k, i); Repaint(); }
                }
            }
            if (!Valid(w)) return;

            var b = ListOf(w, selList)[selIdx];
            Matrix4x4 bm = m * b.Local;
            Vector3 center = bm.MultiplyPoint3x4(Vector3.zero);
            Quaternion rot = w.transform.rotation * Quaternion.Euler(0f, b.yaw, 0f);

            // 크기
            using (new Handles.DrawingScope(bm))
            {
                bounds.center = Vector3.zero;
                bounds.size = b.size;
                bounds.SetColor(RSBlockWalls.ManualColor);
                EditorGUI.BeginChangeCheck();
                bounds.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 off = Quaternion.Euler(0f, b.yaw, 0f) * bounds.center;
                    Vector3 ns = bounds.size;
                    Edit(w, x => { x.center += off; x.size = new Vector3(Mathf.Max(0.02f, ns.x), Mathf.Max(0.02f, ns.y), Mathf.Max(0.02f, ns.z)); return x; });
                    return;
                }
            }

            // 옮기기
            EditorGUI.BeginChangeCheck();
            Vector3 np = Handles.PositionHandle(center, Tools.pivotRotation == PivotRotation.Local ? rot : Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 lp = w.transform.InverseTransformPoint(np);
                Edit(w, x => { x.center = lp; return x; });
                return;
            }

            // 방향
            Handles.color = new Color(1f, 0.85f, 0.2f);
            EditorGUI.BeginChangeCheck();
            Quaternion nr = Handles.Disc(rot, center, w.transform.up, HandleUtility.GetHandleSize(center) * 1.1f, false, 5f);
            if (EditorGUI.EndChangeCheck())
            {
                float ny = (Quaternion.Inverse(w.transform.rotation) * nr).eulerAngles.y;
                Edit(w, x => { x.yaw = Mathf.DeltaAngle(0f, ny); return x; });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 편집
        // ─────────────────────────────────────────────────────────────

        /// <summary>자동 벽이면 수동 벽으로 옮기고(원래 자리는 숨김 기록) 고친다</summary>
        static void Edit(RSBlockWalls w, System.Func<RSBlockWalls.Box, RSBlockWalls.Box> f)
        {
            Undo.RecordObject(w, "이동 막기 벽 조정");
            if (selList == 0)
            {
                var orig = w.boxes[selIdx];
                w.boxes.RemoveAt(selIdx);
                w.suppressed.Add(orig);
                w.manual.Add(orig);
                Select(w, 1, w.manual.Count - 1);
            }
            w.manual[selIdx] = f(w.manual[selIdx]);
            Changed(w);
        }

        static void Delete(RSBlockWalls w)
        {
            if (!Valid(w)) return;
            Undo.RecordObject(w, "이동 막기 벽 지우기");
            if (selList == 0) { w.suppressed.Add(w.boxes[selIdx]); w.boxes.RemoveAt(selIdx); }
            else w.manual.RemoveAt(selIdx);
            Deselect();
            Changed(w);
        }

        static void Duplicate(RSBlockWalls w)
        {
            if (!Valid(w)) return;
            var b = ListOf(w, selList)[selIdx];
            Undo.RecordObject(w, "이동 막기 벽 복제");
            b.center += Quaternion.Euler(0f, b.yaw, 0f) * new Vector3(Mathf.Max(b.size.x, b.size.z) <= b.size.x ? b.size.x : 0f, 0f, b.size.z > b.size.x ? b.size.z : 0f);
            w.manual.Add(b);
            Select(w, 1, w.manual.Count - 1);
            Changed(w);
        }

        static void AddAtView(RSBlockWalls w)
        {
            var sv = SceneView.lastActiveSceneView;
            Vector3 p = sv != null ? sv.pivot : w.transform.position;
            RaycastHit hit;
            float ground = p.y;
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
            const float h = 2.3f;
            Vector3 lc = w.transform.InverseTransformPoint(new Vector3(p.x, ground - 0.3f + h * 0.5f, p.z));
            Undo.RecordObject(w, "수동 벽 추가");
            w.manual.Add(new RSBlockWalls.Box(lc, new Vector3(2f, h, 0.1f)));
            Select(w, 1, w.manual.Count - 1);
            Changed(w);
        }

        static void Changed(RSBlockWalls w)
        {
            EditorUtility.SetDirty(w);
            PrefabUtility.RecordPrefabInstancePropertyModifications(w);
            w.Build();
            SceneView.RepaintAll();
        }
    }

    /// <summary>창 · 인스펙터가 같이 쓰는 도우미</summary>
    public static class RSBlockWallsUtil
    {
        /// <summary>맵 프리팹 인스턴스 안이면 그 프리팹에 저장한다. 결과 설명을 돌려준다</summary>
        public static string ApplyToPrefab(RSBlockWalls comp)
        {
            var go = comp.gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAddedGameObjectOverride(go))
                return "씬 (" + go.scene.name + ")";
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            string path = root != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) : null;
            if (string.IsNullOrEmpty(path)) return "씬 (" + go.scene.name + ")";
            try
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
                if (PrefabUtility.IsAddedGameObjectOverride(go)) PrefabUtility.ApplyAddedGameObject(go, path, InteractionMode.AutomatedAction);
                else if (PrefabUtility.IsAddedComponentOverride(comp)) PrefabUtility.ApplyAddedComponent(comp, path, InteractionMode.AutomatedAction);
                else PrefabUtility.ApplyObjectOverride(comp, path, InteractionMode.AutomatedAction);
                return "프리팹 " + path;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[이동 막기] 프리팹에 반영하지 못해 씬에만 남겼습니다 — 맵 루트에서 Overrides → Apply All 로 반영하세요\n" + e);
                return "씬 (프리팹 반영 실패 — Overrides → Apply All)";
            }
        }

        /// <summary>새 자동 벽에서 숨긴 자리(손으로 옮기거나 지운 자동 벽)와 겹치는 구간을 잘라 낸다</summary>
        public static List<RSBlockWalls.Box> Clip(RSBlockWalls.Box nb, List<RSBlockWalls.Box> sup)
        {
            var result = new List<RSBlockWalls.Box>();
            bool alongX = nb.size.x >= nb.size.z;
            int la = alongX ? 0 : 2, ta = alongX ? 2 : 0;    // 긴 축 · 얇은 축
            var pieces = new List<Vector2> { new Vector2(nb.center[la] - nb.size[la] * 0.5f, nb.center[la] + nb.size[la] * 0.5f) };
            if (sup != null)
                foreach (var s in sup)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(s.yaw, 0f)) > 0.01f) continue;
                    if (s.size[la] < 0.2f) continue;                                                  // 수직으로 놓인 얇은 벽은 자르지 않는다 (틈이 생김)
                    if (Mathf.Abs(nb.center[ta] - s.center[ta]) > s.size[ta] * 0.5f + 0.15f) continue;
                    if (Mathf.Abs(nb.center.y - s.center.y) > (nb.size.y + s.size.y) * 0.5f) continue;
                    float c0 = s.center[la] - s.size[la] * 0.5f, c1 = s.center[la] + s.size[la] * 0.5f;
                    var next = new List<Vector2>();
                    foreach (var p in pieces)
                    {
                        if (c1 <= p.x || c0 >= p.y) { next.Add(p); continue; }
                        if (c0 > p.x) next.Add(new Vector2(p.x, c0));
                        if (c1 < p.y) next.Add(new Vector2(c1, p.y));
                    }
                    pieces = next;
                }
            foreach (var p in pieces)
            {
                if (p.y - p.x < 0.05f) continue;
                var c = nb.center; c[la] = (p.x + p.y) * 0.5f;
                var sz = nb.size; sz[la] = p.y - p.x;
                result.Add(new RSBlockWalls.Box(c, sz));
            }
            return result;
        }
    }
}
