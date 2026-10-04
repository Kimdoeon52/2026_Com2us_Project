// RE:AL STEEL - 편집 메시 인스펙터 · 메뉴 · 가져오기 · 딱 맞는 그림 만들기
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    [CustomEditor(typeof(RSEditMesh))]
    public class RSEditMeshEditor : Editor
    {
        GameObject importSrc;

        public override void OnInspectorGUI()
        {
            RSHelpGUI.DrawSummary(target);
            var m = (RSEditMesh)target;

            bool active = ToolManager.activeToolType == typeof(RSEditTool);
            var old = GUI.backgroundColor;
            if (active) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button(active ? "● 편집 도구 켜짐 (씬 뷰 왼쪽 위 패널)" : "○ 편집 도구 켜기 (모양 · 칠하기)", GUILayout.Height(26)))
            {
                if (active) ToolManager.RestorePreviousPersistentTool();
                else ToolManager.SetActiveTool<RSEditTool>();
            }
            GUI.backgroundColor = old;

            RSInspector.Draw(serializedObject);

            m.Parts(out int parts);
            EditorGUILayout.HelpBox("점 " + m.verts.Count + " · 면 " + m.faces.Count + " · 부품 " + parts + " · 재질 " + m.builtMaterials + "개 (= 드로우콜) · 삼각형 " + m.builtTriangles, MessageType.None);

            if (m.faces.Count == 0 && GUILayout.Button("그려서 시작 (씬 뷰에서 바닥을 끌기)"))
            {
                RSEditCore.DrawWhenReady(m);
                if (!active) ToolManager.SetActiveTool<RSEditTool>();
                SceneView.RepaintAll();
            }
            if (m.faces.Count == 0 && GUILayout.Button("1m 상자 하나로 시작"))
            {
                Undo.RecordObject(m, "상자");
                RSEditOps.AddBox(m, Vector3.zero, Vector3.one, null);
                m.MarkDirty(); EditorUtility.SetDirty(m);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                importSrc = (GameObject)EditorGUILayout.ObjectField(importSrc, typeof(GameObject), true);
                GUI.enabled = importSrc != null;
                if (GUILayout.Button("모델 덧붙이기", GUILayout.Width(96)))
                {
                    var src = importSrc;
                    EditorApplication.delayCall += () => { if (m != null && src != null) RSEditMeshTools.Import(src, m, false); };
                }
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("기준점 = 바닥 가운데", "점들을 옮겨 이 오브젝트의 기준점이 모양의 바닥 가운데에 오게 한다 (월드 위치는 그대로)"))) RSEditMeshTools.PivotToBottom(m);
                if (GUILayout.Button(new GUIContent("면 정리", "같은 자리 점 합치기 + 같은 평면의 이웃 삼각형을 사각형 · 다각형으로"))) { RSEditMeshTools.Tidy(m); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.Space(4);
            if (GUILayout.Button("프리팹으로 저장 (소품처럼 여러 번 놓기)")) RSEditMeshTools.SavePrefab(m);
        }
    }

    public static class RSEditMeshTools
    {
        // ─────────────────────────────────────────────────────────────
        // 메뉴
        // ─────────────────────────────────────────────────────────────

        [MenuItem("GameObject/RE_AL STEEL/편집 메시 (새로 그리기)", false, 8)]
        static void CreateNew()
        {
            var go = new GameObject("편집 메시");
            Undo.RegisterCreatedObjectUndo(go, "편집 메시");
            var sv = SceneView.lastActiveSceneView;
            if (Selection.activeTransform != null) go.transform.position = Selection.activeTransform.position;
            else if (sv != null) go.transform.position = new Vector3(Mathf.Round(sv.pivot.x), Mathf.Round(sv.pivot.y), Mathf.Round(sv.pivot.z));
            var m = go.AddComponent<RSEditMesh>();
            m.palette = RSSurfaceTools.DefaultSet();
            m.MarkDirty();
            RSEditCore.DrawWhenReady(m);
            Select(go);
        }

        [MenuItem("GameObject/RE_AL STEEL/선택한 모델을 편집 메시로", false, 8)]
        static void ConvertSelected()
        {
            var src = Selection.activeGameObject;
            if (src == null) return;
            var go = new GameObject(src.name + " (편집)");
            Undo.RegisterCreatedObjectUndo(go, "편집 메시로");
            go.transform.SetParent(src.transform.parent, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
            go.layer = src.layer;
            var m = go.AddComponent<RSEditMesh>();
            m.palette = RSSurfaceTools.DefaultSet();
            if (!Import(src, m, true)) { Undo.DestroyObjectImmediate(go); return; }
            PivotToBottom(m, false);
            Select(go);
        }

        [MenuItem("GameObject/RE_AL STEEL/선택한 모델을 편집 메시로", true)]
        static bool ConvertSelectedValidate()
        {
            var s = Selection.activeGameObject;
            return s != null && s.GetComponent<RSEditMesh>() == null && s.GetComponentInChildren<MeshFilter>(true) != null;
        }

        static void Select(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorApplication.delayCall += () => { if (Selection.activeGameObject == go) ToolManager.SetActiveTool<RSEditTool>(); };
        }

        // ─────────────────────────────────────────────────────────────
        // 가져오기
        // ─────────────────────────────────────────────────────────────

        /// <summary>모델(자식 메시 전부)을 편집 메시에 덧붙인다. hideSource = 원래 모델 끄기</summary>
        public static bool Import(GameObject src, RSEditMesh m, bool hideSource)
        {
            if (src == null || m == null) return false;
            var mfs = new List<MeshFilter>();
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !mf.name.StartsWith("__RS_")) mfs.Add(mf);
            if (mfs.Count == 0) { EditorUtility.DisplayDialog("가져오기", "'" + src.name + "' 에 메시가 없습니다.", "확인"); return false; }

            // 읽기 꺼진 모델은 잠깐 켰다가 되돌린다
            var restore = new List<ModelImporter>();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh.isReadable) continue;
                var mi = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mf.sharedMesh)) as ModelImporter;
                if (mi == null || restore.Contains(mi)) continue;
                if (restore.Count == 0 && !EditorUtility.DisplayDialog("가져오기",
                    "'" + Path.GetFileName(mi.assetPath) + "' 은 메시 읽기(Read/Write)가 꺼져 있습니다.\n가져오는 동안만 잠깐 켜고 끝나면 원래대로 돌립니다.", "계속", "취소"))
                    return false;
                mi.isReadable = true; mi.SaveAndReimport();
                restore.Add(mi);
            }

            Undo.RecordObject(m, "모델 가져오기");
            int faces = 0, matched = 0;
            var byTex = SurfacesByTexture();
            try
            {
                foreach (var mf in mfs)
                {
                    var mesh = mf.sharedMesh;   // 다시 가져온 뒤의 메시
                    var toLocal = m.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    // 머티리얼 텍스처가 재질(RSSurface)의 텍스처와 같으면 그 재질로 (반복)
                    var mr = mf.GetComponent<MeshRenderer>();
                    var surfs = new List<RSSurface>();
                    if (mr != null)
                        foreach (var mat in mr.sharedMaterials)
                        {
                            RSSurface found = null;
                            var tex = MainTexture(mat);
                            if (tex != null) byTex.TryGetValue(tex, out found);
                            if (found != null) matched++;
                            surfs.Add(found);
                        }
                    faces += RSEditOps.AppendMesh(m, mesh, toLocal, 1e-4f, surfs);
                }
            }
            finally
            {
                foreach (var mi in restore) { mi.isReadable = false; mi.SaveAndReimport(); }
            }
            int merged = RSEditOps.MergeCoplanar(m);
            m.MarkDirty(); EditorUtility.SetDirty(m);
            RSEditCore.ClearSelection();
            m.Parts(out int parts);
            if (hideSource) { Undo.RecordObject(src, "원래 모델 끄기"); src.SetActive(false); }
            Debug.Log("[편집 메시] '" + src.name + "' 가져옴: 삼각형 " + faces + " → 면 " + m.faces.Count + " (같은 평면 " + merged + "번 합침) · 부품 " + parts +
                      " · 재질 맞춘 머티리얼 " + matched +
                      (hideSource ? " · 원래 모델은 꺼 두었습니다" : ""), m);
            return true;
        }

        static Dictionary<Texture, RSSurface> SurfacesByTexture()
        {
            var d = new Dictionary<Texture, RSSurface>();
            foreach (var g in AssetDatabase.FindAssets("t:RSSurface"))
            {
                var s = AssetDatabase.LoadAssetAtPath<RSSurface>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null && s.texture != null && !d.ContainsKey(s.texture)) d[s.texture] = s;
            }
            return d;
        }

        static Texture MainTexture(Material mat)
        {
            if (mat == null) return null;
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) return mat.GetTexture("_BaseMap");
            if (mat.HasProperty("_MainTex")) return mat.GetTexture("_MainTex");
            return null;
        }

        /// <summary>같은 자리 점 합치기 + 같은 평면 이웃 면 합치기</summary>
        public static void Tidy(RSEditMesh m)
        {
            Undo.RecordObject(m, "면 정리");
            int before = m.faces.Count;
            // 같은 자리 점 합치기: 빈 메시에 다시 덧붙이는 방식
            var map = new Dictionary<Vector3Int, int>();
            var remap = new int[m.verts.Count];
            var nv = new List<Vector3>();
            for (int i = 0; i < m.verts.Count; i++)
            {
                var p = m.verts[i];
                var k = new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
                if (!map.TryGetValue(k, out int idx)) { idx = nv.Count; nv.Add(p); map[k] = idx; }
                remap[i] = idx;
            }
            m.verts.Clear(); m.verts.AddRange(nv);
            var keep = new List<RSEFace>();
            foreach (var f in m.faces)
            {
                var l = new List<int>();
                foreach (int vi in f.v) { int x = vi >= 0 && vi < remap.Length ? remap[vi] : -1; if (x >= 0 && (l.Count == 0 || l[l.Count - 1] != x)) l.Add(x); }
                while (l.Count > 1 && l[0] == l[l.Count - 1]) l.RemoveAt(l.Count - 1);
                if (l.Count < 3) continue;
                f.v = l.ToArray();
                keep.Add(f);
            }
            m.faces.Clear(); m.faces.AddRange(keep);
            RSEditOps.MergeCoplanar(m);
            RSEditOps.Compact(m);
            m.MarkDirty(); EditorUtility.SetDirty(m);
            RSEditCore.ClearSelection();
            Debug.Log("[편집 메시] 면 정리: " + before + " → " + m.faces.Count, m);
        }

        /// <summary>점들을 옮겨 기준점을 모양의 바닥 가운데로 (월드 위치는 그대로)</summary>
        public static void PivotToBottom(RSEditMesh m, bool undo = true)
        {
            if (m.verts.Count == 0) return;
            var b = new Bounds(m.verts[0], Vector3.zero);
            foreach (var v in m.verts) b.Encapsulate(v);
            Vector3 c = new Vector3(b.center.x, b.min.y, b.center.z);
            if (c.sqrMagnitude < 1e-10f) return;
            if (undo) { Undo.RecordObject(m, "기준점"); Undo.RecordObject(m.transform, "기준점"); }
            for (int i = 0; i < m.verts.Count; i++) m.verts[i] -= c;
            m.transform.position = m.transform.TransformPoint(c);
            m.MarkDirty(); EditorUtility.SetDirty(m);
        }

        // ─────────────────────────────────────────────────────────────
        // 딱 맞는 그림
        // ─────────────────────────────────────────────────────────────

        /// <summary>그 면(같은 그룹 전체)에 1m = 32px 로 꼭 맞는 그림 파일을 만들고 붙인다.
        /// 바탕은 재질 그림을 테두리 유지(9칸)로 늘린 것 → Aseprite 로 고쳐 그리면 된다</summary>
        public static void MakeFitPicture(RSEditMesh m, int face, RSSurface src, int border)
        {
            if (!m.ValidFace(face)) return;
            if (src == null) src = m.faces[face].surface;
            var px = m.FitPixels(face);
            int W = px.x, H = px.y;
            if (W * H > 4096 * 4096) { EditorUtility.DisplayDialog("그림 만들기", "너무 큽니다 (" + W + "×" + H + ")", "확인"); return; }

            Color32[] sp = null; int sw = 0, sh = 0;
            if (src != null && src.texture != null) sp = ReadPixels(src.texture, out sw, out sh);
            var dst = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    dst[y * W + x] = sp != null ? sp[Slice(y, H, sh, border) * sw + Slice(x, W, sw, border)] : new Color32(158, 158, 153, 255);

            string folder = RSPaths.Ensure(RSPaths.Textures + "/Build/" + RSSurfaceTools.Safe(m.name));
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + RSSurfaceTools.Safe(m.name) + "_" + W + "x" + H + ".png");
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            outTex.SetPixels32(dst); outTex.Apply();
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(path);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var surf = RSSurfaceTools.CreateFromTexture(tex, RSSurfaceTools.SurfacesRoot + "/" + RSSurfaceTools.Safe(m.name));
            RSSurfaceTools.MakePixelTexture(tex, false);
            if (surf != null && src != null)
            {
                surf.tint = src.tint; surf.cutout = src.cutout; surf.cutoff = src.cutoff; surf.wetResponse = src.wetResponse; surf.surfaceTag = src.surfaceTag;
                EditorUtility.SetDirty(surf);
                RSSurfaceTools.EnsureMaterial(surf);
                AssetDatabase.SaveAssets();
            }
            if (surf == null) return;

            Undo.RecordObject(m, "딱 맞는 그림");
            int g = m.faces[face].fitGroup;
            for (int f = 0; f < m.faces.Count; f++)
            {
                if (f != face && (g == 0 || m.faces[f].fitGroup != g)) continue;
                m.faces[f].surface = surf;
                m.faces[f].mode = RSEditPaintMode.딱맞게;
            }
            m.MarkDirty(); EditorUtility.SetDirty(m);
            RSEditCore.brush.surface = surf;
            EditorGUIUtility.PingObject(tex);
            if (EditorUtility.DisplayDialog("그림 만들기", W + " × " + H + " px 그림을 만들어 붙였습니다.\n" + path + "\n\nAseprite 로 열어 고쳐 그릴까요? (저장하면 바로 반영)", "Aseprite 로 열기", "나중에"))
                RSSheetBuilder.Open(path);
        }

        /// <summary>텍스처 픽셀 읽기: png 원본 → 그대로, 아니면(.aseprite · .psd …) GPU 로 복사해 읽기</summary>
        static Color32[] ReadPixels(Texture2D tex, out int w, out int h)
        {
            w = tex.width; h = tex.height;
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                string path = AssetDatabase.GetAssetPath(tex);
                bool ok = File.Exists(path) && t.LoadImage(File.ReadAllBytes(path)) && t.width == w && t.height == h;
                Color32[] px = ok ? t.GetPixels32() : null;
                Object.DestroyImmediate(t);
                if (px != null) return px;
            }
            catch (System.Exception) { }
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var r = new Texture2D(w, h, TextureFormat.RGBA32, false);
            r.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            r.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var res = r.GetPixels32();
            Object.DestroyImmediate(r);
            return res;
        }

        /// <summary>9칸 늘리기: 테두리는 그대로, 가운데는 반복</summary>
        static int Slice(int x, int D, int S, int b)
        {
            if (S <= 0) return 0;
            if (b <= 0 || b * 2 >= S || b * 2 >= D) return ((x % S) + S) % S;
            if (x < b) return x;
            if (x >= D - b) return S - (D - x);
            int mid = S - 2 * b;
            return b + (x - b) % mid;
        }

        // ─────────────────────────────────────────────────────────────
        // 프리팹
        // ─────────────────────────────────────────────────────────────

        public static void SavePrefab(RSEditMesh m)
        {
            string folder = RSPaths.Ensure(RSPaths.Root + "/Prefabs/Build");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + RSSurfaceTools.Safe(m.name) + ".prefab");
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(m.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(m.gameObject, InteractionMode.UserAction);
                Debug.Log("[편집 메시] 이 프리팹에 반영했습니다", m);
                return;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(m.gameObject))
            {
                var copy = PrefabUtility.SaveAsPrefabAsset(m.gameObject, path);
                if (copy != null) { EditorGUIUtility.PingObject(copy); Debug.Log("[편집 메시] 다른 프리팹 안에 있어서 새 프리팹으로 복사했습니다: " + path, copy); }
                return;
            }
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(m.gameObject, path, InteractionMode.UserAction);
            if (prefab != null) { EditorGUIUtility.PingObject(prefab); Debug.Log("[편집 메시] 프리팹 저장: " + path, prefab); }
        }
    }
}
