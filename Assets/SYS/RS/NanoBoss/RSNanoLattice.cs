// RE:AL STEEL - 나노봇 격자 (기계 군체)
//
// 봇은 무작위 자리가 아니라 부위마다 깔린 '격자 자리' 에 맞물린다 → 촘촘한 기계 군체.
//   덩어리   육각 격자 (비늘). 가운데부터 바깥으로 채운다
//   줄기     관절선을 따라 줄 × 두께 방향 칸 (벽돌처럼 엇갈림). 뿌리부터 끝으로 채운다
//   웅덩이   바닥을 따라 깔린 격자. 가운데부터 채운다
// 자리표는 '채우는 순서' 대로 정렬돼 있다. 부위의 '양' = 앞에서부터 채운 비율.
//
// NanoSlotSet: 자리마다 주인 봇. 맞아서 빠진 자리는 잠시 비워 두고(구멍이 보임), 그 뒤
//   ① 남는 봇이 날아와 채우거나 ② 맨 뒤 자리 봇이 앞쪽 구멍으로 옮겨 와 형태가 한 칸씩 줄어든다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public static class NanoLattice
    {
        const float Row = 0.8660254f;

        static float Fract(float x) => x - Mathf.Floor(x);

        /// <summary>
        /// 타원 안 육각 격자 (간격 1), 가운데부터. x 는 aspect 로 나눈 '원 좌표'.
        /// 반환 (x, y, z(-1~1 의사난수), r(원 좌표 반지름))
        /// </summary>
        public static Vector4[] EllipseHex(int n, float aspect)
        {
            n = Mathf.Max(1, n);
            aspect = Mathf.Max(0.05f, aspect);
            int R = Mathf.CeilToInt(Mathf.Sqrt(n * Row / (Mathf.PI * aspect))) + 2;
            var list = new List<Vector4>(n * 2);
            while (true)
            {
                list.Clear();
                int rows = Mathf.CeilToInt(R / Row) + 1;
                int cols = Mathf.CeilToInt(R * aspect) + 2;
                for (int j = -rows; j <= rows; j++)
                {
                    float y = j * Row;
                    float off = (j & 1) != 0 ? 0.5f : 0f;
                    for (int i = -cols; i <= cols; i++)
                    {
                        float nx = (i + off) / aspect;
                        float r2 = nx * nx + y * y;
                        if (r2 > R * R) continue;
                        list.Add(new Vector4(nx, y, 0f, Mathf.Sqrt(r2)));
                    }
                }
                if (list.Count >= n) break;
                R += 2;
            }
            SortSpread(list);
            var res = new Vector4[n];
            for (int k = 0; k < n; k++)
            {
                var v = list[k];
                v.z = Fract(k * 0.618034f + 0.31f) * 2f - 1f;
                res[k] = v;
            }
            return res;
        }

        /// <summary>반지름 순. 같은 반지름(고리) 안에서는 각도를 흩어 놓아 반쯤 찬 고리가 한쪽에 몰리지 않게</summary>
        static void SortSpread(List<Vector4> list)
        {
            list.Sort((a, b) =>
            {
                if (Mathf.Abs(a.w - b.w) > 1e-4f) return a.w.CompareTo(b.w);
                return Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x));
            });
            var tmp = new List<Vector4>();
            int s = 0;
            while (s < list.Count)
            {
                int e = s + 1;
                while (e < list.Count && Mathf.Abs(list[e].w - list[s].w) <= 1e-4f) e++;
                int g = e - s;
                if (g > 2)
                {
                    int bits = 1;
                    while ((1 << bits) < g) bits++;
                    tmp.Clear();
                    for (int k = 0; k < g; k++) tmp.Add(list[s + k]);
                    var order = new int[g];
                    var key = new int[g];
                    for (int k = 0; k < g; k++) { order[k] = k; key[k] = BitRev(k, bits); }
                    Array.Sort(key, order);
                    for (int k = 0; k < g; k++) list[s + k] = tmp[order[k]];
                }
                s = e;
            }
        }

        static int BitRev(int v, int bits)
        {
            int r = 0;
            for (int i = 0; i < bits; i++) { r = (r << 1) | (v & 1); v >>= 1; }
            return r;
        }

        /// <summary>덩어리 부위 — 단위 타원 좌표 (x, y), z, 채움 순서 비율. space = 월드 간격</summary>
        public static Vector4[] Ellipse(int n, Vector2 size, out float space)
        {
            n = Mathf.Max(1, n);
            size = new Vector2(Mathf.Max(0.01f, Mathf.Abs(size.x)), Mathf.Max(0.01f, Mathf.Abs(size.y)));
            var hex = EllipseHex(n, size.x / size.y);
            float ext = hex[n - 1].w + 0.5f;
            var res = new Vector4[n];
            for (int k = 0; k < n; k++)
                res[k] = new Vector4(hex[k].x / ext, hex[k].y / ext, hex[k].z * 0.6f, k / (float)n);
            space = size.y / ext;
            return res;
        }

        /// <summary>
        /// 줄기 — (u 0~1, 가로 a, z -1~1, 채움 순서 비율). 가로 a: 보통 -1~1 (두께 방향), floor 면 0~1 (바닥 → 물마루).
        /// width = u 에 따른 전체 폭 (m, 균등 표본), length = 관절선 길이 (m)
        /// </summary>
        public static Vector4[] Chain(int n, float length, float[] width, bool floor, out float space)
        {
            n = Mathf.Max(1, n);
            length = Mathf.Max(0.01f, length);
            float avg = 0f;
            for (int k = 0; k < width.Length; k++) avg += Mathf.Max(0.01f, width[k]);
            avg /= Mathf.Max(1, width.Length);
            float s0 = Mathf.Sqrt(length * avg / n);
            float lo = s0 * 0.25f, hi = s0 * 4f;
            for (int it = 0; it < 28; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (CountChain(mid, length, width) >= n) lo = mid; else hi = mid;
            }
            float s = lo;
            int guard = 0;
            while (CountChain(s, length, width) < n && guard++ < 200) s *= 0.97f;

            int rows = Mathf.Max(1, Mathf.RoundToInt(length / s));
            var list = new List<Vector4>(n + rows * 2);
            var keys = new List<float>(n + rows * 2);
            for (int k = 0; k < rows; k++)
            {
                float u = (k + 0.5f) / rows;
                int cols = Mathf.Max(1, Mathf.RoundToInt(W(width, u) / s));
                float shift = cols > 1 ? ((k & 1) == 0 ? -0.25f : 0.25f) : 0f;
                for (int c = 0; c < cols; c++)
                {
                    float f = (c + 0.5f + shift) / cols;
                    float a = floor ? f : f * 2f - 1f;
                    float z = Fract(k * 0.618034f + c * 0.381966f + 0.17f) * 2f - 1f;
                    list.Add(new Vector4(u, a, z, 0f));
                    keys.Add(k * 4f + (floor ? f : Mathf.Abs(a)));   // 뿌리부터, 같은 줄은 가운데(파도는 바닥)부터
                }
            }
            var order = new int[list.Count];
            var karr = keys.ToArray();
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(karr, order);
            var res = new Vector4[n];
            for (int k = 0; k < n; k++)
            {
                var v = list[order[Mathf.Min(k, order.Length - 1)]];
                v.w = k / (float)n;
                res[k] = v;
            }
            space = s;
            return res;
        }

        static float W(float[] width, float u)
        {
            if (width.Length == 1) return width[0];
            float f = Mathf.Clamp01(u) * (width.Length - 1);
            int i = Mathf.Min((int)f, width.Length - 2);
            return Mathf.Lerp(width[i], width[i + 1], f - i);
        }

        static int CountChain(float s, float length, float[] width)
        {
            int rows = Mathf.Max(1, Mathf.RoundToInt(length / s));
            int total = 0;
            for (int k = 0; k < rows; k++) total += Mathf.Max(1, Mathf.RoundToInt(W(width, (k + 0.5f) / rows) / s));
            return total;
        }

        /// <summary>웅덩이 — (x -0.5~0.5, 층 0~1, z -0.5~0.5, 채움 순서 비율). 가운데부터</summary>
        public static Vector4[] Puddle(int n, Vector2 extent, out float space)
        {
            n = Mathf.Max(1, n);
            float ex = Mathf.Max(0.1f, extent.x), ez = Mathf.Max(0.1f, extent.y);
            float s = Mathf.Sqrt(ex * ez / n);
            int nz = Mathf.Max(1, Mathf.RoundToInt(ez / s));
            int nx = Mathf.CeilToInt(n / (float)nz);
            var list = new List<Vector4>(nx * nz);
            var keys = new List<float>(nx * nz);
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = (i + 0.5f + ((j & 1) != 0 ? 0.25f : -0.25f)) / nx - 0.5f;
                float z = (j + 0.5f) / nz - 0.5f;
                float y = ((i * 3 + j * 5) % 4) / 3f;
                list.Add(new Vector4(x, y, z, 0f));
                keys.Add(x * ex * x * ex + z * ez * z * ez);
            }
            var order = new int[list.Count];
            var karr = keys.ToArray();
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(karr, order);
            var res = new Vector4[n];
            for (int k = 0; k < n; k++)
            {
                var v = list[order[Mathf.Min(k, order.Length - 1)]];
                v.w = k / (float)n;
                res[k] = v;
            }
            space = Mathf.Min(ex / nx, s);
            return res;
        }

        /// <summary>
        /// 가는 줄기 (두뇌의 가시 · 채찍) — 봇 n 개를 뿌리부터 격자로. (u, 가로 -1~1, z) 를 돌려준다
        /// </summary>
        public static Vector3[] Taper(int n, float r0, float r1, float length)
        {
            var w = new float[8];
            for (int k = 0; k < w.Length; k++) w[k] = 2f * Mathf.Max(0.02f, Mathf.Lerp(r0, r1, k / (w.Length - 1f)));
            var ch = Chain(n, length, w, false, out _);
            var res = new Vector3[n];
            for (int k = 0; k < n; k++) res[k] = new Vector3(ch[k].x, ch[k].y, ch[k].z * 0.6f);
            return res;
        }

        /// <summary>저불일치 수열 (기사 몸을 고르게 채울 때)</summary>
        public static float Halton(int index, int b)
        {
            float f = 1f, r = 0f;
            int i = index + 1;
            while (i > 0) { f /= b; r += f * (i % b); i /= b; }
            return r;
        }
    }

    /// <summary>격자 자리마다 주인 봇. 빈자리 채우기 · 다지기</summary>
    public sealed class NanoSlotSet
    {
        public int[] owner = Array.Empty<int>();
        public float[] freeAt = Array.Empty<float>();
        public int Count => owner.Length;
        public int Occupied { get; private set; }
        readonly List<int> needs = new List<int>();

        public void Resize(int n)
        {
            if (owner.Length != n) { owner = new int[n]; freeAt = new float[n]; }
            for (int k = 0; k < n; k++) { owner[k] = -1; freeAt[k] = 0f; }
            Occupied = 0;
        }

        static bool Member(ref NanoBot b, int guide, int limb) =>
            b.mode == BotMode.Bound && b.guide == guide && (limb < 0 || b.slot == limb);

        /// <summary>
        /// 한 프레임 정리. members 가 null 이면 모든 봇에서 guide 에 붙은 봇을 찾는다.
        /// hitDelay = 맞아서 빠진 자리를 비워 두는 시간. maxMoves = 이번에 다질(뒤 → 앞으로 옮길) 최대 수.
        /// assigned 가 있으면 새로 자리를 받은 봇을 담는다
        /// </summary>
        public int Maintain(NanoBot[] bots, List<int> members, int guide, int limb, float now, float hitDelay, int maxMoves, List<int> assigned = null, Vector4[] coords = null)
        {
            int n = owner.Length;
            if (n == 0) return 0;

            // 1. 떠난 봇 자리 비우기
            for (int k = 0; k < n; k++)
            {
                int o = owner[k];
                if (o < 0) continue;
                if (o < bots.Length && Member(ref bots[o], guide, limb) && bots[o].cell == k) continue;
                owner[k] = -1;
                freeAt[k] = now + (o < bots.Length && bots[o].mode == BotMode.Debris ? hitDelay : 0f);
            }

            // 2. 자리 없는 봇
            needs.Clear();
            if (members != null)
            {
                for (int m = 0; m < members.Count; m++)
                {
                    int i = members[m];
                    int c = bots[i].cell;
                    if (c < 0 || c >= n || owner[c] != i) needs.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < bots.Length; i++)
                {
                    if (!Member(ref bots[i], guide, limb)) continue;
                    int c = bots[i].cell;
                    if (c < 0 || c >= n || owner[c] != i) needs.Add(i);
                }
            }

            // 3. 앞쪽 빈자리부터 (구멍 대기가 끝난 자리 먼저)
            int cur = 0, any = 0;
            for (int q = 0; q < needs.Count; q++)
            {
                int i = needs[q];
                int k = -1;
                while (cur < n) { if (owner[cur] < 0 && freeAt[cur] <= now) { k = cur++; break; } cur++; }
                if (k < 0)
                {
                    while (any < n) { if (owner[any] < 0) { k = any++; break; } any++; }
                }
                if (k < 0) { bots[i].cell = -1; continue; }
                owner[k] = i;
                bots[i].cell = k;
                assigned?.Add(i);
            }

            // 4. 다지기 — 맨 바깥(채움 순서 끝) 봇을 앞쪽 빈자리로 (형태가 한 칸씩 줄어든다).
            //    좌표가 있으면 바깥 몇십 자리 중 구멍에 가장 가까운 봇을 고른다 → 몸을 가로질러 날아가지 않는다
            int moves = 0, lo = 0, hi = n - 1;
            while (moves < maxMoves)
            {
                while (lo < n && !(owner[lo] < 0 && freeAt[lo] <= now)) lo++;
                while (hi >= 0 && owner[hi] < 0) hi--;
                if (lo >= hi || lo >= n || hi < 0) break;
                int pick = hi;
                if (coords != null && coords.Length == n)
                {
                    Vector2 hole = coords[lo];
                    float best = float.MaxValue;
                    int seen = 0;
                    for (int k = hi; k > lo && seen < 48; k--)
                    {
                        if (owner[k] < 0) continue;
                        seen++;
                        float d = ((Vector2)coords[k] - hole).sqrMagnitude;
                        if (d < best) { best = d; pick = k; }
                    }
                }
                int i = owner[pick];
                owner[pick] = -1;
                freeAt[pick] = now;
                owner[lo] = i;
                bots[i].cell = lo;
                moves++;
            }

            int occ = 0;
            for (int k = 0; k < n; k++) if (owner[k] >= 0) occ++;
            Occupied = occ;
            return occ;
        }
    }
}
