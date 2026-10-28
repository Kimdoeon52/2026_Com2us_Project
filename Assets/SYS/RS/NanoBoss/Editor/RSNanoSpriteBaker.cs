// RE:AL STEEL - 나노봇 그림 굽기 (스프라이트 시트 → 봇 자리)
//
// 원본 PNG 바이트를 직접 읽는다 → 텍스처 가져오기 설정(읽기 허용 · 압축)과 상관없이 픽셀 그대로.
// 프레임 나누기: 잘라 둔 스프라이트(Sprite Mode = Multiple)가 2개 이상이면 그 이름 순서 · 각 스프라이트 피벗,
//               아니면 가로 × 세로 칸 (위 줄부터 왼쪽 → 오른쪽), 피벗은 설정값.
// 봇 배정: 첫 프레임은 힐베르트 곡선 순서(가까운 픽셀끼리 가까운 번호)로 고르게 나눈다.
//          다음 프레임부터는 ① 같은 픽셀이 남아 있으면 그 봇이 그대로 ② 나머지는 가장 가까운 빈 픽셀로 →
//          그림이 조금 바뀌면 봇도 조금만 움직인다 (안 바뀐 곳은 가만히).
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    public static class RSNanoSpriteBaker
    {
        static string Dir => RealSteel.Common.EditorTools.RSPaths.Ensure(RealSteel.Common.EditorTools.RSPaths.Data + "/NanoBoss/그림");

        /// <summary>시트에 맞는 그림 모양 에셋을 찾거나 만들고 굽는다</summary>
        public static RSNanoSpriteShape CreateOrFind(Texture2D sheet)
        {
            string path = Dir + "/" + sheet.name + ".asset";
            var shape = AssetDatabase.LoadAssetAtPath<RSNanoSpriteShape>(path);
            if (!shape)
            {
                shape = ScriptableObject.CreateInstance<RSNanoSpriteShape>();
                shape.sheet = sheet;
                // 잘라 둔 스프라이트가 없으면 정사각 칸으로 짐작 (가로로 이어 붙인 시트가 흔하다)
                if (Slices(sheet).Count < 2 && sheet.height > 0 && sheet.width % sheet.height == 0)
                {
                    shape.columns = sheet.width / sheet.height;
                    shape.rows = 1;
                }
                AssetDatabase.CreateAsset(shape, path);
            }
            Bake(shape);
            AssetDatabase.SaveAssets();
            return shape;
        }

        public static string KeyOf(RSNanoSpriteShape s)
        {
            if (!s || !s.sheet) return "";
            string path = AssetDatabase.GetAssetPath(s.sheet);
            long t = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            return $"{AssetDatabase.AssetPathToGUID(path)}|{t}|{s.columns}|{s.rows}|{s.frameLimit}|{s.darkness:0.###}|{s.invert}|{s.alphaCut:0.###}|{s.pivot}|{Slices(s.sheet).Count}";
        }

        public static bool NeedsBake(RSNanoSpriteShape s) => s && s.sheet && (s.bakedKey != KeyOf(s) || !s.Ready);

        static List<Sprite> Slices(Texture2D sheet)
        {
            var list = new List<Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(sheet)))
                if (o is Sprite sp) list.Add(sp);
            return list;
        }

        static int NaturalKey(string name)
        {
            var m = Regex.Match(name, @"(\d+)$");
            return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : 0;
        }

        static bool Read(Texture2D sheet, out Color32[] pix, out int W, out int H)
        {
            pix = null; W = H = 0;
            string path = AssetDatabase.GetAssetPath(sheet);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool ok = t.LoadImage(File.ReadAllBytes(path));
            if (ok) { W = t.width; H = t.height; pix = t.GetPixels32(); }
            Object.DestroyImmediate(t);
            return ok;
        }

        /// <summary>프레임마다 칠한 픽셀 (피벗 기준 정수 좌표)</summary>
        public static List<List<Vector2Int>> Frames(RSNanoSpriteShape s, out string error)
        {
            error = null;
            var frames = new List<List<Vector2Int>>();
            if (!s.sheet) { error = "스프라이트 시트가 없다"; return frames; }
            if (!Read(s.sheet, out var pix, out int W, out int H)) { error = "PNG · JPG 만 읽을 수 있다"; return frames; }

            var rects = new List<(RectInt r, Vector2 pivot)>();
            var slices = Slices(s.sheet);
            if (slices.Count >= 2)
            {
                slices.Sort((a, b) => NaturalKey(a.name) != NaturalKey(b.name) ? NaturalKey(a.name).CompareTo(NaturalKey(b.name)) : string.CompareOrdinal(a.name, b.name));
                float sx = W / Mathf.Max(1f, s.sheet.width), sy = H / Mathf.Max(1f, s.sheet.height);   // 가져오기 최대 크기로 줄어든 경우 대비
                foreach (var sp in slices)
                {
                    var r = sp.rect;
                    rects.Add((new RectInt(Mathf.RoundToInt(r.x * sx), Mathf.RoundToInt(r.y * sy), Mathf.RoundToInt(r.width * sx), Mathf.RoundToInt(r.height * sy)), sp.pivot));
                }
            }
            else
            {
                int cw = W / Mathf.Max(1, s.columns), ch = H / Mathf.Max(1, s.rows);
                for (int row = 0; row < s.rows; row++)
                for (int col = 0; col < s.columns; col++)
                    rects.Add((new RectInt(col * cw, H - (row + 1) * ch, cw, ch),
                               s.pivot == NanoSpritePivot.Center ? new Vector2(cw * 0.5f, ch * 0.5f) : new Vector2(cw * 0.5f, 0f)));
            }
            if (s.frameLimit > 0 && rects.Count > s.frameLimit) rects.RemoveRange(s.frameLimit, rects.Count - s.frameLimit);

            int cut = Mathf.RoundToInt(s.alphaCut * 255f);
            foreach (var (r, pv) in rects)
            {
                var list = new List<Vector2Int>();
                for (int y = r.y; y < r.y + r.height; y++)
                for (int x = r.x; x < r.x + r.width; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    var c = pix[y * W + x];
                    if (c.a < cut || c.a == 0) continue;
                    float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    if (s.invert ? lum <= s.darkness : lum >= s.darkness) continue;
                    list.Add(new Vector2Int(Mathf.FloorToInt(x - r.x - pv.x), Mathf.FloorToInt(y - r.y - pv.y)));
                }
                frames.Add(list);
            }
            if (frames.Count == 0) error = "프레임이 없다 — 가로 · 세로 칸 수를 확인";
            return frames;
        }

        // ─────────────────────────────────────────────────────────────
        // 굽기
        // ─────────────────────────────────────────────────────────────
        public static void Bake(RSNanoSpriteShape s)
        {
            var frames = Frames(s, out string err);
            Undo.RecordObject(s, "그림 굽기");
            int F = frames.Count, N = 0;
            foreach (var f in frames) N = Mathf.Max(N, f.Count);
            s.frameCount = F;
            s.botCount = N;
            s.filled = new int[F];
            s.pixels = new int[F * N];
            s.bakedKey = KeyOf(s);
            if (F == 0 || N == 0)
            {
                s.frameCount = 0; s.botCount = 0;
                EditorUtility.SetDirty(s);
                if (err != null) Debug.LogWarning("[나노봇 그림] " + s.name + ": " + err, s);
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var f in frames)
                foreach (var p in f) { minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y); }
            s.minX = minX; s.minY = minY; s.maxX = maxX; s.maxY = maxY;
            int GW = maxX - minX + 1, GH = maxY - minY + 1;
            int hn = 1;
            while (hn < Mathf.Max(GW, GH)) hn <<= 1;

            var grid = new int[GW * GH];
            var prev = new Vector2Int[N];
            var cap = new List<int>();
            var assign = new int[N];
            var order = new List<int>();

            for (int fi = 0; fi < F; fi++)
            {
                var P = frames[fi];
                s.filled[fi] = P.Count;
                if (P.Count == 0)
                {
                    for (int k = 0; k < N; k++) s.pixels[fi * N + k] = RSNanoSpriteShape.Pack(0, 0, Mathf.Min(k, 127));
                    continue;
                }
                // 힐베르트 순서로 정렬
                P.Sort((a, b) => Hilbert(hn, a.x - minX, a.y - minY).CompareTo(Hilbert(hn, b.x - minX, b.y - minY)));
                // 픽셀마다 받을 봇 수 (N ≥ P: 고르게 나눠 남는 봇을 퍼뜨림)
                cap.Clear();
                int baseCap = N / P.Count, rem = N % P.Count;
                for (int i = 0; i < P.Count; i++) cap.Add(baseCap);
                for (int j = 0; j < rem; j++) cap[(int)((long)j * P.Count / rem)]++;
                for (int i = 0; i < grid.Length; i++) grid[i] = -1;
                for (int i = 0; i < P.Count; i++) grid[(P[i].y - minY) * GW + (P[i].x - minX)] = i;

                for (int k = 0; k < N; k++) assign[k] = -1;
                if (fi == 0)
                {
                    int k = 0;
                    for (int i = 0; i < P.Count && k < N; i++)
                        for (int c = 0; c < cap[i] && k < N; c++) assign[k++] = i;
                    for (int i = 0; i < P.Count; i++) cap[i] = 0;
                }
                else
                {
                    // ① 같은 픽셀이 남아 있으면 그대로
                    for (int k = 0; k < N; k++)
                    {
                        int gx = prev[k].x - minX, gy = prev[k].y - minY;
                        if (gx < 0 || gy < 0 || gx >= GW || gy >= GH) continue;
                        int i = grid[gy * GW + gx];
                        if (i >= 0 && cap[i] > 0) { assign[k] = i; cap[i]--; }
                    }
                    // ② 나머지는 가장 가까운 빈 픽셀로 (지난 자리의 힐베르트 순서대로)
                    order.Clear();
                    for (int k = 0; k < N; k++) if (assign[k] < 0) order.Add(k);
                    order.Sort((a, b) => Hilbert(hn, prev[a].x - minX, prev[a].y - minY).CompareTo(Hilbert(hn, prev[b].x - minX, prev[b].y - minY)));
                    int left = 0;
                    foreach (int c in cap) left += c;
                    int scan = 0;
                    foreach (int k in order)
                    {
                        int i = Nearest(grid, GW, GH, cap, prev[k].x - minX, prev[k].y - minY);
                        if (i < 0)
                        {
                            while (scan < cap.Count && cap[scan] <= 0) scan++;
                            if (scan >= cap.Count) break;
                            i = scan;
                        }
                        assign[k] = i;
                        cap[i]--;
                        left--;
                    }
                }
                var rank = new int[P.Count];
                for (int k = 0; k < N; k++)
                {
                    int i = Mathf.Max(0, assign[k]);
                    var p = P[i];
                    s.pixels[fi * N + k] = RSNanoSpriteShape.Pack(p.x, p.y, rank[i]++);
                    prev[k] = p;
                }
            }
            EditorUtility.SetDirty(s);
        }

        /// <summary>가장 가까운 빈 자리 (사각 고리를 넓혀 가며)</summary>
        static int Nearest(int[] grid, int W, int H, List<int> cap, int cx, int cy)
        {
            int maxR = Mathf.Max(W, H);
            for (int r = 1; r <= maxR; r++)
            {
                int best = -1;
                float bd = float.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                {
                    int y = cy + dy;
                    if (y < 0 || y >= H) continue;
                    int step = (dy == -r || dy == r) ? 1 : 2 * r;
                    for (int dx = -r; dx <= r; dx += step)
                    {
                        int x = cx + dx;
                        if (x < 0 || x >= W) continue;
                        int i = grid[y * W + x];
                        if (i < 0 || cap[i] <= 0) continue;
                        float d = dx * dx + dy * dy;
                        if (d < bd) { bd = d; best = i; }
                    }
                }
                if (best >= 0) return best;
            }
            return -1;
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

        // ─────────────────────────────────────────────────────────────
        // 미리보기 그림 (인스펙터)
        // ─────────────────────────────────────────────────────────────
        static Texture2D previewTex;
        static RSNanoSpriteShape previewShape;
        static int previewFrame = -1;
        static string previewKey;

        public static Texture2D Preview(RSNanoSpriteShape s, int frame)
        {
            if (!s || !s.Ready) return null;
            frame = Mathf.Clamp(frame, 0, s.frameCount - 1);
            if (previewTex && previewShape == s && previewFrame == frame && previewKey == s.bakedKey) return previewTex;
            int W = s.maxX - s.minX + 1, H = s.maxY - s.minY + 1;
            if (!previewTex || previewTex.width != W || previewTex.height != H)
            {
                if (previewTex) Object.DestroyImmediate(previewTex);
                previewTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            }
            var px = new Color32[W * H];
            var bg = new Color32(40, 44, 54, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            for (int k = 0; k < s.botCount; k++)
            {
                int v = s.pixels[frame * s.botCount + k];
                int x = (v & 0xFFF) - 2048 - s.minX, y = ((v >> 12) & 0xFFF) - 2048 - s.minY;
                if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = new Color32(140, 220, 255, 255);
            }
            previewTex.SetPixels32(px);
            previewTex.Apply();
            previewShape = s; previewFrame = frame; previewKey = s.bakedKey;
            return previewTex;
        }
    }
}
