// RE:AL STEEL - 도안 만들기 · 다시 읽기
//
// 만들기(갱신):
//  1. 조립품에서 '도안 칸' 으로 칠한 면을 모은다 — 칸 크기 = 면 크기(1m = 32 px)
//  2. 이미 있던 칸은 자리 · 그림 유지. 새 칸 · 크기가 바뀐 칸만 빈 곳에 놓는다
//     크기가 바뀐 칸의 그림은 가장자리를 살려 늘리거나 줄인다 (가운데 줄을 반복 · 생략)
//     복제한 부품의 새 칸은 원래 부품 칸의 그림으로 시작
//  3. 원본 .aseprite = [가이드(참조 레이어, 칸 색 · 테두리 · 위쪽 표시)] + [그림]
//  4. 결과 PNG 처리 → 조립품이 쓴다
// 다시 읽기: 원본이 저장되면 결과 PNG 만 다시 처리 (칸 배치는 그대로)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RealSteel.Common;
using RealSteel.Common.EditorTools;

namespace RealSteel.Build.EditorTools
{
    public static class RSSheetBuilder
    {
        const int Pad = 1;          // 칸 둘레 여백 (번짐 막기)
        const int MaxSize = 4096;

        public static string BuildRoot { get { return RSPaths.Data + "/Build"; } }

        // ─────────────────────────────────────────────────────────────
        // 만들기 · 갱신
        // ─────────────────────────────────────────────────────────────

        public static RSSheet UpdateSheet(RSAssembly a, bool openInAseprite)
        {
            var needs = new List<RSAssembly.CellNeed>();
            a.CollectSheetNeeds(needs);
            if (needs.Count == 0)
            {
                EditorUtility.DisplayDialog("도안", "'도안 칸' 으로 칠한 면이 없습니다.\n\n씬 뷰 조립 도구의 '칠하기' 에서 방식을 '도안 칸' 으로 고르고 면을 클릭하세요.", "확인");
                return a.sheet;
            }

            var sheet = a.sheet;
            if (sheet == null)
            {
                string folder = RSPaths.Ensure(BuildRoot + "/" + RSSurfaceTools.Safe(a.name));
                string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + RSSurfaceTools.Safe(a.name) + "_도안.asset");
                sheet = ScriptableObject.CreateInstance<RSSheet>();
                sheet.source = Path.ChangeExtension(assetPath, null) + ".aseprite";
                AssetDatabase.CreateAsset(sheet, assetPath);
                Undo.RecordObject(a, "도안 만들기");
                a.sheet = sheet;
                EditorUtility.SetDirty(a);
            }
            string src = sheet.source;
            if (string.IsNullOrEmpty(src)) { src = Path.ChangeExtension(AssetDatabase.GetAssetPath(sheet), null) + ".aseprite"; sheet.source = src; }

            // 기존 그림
            RGBA[] oldPx = null; int oldW = 0, oldH = 0;
            if (File.Exists(src)) { try { oldPx = ReadSource(src, out oldW, out oldH); } catch (Exception e) { Debug.LogWarning("[도안] 원본을 못 읽었습니다: " + e.Message); } }

            var oldCells = new Dictionary<string, RSSheetCell>();
            foreach (var c in sheet.cells) if (c != null && !string.IsNullOrEmpty(c.key)) oldCells[c.key] = c;
            var hints = new Dictionary<string, string>();
            foreach (var h in a.copyHints) { int k = h.IndexOf('>'); if (k > 0) hints[h.Substring(0, k)] = h.Substring(k + 1); }

