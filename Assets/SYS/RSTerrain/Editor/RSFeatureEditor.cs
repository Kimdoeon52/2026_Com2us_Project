// RE:AL STEEL - RS Terrain 요소 에디터 (씬 핸들)
//   둔덕 · 작업장 : 사각 핸들로 크기
//   더미 · 웅덩이 : 원 핸들로 반경
//   길 · 배수로   : 점 클릭 → 이동 핸들, Shift+클릭 = 끝에 점 추가, Ctrl+점 클릭 = 삭제
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using RealSteel.Common.EditorTools;

namespace RealSteel.Terrain.EditorTools
{
    [CustomEditor(typeof(RSTerrainFeature), true)]
    public class RSFeatureEditor : Editor
    {
        readonly BoxBoundsHandle box = new BoxBoundsHandle();
        static int selectedPoint = -1;

        public override void OnInspectorGUI()
        {
            var f = (RSTerrainFeature)target;
            RSHelpGUI.DrawSummary(target);
            if (f.Owner == null)
                EditorGUILayout.HelpBox("RS 지형(RSTerrain) 오브젝트의 자식이어야 적용됩니다.", MessageType.Warning);

            DrawDefaultInspector();

            var sp = f as RSSpline;
            if (sp != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("씬: 점 클릭 = 선택 후 이동 · Shift+클릭 = 끝에 점 추가 · Ctrl+점 클릭 = 삭제", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("끝에 점 추가"))
                    {
                        Undo.RecordObject(sp, "점 추가");
                        Vector3 last = sp.points.Count > 0 ? sp.points[sp.points.Count - 1] : Vector3.zero;
                        Vector3 prev = sp.points.Count > 1 ? sp.points[sp.points.Count - 2] : last - Vector3.right * 3f;
                        sp.points.Add(last + (last - prev).normalized * 3f);
                        sp.Notify();
                    }
                    if (GUILayout.Button("마지막 점 삭제") && sp.points.Count > 2)
                    {
                        Undo.RecordObject(sp, "점 삭제");
                        sp.points.RemoveAt(sp.points.Count - 1);
                        sp.Notify();
                    }
                    if (GUILayout.Button("순서 뒤집기"))
                    {
                        Undo.RecordObject(sp, "점 순서 뒤집기");
                        sp.points.Reverse();
                        sp.Notify();
                    }
                }
                if (sp.kind == RSSpline.Kind.Path && sp.heightMode == RSSpline.PathHeight.Points &&
                    GUILayout.Button("점 높이를 현재 지면에 맞추기"))
                    SnapPointsToGround(sp);
            }

            var pad = f as RSPad;
            if (pad != null && GUILayout.Button("윗면 높이를 현재 지면에 맞추기"))
            {
                var t = pad.Owner;
                if (t != null && t.HasHeights)
                {
                    Undo.RecordObject(pad.transform, "작업장 높이");
                    var l = t.transform.InverseTransformPoint(pad.transform.position);
                    l.y = t.SampleHeight(l.x, l.z);
                    pad.transform.position = t.transform.TransformPoint(l);
                }
            }

