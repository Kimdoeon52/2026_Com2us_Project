// RE:AL STEEL - 나노봇 3페 그림 기사: 봇 → 칸 짝짓기
//
// 프레임이 바뀔 때 봇이 '가장 덜 움직이게' 새 프레임의 칸을 맡긴다 (Burst 잡, 프레임이 바뀔 때만).
//   1. 그대로   — 지금 맡은 칸이 새 프레임에도 있으면 그 칸 (몸통처럼 안 움직이는 곳은 봇도 안 움직인다)
//   2. 가까이   — 남은 봇과 빈 칸을 주변(배율 + 2칸)에서 짝 (윤곽이 1~2px 바뀐 곳). 적은 쪽이 찾는다
//   3. 곡선 순서 — 그래도 남으면 남은 봇 · 빈 칸을 힐베르트 순서로 늘어놓고 비율대로 짝 (팔 · 칼처럼 크게 옮긴 곳)
//   4. 남는 봇  — 가까운 칸에 겹쳐 앉는다 (같은 칸 = 화면에선 안 보임). 군집이 곧 후광으로 돌려보낸다
// 봇이 모자라면 몸 칸을 고르게 솎는다 (픽셀마다 배율 × 배율 칸 중 일부만, 점묘) — 팔다리 하나가 통째로 비지 않게.
// 기사 좌표 = 발밑 기준 칸 (오른쪽을 볼 때). 몸을 돌리면 맡은 칸을 거울로 뒤집고 다시 짝짓는다.
using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace RealSteel.NanoBoss
{
    public sealed class NanoKnightFill
    {
        // ── 번호(cell)마다 맡은 칸 ──
        public int[] locX = Array.Empty<int>(), locY = Array.Empty<int>();
        public byte[] kind = Array.Empty<byte>();
        public uint[] color = Array.Empty<uint>();
        public byte[] has = Array.Empty<byte>();

        // ── 작업용 (잡이 쓴다) ──
        int[] tX = new int[256], tY = new int[256], tKind = new int[256], tClaim = new int[256];
        uint[] tCol = new uint[256];
        int[] grid = new int[1], gridStamp = new int[1], botHead = new int[1], botStamp = new int[1];
        int[] botNext = new int[256], lx = new int[256], ly = new int[256], tgt = new int[256];
        int[] uIdx = new int[256], fIdx = new int[256], uKey = new int[256], fKey = new int[256], sortTmp = new int[512];
        readonly GCHandle[] handles = new GCHandle[40];
        int[] order = new int[1];
        int orderS;
        readonly int[] state = new int[8];   // 0 칸 도장 · 1 맡음 도장 · 2 봇 도장 · 3 칸 수 · 4 그대로 · 5 가까이 · 6 곡선 · 7 겹침
        int gw, gh, gx0, gy0, hilbertN;

        /// <summary>Unity 밖(하네스)에서는 false — 잡을 그냥 부른다</summary>
        public static bool UseBurst = true;

        public int lastTargets => state[3];
        public int lastKept => state[4];
        public int lastNear => state[5];
        public int lastCurve => state[6];
        public int lastStacked => state[7];

        public void Ensure(int n)
        {
            if (locX.Length >= n) return;
            Array.Resize(ref locX, n); Array.Resize(ref locY, n); Array.Resize(ref kind, n);
            Array.Resize(ref color, n); Array.Resize(ref has, n);
        }

        public void Clear() { Array.Clear(has, 0, has.Length); }

        /// <summary>번호 c 의 칸을 잊는다 (봇이 떨어져 나감 · 새로 옴)</summary>
        public void Forget(int c) { if (c >= 0 && c < has.Length) has[c] = 0; }

        public bool Has(int c) => has[c] != 0;

        /// <summary>몸을 돌렸다 — 맡은 칸을 거울로 (월드에서 제자리)</summary>
        public void Mirror()
        {
            for (int c = 0; c < has.Length; c++) if (has[c] != 0) locX[c] = -locX[c] - 1;
        }

        /// <summary>
        /// 픽셀 안 칸을 채우는 순서 (배율 × 배율). 봇이 모자라 일부만 채울 때 줄무늬 대신 고른 점묘가 되게 — 베이어 행렬.
        /// order[k] = k 번째로 채울 칸 (x + y × S)
        /// </summary>
        public static int[] DitherOrder(int S)
        {
            int S2 = S * S;
            int[] rank;
            if (S == 2) rank = new[] { 0, 2, 3, 1 };
            else if (S == 3) rank = new[] { 0, 7, 3, 6, 5, 2, 4, 1, 8 };
            else if (S == 4) rank = new[] { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            else { rank = new int[S2]; for (int k = 0; k < S2; k++) rank[k * (S + 1) % S2] = k; }
            var order = new int[S2];
            for (int sub = 0; sub < S2; sub++) order[rank[sub]] = sub;
            return order;
        }

        static void Grow<T>(ref T[] a, int n) { if (a.Length < n) Array.Resize(ref a, Math.Max(n, a.Length * 2)); }

        /// <summary>
        /// 이번 프레임 칸을 만들고 봇(번호 목록)에게 짝지어 준다.
        /// hintX · hintY: 칸이 없는 번호의 지금 위치 (기사 좌표 칸) — 새로 온 봇이 가까운 칸부터 맡게
        /// </summary>
        public unsafe void Match(NanoKnightClip clip, int frame, int S, int cellW, int cellH, int rootX, int rootY,
                                 int[] slots, int count, int[] hintX, int[] hintY)
        {
            S = Math.Max(1, S);
            int S2 = S * S;
            // 격자 (기사 좌표 → 칸 번호)
            const int pad = 48;
            int w = cellW * S + pad * 2, h = cellH * S + pad * 2, x0 = -rootX * S - pad, y0 = -rootY * S - pad;
            if (w != gw || h != gh || x0 != gx0 || y0 != gy0 || grid.Length != w * h)
            {
                gw = w; gh = h; gx0 = x0; gy0 = y0;
                grid = new int[w * h]; gridStamp = new int[w * h];
                botHead = new int[w * h]; botStamp = new int[w * h];
                state[0] = state[2] = 0;
                hilbertN = 1;
                while (hilbertN < Math.Max(w, h)) hilbertN *= 2;
            }
            if (orderS != S) { order = DitherOrder(S); orderS = S; }
            int f = Math.Max(0, Math.Min(clip.frames - 1, frame));
            int nBB = clip.Body(f) + clip.Blade(f), nf = clip.Fx(f);
            int maxT = (nBB + nf) * S2 + 1;
            Grow(ref tX, maxT); Grow(ref tY, maxT); Grow(ref tKind, maxT); Grow(ref tCol, maxT); Grow(ref tClaim, maxT);
            Grow(ref fIdx, maxT); Grow(ref fKey, maxT); Grow(ref sortTmp, Math.Max(maxT, count) * 2);
            Grow(ref botNext, count); Grow(ref lx, count); Grow(ref ly, count); Grow(ref tgt, count);
            Grow(ref uIdx, count); Grow(ref uKey, count);
            Ensure(slots.Length);

            var hs = handles;
            int hn = 0;
            try
            {
                void* P(Array a) { hs[hn] = GCHandle.Alloc(a, GCHandleType.Pinned); return (void*)hs[hn++].AddrOfPinnedObject(); }
                var job = new NanoKnightMatchJob
                {
                    px = (int*)P(clip.px), pcol = (int*)P(clip.color), s0 = clip.start[f], nBB = nBB, nf = nf,
                    S = S, count = count, slots = (int*)P(slots), hintX = (int*)P(hintX), hintY = (int*)P(hintY),
                    locX = (int*)P(locX), locY = (int*)P(locY), kind = (byte*)P(kind), color = (uint*)P(color), has = (byte*)P(has),
                    tX = (int*)P(tX), tY = (int*)P(tY), tKind = (int*)P(tKind), tCol = (uint*)P(tCol), tClaim = (int*)P(tClaim),
                    grid = (int*)P(grid), gridStamp = (int*)P(gridStamp), botHead = (int*)P(botHead), botStamp = (int*)P(botStamp),
                    botNext = (int*)P(botNext), lx = (int*)P(lx), ly = (int*)P(ly), tgt = (int*)P(tgt),
                    uIdx = (int*)P(uIdx), fIdx = (int*)P(fIdx), uKey = (int*)P(uKey), fKey = (int*)P(fKey), sortTmp = (int*)P(sortTmp),
                    state = (int*)P(state), order = (int*)P(order),
                    gw = gw, gh = gh, gx0 = gx0, gy0 = gy0, hilbertN = hilbertN,
                };
                if (UseBurst) job.Run();
                else job.Execute();
            }
            finally
            {
                for (int i = 0; i < hn; i++) hs[i].Free();
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct NanoKnightMatchJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public int* px, pcol, slots, hintX, hintY, locX, locY;
        [NativeDisableUnsafePtrRestriction] public byte* kind, has;
        [NativeDisableUnsafePtrRestriction] public uint* color, tCol;
        [NativeDisableUnsafePtrRestriction] public int* tX, tY, tKind, tClaim, grid, gridStamp, botHead, botStamp, botNext;
        [NativeDisableUnsafePtrRestriction] public int* lx, ly, tgt, uIdx, fIdx, uKey, fKey, sortTmp, state, order;
        public int s0, nBB, nf, S, count, gw, gh, gx0, gy0, hilbertN;
        int gStamp, claimStamp, bStamp, tCount;

        static float Hash01(int x, int y)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u;
            h ^= h >> 13; h *= 0x5BD1E995u; h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }

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

        /// <summary>key 순서로 idx 를 정렬 (기수 정렬 11비트 × 2 — 키 22비트 = 격자 2048 까지)</summary>
        void Sort(int* key, int* idx, int n)
        {
            if (n < 2) return;
            int* k2 = sortTmp, i2 = sortTmp + n;
            int* cnt = stackalloc int[2048];
            for (int pass = 0; pass < 2; pass++)
            {
                int sh = pass * 11;
                for (int b = 0; b < 2048; b++) cnt[b] = 0;
                for (int i = 0; i < n; i++) cnt[(key[i] >> sh) & 2047]++;
                int sum = 0;
                for (int b = 0; b < 2048; b++) { int c = cnt[b]; cnt[b] = sum; sum += c; }
                for (int i = 0; i < n; i++) { int p = cnt[(key[i] >> sh) & 2047]++; k2[p] = key[i]; i2[p] = idx[i]; }
                for (int i = 0; i < n; i++) { key[i] = k2[i]; idx[i] = i2[i]; }
            }
        }

        int G(int x, int y)
        {
            int gx = x - gx0, gy = y - gy0;
            if (gx < 0 || gy < 0 || gx >= gw || gy >= gh) return -1;
            int k = gy * gw + gx;
            return gridStamp[k] == gStamp ? grid[k] : -1;
        }

        public void Execute()
        {
            gStamp = state[0] + 1;
            if (gStamp <= 0) { for (int k = 0; k < gw * gh; k++) gridStamp[k] = 0; gStamp = 1; }
            claimStamp = state[1] + 1;
            if (claimStamp <= 0) claimStamp = 1;
            bStamp = state[2];
            int S2 = S * S;

            // 1) 칸 만들기 — 몸 · 칼날 (봇이 모자라면 고르게 솎음) → 효과 (남는 만큼, 최대 1/3)
            int fxCap = S2 / 3 > 1 ? S2 / 3 : 1;
            int bodyLv, fxLv; float bodyFrac, fxFrac;
            if (count >= nBB * S2)
            {
                bodyLv = S2; bodyFrac = 0f;
                int rem = count - nBB * S2;
                if (nf <= 0) { fxLv = 0; fxFrac = 0f; }
                else if (rem >= nf * fxCap) { fxLv = fxCap; fxFrac = 0f; }
                else { fxLv = rem / nf; fxFrac = (rem - fxLv * nf) / (float)nf; }
            }
            else
            {
                bodyLv = nBB > 0 ? count / nBB : 0;
                bodyFrac = nBB > 0 ? (count - bodyLv * nBB) / (float)nBB : 0f;
                fxLv = 0; fxFrac = 0f;
            }
            tCount = 0;
            for (int p = 0; p < nBB + nf; p++)
            {
                bool fx = p >= nBB;
                int lv = fx ? fxLv : bodyLv; float fr = fx ? fxFrac : bodyFrac;
                if (lv <= 0 && fr <= 0f) continue;
                int v = px[s0 + p];
                int X = ((v & 0xFFF) - 2048) * S, Y = (((v >> 12) & 0xFFF) - 2048) * S, K = (v >> 24) & 0xFF;
                uint col = (uint)pcol[s0 + p];
                for (int k = 0; k < S2; k++)
                {
                    int sub = order[k];                    // 단계 k = 베이어 점묘 순서
                    int x = X + sub % S, y = Y + sub / S;
                    if (k > lv || (k == lv && Hash01(x, y) >= fr)) continue;
                    int gx = x - gx0, gy = y - gy0;
                    if (gx < 0 || gy < 0 || gx >= gw || gy >= gh) continue;
                    int gk = gy * gw + gx;
                    if (gridStamp[gk] == gStamp) continue;
                    gridStamp[gk] = gStamp; grid[gk] = tCount;
                    tX[tCount] = x; tY[tCount] = y; tKind[tCount] = K; tCol[tCount] = col; tClaim[tCount] = 0;
                    tCount++;
                }
            }
            int kept = 0, near = 0, curve = 0, stacked = 0;
            if (count > 0 && tCount == 0)
                for (int j = 0; j < count; j++) has[slots[j]] = 0;
            if (count > 0 && tCount > 0)
            {
                // 2) 그대로
                int nu = 0;
                for (int j = 0; j < count; j++)
                {
                    int c = slots[j];
                    tgt[j] = -1;
                    int x, y;
                    if (has[c] != 0) { x = locX[c]; y = locY[c]; }
                    else { x = hintX[j]; y = hintY[j]; }
                    x = x < gx0 ? gx0 : x >= gx0 + gw ? gx0 + gw - 1 : x;
                    y = y < gy0 ? gy0 : y >= gy0 + gh ? gy0 + gh - 1 : y;
                    lx[j] = x; ly[j] = y;
                    if (has[c] != 0)
                    {
                        int t = G(x, y);
                        if (t >= 0 && tClaim[t] != claimStamp) { tClaim[t] = claimStamp; tgt[j] = t; kept++; continue; }
                    }
                    uIdx[nu++] = j;
                }

                // 3) 가까이 — 적은 쪽이 찾는다
                int R = S + 2;
                int freeLeft = tCount - kept;
                if (nu > 0 && freeLeft > 0)
                {
                    if (nu <= freeLeft)
                    {
                        int keep = 0;
                        for (int u = 0; u < nu; u++)
                        {
                            int j = uIdx[u];
                            int t = freeLeft > 0 ? NearestT(lx[j], ly[j], R, true) : -1;
                            if (t >= 0) { tClaim[t] = claimStamp; tgt[j] = t; near++; freeLeft--; }
                            else uIdx[keep++] = j;
                        }
                        nu = keep;
                    }
                    else
                    {
                        bStamp++;
                        if (bStamp <= 0) { for (int k = 0; k < gw * gh; k++) botStamp[k] = 0; bStamp = 1; }
                        for (int u = 0; u < nu; u++)
                        {
                            int j = uIdx[u];
                            int k = (ly[j] - gy0) * gw + (lx[j] - gx0);
                            botNext[j] = botStamp[k] == bStamp ? botHead[k] : -1;
                            botHead[k] = j; botStamp[k] = bStamp;
                        }
                        for (int t = 0; t < tCount && freeLeft > 0; t++)
                        {
                            if (tClaim[t] == claimStamp) continue;
                            int j = NearestBot(tX[t], tY[t], R);
                            if (j < 0) continue;
                            tClaim[t] = claimStamp; tgt[j] = t; near++; freeLeft--;
                        }
                        int keep2 = 0;
                        for (int u = 0; u < nu; u++) if (tgt[uIdx[u]] < 0) uIdx[keep2++] = uIdx[u];
                        nu = keep2;
                    }
                }

                // 4) 곡선 순서 — 남은 봇과 빈 칸을 힐베르트 순서로 비율대로 짝
                if (nu > 0 && freeLeft > 0)
                {
                    int nfree = 0;
                    for (int t = 0; t < tCount; t++)
                        if (tClaim[t] != claimStamp) { fIdx[nfree] = t; fKey[nfree] = Hilbert(hilbertN, tX[t] - gx0, tY[t] - gy0); nfree++; }
                    for (int u = 0; u < nu; u++) uKey[u] = Hilbert(hilbertN, lx[uIdx[u]] - gx0, ly[uIdx[u]] - gy0);
                    Sort(uKey, uIdx, nu);
                    Sort(fKey, fIdx, nfree);
                    if (nu <= nfree)
                    {
                        for (int u = 0; u < nu; u++)
                        {
                            int t = fIdx[(int)((u + 0.5f) * nfree / nu)];
                            tClaim[t] = claimStamp; tgt[uIdx[u]] = t; curve++;
                        }
                        nu = 0;
                    }
                    else
                    {
                        int keep = 0, next = 0;
                        for (int u = 0; u < nu; u++)
                        {
                            int want = next < nfree ? (int)((next + 0.5f) * nu / nfree) : int.MaxValue;
                            if (u == want) { int t = fIdx[next++]; tClaim[t] = claimStamp; tgt[uIdx[u]] = t; curve++; }
                            else uIdx[keep++] = uIdx[u];
                        }
                        nu = keep;
                    }
                }

                // 5) 남는 봇 — 가까운 칸에 겹쳐 앉는다
                for (int u = 0; u < nu; u++)
                {
                    int j = uIdx[u];
                    int t = NearestT(lx[j], ly[j], R, false);
                    if (t < 0) t = (int)((u + 0.5f) * tCount / nu);   // 멀리 떨어진 봇 — 칸 목록에 고르게
                    if (t >= tCount) t = tCount - 1;
                    tgt[j] = t; stacked++;
                }

                // 6) 적기
                for (int j = 0; j < count; j++)
                {
                    int c = slots[j], t = tgt[j];
                    if (t < 0) { has[c] = 0; continue; }
                    has[c] = 1;
                    locX[c] = tX[t]; locY[c] = tY[t];
                    kind[c] = (byte)tKind[t];
                    color[c] = tCol[t];
                }
            }
            state[0] = gStamp; state[1] = claimStamp; state[2] = bStamp;
            state[3] = tCount; state[4] = kept; state[5] = near; state[6] = curve; state[7] = stacked;
        }

        /// <summary>(x, y) 에서 반경 R 안의 가장 가까운 칸 (네모 고리 순서). free = 아무도 안 맡은 칸만</summary>
        int NearestT(int x, int y, int R, bool free)
        {
            int t0 = G(x, y);
            if (t0 >= 0 && (!free || tClaim[t0] != claimStamp)) return t0;
            for (int d = 1; d <= R; d++)
            {
                int best = -1, bestD = int.MaxValue;
                for (int i = -d; i <= d; i++)
                {
                    TryT(x + i, y - d, x, y, free, ref best, ref bestD);
                    TryT(x + i, y + d, x, y, free, ref best, ref bestD);
                    if (i == -d || i == d) continue;
                    TryT(x - d, y + i, x, y, free, ref best, ref bestD);
                    TryT(x + d, y + i, x, y, free, ref best, ref bestD);
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        void TryT(int x, int y, int ox, int oy, bool free, ref int best, ref int bestD)
        {
            int t = G(x, y);
            if (t < 0 || (free && tClaim[t] == claimStamp)) return;
            int d = (x - ox) * (x - ox) + (y - oy) * (y - oy);
            if (d < bestD) { bestD = d; best = t; }
        }

        /// <summary>칸 (x, y) 에서 반경 R 안의 가장 가까운, 아직 칸이 없는 봇</summary>
        int NearestBot(int x, int y, int R)
        {
            for (int d = 0; d <= R; d++)
            {
                int best = -1, bestD = int.MaxValue;
                if (d == 0) TryBot(x, y, x, y, ref best, ref bestD);
                else
                    for (int i = -d; i <= d; i++)
                    {
                        TryBot(x + i, y - d, x, y, ref best, ref bestD);
                        TryBot(x + i, y + d, x, y, ref best, ref bestD);
                        if (i == -d || i == d) continue;
                        TryBot(x - d, y + i, x, y, ref best, ref bestD);
                        TryBot(x + d, y + i, x, y, ref best, ref bestD);
                    }
                if (best >= 0) return best;
            }
            return -1;
        }

        void TryBot(int x, int y, int ox, int oy, ref int best, ref int bestD)
        {
            int gx = x - gx0, gy = y - gy0;
            if (gx < 0 || gy < 0 || gx >= gw || gy >= gh) return;
            int k = gy * gw + gx;
            if (botStamp[k] != bStamp) return;
            for (int j = botHead[k]; j >= 0; j = botNext[j])
            {
                if (tgt[j] >= 0) continue;
                int d = (x - ox) * (x - ox) + (y - oy) * (y - oy);
                if (d < bestD) { bestD = d; best = j; }
                return;
            }
        }
    }
}