            // 칸 배치: 유지할 것 먼저
            int W = Mathf.Max(sheet.width, 32), H = Mathf.Max(sheet.height, 32);
            var placed = new List<RSSheetCell>();
            var todo = new List<RSAssembly.CellNeed>();
            foreach (var n in needs)
            {
                if (oldCells.TryGetValue(n.key, out var oc) && oc.rect.width == n.size.x && oc.rect.height == n.size.y && Inside(oc.rect, W, H))
                    placed.Add(new RSSheetCell { key = n.key, rect = oc.rect, label = n.label, fallback = n.fallback });
                else todo.Add(n);
            }
            todo.Sort((x, y) => (y.size.y * 10000 + y.size.x).CompareTo(x.size.y * 10000 + x.size.x));
            foreach (var n in todo)
            {
                RectInt r;
                while (!FindSpot(placed, n.size, W, H, out r))
                {
                    if (W >= MaxSize && H >= MaxSize) { Debug.LogError("[도안] 칸이 너무 많아 4096 × 4096 에 다 안 들어갑니다"); return sheet; }
                    if (W <= H) W = Mathf.Min(MaxSize, W * 2); else H = Mathf.Min(MaxSize, H * 2);
                }
                placed.Add(new RSSheetCell { key = n.key, rect = r, label = n.label, fallback = n.fallback });
            }
            // 크기를 줄일 수 있으면 (칸 오른쪽 · 위 끝까지)
            int needW = 32, needH = 32;
            foreach (var c in placed) { needW = Mathf.Max(needW, c.rect.xMax + Pad); needH = Mathf.Max(needH, c.rect.yMax + Pad); }
            W = Mathf.Max(needW, Mathf.Min(W, RoundUp(needW))); H = Mathf.Max(needH, Mathf.Min(H, RoundUp(needH)));

            // 새 그림: 칸마다 옛 그림을 옮긴다 (유니티 좌표 = 아래에서 위)
            var paint = new RGBA[W * H];
            foreach (var c in placed)
            {
                RSSheetCell from = null;
                if (oldCells.TryGetValue(c.key, out var oc)) from = oc;
                else if (hints.TryGetValue(KeyPart(c.key), out var origId) && oldCells.TryGetValue(origId + c.key.Substring(c.key.IndexOf(':')), out var hc)) from = hc;
                if (from == null || oldPx == null) continue;
                CopyResized(oldPx, oldW, oldH, from.rect, paint, W, H, c.rect);
            }

            // 원본 쓰기 (.aseprite: 가이드 + 그림 / .png: 그림만 + 가이드 png)
            var guide = MakeGuide(placed, W, H);
            if (src.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                WritePng(src, paint, W, H);
                WritePng(Path.ChangeExtension(src, null) + "_가이드.png", guide, W, H);
            }
            else
            {
                RSAseprite.Write(src, W, H, new List<AseLayer>
                {
                    new AseLayer { name = "가이드 (참조 · 그림에 안 들어감)", reference = true, opacity = 140, pixels = FlipRows(guide, W, H) },
                    new AseLayer { name = "그림", pixels = FlipRows(paint, W, H) },
                });
            }

            Undo.RecordObject(sheet, "도안 갱신");
            sheet.width = W; sheet.height = H;
            sheet.cells = placed;
            EditorUtility.SetDirty(sheet);
            Undo.RecordObject(a, "도안 갱신");
            a.copyHints.Clear();
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();

            Process(sheet, paint, W, H);
            a.MarkDirty();
            Debug.Log("[도안] '" + a.name + "' 칸 " + placed.Count + "개, " + W + " × " + H + " 픽셀 → " + src + "\n" +
                "Aseprite 로 열어 '그림' 레이어에 그리고 저장하면 자동으로 다시 읽습니다.", sheet);
            if (openInAseprite) Open(src);
            return sheet;
        }

        static string KeyPart(string key) { int k = key.IndexOf(':'); return k > 0 ? key.Substring(0, k) : key; }
        static int RoundUp(int v) { int r = 32; while (r < v) r *= 2; return r; }
        static bool Inside(RectInt r, int W, int H) { return r.xMin >= Pad && r.yMin >= Pad && r.xMax + Pad <= W && r.yMax + Pad <= H; }

        /// <summary>왼쪽 아래부터 첫 빈자리 (여백 포함)</summary>
        static bool FindSpot(List<RSSheetCell> placed, Vector2Int size, int W, int H, out RectInt r)
        {
            r = default;
            int step = 1;
            for (int y = Pad; y + size.y + Pad <= H; y += step)
                for (int x = Pad; x + size.x + Pad <= W; x += step)
                {
                    var cand = new RectInt(x, y, size.x, size.y);
                    bool hit = false;
                    foreach (var c in placed)
                    {
                        var g = new RectInt(c.rect.x - Pad * 2, c.rect.y - Pad * 2, c.rect.width + Pad * 4, c.rect.height + Pad * 4);
                        if (cand.Overlaps(g)) { hit = true; x = Mathf.Max(x, g.xMax - 1); break; }
                    }
                    if (!hit) { r = cand; return true; }
                }
            return false;
        }