            var sc = f as RSScatter;
            if (sc != null)
            {
                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("기본 머티리얼 채우기 (MAT_Stage_*)"))
                    {
                        Undo.RecordObject(sc, "폐자재 머티리얼");
                        RSTerrainMenu.FillScatterMaterials(sc);
                        sc.Notify();
                    }
                    if (GUILayout.Button("다른 배치로 (시드 바꾸기)"))
                    {
                        Undo.RecordObject(sc, "폐자재 배치");
                        sc.salt = Random.Range(1, 999999);
                        sc.Notify();
                    }
                }
            }

            var fol = f as RSFoliage;
            if (fol != null) RSFoliageTools.DrawFoliageInspector(fol);

            var owner = f.Owner;
            if (owner != null)
            {
                EditorGUILayout.Space(4f);
                if (GUILayout.Button("지형 다시 만들기")) { owner.Rebuild(); SceneView.RepaintAll(); }
            }
        }

        static void SnapPointsToGround(RSSpline sp)
        {
            var t = sp.Owner;
            if (t == null || !t.HasHeights) return;
            Undo.RecordObject(sp, "점 높이 맞추기");
            for (int i = 0; i < sp.points.Count; i++)
            {
                Vector3 w = sp.transform.TransformPoint(sp.points[i]);
                Vector3 l = t.transform.InverseTransformPoint(w);
                l.y = t.SampleHeight(l.x, l.z);
                sp.points[i] = sp.transform.InverseTransformPoint(t.transform.TransformPoint(l));
            }
            sp.Notify();
        }

        void OnSceneGUI()
        {
            var f = (RSTerrainFeature)target;
            var t = f.Owner;
            if (t == null) return;

            if (f is RSPlateau) SizeHandle(f, ((RSPlateau)f).size, v => ((RSPlateau)f).size = v);
            else if (f is RSPad) SizeHandle(f, ((RSPad)f).size, v => ((RSPad)f).size = v);
            else if (f is RSHeap) RadiusHandle(f, ((RSHeap)f).radius, v => ((RSHeap)f).radius = v);
            else if (f is RSPit) RadiusHandle(f, ((RSPit)f).radius, v => ((RSPit)f).radius = v);
            else if (f is RSScatter && ((RSScatter)f).area == RSScatter.Area.Circle)
                RadiusHandle(f, ((RSScatter)f).radius, v => ((RSScatter)f).radius = v);
            else if (f is RSFoliage && ((RSFoliage)f).area == RSFoliage.Area.Circle)
                RadiusHandle(f, ((RSFoliage)f).radius, v => ((RSFoliage)f).radius = v);
            else if (f is RSSpline) SplineHandles((RSSpline)f, t);
        }

        void SizeHandle(RSTerrainFeature f, Vector2 size, System.Action<Vector2> set)
        {
            var rot = Quaternion.Euler(0f, f.transform.eulerAngles.y, 0f);
            using (new Handles.DrawingScope(f.GizmoColor, Matrix4x4.TRS(f.transform.position, rot, Vector3.one)))
            {
                box.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;
                box.center = Vector3.zero;
                box.size = new Vector3(size.x, 0f, size.y);
                EditorGUI.BeginChangeCheck();
                box.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(f, "크기");
                    Undo.RecordObject(f.transform, "크기");
                    set(new Vector2(Mathf.Max(0.2f, box.size.x), Mathf.Max(0.2f, box.size.z)));
                    f.transform.position = Handles.matrix.MultiplyPoint3x4(box.center);
                    f.Notify();
                }
            }
        }

        void RadiusHandle(RSTerrainFeature f, float r, System.Action<float> set)
        {
            Handles.color = f.GizmoColor;
            EditorGUI.BeginChangeCheck();
            float nr = Handles.RadiusHandle(Quaternion.identity, f.transform.position, r);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(f, "반경");
                set(Mathf.Max(0.1f, nr));
                f.Notify();
            }
        }

        void SplineHandles(RSSpline sp, RSTerrain t)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (e.shift && e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            var world = new List<Vector3>();
            foreach (var p in sp.points) world.Add(sp.transform.TransformPoint(p));

            Handles.color = sp.GizmoColor;
            if (world.Count > 1) Handles.DrawAAPolyLine(3f, world.ToArray());

            for (int i = 0; i < world.Count; i++)
            {
                float hs = HandleUtility.GetHandleSize(world[i]) * 0.09f;
                Handles.color = i == selectedPoint ? Color.yellow : Color.white;
                if (Handles.Button(world[i], Quaternion.identity, hs, hs * 1.3f, Handles.SphereHandleCap))
                {
                    if (e.control && sp.points.Count > 2)
                    {
                        Undo.RecordObject(sp, "점 삭제");
                        sp.points.RemoveAt(i);
                        selectedPoint = -1;
                        sp.Notify();
                        return;
                    }
                    selectedPoint = i;
                }
            }

            if (selectedPoint >= 0 && selectedPoint < world.Count)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 np = Handles.PositionHandle(world[selectedPoint], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sp, "점 이동");
                    // 길 '지형 따라' 는 높이가 의미 없으니 지면에 붙여 둔다
                    if (sp.kind == RSSpline.Kind.Path && sp.heightMode == RSSpline.PathHeight.Follow)
                        np.y = t.SampleHeightWorld(np);
                    sp.points[selectedPoint] = sp.transform.InverseTransformPoint(np);
                    sp.Notify();
                }
            }

            // Shift+클릭 → 끝에 점 추가
            if (e.shift && e.type == EventType.MouseDown && e.button == 0)
            {
                Vector3 hit;
                if (t.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition), out hit))
                {
                    Undo.RecordObject(sp, "점 추가");
                    sp.points.Add(sp.transform.InverseTransformPoint(hit));
                    selectedPoint = sp.points.Count - 1;
                    sp.Notify();
                    e.Use();
                }
            }
        }
    }
}
