// RE:AL STEEL - 재질 · 재질 세트 에디터
//
//  · 재질 세트 인스펙터에 텍스처를 끌어다 놓으면 → 재질 에셋이 생기고 세트에 들어간다
//  · 텍스처 가져오기 설정을 픽셀용으로 자동 (Point · 무압축 · 밉맵 끔 · 2의 거듭제곱 늘이기 끔 · 반복)
//  · 머티리얼은 재질 에셋 안에 같이 저장 (셰이더 RE_AL STEEL/Build Pixel Lit)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Common.EditorTools
{
    /// <summary>재질 만들기 · 텍스처 설정 · 머티리얼 (다른 도구도 부른다)</summary>
    public static class RSSurfaceTools
    {
        public static string SurfacesRoot { get { return RSPaths.Data + "/Surfaces"; } }
        static bool warnedShader;

        /// <summary>텍스처 가져오기 설정을 픽셀용으로 맞춘다. 바꿨으면 true</summary>
        public static bool MakePixelTexture(Texture2D tex, bool repeat = true)
        {
            if (tex == null) return false;
            string path = AssetDatabase.GetAssetPath(tex);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return false;
            bool changed = false;
            if (ti.textureType != TextureImporterType.Default && ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Default; changed = true; }
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; changed = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
            var wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; changed = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }
            if (ti.maxTextureSize < 4096) { ti.maxTextureSize = 4096; changed = true; }
            if (changed) ti.SaveAndReimport();
            return changed;
        }

        /// <summary>가져오기 설정이 픽셀용인지 (아니면 무엇이 다른지)</summary>
        public static string PixelProblems(Texture2D tex)
        {
            if (tex == null) return null;
            var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (ti == null) return null;
            var p = new List<string>();
            if (ti.filterMode != FilterMode.Point) p.Add("필터가 Point 가 아님 (흐려짐)");
            if (ti.mipmapEnabled) p.Add("밉맵 켜짐");
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) p.Add("압축 켜짐 (색 번짐)");
            if (ti.npotScale != TextureImporterNPOTScale.None) p.Add("크기 늘이기 켜짐");
            return p.Count == 0 ? null : string.Join(" · ", p);
        }

        /// <summary>재질 머티리얼이 없으면 에셋 안에 만들고, 값을 다시 넣는다</summary>
        public static Material EnsureMaterial(RSSurface s)
        {
            if (s == null) return null;
            var shader = Shader.Find(RSSurface.ShaderName);
            if (shader == null) { if (!warnedShader) { warnedShader = true; Debug.LogWarning("[재질] 셰이더 '" + RSSurface.ShaderName + "' 를 못 찾았습니다"); } return null; }
            string path = AssetDatabase.GetAssetPath(s);
            if (s.material == null)
            {
                var m = new Material(shader) { name = s.name + " (머티리얼)" };
                if (!string.IsNullOrEmpty(path)) AssetDatabase.AddObjectToAsset(m, s);
                s.material = m;
                EditorUtility.SetDirty(s);
            }
            else if (s.material.shader != shader) s.material.shader = shader;
            s.material.name = s.name + " (머티리얼)";
            s.ApplyTo(s.material);
            EditorUtility.SetDirty(s.material);
            return s.material;
        }

        /// <summary>텍스처에서 재질 에셋을 만든다 (같은 폴더에 같은 이름이 있으면 그것을 돌려준다)</summary>
        public static RSSurface CreateFromTexture(Texture2D tex, string folder)
        {
            if (tex == null) return null;
            MakePixelTexture(tex);
            RSPaths.Ensure(folder);
            string path = folder + "/" + Safe(tex.name) + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<RSSurface>(path);
            if (existing != null) return existing;
            var s = ScriptableObject.CreateInstance<RSSurface>();
            s.texture = tex;
            s.displayName = tex.name;
            s.cutout = HasHoles(tex);
            AssetDatabase.CreateAsset(s, path);
            EnsureMaterial(s);
            AssetDatabase.SaveAssets();
            return s;
        }

        /// <summary>완전히 투명한 픽셀이 있으면 (오려내기 후보)</summary>
        static bool HasHoles(Texture2D tex)
        {
            try
            {
                string p = AssetDatabase.GetAssetPath(tex);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(File.ReadAllBytes(p))) { Object.DestroyImmediate(t); return false; }
                var px = t.GetPixels32();
                Object.DestroyImmediate(t);
                int holes = 0;
                foreach (var c in px) if (c.a < 8) holes++;
                return holes > px.Length / 50;
            }
            catch { return false; }
        }

        public static string Safe(string n)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
            return n.Trim();
        }

        /// <summary>세트가 없으면 '공용' 세트를 만든다</summary>
        public static RSSurfaceSet DefaultSet()
        {
            foreach (var g in AssetDatabase.FindAssets("t:RSSurfaceSet"))
            {
                var s = AssetDatabase.LoadAssetAtPath<RSSurfaceSet>(AssetDatabase.GUIDToAssetPath(g));
                if (s != null) return s;
            }
            RSPaths.Ensure(SurfacesRoot);
            var set = ScriptableObject.CreateInstance<RSSurfaceSet>();
            set.note = "자동으로 만든 기본 세트";
            AssetDatabase.CreateAsset(set, SurfacesRoot + "/공용.asset");
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>썸네일 (텍스처를 픽셀 그대로)</summary>
        public static void DrawSwatch(Rect r, RSSurface s, bool selected)
        {
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
            if (s != null && s.texture != null)
            {
                var old = GUI.color;
                GUI.color = s.tint;
                GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), s.texture, ScaleMode.ScaleAndCrop);
                GUI.color = old;
            }
            if (selected)
            {
                var c = new Color(1f, 0.8f, 0.2f);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), c);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), c);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), c);
                EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), c);
            }
        }
    }

    [CustomEditor(typeof(RSSurface)), CanEditMultipleObjects]
    public class RSSurfaceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var s = (RSSurface)target;
            serializedObject.Update();
            if (targets.Length == 1 && s.texture != null)
            {
                var r = GUILayoutUtility.GetRect(96, 96, GUILayout.ExpandWidth(false));
                RSSurfaceTools.DrawSwatch(r, s, false);
                var px = s.TexturePixels;
                EditorGUILayout.LabelField(px.x + " × " + px.y + " 픽셀 = " + (px.x / (float)RSSurface.PixelsPerMeter).ToString("0.##") + " × " +
                    (px.y / (float)RSSurface.PixelsPerMeter).ToString("0.##") + " m 마다 반복 (1m = 32 픽셀)", EditorStyles.miniLabel);
            }
            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, "m_Script", "material");
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (targets.Length != 1) return;

            string prob = RSSurfaceTools.PixelProblems(s.texture);
            if (prob != null)
            {
                EditorGUILayout.HelpBox("텍스처 설정이 픽셀용이 아닙니다: " + prob, MessageType.Warning);
                if (GUILayout.Button("픽셀용으로 맞추기")) RSSurfaceTools.MakePixelTexture(s.texture);
            }
            if (changed || s.material == null)
            {
                if (s.texture != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(s)))
                {
                    if (changed) RSSurfaceTools.MakePixelTexture(s.texture);
                    RSSurfaceTools.EnsureMaterial(s);
                }
            }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("머티리얼 (자동)", s.material, typeof(Material), false);
        }
    }

    [CustomEditor(typeof(RSSurfaceSet))]
    public class RSSurfaceSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var set = (RSSurfaceSet)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("note"));
            serializedObject.ApplyModifiedProperties();

            // 끌어다 놓기
            var drop = GUILayoutUtility.GetRect(0, 48, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "여기에 텍스처(또는 재질)를 끌어다 놓으면 재질이 만들어져 들어갑니다\n(픽셀 설정 · 머티리얼 자동, 1m = 32 픽셀)", EditorStyles.helpBox);
            HandleDrop(drop, set);

            EditorGUILayout.Space(4);
            int count = set.surfaces.Count;   // 이번 이벤트 동안 고정 (빼기 버튼으로 줄어도 레이아웃이 안 어긋나게)
            int cols = Mathf.Max(1, (int)((EditorGUIUtility.currentViewWidth - 40) / 92));
            int remove = -1, up = -1;
            for (int i = 0; i < set.surfaces.Count; i += cols)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + cols, set.surfaces.Count); j++)
                    {
                        var s = set.surfaces[j];
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(86)))
                        {
                            var r = GUILayoutUtility.GetRect(80, 64, GUILayout.Width(84));
                            RSSurfaceTools.DrawSwatch(r, s, Selection.activeObject == s && s != null);
                            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition) && s != null)
                            {
                                EditorGUIUtility.PingObject(s);
                                if (Event.current.clickCount == 2) Selection.activeObject = s;
                                Event.current.Use();
                            }
                            GUILayout.Label(s != null ? s.Label : "(빈 칸)", EditorStyles.miniLabel, GUILayout.Width(84));
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                using (new EditorGUI.DisabledScope(j == 0))
                                    if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26))) up = j;
                                if (GUILayout.Button("빼기", EditorStyles.miniButtonRight, GUILayout.Width(54))) remove = j;
                            }
                        }
                    }
                }
            }
            if (remove >= 0) { Undo.RecordObject(set, "재질 빼기"); set.surfaces.RemoveAt(remove); EditorUtility.SetDirty(set); }
            if (up > 0) { Undo.RecordObject(set, "재질 순서"); var t = set.surfaces[up]; set.surfaces[up] = set.surfaces[up - 1]; set.surfaces[up - 1] = t; EditorUtility.SetDirty(set); }
            if (count == 0)
                EditorGUILayout.HelpBox("아직 재질이 없습니다. 픽셀 텍스처(PNG)를 위 칸에 끌어다 놓으세요.", MessageType.None);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("모든 재질 텍스처 설정 · 머티리얼 다시 맞추기", EditorStyles.miniButton))
            {
                foreach (var s in set.surfaces)
                {
                    if (s == null) continue;
                    RSSurfaceTools.MakePixelTexture(s.texture);
                    RSSurfaceTools.EnsureMaterial(s);
                }
                AssetDatabase.SaveAssets();
            }
        }

        static void HandleDrop(Rect r, RSSurfaceSet set)
        {
            var e = Event.current;
            if (!r.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type != EventType.DragPerform) { e.Use(); return; }
            DragAndDrop.AcceptDrag();
            string folder = RSSurfaceTools.SurfacesRoot + "/" + RSSurfaceTools.Safe(set.name);
            Undo.RecordObject(set, "재질 추가");
            int added = 0;
            foreach (var o in DragAndDrop.objectReferences)
            {
                RSSurface s = o as RSSurface;
                if (s == null && o is Texture2D tex) s = RSSurfaceTools.CreateFromTexture(tex, folder);
                if (s != null && !set.surfaces.Contains(s)) { set.surfaces.Add(s); added++; }
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[재질 세트] '" + set.name + "' 에 " + added + "개 추가 (재질 폴더: " + folder + ")", set);
            e.Use();
        }
    }
}