        /// <summary>그림을 새 칸 크기로 — 가장자리를 살리고 가운데 줄을 반복하거나 뺀다 (픽셀 그림용)</summary>
        static void CopyResized(RGBA[] src, int sw, int sh, RectInt from, RGBA[] dst, int dw, int dh, RectInt to)
        {
            for (int y = 0; y < to.height; y++)
            {
                int sy = MapEdge(y, to.height, from.height);
                for (int x = 0; x < to.width; x++)
                {
                    int sx = MapEdge(x, to.width, from.width);
                    int fx = from.x + sx, fy = from.y + sy;
                    int tx = to.x + x, ty = to.y + y;
                    if (fx < 0 || fy < 0 || fx >= sw || fy >= sh || tx < 0 || ty < 0 || tx >= dw || ty >= dh) continue;
                    dst[ty * dw + tx] = src[fy * sw + fx];
                }
            }
        }

        static int MapEdge(int i, int target, int source)
        {
            if (target == source) return i;
            int half = source / 2;
            if (i < half && i < target / 2 + (target % 2)) return Mathf.Min(i, source - 1);
            int fromEnd = target - 1 - i;
            if (fromEnd < source - half) return Mathf.Max(0, source - 1 - fromEnd);
            return half;   // 가운데: 가운데 줄 반복
        }

        // ─────────────────────────────────────────────────────────────
        // 가이드 (칸 색 · 테두리 · 위쪽 표시)
        // ─────────────────────────────────────────────────────────────

        static RGBA[] MakeGuide(List<RSSheetCell> cells, int W, int H)
        {
            var g = new RGBA[W * H];
            var partHue = new Dictionary<string, float>();
            foreach (var c in cells)
            {
                string pk = KeyPart(c.key);
                if (!partHue.TryGetValue(pk, out float hue)) { hue = (partHue.Count * 0.618034f) % 1f; partHue[pk] = hue; }
                Color fill = Color.HSVToRGB(hue, 0.55f, 0.95f);
                Color line = Color.HSVToRGB(hue, 0.8f, 0.45f);
                var r = c.rect;
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                    {
                        bool edge = x == r.xMin || y == r.yMin || x == r.xMax - 1 || y == r.yMax - 1;
                        bool check = ((x - r.xMin) / 4 + (y - r.yMin) / 4) % 2 == 0;
                        Color col = edge ? line : fill * (check ? 1f : 0.88f);
                        g[y * W + x] = new RGBA((byte)(col.r * 255), (byte)(col.g * 255), (byte)(col.b * 255), edge ? (byte)255 : (byte)170);
                    }
                // 위쪽 표시: 위 가장자리 가운데 작은 삼각형 (칸이 6px 이상일 때)
                if (r.width >= 6 && r.height >= 6)
                {
                    int cx = r.xMin + r.width / 2, top = r.yMax - 2;
                    for (int k = 0; k < 3; k++)
                        for (int dx = -k; dx <= k; dx++)
                        {
                            int x = cx + dx, y = top - k;
                            if (x > r.xMin && x < r.xMax - 1 && y > r.yMin) g[y * W + x] = new RGBA(255, 255, 255, 255);
                        }
                }
                // 왼쪽 아래 점: 칸 원점
                g[r.yMin * W + r.xMin] = new RGBA(0, 0, 0, 255);
            }
            return g;
        }

        // ─────────────────────────────────────────────────────────────
        // 결과 처리 (빈 칸 채우기 · 여백 번짐)
        // ─────────────────────────────────────────────────────────────

        /// <summary>원본을 다시 읽어 결과만 갱신 (칸 배치는 그대로)</summary>
        public static bool Reprocess(RSSheet sheet)
        {
            if (sheet == null || string.IsNullOrEmpty(sheet.source) || !File.Exists(sheet.source)) return false;
            RGBA[] px;
            int w, h;
            try { px = ReadSource(sheet.source, out w, out h); }
            catch (Exception e) { Debug.LogWarning("[도안] '" + sheet.source + "' 를 못 읽었습니다: " + e.Message, sheet); return false; }
            if (w != sheet.width || h != sheet.height)
                Debug.LogWarning("[도안] 원본 크기(" + w + "×" + h + ")가 칸 표(" + sheet.width + "×" + sheet.height + ")와 다릅니다. 캔버스 크기를 바꾸지 마세요 — 조립품 인스펙터 '도안 갱신' 으로 다시 맞춥니다.", sheet);
            Process(sheet, px, w, h);
            return true;
        }

