// RE:AL STEEL - 나노봇 봇 종류 (그림 PNG + 비율 + 쓰는 곳)
//
// 군집 인스펙터 '봇 종류' 목록에 PNG 를 한 줄에 하나씩 넣는다.
//   · 쓰는 곳: 몸 (격자에 맞물린 판) · 테두리 (실루엣 가장자리) · 유체 (방울) · 낱개 (날아갈 때 · 떨어진 봇)
//   · 비율: 같은 '쓰는 곳' 끼리 나눠 갖는다 (85 · 10 · 5 → 85% · 10% · 5%)
//   · 몸 종류만 넣으면 테두리 (윗줄 밝게) · 유체 (한 칸 크게 + 모서리 깎기) · 낱개 (몸 그림 그대로) 는 자동
//   · 목록이 비면 코드에 그린 시험 그림
//   · 봇마다 종류는 처음에 정해져 끝까지 같다 (빨간 봇은 날아가도 · 녹았다 굳어도 빨간 봇)
//   · PNG 의 '칠한 부분' (알파 50% 이상) 만 쓴다. 여백은 잘라 내고 가운데에 놓는다. 한 변 최대 16px
//   · 격자 간격 = 비율이 가장 큰 몸 그림의 칠한 크기 (군집 '간격을 그림에 맞춤')
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public enum NanoBotUse
    {
        [InspectorName("몸 (격자 판)")] Body,
        [InspectorName("테두리 (가장자리 판)")] Edge,
        [InspectorName("유체 (방울)")] Fluid,
        [InspectorName("낱개 (날아갈 때)")] Loose,
    }

    [Serializable]
    public class NanoBotKind
    {
        [Tooltip("봇 그림 PNG. 칠한 부분만 쓴다 (투명한 여백은 잘라 냄). 한 변 최대 16px")]
        public Texture2D picture;
        [Tooltip("어디에 쓰는 그림인가")]
        public NanoBotUse use = NanoBotUse.Body;
        [Tooltip("같은 '쓰는 곳' 끼리의 비율. 아무 숫자나 — 합에 대한 % 로 나눈다. 0 = 안 씀")]
        [Min(0f)] public float ratio = 1f;
    }

    /// <summary>만든 아틀라스와 봇마다 그림 고르는 표</summary>
    public sealed class NanoBotAtlas
    {
        public Texture2D texture;
        public int tile, cols, rows, count;
        public Color32[][] tiles;                  // 칸마다 tile × tile (위쪽 0) — 인스펙터 미리보기
        public int[] body, edge, drop, loose;      // 칸 번호
        public float[] bodyW, edgeW, dropW, looseW;
        public int[] bodyEdge, bodyDrop;           // 몸 종류마다 자동 테두리 · 방울 칸
        public Vector2Int[] bodySize;              // 몸 그림 칠한 크기
        public int main;                           // 비율이 가장 큰 몸 종류
        public bool custom;                        // 사용자 몸 그림을 쓰는가
        public readonly List<string> notes = new List<string>();

        public int MainSize => Mathf.Max(bodySize[main].x, bodySize[main].y);

        static float U(int i, uint salt)
        {
            uint h = (uint)i * 2654435761u ^ (salt * 2246822519u + 0x9E3779B9u);
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        static int Pick(float[] w, float u)
        {
            float sum = 0f;
            for (int k = 0; k < w.Length; k++) sum += w[k];
            float t = u * sum;
            for (int k = 0; k < w.Length; k++) { t -= w[k]; if (t < 0f) return k; }
            return w.Length - 1;
        }

        /// <summary>봇 번호로 종류를 고른다 (같은 번호 = 늘 같은 종류)</summary>
        public void Assign(int i, ref NanoBot b)
        {
            int k = Pick(bodyW, U(i, 1));
            b.tileBody = (ushort)body[k];
            b.tileEdge = (ushort)(edge.Length > 0 ? edge[Pick(edgeW, U(i, 2))] : bodyEdge[k]);
            b.tileDrop = (ushort)(drop.Length > 0 ? drop[Pick(dropW, U(i, 3))] : bodyDrop[k]);
            b.tileLoose = (ushort)(loose.Length > 0 ? loose[Pick(looseW, U(i, 4))] : body[k]);
        }

        /// <summary>몸 종류 k 의 실제 비율 (0~1)</summary>
        public float BodyShare(int k)
        {
            float sum = 0f;
            foreach (var w in bodyW) sum += w;
            return sum > 0f ? bodyW[k] / sum : 0f;
        }
    }

    /// <summary>봇 그림 — 코드 시험 그림 + PNG 종류 목록 → 아틀라스</summary>
    public static class RSNanoBotSprites
    {
        public const int Tile = 8;          // 픽셀 밀도 기준: botWorldSize = 8px
        public const int MaxTile = 16;
        public const int Pad = 1;

        struct Pic
        {
            public int w, h;
            public Color32[] px;            // 위쪽 0
            public Color32 this[int x, int y] { get => px[y * w + x]; set => px[y * w + x] = value; }
        }

        // . 투명  d 어두움  n 이음매  m 중간  l 밝음  c 코어(하늘)
        static readonly string[][] LooseArt =
        {
            new[] { "lm..ml", "mmmmmm", "dddddd" },             // 아령 (마이크로봇)
            new[] { "..l..", ".lmm.", "lmcmd", ".dmd.", "..d.." }, // 마름모
            new[] { "lllm", "lcmd", "lmmd", "mmdd" },           // 네모 칩
            new[] { "...l", "..lm", ".lmd", "lmdd" },           // 파편
            new[] { "lm", "md" },                                // 점
        };
        // 속판 3px — 대비를 낮춘 면 + 오른쪽 · 아래 1px 이음매(n). 판끼리 이어져 한 장의 장갑판처럼 읽힌다
        static readonly string[] BodyArt = { "mmn", "mmn", "nnn" };
        // 테두리 판 3px — 윗줄만 밝다
        static readonly string[] EdgeArt = { "lll", "mmn", "nnn" };
        // 방울 4px — 모서리가 둥글어 겹치면 이어진 액체로 읽힌다
        static readonly string[] DropArt = { ".lm.", "lmmd", "mmmd", ".dd." };

        static readonly HashSet<int> warned = new HashSet<int>();

        // ─────────────────────────────────────────────────────────────
        // 아틀라스
        // ─────────────────────────────────────────────────────────────
        /// <summary>목록이 바뀌었는지 보는 값 (그림 · 다시 가져오기 · 쓰는 곳 · 비율)</summary>
        public static int Signature(List<NanoBotKind> kinds)
        {
            unchecked
            {
                int h = 17;
                if (kinds != null)
                    foreach (var k in kinds)
                    {
                        if (k == null) { h = h * 31 + 1; continue; }
                        h = h * 31 + (k.picture ? k.picture.GetInstanceID() : 0);
                        h = h * 31 + (k.picture ? (int)k.picture.updateCount : 0);
                        h = h * 31 + (int)k.use;
                        h = h * 31 + k.ratio.GetHashCode();
                    }
                return h;
            }
        }

        public static NanoBotAtlas Build(List<NanoBotKind> kinds)
        {
            var A = new NanoBotAtlas();
            var pics = new List<Pic>();
            var lists = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var weights = new[] { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
            var bodyPics = new List<Pic>();

            if (kinds != null)
                foreach (var k in kinds)
                {
                    if (k == null || !k.picture || k.ratio <= 0f) continue;
                    if (!Read(k.picture, A.notes, out var p)) continue;
                    int u = (int)k.use;
                    lists[u].Add(pics.Count);
                    weights[u].Add(k.ratio);
                    pics.Add(p);
                    if (k.use == NanoBotUse.Body) bodyPics.Add(p);
                }

            A.custom = bodyPics.Count > 0;
            if (!A.custom)
            {
                // 시험 그림: 몸 · 테두리 · 방울은 손으로 그린 판 (사용자가 테두리 · 유체만 넣었으면 그것을 쓴다)
                var bp = FromArt(BodyArt);
                lists[0].Add(pics.Count); weights[0].Add(1f); pics.Add(bp); bodyPics.Add(bp);
                if (lists[1].Count == 0) { lists[1].Add(pics.Count); weights[1].Add(1f); pics.Add(FromArt(EdgeArt)); }
                if (lists[2].Count == 0) { lists[2].Add(pics.Count); weights[2].Add(1f); pics.Add(FromArt(DropArt)); }
                if (lists[3].Count == 0)
                    foreach (var a in LooseArt) { lists[3].Add(pics.Count); weights[3].Add(1f); pics.Add(FromArt(a)); }
            }

            // 몸마다 자동 테두리 · 방울 (목록에 테두리 · 유체 그림이 없을 때만)
            int nb = lists[0].Count;
            A.bodyEdge = new int[nb];
            A.bodyDrop = new int[nb];
            A.bodySize = new Vector2Int[nb];
            for (int k = 0; k < nb; k++)
            {
                var p = pics[lists[0][k]];
                A.bodySize[k] = new Vector2Int(p.w, p.h);
                if (lists[1].Count == 0) { A.bodyEdge[k] = pics.Count; pics.Add(EdgeOf(p)); }
                else A.bodyEdge[k] = lists[1][0];
                if (lists[2].Count == 0) { A.bodyDrop[k] = pics.Count; pics.Add(DropOf(p)); }
                else A.bodyDrop[k] = lists[2][0];
            }

            A.body = lists[0].ToArray(); A.bodyW = weights[0].ToArray();
            A.edge = lists[1].ToArray(); A.edgeW = weights[1].ToArray();
            A.drop = lists[2].ToArray(); A.dropW = weights[2].ToArray();
            A.loose = lists[3].ToArray(); A.looseW = weights[3].ToArray();

            A.main = 0;
            for (int k = 1; k < nb; k++) if (A.bodyW[k] > A.bodyW[A.main]) A.main = k;
            if (A.custom)
            {
                var ms = A.bodySize[A.main];
                for (int k = 0; k < nb; k++)
                    if (A.bodySize[k] != ms)
                        A.notes.Add($"몸 그림 크기가 다르다: {A.bodySize[k].x}×{A.bodySize[k].y} (기준 {ms.x}×{ms.y}) — 격자는 기준 크기라 {(A.bodySize[k].x > ms.x || A.bodySize[k].y > ms.y ? "이웃과 겹친다" : "틈이 생긴다")}");
            }

            // 칸 크기 = 가장 큰 그림 (8 이상)
            int T = Tile;
            foreach (var p in pics) T = Mathf.Max(T, Mathf.Max(p.w, p.h));
            T = Mathf.Min(T, MaxTile);
            int n = pics.Count, cell = T + Pad * 2;
            int cols = Mathf.Min(n, 16), rows = (n + cols - 1) / cols;
            int W = cols * cell, H = rows * cell;
            var px = new Color32[W * H];
            A.tiles = new Color32[n][];
            for (int t = 0; t < n; t++)
            {
                var p = pics[t];
                var tl = new Color32[T * T];
                int ox = (T - p.w) / 2, oy = (T - p.h) / 2;
                for (int y = 0; y < p.h; y++)
                for (int x = 0; x < p.w; x++)
                {
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= T || ty >= T) continue;
                    var c = p[x, y];
                    if (c.a < 128) continue;
                    c.a = 255;
                    tl[ty * T + tx] = c;
                    int ix = (t % cols) * cell + Pad + tx, iyTop = (t / cols) * cell + Pad + ty;
                    px[(H - 1 - iyTop) * W + ix] = c;
                }
                A.tiles[t] = tl;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false)
            {
                name = A.custom ? "NanoBot Sprites (PNG)" : "NanoBot Sprites (시험)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            A.texture = tex;
            A.tile = T; A.cols = cols; A.rows = rows; A.count = n;
            return A;
        }

        // ─────────────────────────────────────────────────────────────
        // 그림 읽기 · 자동 테두리 · 자동 방울
        // ─────────────────────────────────────────────────────────────
        static bool Read(Texture2D t, List<string> notes, out Pic pic)
        {
            pic = default;
            if (!t.isReadable)
            {
                notes.Add($"'{t.name}' 을(를) 읽을 수 없다 (가져오기 설정 Read/Write) — 편집기가 곧 자동으로 맞춘다");
                if (warned.Add(t.GetInstanceID()))
                    UnityEngine.Debug.LogWarning($"[나노봇] 봇 그림 '{t.name}' 을(를) 읽을 수 없습니다 (가져오기 설정 Read/Write). 편집기에서는 자동으로 맞춥니다.", t);
                return false;
            }
            var src = t.GetPixels32();
            int w = t.width, h = t.height;
            int x0 = w, y0 = h, x1 = -1, y1 = -1;                      // 위쪽 0 기준 칠한 범위
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (src[(h - 1 - y) * w + x].a >= 128)
                {
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            if (x1 < 0) { notes.Add($"'{t.name}' 에 칠한 픽셀이 없다 (알파 50% 이상)"); return false; }
            int pw = x1 - x0 + 1, ph = y1 - y0 + 1;
            if (pw > MaxTile || ph > MaxTile)
            {
                notes.Add($"'{t.name}' 칠한 크기 {pw}×{ph} — 한 변 {MaxTile}px 까지만 쓴다 (가운데를 자름)");
                x0 += Mathf.Max(0, (pw - MaxTile) / 2); y0 += Mathf.Max(0, (ph - MaxTile) / 2);
                pw = Mathf.Min(pw, MaxTile); ph = Mathf.Min(ph, MaxTile);
            }
            pic = new Pic { w = pw, h = ph, px = new Color32[pw * ph] };
            for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
                pic[x, y] = src[(h - 1 - (y0 + y)) * w + x0 + x];
            return true;
        }

        static Pic FromArt(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var p = new Pic { w = w, h = h, px = new Color32[w * h] };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (rows[y][x] != '.') p[x, y] = Col(rows[y][x]);
            return p;
        }

        static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        /// <summary>자동 테두리: 칸마다 맨 위 칠한 픽셀을 밝게</summary>
        static Pic EdgeOf(Pic s)
        {
            var p = new Pic { w = s.w, h = s.h, px = (Color32[])s.px.Clone() };
            var hi = new Color32(220, 228, 240, 255);
            for (int x = 0; x < p.w; x++)
                for (int y = 0; y < p.h; y++)
                    if (p[x, y].a >= 128) { var c = Lerp(p[x, y], hi, 0.45f); c.a = 255; p[x, y] = c; break; }
            return p;
        }

        /// <summary>자동 방울: 오른쪽 · 아래로 한 칸 넓혀 이웃과 겹치게 + 네 모서리를 깎아 둥글게 + 위 밝게 · 아래 어둡게</summary>
        static Pic DropOf(Pic s)
        {
            int w = Mathf.Min(s.w + 1, MaxTile), h = Mathf.Min(s.h + 1, MaxTile);
            var p = new Pic { w = w, h = h, px = new Color32[w * h] };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                p[x, y] = s[Mathf.Min(x, s.w - 1), Mathf.Min(y, s.h - 1)];
            if (w >= 3 && h >= 3)
            {
                p[0, 0] = default; p[w - 1, 0] = default; p[0, h - 1] = default; p[w - 1, h - 1] = default;
            }
            var hi = new Color32(220, 228, 240, 255);
            var lo = new Color32(20, 22, 30, 255);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++) if (p[x, y].a >= 128) { var c = Lerp(p[x, y], hi, 0.3f); c.a = 255; p[x, y] = c; break; }
                for (int y = h - 1; y >= 0; y--) if (p[x, y].a >= 128) { var c = Lerp(p[x, y], lo, 0.3f); c.a = 255; p[x, y] = c; break; }
            }
            return p;
        }

        // ─────────────────────────────────────────────────────────────
        // PNG 로 뽑기 (편집기 메뉴)
        // ─────────────────────────────────────────────────────────────
        /// <summary>시험 그림을 8 × 8 PNG 용 텍스처로 (가운데에 놓음). which: 0 = 몸, 1~5 = 낱개</summary>
        public static Texture2D DefaultPicture(int which)
        {
            var p = FromArt(which == 0 ? BodyArt : LooseArt[Mathf.Clamp(which - 1, 0, LooseArt.Length - 1)]);
            var t = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false, false);
            var px = new Color32[Tile * Tile];
            int ox = (Tile - p.w) / 2, oy = (Tile - p.h) / 2;
            for (int y = 0; y < p.h; y++)
            for (int x = 0; x < p.w; x++)
                px[(Tile - 1 - (oy + y)) * Tile + ox + x] = p[x, y];
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        public static int LooseCount => LooseArt.Length;

        static Color32 Col(char c)
        {
            switch (c)
            {
                case 'o': return new Color32(14, 15, 20, 255);
                case 'd': return new Color32(46, 50, 62, 255);
                case 'm': return new Color32(78, 86, 104, 255);
                case 'n': return new Color32(62, 68, 84, 255);
                case 'l': return new Color32(132, 144, 168, 255);
                case 'c': return new Color32(110, 230, 255, 255);
                default: return new Color32(255, 0, 255, 255);
            }
        }
    }
}
