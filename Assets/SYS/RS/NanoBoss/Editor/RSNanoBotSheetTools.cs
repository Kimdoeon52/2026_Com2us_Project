// RE:AL STEEL - 나노봇 봇 그림 PNG 도구
//
// Tools → RE_AL STEEL → 나노봇 → 봇 그림 PNG 만들기 (지금 그림으로)
//   지금 코드 그림을 SYS/Sprites/NanoBoss/ 에 PNG 로 뽑고 (봇_몸 · 봇_낱개1~5, 8×8 캔버스),
//   열린 씬의 나노봇 군집 '봇 종류' 목록에 넣는다. 이 PNG 를 픽셀 툴에서 고쳐 저장하면 바로 바뀐다.
// 봇 종류 PNG 는 가져오기 설정(읽기 허용 · 점 필터 · 무압축 · 밉맵 없음)을 자동으로 맞추고,
// 다시 가져와지면 군집 그림을 다시 읽는다.
using System.Collections.Generic;
using System.IO;
using RealSteel.Common.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    [InitializeOnLoad]
    public static class RSNanoBotSheetTools
    {
        static RSNanoBotSheetTools()
        {
            RSNanoSwarm.fixSheetImport = t => EditorApplication.delayCall += () => { if (t) FixImport(t); };
        }

        /// <summary>봇 그림 PNG 가져오기 설정: 읽기 허용 · 점 필터 · 무압축 · 밉맵 없음 · 크기 그대로</summary>
        public static void FixImport(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
            if (imp.isReadable && imp.filterMode == FilterMode.Point && imp.textureCompression == TextureImporterCompression.Uncompressed
                && !imp.mipmapEnabled && imp.npotScale == TextureImporterNPOTScale.None) return;
            imp.isReadable = true;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
        }

        [MenuItem("Tools/RE_AL STEEL/나노봇/봇 그림 PNG 만들기 (지금 그림으로)", false, 60)]
        public static void Export()
        {
            string dir = RSPaths.Ensure(RSPaths.Root + "/Sprites/NanoBoss");
            var names = new List<string> { "봇_몸" };
            for (int k = 1; k <= RSNanoBotSprites.LooseCount; k++) names.Add("봇_낱개" + k);

            bool any = false;
            foreach (var n in names) any |= File.Exists(dir + "/" + n + ".png");
            bool write = true;
            if (any)
            {
                int a = EditorUtility.DisplayDialogComplex("봇 그림 PNG",
                    dir + " 에 봇 그림 PNG 가 이미 있습니다.\n지금 코드 그림으로 덮어쓸까요? (고친 그림이 있으면 사라집니다)",
                    "덮어쓰기", "취소", "그대로 두고 연결만");
                if (a == 1) return;
                write = a == 0;
            }

            var tex = new List<Texture2D>();
            for (int k = 0; k < names.Count; k++)
            {
                string path = dir + "/" + names[k] + ".png";
                if (write || !File.Exists(path))
                {
                    var t = RSNanoBotSprites.DefaultPicture(k);
                    File.WriteAllBytes(path, t.EncodeToPNG());
                    Object.DestroyImmediate(t);
                    AssetDatabase.ImportAsset(path);
                }
                var a = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                FixImport(a);
                tex.Add(a);
            }

            var kinds = new List<NanoBotKind> { new NanoBotKind { picture = tex[0], use = NanoBotUse.Body, ratio = 100f } };
            for (int k = 1; k < tex.Count; k++) kinds.Add(new NanoBotKind { picture = tex[k], use = NanoBotUse.Loose, ratio = 20f });
            foreach (var sw in Object.FindObjectsByType<RSNanoSwarm>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(sw, "봇 종류");
                sw.botKinds = new List<NanoBotKind>();
                foreach (var k in kinds) sw.botKinds.Add(new NanoBotKind { picture = k.picture, use = k.use, ratio = k.ratio });
                EditorUtility.SetDirty(sw);
                sw.RebuildAtlas();
            }
            SceneView.RepaintAll();
            Selection.activeObject = tex[0];
            EditorGUIUtility.PingObject(tex[0]);
            Debug.Log("[나노봇] 봇 그림을 " + dir + " 에 뽑아 군집 '봇 종류' 에 넣었습니다. 봇_몸.png 를 고치면 몸 · 테두리 · 방울이 함께 바뀝니다.", tex[0]);
        }
    }

    /// <summary>봇 그림 PNG 를 고쳐 저장하면 군집 그림을 다시 읽는다</summary>
    class RSNanoBotSheetWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Length == 0) return;
            var set = new HashSet<string>(imported);
            bool any = false;
            foreach (var sw in Object.FindObjectsByType<RSNanoSwarm>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sw.botKinds == null) continue;
                bool hit = false;
                foreach (var k in sw.botKinds)
                {
                    if (k == null || !k.picture) continue;
                    if (!set.Contains(AssetDatabase.GetAssetPath(k.picture))) continue;
                    RSNanoBotSheetTools.FixImport(k.picture);
                    hit = true;
                }
                if (hit) { sw.RebuildAtlas(); any = true; }
            }
            if (any) { SceneView.RepaintAll(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }
        }
    }
}