        /// <summary>원본 읽기 → 유니티 좌표(아래에서 위) 픽셀</summary>
        static RGBA[] ReadSource(string path, out int w, out int h)
        {
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!t.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException("PNG 를 못 읽음");
                    w = t.width; h = t.height;
                    var c = t.GetPixels32();
                    var o = new RGBA[c.Length];
                    for (int i = 0; i < c.Length; i++) o[i] = new RGBA(c[i].r, c[i].g, c[i].b, c[i].a);
                    return o;
                }
                finally { UnityEngine.Object.DestroyImmediate(t); }
            }
            var top = RSAseprite.ReadComposite(path, out w, out h);
            return FlipRows(top, w, h);
        }

        static RGBA[] FlipRows(RGBA[] p, int w, int h)
        {
            var o = new RGBA[p.Length];
            for (int y = 0; y < h; y++) Array.Copy(p, y * w, o, (h - 1 - y) * w, w);
            return o;
        }

        static void Process(RSSheet sheet, RGBA[] paint, int W, int H)
        {
            var o = (RGBA[])paint.Clone();
            if (o.Length != W * H) Array.Resize(ref o, W * H);
            var texCache = new Dictionary<RSSurface, (RGBA[] px, int w, int h)>();
            foreach (var c in sheet.cells)
            {
                var r = c.rect;
                if (r.xMax > W || r.yMax > H) continue;
                int drawn = 0;
                for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) if (o[y * W + x].a > 0) drawn++;
                if (sheet.fillEmpty)
                {
                    if (drawn == 0) FillWithSurface(o, W, r, c.fallback, texCache);
                    else if (drawn < r.width * r.height) FillNearest(o, W, r);
                }
                Bleed(o, W, H, r);
            }
            // 결과 PNG
            string outPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(sheet), null) + "_결과.png";
            WritePng(outPath, o, W, H);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (tex != null)
            {
                RSSurfaceTools.MakePixelTexture(tex, false);
                if (sheet.texture != tex) { sheet.texture = tex; EditorUtility.SetDirty(sheet); AssetDatabase.SaveAssets(); }
            }
            // 조립품들이 새 텍스처 크기를 쓰게 다시 만든다
            foreach (var a in UnityEngine.Object.FindObjectsByType<RSAssembly>(FindObjectsSortMode.None))
                if (a.sheet == sheet) a.MarkDirty();
        }

        static void FillWithSurface(RGBA[] o, int W, RectInt r, RSSurface s, Dictionary<RSSurface, (RGBA[] px, int w, int h)> cache)
        {
            RGBA[] tp = null; int tw = 0, th = 0;
            Color tint = Color.white;
            if (s != null && s.texture != null)
            {
                if (!cache.TryGetValue(s, out var t))
                {
                    var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        string p = AssetDatabase.GetAssetPath(s.texture);
                        if (File.Exists(p) && tmp.LoadImage(File.ReadAllBytes(p)))
                        {
                            var c = tmp.GetPixels32();
                            var arr = new RGBA[c.Length];
                            for (int i = 0; i < c.Length; i++) arr[i] = new RGBA(c[i].r, c[i].g, c[i].b, c[i].a);
                            t = (arr, tmp.width, tmp.height);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(tmp); }
                    cache[s] = t;
                }
                tp = t.px; tw = t.w; th = t.h;
                tint = s.tint;
            }
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    RGBA c;
                    if (tp != null && tw > 0) c = tp[((y - r.yMin) % th) * tw + ((x - r.xMin) % tw)];
                    else c = new RGBA(158, 158, 153, 255);
                    c.r = (byte)(c.r * tint.r); c.g = (byte)(c.g * tint.g); c.b = (byte)(c.b * tint.b); c.a = 255;
                    o[y * W + x] = c;
                }
        }

        /// <summary>칸 안 빈 픽셀을 가장 가까운 그린 픽셀 색으로 (너비 우선 번짐)</summary>
        static void FillNearest(RGBA[] o, int W, RectInt r)
        {
            var q = new Queue<Vector2Int>();
            var done = new bool[r.width * r.height];
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    if (o[y * W + x].a > 0) { q.Enqueue(new Vector2Int(x, y)); done[(y - r.yMin) * r.width + (x - r.xMin)] = true; }
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                var c = o[p.y * W + p.x];
                c.a = 255;
                for (int k = 0; k < 4; k++)
                {
                    int x = p.x + dx[k], y = p.y + dy[k];
                    if (x < r.xMin || y < r.yMin || x >= r.xMax || y >= r.yMax) continue;
                    int i = (y - r.yMin) * r.width + (x - r.xMin);
                    if (done[i]) continue;
                    done[i] = true;
                    o[y * W + x] = c;
                    q.Enqueue(new Vector2Int(x, y));
                }
            }
            // 그린 픽셀 중 반투명 → 불투명 (오려내기 칸이 아니므로)
            for (int y = r.yMin; y < r.yMax; y++) for (int x = r.xMin; x < r.xMax; x++) { var c = o[y * W + x]; if (c.a > 0) { c.a = 255; o[y * W + x] = c; } }
        }

        /// <summary>칸 둘레 여백 1px 에 가장자리 색을 번져 둔다 (경계에서 옆 칸이 비치지 않게)</summary>
        static void Bleed(RGBA[] o, int W, int H, RectInt r)
        {
            for (int y = r.yMin - Pad; y < r.yMax + Pad; y++)
                for (int x = r.xMin - Pad; x < r.xMax + Pad; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    if (x >= r.xMin && x < r.xMax && y >= r.yMin && y < r.yMax) continue;
                    int sx = Mathf.Clamp(x, r.xMin, r.xMax - 1), sy = Mathf.Clamp(y, r.yMin, r.yMax - 1);
                    o[y * W + x] = o[sy * W + sx];
                }
        }

        static void WritePng(string path, RGBA[] px, int W, int H)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                var c = new Color32[px.Length];
                for (int i = 0; i < px.Length; i++) c[i] = new Color32(px[i].r, px[i].g, px[i].b, px[i].a);
                t.SetPixels32(c);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, t.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        // ─────────────────────────────────────────────────────────────
        // Aseprite 로 열기
        // ─────────────────────────────────────────────────────────────

        const string AsepritePathKey = "RS_AsepritePath";

        public static string AsepriteExe
        {
            get { return EditorPrefs.GetString(AsepritePathKey, ""); }
            set { EditorPrefs.SetString(AsepritePathKey, value); }
        }

        public static void Open(string assetPath)
        {
            string full = Path.GetFullPath(assetPath);
            string exe = AsepriteExe;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                foreach (var guess in new[]
                {
                    @"C:\Program Files\Aseprite\Aseprite.exe",
                    @"C:\Program Files (x86)\Steam\steamapps\common\Aseprite\Aseprite.exe",
                    @"C:\Program Files\Steam\steamapps\common\Aseprite\Aseprite.exe",
                    "/Applications/Aseprite.app/Contents/MacOS/aseprite",
                })
                    if (File.Exists(guess)) { exe = guess; AsepriteExe = exe; break; }
            }
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                exe = EditorUtility.OpenFilePanel("Aseprite 실행 파일 찾기 (한 번만)", "C:/Program Files", "exe");
                if (string.IsNullOrEmpty(exe)) { EditorUtility.RevealInFinder(full); return; }
                AsepriteExe = exe;
            }
            try { System.Diagnostics.Process.Start(exe, "\"" + full + "\""); }
            catch (Exception e) { Debug.LogWarning("[도안] Aseprite 를 못 열었습니다: " + e.Message); EditorUtility.RevealInFinder(full); }
        }
    }

    /// <summary>원본(.aseprite · .png)이 저장되면 결과를 다시 만든다</summary>
    public class RSSheetWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported == null || imported.Length == 0) return;
            HashSet<string> paths = null;
            foreach (var p in imported)
            {
                if (p.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".ase", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    if (p.EndsWith("_결과.png", StringComparison.Ordinal) || p.EndsWith("_가이드.png", StringComparison.Ordinal)) continue;
                    if (paths == null) paths = new HashSet<string>();
                    paths.Add(p.Replace('\\', '/'));
                }
            }
            if (paths == null) return;
            EditorApplication.delayCall += () =>
            {
                foreach (var g in AssetDatabase.FindAssets("t:RSSheet"))
                {
                    var s = AssetDatabase.LoadAssetAtPath<RSSheet>(AssetDatabase.GUIDToAssetPath(g));
                    if (s != null && !string.IsNullOrEmpty(s.source) && paths.Contains(s.source.Replace('\\', '/')))
                        if (RSSheetBuilder.Reprocess(s)) Debug.Log("[도안] '" + s.source + "' 저장 → 결과를 다시 만들었습니다", s);
                }
            };
        }
    }
}
