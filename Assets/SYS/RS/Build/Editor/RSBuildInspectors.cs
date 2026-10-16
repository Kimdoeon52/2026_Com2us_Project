// RE:AL STEEL - 조립품 · 부품 인스펙터 + 메뉴
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    [CustomEditor(typeof(RSAssembly))]
    public class RSAssemblyEditor : Editor
    {
        static readonly List<RSAssembly.CellNeed> needs = new List<RSAssembly.CellNeed>();
        static readonly List<RSPart> tmpParts = new List<RSPart>();
        Vector2 sheetScroll;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var a = (RSAssembly)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                bool active = ToolManager.activeToolType == typeof(RSBuildToolAssembly);
                var old = GUI.backgroundColor;
                if (active) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button(active ? "● 조립 도구 켜짐 (씬 뷰 왼쪽 위 패널)" : "○ 조립 도구 켜기 (그리기 · 밀기 · 칠하기)", GUILayout.Height(26)))
                {
                    if (active) ToolManager.RestorePreviousPersistentTool();
                    else ToolManager.SetActiveTool<RSBuildToolAssembly>();
                }
                GUI.backgroundColor = old;
            }

            RSInspector.Draw(serializedObject);

            // 상태
            a.Parts(tmpParts);
            var mr = a.GetComponentInChildren<MeshRenderer>();
            var mf = a.GetComponentInChildren<MeshFilter>();
            int tris = mf != null && mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
            EditorGUILayout.HelpBox("부품 " + tmpParts.Count + "개 · 재질 " + (mr != null ? mr.sharedMaterials.Length : 0) + "개 (= 드로우콜) · 삼각형 " + tris, MessageType.None);

            // 부품 추가
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("부품 추가", GUILayout.Width(56));
                foreach (RSPartShape s in System.Enum.GetValues(typeof(RSPartShape)))
                    if (GUILayout.Button(s.ToString(), EditorStyles.miniButton)) AddPart(a, s);
            }

            DrawSheet(a);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("프리팹으로 저장 (소품처럼 여러 번 놓기)")) SavePrefab(a);
        }

        static void AddPart(RSAssembly a, RSPartShape s)
        {
            var go = new GameObject(s.ToString());
            Undo.RegisterCreatedObjectUndo(go, "부품 추가");
            go.transform.SetParent(a.transform, false);
            var p = go.AddComponent<RSPart>();
            p.shape = s;
            if (s == RSPartShape.판) p.size = new Vector3(1, 1, RSAssembly.Pixel);
            if (s == RSPartShape.지붕) p.size = new Vector3(2, 0.75f, 2);
            Selection.activeGameObject = go;
        }

        void DrawSheet(RSAssembly a)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("도안 (전용 그림)", EditorStyles.boldLabel);
            a.CollectSheetNeeds(needs);
            var sheet = a.sheet;
            if (needs.Count == 0 && sheet == null)
            {
                EditorGUILayout.HelpBox("쿠션 · 서랍 앞판처럼 모양에 맞춰 그린 그림이 필요한 면은, 조립 도구 '칠하기' 에서 방식을 '도안 칸' 으로 골라 그 면을 클릭하세요. 그다음 여기서 도안을 만듭니다.", MessageType.None);
                return;
            }
            int stale = 0;
            if (sheet != null)
                foreach (var n in needs)
                {
                    var c = sheet.Find(n.key);
                    if (c == null || c.rect.width != n.size.x || c.rect.height != n.size.y) stale++;
                }
            else stale = needs.Count;
            if (stale > 0)
                EditorGUILayout.HelpBox("새로 생겼거나 크기가 바뀐 칸 " + stale + "개 — '도안 갱신' 을 누르면 칸을 놓고, 이미 그린 그림은 그대로 둡니다 (크기가 바뀐 칸은 가장자리를 살려 늘림).", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(sheet == null ? "도안 만들기" : "도안 갱신")) { RSSheetBuilder.UpdateSheet(a, false); GUIUtility.ExitGUI(); }
                using (new EditorGUI.DisabledScope(sheet == null || !File.Exists(sheet.source)))
                {
                    if (GUILayout.Button("Aseprite 로 열기")) RSSheetBuilder.Open(sheet.source);
                    if (GUILayout.Button("다시 읽기", GUILayout.Width(70))) { RSSheetBuilder.Reprocess(sheet); GUIUtility.ExitGUI(); }
                }
            }
            if (sheet == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("원본", sheet.source, EditorStyles.miniLabel);
                if (GUILayout.Button("폴더", EditorStyles.miniButton, GUILayout.Width(40))) EditorUtility.RevealInFinder(Path.GetFullPath(sheet.source));
            }
            EditorGUI.BeginChangeCheck();
            bool fill = EditorGUILayout.Toggle(new GUIContent("빈 곳 채우기", "아직 안 그린 칸 = 그 면 재질로, 일부만 그린 칸 = 가장 가까운 그린 픽셀로"), sheet.fillEmpty);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(sheet, "빈 곳 채우기"); sheet.fillEmpty = fill; EditorUtility.SetDirty(sheet); RSSheetBuilder.Reprocess(sheet); GUIUtility.ExitGUI(); }

            // 미리보기: 결과 텍스처 + 칸 이름
            var tex = sheet.texture;
            if (tex == null) return;
            float scale = Mathf.Max(1f, Mathf.Floor(Mathf.Min(6f, (EditorGUIUtility.currentViewWidth - 40) / Mathf.Max(1, tex.width))));
            float w = tex.width * scale, h = tex.height * scale;
            sheetScroll = EditorGUILayout.BeginScrollView(sheetScroll, GUILayout.Height(Mathf.Min(h + 16, 360)));
            var r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
            EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            string hover = null;
            foreach (var c in sheet.cells)
            {
                var cr = new Rect(r.x + c.rect.x * scale, r.yMax - c.rect.yMax * scale, c.rect.width * scale, c.rect.height * scale);
                Outline(cr, new Color(1, 1, 1, 0.35f));
                if (cr.Contains(Event.current.mousePosition)) { hover = c.label + "  (" + c.rect.width + " × " + c.rect.height + " px)"; Outline(cr, new Color(1f, 0.8f, 0.2f)); }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField(hover ?? (tex.width + " × " + tex.height + " 픽셀 · 칸 " + sheet.cells.Count + "개 (칸에 마우스를 올리면 이름)"), EditorStyles.miniLabel);
            if (Event.current.type == EventType.MouseMove) Repaint();
        }

        static void Outline(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        public override bool RequiresConstantRepaint() { return false; }

        static void SavePrefab(RSAssembly a)
        {
            string folder = RSPaths.Ensure(RSPaths.Root + "/Prefabs/Build");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + RSSurfaceTools.Safe(a.name) + ".prefab");
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(a.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(a.gameObject, InteractionMode.UserAction);
                Debug.Log("[조립] 이 조립품의 프리팹에 반영했습니다", a);
                return;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(a.gameObject))
            {
                // 다른 프리팹(레벨 등) 안에 든 조립품: 그 프리팹은 건드리지 않고 새 프리팹으로 복사
                var copy = PrefabUtility.SaveAsPrefabAsset(a.gameObject, path);
                if (copy != null) { EditorGUIUtility.PingObject(copy); Debug.Log("[조립] 다른 프리팹 안에 있어서 새 프리팹으로 복사했습니다: " + path, copy); }
                return;
            }
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(a.gameObject, path, InteractionMode.UserAction);
            if (prefab != null) { EditorGUIUtility.PingObject(prefab); Debug.Log("[조립] 프리팹 저장: " + path + " — 배치 도구 · 드래그로 여러 번 놓을 수 있습니다", prefab); }
        }

        [MenuItem("GameObject/RE_AL STEEL/조립품 (건물 · 가구 · 울타리)", false, 9)]
        static void Create()
        {
            var go = new GameObject("조립품");
            Undo.RegisterCreatedObjectUndo(go, "조립품");
            var sv = SceneView.lastActiveSceneView;
            if (Selection.activeTransform != null) go.transform.position = Selection.activeTransform.position;
            else if (sv != null) go.transform.position = new Vector3(Mathf.Round(sv.pivot.x), Mathf.Round(sv.pivot.y), Mathf.Round(sv.pivot.z));
            var a = go.AddComponent<RSAssembly>();
            a.palette = RSSurfaceTools.DefaultSet();
            if (a.palette != null && a.palette.surfaces.Count > 0) a.defaultSurface = a.palette.surfaces[0];
            Selection.activeGameObject = go;
            EditorApplication.delayCall += () => { if (Selection.activeGameObject == go) ToolManager.SetActiveTool<RSBuildToolAssembly>(); };
        }
    }

    [CustomEditor(typeof(RSPart)), CanEditMultipleObjects]
    public class RSPartEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var p = (RSPart)target;
            if (targets.Length == 1)
            {
                var a = p.Owner;
                if (a == null) EditorGUILayout.HelpBox("조립품(RSAssembly) 아래에 있어야 보입니다.", MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool active = ToolManager.activeToolType == typeof(RSBuildToolPart);
                    if (GUILayout.Button(active ? "● 조립 도구 켜짐" : "○ 조립 도구 켜기", GUILayout.Height(22)))
                    {
                        if (active) ToolManager.RestorePreviousPersistentTool(); else ToolManager.SetActiveTool<RSBuildToolPart>();
                    }
                    if (a != null && GUILayout.Button("조립품 고르기", GUILayout.Height(22), GUILayout.Width(90))) Selection.activeGameObject = a.gameObject;
                }
                var px = p.size * RSSurface.PixelsPerMeter;
                EditorGUILayout.LabelField("크기 = " + Mathf.RoundToInt(px.x) + " × " + Mathf.RoundToInt(px.y) + " × " + Mathf.RoundToInt(px.z) + " 픽셀", EditorStyles.miniLabel);
            }
            RSInspector.Draw(serializedObject);

            if (targets.Length != 1) return;
            // 면 목록: 칠 요약
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("면별 칠", EditorStyles.boldLabel);
            var names = RSShapes.FaceNames(p);
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null) continue;
                var paint = p.PaintFor(i);
                bool own = paint != p.paint;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(names[i], GUILayout.Width(90));
                    EditorGUILayout.LabelField((paint.mode == RSPaintMode.반복 ? "반복" : paint.mode == RSPaintMode.도안칸 ? "도안 칸" : "늘리기") + " · " +
                        (paint.surface != null ? paint.surface.Label : "기본") + (own ? "" : "  (기본 칠)"), EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(!own))
                        if (GUILayout.Button("기본으로", EditorStyles.miniButton, GUILayout.Width(60)))
                        {
                            Undo.RecordObject(p, "면 칠 지우기");
                            p.facePaints.RemoveAll(x => x != null && x.face == i);
                            p.Touch();
                            EditorUtility.SetDirty(p);
                        }
                }
            }
        }
    }
}
