// RE:AL STEEL - 나노봇 3페 기사 동작 굽기
//
// Tools → RE_AL STEEL → 나노봇 → 3페 기사 동작 굽기
//   SYS/Sprites/NanoBoss/기사/ 의 동작 시트 PNG (RunIn.png · Running.png · … · Guard3.png) 를 읽어
//   Data/NanoBoss/기사/기사_동작.asset 에 굽고, 열린 씬의 나노봇 두뇌 '3페 기사 동작' 칸에 넣는다.
// 시트 PNG 를 고쳐 저장하면 다시 굽는다. 에셋 인스펙터의 '다시 굽기' 도 같다.
//
// 굽기: 칸마다 칠한 픽셀을 발밑(root) 기준 좌표로 → 몸 · 칼날 · 효과로 나눔 → 종류마다 힐베르트 순서로 정렬
//   몸    이동 동작(RunIn · Running · RunOut · Dash · BackDash)에 나오는 색
//   칼날  몸 색 중 밝고 푸른 색
//   효과  이동 동작에 없는 색 (검기 · 불꽃 · 먼지)
using System.Collections.Generic;
using System.IO;
using RealSteel.Common.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    public static class RSNanoKnightBaker
    {
        public static string SheetDir => RSPaths.Root + "/Sprites/NanoBoss/기사";
        static string AssetPath => RSPaths.Ensure(RSPaths.Data + "/NanoBoss/기사") + "/기사_동작.asset";

        [MenuItem("Tools/RE_AL STEEL/나노봇/3페 기사 동작 굽기", false, 61)]
        public static void Menu()
        {
            if (!AssetDatabase.IsValidFolder(SheetDir))
            {
                EditorUtility.DisplayDialog("3페 기사 동작", SheetDir + " 폴더가 없습니다.\n동작 시트 PNG (RunIn · Running · … · Guard3) 를 넣어 주세요.", "확인");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<RSNanoKnightMoves>(AssetPath);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<RSNanoKnightMoves>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            int found = 0;
            foreach (var m in RSNanoKnightMoves.All)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetDir + "/" + RSNanoKnightMoves.FileName(m) + ".png");
                if (!tex) continue;
                asset.Get(m).sheet = tex;
                found++;
            }
            Bake(asset);
            int assigned = 0;
            foreach (var brain in Object.FindObjectsByType<RSNanoBossBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (brain.knightMoves == asset) continue;
                Undo.RecordObject(brain, "3페 기사 동작");
                brain.knightMoves = asset;
                EditorUtility.SetDirty(brain);
                assigned++;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[나노봇] 3페 기사 동작 {found}개를 구웠습니다 ({AssetPath}). 두뇌 {assigned}개에 넣었습니다 — 씬을 저장하세요. " +
                      $"기사 몸 봇 최대 {asset.MaxBodyBots():N0}개 (배율 {asset.pixelScale}).", asset);
        }

        static string KeyOf(RSNanoKnightMoves a, NanoKnightClip c)
        {
            if (!c.sheet) return "";
            string path = AssetDatabase.GetAssetPath(c.sheet);
            long t = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            return $"{AssetDatabase.AssetPathToGUID(path)}|{t}|{a.cellSize}|{a.root}|{a.alphaCut:0.###}";
        }

        static Color32[] ReadRaw(Texture2D tex, out int w, out int h)
        {
            w = h = 0;
            string path = AssetDatabase.GetAssetPath(tex);
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = t.LoadImage(File.ReadAllBytes(path));
            Color32[] px = ok ? t.GetPixels32() : null;
            w = t.width; h = t.height;
            Object.DestroyImmediate(t);
            return px;
        }

        /// <summary>전부 굽는다 (몸 색은 이동 동작들에서 모은다)</summary>
        public static void Bake(RSNanoKnightMoves a)
        {
            Undo.RecordObject(a, "3페 기사 동작 굽기");
            byte cut = (byte)Mathf.RoundToInt(a.alphaCut * 255f);
            // 몸 색
            var basePal = new HashSet<int>();
            foreach (var m in RSNanoKnightMoves.BaseMoves)
            {
                var c = a.Get(m);
                if (!c.sheet) continue;
                var px = ReadRaw(c.sheet, out int w, out int h);
                if (px == null) continue;
                foreach (var p in px) if (p.a >= cut && p.a > 0) basePal.Add(p.r | (p.g << 8) | (p.b << 16));
            }
            foreach (var m in RSNanoKnightMoves.All) BakeClip(a, a.Get(m), basePal, cut);
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
        }

        static void BakeClip(RSNanoKnightMoves a, NanoKnightClip c, HashSet<int> basePal, byte cut)
        {
            if (!c.sheet) { c.frames = 0; c.start = new int[0]; c.counts = new int[0]; c.px = new int[0]; c.color = new int[0]; c.bakedKey = ""; return; }
            var src = ReadRaw(c.sheet, out int W, out int H);
            if (src == null) return;
            int cw = Mathf.Max(1, a.cellSize.x), ch = Mathf.Max(1, a.cellSize.y);
            int frames = Mathf.Max(1, W / cw);
            var start = new int[frames + 1];
            var counts = new int[frames * 3];
            var px = new List<int>();
            var col = new List<int>();
            var lists = new[] { new List<(int key, int v, int c)>(), new List<(int key, int v, int c)>(), new List<(int key, int v, int c)>() };
            for (int f = 0; f < frames; f++)
            {
                foreach (var l in lists) l.Clear();
                for (int y = 0; y < Mathf.Min(ch, H); y++)           // y = 아래부터
                for (int x = 0; x < cw; x++)
                {
                    int sx = f * cw + x;
                    if (sx >= W) continue;
                    var p = src[y * W + sx];
                    if (p.a < cut || p.a == 0) continue;
                    int rgb = p.r | (p.g << 8) | (p.b << 16);
                    float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                    int kind = !basePal.Contains(rgb) ? 2 : (p.b >= p.r + 15 && lum >= 0.6f ? 1 : 0);
                    int lx = x - a.root.x, ly = y - a.root.y;
                    lists[kind].Add((Hilbert(512, x & 511, y & 511), RSNanoKnightMoves.Pack(lx, ly, kind), rgb));
                }
                start[f] = px.Count;
                for (int k = 0; k < 3; k++)
                {
                    lists[k].Sort((p, q) => p.key.CompareTo(q.key));
                    foreach (var e in lists[k]) { px.Add(e.v); col.Add(e.c); }
                    counts[f * 3 + k] = lists[k].Count;
                }
            }
            start[frames] = px.Count;
            c.frames = frames;
            c.start = start;
            c.counts = counts;
            c.px = px.ToArray();
            c.color = col.ToArray();
            c.bakedKey = KeyOf(a, c);
        }

        /// <summary>힐베르트 곡선 번호 (n = 2 의 거듭제곱)</summary>
        static int Hilbert(int n, int x, int y)
        {
            int d = 0;
            for (int s = n / 2; s > 0; s /= 2)
            {
                int rx = (x & s) > 0 ? 1 : 0;
                int ry = (y & s) > 0 ? 1 : 0;
                d += s * s * ((3 * rx) ^ ry);
                if (ry == 0)
                {
                    if (rx == 1) { x = s - 1 - x; y = s - 1 - y; }
                    int t = x; x = y; y = t;
                }
            }
            return d;
        }

        public static bool NeedsBake(RSNanoKnightMoves a)
        {
            foreach (var m in RSNanoKnightMoves.All)
            {
                var c = a.Get(m);
                if (c.sheet && (c.bakedKey != KeyOf(a, c) || !c.Ready)) return true;
            }
            return false;
        }
    }

    /// <summary>시트 PNG 를 고쳐 저장하면 다시 굽는다</summary>
    class RSNanoKnightWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool any = false;
            foreach (var p in imported) if (p.EndsWith(".png") && p.Replace('\\', '/').Contains("/Sprites/NanoBoss/기사/")) { any = true; break; }
            if (!any) return;
            EditorApplication.delayCall += () =>
            {
                foreach (var guid in AssetDatabase.FindAssets("t:RSNanoKnightMoves"))
                {
                    var a = AssetDatabase.LoadAssetAtPath<RSNanoKnightMoves>(AssetDatabase.GUIDToAssetPath(guid));
                    if (a && RSNanoKnightBaker.NeedsBake(a)) RSNanoKnightBaker.Bake(a);
                }
            };
        }
    }

    [CustomEditor(typeof(RSNanoKnightMoves))]
    class RSNanoKnightMovesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var a = (RSNanoKnightMoves)target;
            EditorGUILayout.HelpBox("3페 기사의 몸 = 이 동작 시트들. 그림 픽셀 하나 = 칸 '배율 × 배율'.\n" +
                                    "몸(이동 동작의 색) · 칼날(밝고 푸른 색) · 효과(이동 동작에 없는 색 = 검기)로 나눠 굽는다. 공격 프레임(효과 픽셀 120개 이상)의 효과 · 칼날이 공격 판정.\n" +
                                    "시트는 " + RSNanoKnightBaker.SheetDir + " 에 동작 이름.png (RunIn · Running · … · Guard3)", MessageType.None);
            DrawDefaultInspector();
            EditorGUILayout.Space(4);
            int s2 = a.pixelScale * a.pixelScale;
            var sb = new System.Text.StringBuilder();
            foreach (var m in RSNanoKnightMoves.All)
            {
                var c = a.Get(m);
                if (!c.sheet) { sb.Append($"{m}: 시트 없음\n"); continue; }
                if (!c.Ready) { sb.Append($"{m}: 아직 안 구움\n"); continue; }
                int body = 0, fx = 0;
                for (int f = 0; f < c.frames; f++) { body = Mathf.Max(body, (c.Body(f) + c.Blade(f)) * s2); fx = Mathf.Max(fx, c.Fx(f) * s2); }
                bool own = c.frameTime != null && c.frameTime.Length == c.frames;
                sb.Append($"{m}: {c.frames}프레임 {c.Length:0.00}초 ({(own ? "프레임 시간" : "GIF 기준 표")}) · 몸 봇 최대 {body:N0} · 효과 최대 {fx:N0}\n");
            }
            EditorGUILayout.LabelField(sb.ToString().TrimEnd(), EditorStyles.wordWrappedMiniLabel);
            if (RSNanoKnightBaker.NeedsBake(a)) EditorGUILayout.HelpBox("시트가 바뀌었다 — 다시 구워야 한다", MessageType.Warning);
            if (GUILayout.Button("GIF 기준 타이밍을 '프레임 시간' 칸에 복사 (프레임마다 고치려면)"))
            {
                Undo.RecordObject(a, "3페 기사 프레임 시간");
                foreach (var m in RSNanoKnightMoves.All)
                {
                    var c = a.Get(m);
                    if (c.Ready) c.frameTime = (float[])RSNanoKnightMoves.GifTimingFor(m, c.frames).Clone();
                }
                EditorUtility.SetDirty(a);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("다시 굽기"))
            {
                RSNanoKnightBaker.Bake(a);
                GUIUtility.ExitGUI();
            }
        }
    }
}
