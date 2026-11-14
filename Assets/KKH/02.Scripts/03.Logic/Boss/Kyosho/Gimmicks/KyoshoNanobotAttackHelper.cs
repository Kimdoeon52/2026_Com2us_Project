using System;
using System.Collections.Generic;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// 추적 가능한 나노봇 투사체/충격파 엔트리 (시각 봇 집합 + 정확한 원형/선분 히트 판정 결합).
    /// </summary>
    public class KyoshoTrackedBullet
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float HitRadius;
        public float RemainingLife;
        public float WaitTimer;
        public int Damage;
        public BodyPart TargetPart;
        public int PartDurabilityDamage;
        public bool DropToSandOnHit;
        public bool HitResolved;
        public readonly List<int> BotIndices = new List<int>();
    }

    /// <summary>
    /// [쿄쇼 공용 나노봇 공격/발사/판정/스프라이트 폴백 유틸리티]
    /// </summary>
    public static class KyoshoNanobotAttackHelper
    {
        private static readonly List<int> _collectTmp = new List<int>(128);
        private static Sprite _whiteCircleSprite;
        private static Sprite _whiteSquareSprite;
        private static RSNanoSpriteShape _orchestraEnsembleShape;
        private static readonly RSNanoSpriteShape[] _sectionEnsembleShapes = new RSNanoSpriteShape[3];

        /// <summary>
        /// 군집(RSNanoSwarm)에서 count개의 유휴 봇을 꺼내 hold에서 wait 후 vel로 발사하며,
        /// 판정용 BotIndices를 추적 리스트에 기록함.
        /// </summary>
        public static KyoshoTrackedBullet FireTrackedClump(
            RSNanoSwarm swarm,
            Vector3 origin,
            Vector3 holdPos,
            float waitSec,
            Vector3 velocity,
            float lifeSec,
            int clumpSize,
            float hitRadius,
            Color tintColor,
            float glow,
            int damage,
            BodyPart targetPart,
            int partDurabilityDmg,
            bool snapToHold = false,
            int sourceGuide = RSNanoSwarm.G_Blob)
        {
            var bullet = new KyoshoTrackedBullet
            {
                Position = snapToHold || waitSec <= 0f ? holdPos : origin,
                Velocity = velocity,
                HitRadius = hitRadius,
                RemainingLife = lifeSec,
                WaitTimer = Mathf.Max(0f, waitSec),
                Damage = damage,
                TargetPart = targetPart,
                PartDurabilityDamage = partDurabilityDmg,
                DropToSandOnHit = true,
                HitResolved = false
            };

            if (swarm == null || swarm.Count == 0) return bullet;

            var bots = swarm.Bots;
            int fallbackGuide = (swarm.dark > 0.5f && swarm.guideCount[RSNanoSwarm.G_Halo] > 0)
                ? RSNanoSwarm.G_Halo
                : RSNanoSwarm.G_Blob;
            int actualSource = sourceGuide;
            if (actualSource == RSNanoSwarm.G_Blob && fallbackGuide == RSNanoSwarm.G_Halo)
            {
                actualSource = RSNanoSwarm.G_Halo;
            }

            int need = Mathf.Max(1, clumpSize);
            swarm.Collect(actualSource, origin, need, _collectTmp);
            if (_collectTmp.Count < need && actualSource != fallbackGuide)
            {
                int rem = need - _collectTmp.Count;
                var extra = swarm.Collect(fallbackGuide, origin, rem, new List<int>());
                _collectTmp.AddRange(extra);
            }
            if (_collectTmp.Count < need && actualSource != RSNanoSwarm.G_Blob && fallbackGuide != RSNanoSwarm.G_Blob)
            {
                int rem = need - _collectTmp.Count;
                var extra = swarm.Collect(RSNanoSwarm.G_Blob, origin, rem, new List<int>());
                _collectTmp.AddRange(extra);
            }

            if (_collectTmp.Count == 0) return bullet;

            float cs = swarm.Cell;
            Color32 c32 = tintColor;
            int count = _collectTmp.Count;
            for (int k = 0; k < count; k++)
            {
                int idx = _collectTmp[k];
                float angle = k * 2.39996f;
                float r = Mathf.Sqrt((k + 0.5f) / Mathf.Max(1, count)) * Mathf.Min(3.5f, Mathf.Sqrt(count) * 0.6f);
                Vector3 offset = new Vector3(Mathf.Cos(angle) * r * cs, Mathf.Sin(angle) * r * cs, 0f);

                swarm.Fire(idx, holdPos + offset, waitSec, velocity, lifeSec, fallbackGuide, default, 0f, snapToHold || waitSec <= 0f, clump: 1);
                bots[idx].tint = c32;
                bots[idx].glow = glow;
                bullet.BotIndices.Add(idx);
            }

            return bullet;
        }

        /// <summary>
        /// 추적 중인 나노봇 탄환들을 갱신하고 플레이어/바닥 충돌 시 모래로 전환(Drop)하며 피격 콜백 호출.
        /// </summary>
        public static void StepTrackedBullets(
            List<KyoshoTrackedBullet> bullets,
            RSNanoSwarm swarm,
            Transform playerTransform,
            float dt,
            Action<KyoshoTrackedBullet> onPlayerHit)
        {
            if (bullets == null || bullets.Count == 0) return;

            float floorY = swarm != null ? swarm.floorY : 0f;
            Vector3 playerCenter = playerTransform != null ? playerTransform.position + Vector3.up * 0.65f : Vector3.zero;

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                if (b.HitResolved)
                {
                    RestoreBulletBotColors(swarm, b);
                    bullets.RemoveAt(i);
                    continue;
                }

                if (b.WaitTimer > 0f)
                {
                    b.WaitTimer -= dt;
                    if (b.WaitTimer <= 0f && swarm != null && b.BotIndices.Count > 0)
                    {
                        b.Position = swarm.Bots[b.BotIndices[0]].pos;
                    }
                    continue;
                }

                b.Position += b.Velocity * dt;
                b.RemainingLife -= dt;

                // 플레이어 중심 판정 (좌우 0.35m, 상하 0.60m 캡슐 근사)
                if (playerTransform != null)
                {
                    float dx = Mathf.Abs(b.Position.x - playerCenter.x);
                    float dy = Mathf.Abs(b.Position.y - playerCenter.y);
                    float rx = b.HitRadius + 0.22f;
                    float ry = b.HitRadius + 0.36f;
                    if (dx * dx / (rx * rx) + dy * dy / (ry * ry) <= 1.0f)
                    {
                        b.HitResolved = true;
                        DropBulletBotsToSand(swarm, b, b.Velocity * 0.25f);
                        onPlayerHit?.Invoke(b);
                        RestoreBulletBotColors(swarm, b);
                        bullets.RemoveAt(i);
                        continue;
                    }
                }

                // 바닥 착탄 시 모래 변환
                if (b.Position.y <= floorY + 0.08f)
                {
                    b.HitResolved = true;
                    DropBulletBotsToSand(swarm, b, new Vector3(b.Velocity.x * 0.15f, -2f, 0f));
                    RestoreBulletBotColors(swarm, b);
                    bullets.RemoveAt(i);
                    continue;
                }

                if (b.RemainingLife <= 0f)
                {
                    RestoreBulletBotColors(swarm, b);
                    bullets.RemoveAt(i);
                }
            }
        }

        public static void DropBulletBotsToSand(RSNanoSwarm swarm, KyoshoTrackedBullet b, Vector3 vel)
        {
            if (swarm == null || b == null) return;
            var bots = swarm.Bots;
            for (int k = 0; k < b.BotIndices.Count; k++)
            {
                int idx = b.BotIndices[k];
                if (idx >= 0 && idx < bots.Length && bots[idx].mode == BotMode.Free)
                {
                    swarm.Drop(idx, vel + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), UnityEngine.Random.Range(0.5f, 2.2f), 0f));
                }
            }
        }

        public static void RestoreBulletBotColors(RSNanoSwarm swarm, KyoshoTrackedBullet b)
        {
            if (swarm == null || b == null) return;
            var bots = swarm.Bots;
            for (int k = 0; k < b.BotIndices.Count; k++)
            {
                int idx = b.BotIndices[k];
                if (idx >= 0 && idx < bots.Length)
                {
                    bool sparkle = bots[idx].hash3 < swarm.sparkleRatio;
                    bots[idx].glow = sparkle ? 1.2f + bots[idx].hash2 : 0f;
                    bots[idx].tint = sparkle ? swarm.sparkleTint : RSNanoBotColors.Pick(swarm.botColors, idx, swarm.grainNoise);
                }
            }
        }

        public static void ClearTrackedBullets(List<KyoshoTrackedBullet> bullets, RSNanoSwarm swarm)
        {
            if (bullets == null) return;
            foreach (var b in bullets)
            {
                RestoreBulletBotColors(swarm, b);
                if (swarm != null)
                {
                    var bots = swarm.Bots;
                    foreach (int idx in b.BotIndices)
                    {
                        int returnGuide = (swarm.dark > 0.5f && swarm.guideCount[RSNanoSwarm.G_Halo] > 0)
                            ? RSNanoSwarm.G_Halo
                            : RSNanoSwarm.G_Blob;
                        if (idx >= 0 && idx < bots.Length && bots[idx].mode == BotMode.Free)
                        {
                            swarm.Bind(idx, returnGuide);
                        }
                    }
                }
            }
            bullets.Clear();
        }

        /// <summary>
        /// 선분 AB와 플레이어 히트 박스(중심, 반폭/반높이) 간의 최단거리 교차 판정.
        /// </summary>
        public static bool CheckSegmentHitsPlayer(Vector3 a, Vector3 b, Transform player, float thickness)
        {
            if (player == null) return false;
            Vector2 p = (Vector2)player.position + new Vector2(0f, 0.65f);
            Vector2 segA = a;
            Vector2 segB = b;
            Vector2 ab = segB - segA;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - segA, ab) / lenSq) : 0f;
            Vector2 closest = segA + ab * t;

            float rx = thickness + 0.24f;
            float ry = thickness + 0.38f;
            float dx = closest.x - p.x;
            float dy = closest.y - p.y;
            return (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0f;
        }

        public static Sprite GetCircleSprite()
        {
            if (_whiteCircleSprite != null) return _whiteCircleSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01((1f - d) * 4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _whiteCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _whiteCircleSprite;
        }

        public static Sprite GetSquareSprite()
        {
            if (_whiteSquareSprite != null) return _whiteSquareSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var cols = new Color[16];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            _whiteSquareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            return _whiteSquareSprite;
        }

        /// <summary>
        /// 에셋이 아직 베이크되지 않은 런타임 테스트 환경에서도 작동하도록 절차적 RSNanoSpriteShape를 즉석 생성함.
        /// </summary>
        public static RSNanoSpriteShape CreateRuntimeFallbackShape(string name, int width, int height, Func<int, int, Color32?> pixelSampler)
        {
            var shape = ScriptableObject.CreateInstance<RSNanoSpriteShape>();
            shape.name = name;
            var coords = new List<Vector2Int>();
            var cols = new List<int>();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            int halfW = width / 2, halfH = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32? c = pixelSampler(x, y);
                    if (!c.HasValue || c.Value.a < 128) continue;
                    int px = x - halfW;
                    int py = y - halfH;
                    coords.Add(new Vector2Int(px, py));
                    cols.Add(c.Value.r | (c.Value.g << 8) | (c.Value.b << 16));
                    if (px < minX) minX = px;
                    if (py < minY) minY = py;
                    if (px > maxX) maxX = px;
                    if (py > maxY) maxY = py;
                }
            }

            int count = coords.Count;
            if (count == 0)
            {
                coords.Add(Vector2Int.zero);
                cols.Add(0xFFFFFF);
                minX = maxX = minY = maxY = 0;
                count = 1;
            }

            shape.frameCount = 1;
            shape.botCount = count;
            shape.minX = minX;
            shape.minY = minY;
            shape.maxX = maxX;
            shape.maxY = maxY;
            shape.filled = new[] { count };
            shape.pixels = new int[count];
            shape.colors = new int[count];
            for (int k = 0; k < count; k++)
            {
                shape.pixels[k] = RSNanoSpriteShape.Pack(coords[k].x, coords[k].y, 0);
                shape.colors[k] = cols[k];
            }
            shape.bakedKey = $"runtime_{name}_{count}";
            return shape;
        }

        // ========================================================================
        // 오케스트라 악단 & 섹션 다프레임 실루엣 (런타임 폴백 + 에디터 PNG/Shape 공용)
        // ========================================================================

        public const int OrchestraFrameWidth = 192;
        public const int OrchestraFrameHeight = 96;
        public const int OrchestraFrameCount = 4;

        public const int SectionFrameWidth = 64;
        public const int SectionFrameHeight = 56;
        public const int SectionFrameCount = 2;

        public static RSNanoSpriteShape GetOrCreateOrchestraEnsembleShape()
        {
            if (_orchestraEnsembleShape != null && _orchestraEnsembleShape.Ready)
            {
                return _orchestraEnsembleShape;
            }
            _orchestraEnsembleShape = CreateMultiFrameRuntimeShape(
                "Runtime_Kyosho_Orchestra_Ensemble",
                OrchestraFrameWidth,
                OrchestraFrameHeight,
                OrchestraFrameCount,
                SampleOrchestraEnsemblePixel);
            return _orchestraEnsembleShape;
        }

        public static RSNanoSpriteShape GetOrCreateSectionEnsembleShape(KyoshoSectionType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < _sectionEnsembleShapes.Length
                && _sectionEnsembleShapes[idx] != null && _sectionEnsembleShapes[idx].Ready)
            {
                return _sectionEnsembleShapes[idx];
            }

            var shape = CreateMultiFrameRuntimeShape(
                $"Runtime_Section_{type}",
                SectionFrameWidth,
                SectionFrameHeight,
                SectionFrameCount,
                (f, x, y, w, h) => SampleSectionEnsemblePixel(type, f, x, y, w, h));

            if (idx >= 0 && idx < _sectionEnsembleShapes.Length)
            {
                _sectionEnsembleShapes[idx] = shape;
            }
            return shape;
        }

        public static RSNanoSpriteShape CreateMultiFrameRuntimeShape(
            string name,
            int frameW,
            int frameH,
            int frames,
            Func<int, int, int, int, int, Color32> sampler)
        {
            var shape = ScriptableObject.CreateInstance<RSNanoSpriteShape>();
            shape.name = name;
            shape.columns = Mathf.Max(1, frames);
            shape.rows = 1;
            shape.pick = NanoSpritePick.Opaque;
            shape.alphaCut = 0.35f;
            shape.pivot = NanoSpritePivot.Center;

            int ox = frameW / 2;
            int oy = frameH / 2;
            var frameCoords = new List<Vector2Int>[frames];
            var frameCols = new Dictionary<Vector2Int, int>[frames];

            int maxFilled = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int f = 0; f < frames; f++)
            {
                var coords = new List<Vector2Int>(2048);
                var cols = new Dictionary<Vector2Int, int>(2048);
                for (int y = 0; y < frameH; y++)
                {
                    for (int x = 0; x < frameW; x++)
                    {
                        Color32 c = sampler(f, x, y, frameW, frameH);
                        if (c.a < 96) continue;
                        var pt = new Vector2Int(x - ox, y - oy);
                        coords.Add(pt);
                        cols[pt] = c.r | (c.g << 8) | (c.b << 16);
                        if (pt.x < minX) minX = pt.x;
                        if (pt.y < minY) minY = pt.y;
                        if (pt.x > maxX) maxX = pt.x;
                        if (pt.y > maxY) maxY = pt.y;
                    }
                }
                if (coords.Count > maxFilled) maxFilled = coords.Count;
                frameCoords[f] = coords;
                frameCols[f] = cols;
            }

            if (maxFilled == 0)
            {
                shape.frameCount = 1;
                shape.botCount = 1;
                shape.filled = new[] { 1 };
                shape.pixels = new[] { RSNanoSpriteShape.Pack(0, 0, 0) };
                shape.colors = new[] { 0xFFFFFF };
                shape.bakedKey = $"runtime_{name}_empty";
                return shape;
            }

            int gw = maxX - minX + 1;
            int gh = maxY - minY + 1;
            int hn = 1;
            while (hn < Mathf.Max(gw, gh)) hn <<= 1;

            shape.frameCount = frames;
            shape.botCount = maxFilled;
            shape.minX = minX;
            shape.minY = minY;
            shape.maxX = maxX;
            shape.maxY = maxY;
            shape.filled = new int[frames];
            shape.pixels = new int[frames * maxFilled];
            shape.colors = new int[frames * maxFilled];

            var grid = new int[gw * gh];
            var prev = new Vector2Int[maxFilled];
            var cap = new List<int>(maxFilled);
            var assign = new int[maxFilled];
            var order = new List<int>(maxFilled);

            for (int f = 0; f < frames; f++)
            {
                var P = frameCoords[f];
                shape.filled[f] = P.Count;
                if (P.Count == 0)
                {
                    for (int k = 0; k < maxFilled; k++)
                        shape.pixels[f * maxFilled + k] = RSNanoSpriteShape.Pack(0, 0, Mathf.Min(k, 127));
                    continue;
                }

                P.Sort((a, b) => HilbertIndex(hn, a.x - minX, a.y - minY).CompareTo(HilbertIndex(hn, b.x - minX, b.y - minY)));
                cap.Clear();
                int baseCap = maxFilled / P.Count;
                int rem = maxFilled % P.Count;
                for (int i = 0; i < P.Count; i++) cap.Add(baseCap);
                for (int j = 0; j < rem; j++) cap[(int)((long)j * P.Count / rem)]++;

                for (int i = 0; i < grid.Length; i++) grid[i] = -1;
                for (int i = 0; i < P.Count; i++) grid[(P[i].y - minY) * gw + (P[i].x - minX)] = i;
                for (int k = 0; k < maxFilled; k++) assign[k] = -1;

                if (f == 0)
                {
                    int k = 0;
                    for (int i = 0; i < P.Count && k < maxFilled; i++)
                        for (int c = 0; c < cap[i] && k < maxFilled; c++) assign[k++] = i;
                }
                else
                {
                    for (int k = 0; k < maxFilled; k++)
                    {
                        int gx = prev[k].x - minX, gy = prev[k].y - minY;
                        if (gx < 0 || gy < 0 || gx >= gw || gy >= gh) continue;
                        int i = grid[gy * gw + gx];
                        if (i >= 0 && cap[i] > 0) { assign[k] = i; cap[i]--; }
                    }
                    order.Clear();
                    for (int k = 0; k < maxFilled; k++) if (assign[k] < 0) order.Add(k);
                    order.Sort((a, b) => HilbertIndex(hn, prev[a].x - minX, prev[a].y - minY).CompareTo(HilbertIndex(hn, prev[b].x - minX, prev[b].y - minY)));
                    int scan = 0;
                    foreach (int k in order)
                    {
                        int i = FindNearestSlot(grid, gw, gh, cap, prev[k].x - minX, prev[k].y - minY);
                        if (i < 0)
                        {
                            while (scan < cap.Count && cap[scan] <= 0) scan++;
                            if (scan >= cap.Count) break;
                            i = scan;
                        }
                        assign[k] = i;
                        cap[i]--;
                    }
                }

                var rank = new int[P.Count];
                for (int k = 0; k < maxFilled; k++)
                {
                    int i = Mathf.Max(0, assign[k]);
                    var p = P[i];
                    shape.pixels[f * maxFilled + k] = RSNanoSpriteShape.Pack(p.x, p.y, rank[i]++);
                    shape.colors[f * maxFilled + k] = frameCols[f].TryGetValue(p, out int pc) ? pc : 0;
                    prev[k] = p;
                }
            }

            shape.bakedKey = $"runtime_{name}_{frames}_{maxFilled}";
            return shape;
        }

        private static int FindNearestSlot(int[] grid, int gw, int gh, List<int> cap, int cx, int cy)
        {
            int maxR = Mathf.Max(gw, gh);
            for (int r = 1; r <= maxR; r++)
            {
                int y0 = Mathf.Max(0, cy - r), y1 = Mathf.Min(gh - 1, cy + r);
                int x0 = Mathf.Max(0, cx - r), x1 = Mathf.Min(gw - 1, cx + r);
                for (int x = x0; x <= x1; x++)
                {
                    if (cy - r >= 0) { int i = grid[(cy - r) * gw + x]; if (i >= 0 && cap[i] > 0) return i; }
                    if (cy + r < gh) { int i = grid[(cy + r) * gw + x]; if (i >= 0 && cap[i] > 0) return i; }
                }
                for (int y = y0 + 1; y < y1; y++)
                {
                    if (cx - r >= 0) { int i = grid[y * gw + (cx - r)]; if (i >= 0 && cap[i] > 0) return i; }
                    if (cx + r < gw) { int i = grid[y * gw + (cx + r)]; if (i >= 0 && cap[i] > 0) return i; }
                }
            }
            return -1;
        }

        private static uint HilbertIndex(int n, int x, int y)
        {
            uint d = 0;
            for (int s = n >> 1; s > 0; s >>= 1)
            {
                int rx = (x & s) > 0 ? 1 : 0;
                int ry = (y & s) > 0 ? 1 : 0;
                d += (uint)(s * s * ((3 * rx) ^ ry));
                if (ry == 0)
                {
                    if (rx == 1) { x = n - 1 - x; y = n - 1 - y; }
                    int t = x; x = y; y = t;
                }
            }
            return d;
        }

        private static bool InBox(int x, int y, int x0, int y0, int x1, int y1)
            => x >= x0 && x <= x1 && y >= y0 && y <= y1;

        private static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx;
            float dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool InRing(float x, float y, float cx, float cy, float rOuter, float rInner)
        {
            float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
            return d2 <= rOuter * rOuter && d2 >= rInner * rInner;
        }

        private static bool InCapsule(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float abx = bx - ax, aby = by - ay;
            float lenSq = abx * abx + aby * aby;
            float t = lenSq > 1e-5f ? Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / lenSq) : 0f;
            float dx = px - (ax + abx * t);
            float dy = py - (ay + aby * t);
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>
        /// 쿄쇼 본체용 4프레임 오케스트라 악단 실루엣(192x96) 픽셀 샘플러.
        /// 중앙 지휘자 포디움 + 좌측 현악(하프/바이올린/첼로) + 우측 타악(팀파니/베이스드럼/공) + 상단 파이프오르간·금관을 네거티브 스페이스로 분리.
        /// </summary>
        public static Color32 SampleOrchestraEnsemblePixel(int frame, int x, int y, int w, int h)
        {
            // 팔레트 정의 (실루엣 명암 + 악기 파트별 은은한 림 색감)
            Color32 colDarkCoat = new Color32(52, 58, 78, 255);
            Color32 colPlayerDark = new Color32(76, 86, 110, 255);
            Color32 colIvory = new Color32(236, 240, 252, 255);
            Color32 colGold = new Color32(246, 198, 96, 255);
            Color32 colBatonGlow = new Color32(175, 246, 255, 255);
            Color32 colPodium = new Color32(142, 62, 76, 255);
            Color32 colOrganPipe = new Color32(182, 198, 224, 255);
            Color32 colStrings = new Color32(105, 226, 205, 255);
            Color32 colWoodStrings = new Color32(150, 232, 212, 255);
            Color32 colPerc = new Color32(236, 152, 74, 255);
            Color32 colDrumHead = new Color32(245, 226, 188, 255);
            Color32 colBrass = new Color32(240, 148, 218, 255);

            int beat = ((frame % 4) + 4) % 4;
            bool oddBeat = (beat & 1) == 1;
            float sway = beat == 1 ? -1.4f : (beat == 2 ? 1.4f : 0f);
            float nod = (beat == 0 || beat == 3) ? 1.0f : 0f;

            // 1. 중앙: 마에스트로 지휘자 + 계단식 포디움 + 보면대 (x = 74..128, y = 0..68)
            if (InBox(x, y, 74, 0, 118, 4)) return y == 4 ? colGold : colPodium;
            if (InBox(x, y, 79, 5, 113, 8)) return y == 8 ? colGold : colPodium;

            // 보면대 (좌측 전면)
            if (InCapsule(x, y, 82f, 9f, 82f, 25f, 1.0f)) return colGold;
            if (InCapsule(x, y, 76f, 24f, 87f, 28f, 1.7f)) return y > 26 ? colIvory : colPodium;

            // 지휘자 지휘봉 & 오른팔 (4박자 지휘 궤적: 0=↓, 1=←, 2=→, 3=↑)
            float shX = 104.5f, shY = 36f;
            float elX, elY, hdX, hdY, tipX, tipY;
            switch (beat)
            {
                case 0: // ↓
                    elX = 111f; elY = 33f; hdX = 107f; hdY = 23f; tipX = 103f; tipY = 12f; break;
                case 1: // ←
                    elX = 107f; elY = 37f; hdX = 94f; hdY = 33f; tipX = 79f; tipY = 31f; break;
                case 2: // →
                    elX = 113f; elY = 37f; hdX = 121f; hdY = 36f; tipX = 132f; tipY = 41f; break;
                default: // ↑
                    elX = 110f; elY = 44f; hdX = 112f; hdY = 53f; tipX = 117f; tipY = 67f; break;
            }
            if (InEllipse(x, y, tipX, tipY, 2.0f, 2.0f)) return colBatonGlow;
            if (InCapsule(x, y, hdX, hdY, tipX, tipY, 0.9f)) return colGold;
            if (InCapsule(x, y, shX, shY, elX, elY, 2.0f) || InCapsule(x, y, elX, elY, hdX, hdY, 1.7f))
                return colDarkCoat;

            // 왼팔 (표현 손짓)
            float lLift = beat == 3 ? 6f : (beat == 0 ? 1f : 3.5f);
            if (InCapsule(x, y, 89f, 36f, 82f, 32f + lLift * 0.5f, 1.9f)
                || InCapsule(x, y, 82f, 32f + lLift * 0.5f, 75f, 36f + lLift, 1.6f))
                return InCapsule(x, y, 75f, 36f + lLift, 77f, 35f + lLift * 0.8f, 1.5f) ? colIvory : colDarkCoat;

            // 지휘자 연미복 자락 & 하체/상체/머리
            if (InCapsule(x, y, 93f, 22f, 87f + sway * 0.5f, 11f, 2.1f)
                || InCapsule(x, y, 100f, 22f, 106f + sway * 0.5f, 11f, 2.1f))
                return colDarkCoat;
            if (InCapsule(x, y, 93f, 9f, 95f, 23f, 2.1f) || InCapsule(x, y, 100f, 9f, 98f, 23f, 2.1f))
                return colDarkCoat;
            if (InCapsule(x, y, 96.5f, 22f, 96.5f + sway * 0.3f, 36f, 5.8f)
                || InCapsule(x, y, 89f, 36f, 104f, 36f, 2.7f))
            {
                if (Mathf.Abs(x - 96.5f) < (y - 25f) * 0.25f && y >= 28) return colIvory;
                if (y >= 37 && (x <= 91 || x >= 102)) return colGold;
                return colDarkCoat;
            }
            if (InEllipse(x, y, 96.5f + sway * 0.4f, 45f - nod * 0.6f, 4.1f, 4.9f))
                return y >= 47 ? colDarkCoat : colIvory;

            // 2. 좌측 날개: 현악 섹션 — 콘서트 하프(x 6..24), 바이올린(x 26..44), 첼로(x 47..67)
            // [하프]
            if (InBox(x, y, 7, 0, 19, 3)) return colGold;
            if (InCapsule(x, y, 19f, 3f, 19f, 44f, 1.4f)) return colGold;
            if (InCapsule(x, y, 8f, 3f, 14f, 35f, 1.8f)) return colStrings;
            if (InCapsule(x, y, 13f, 35f, 19f, 43f, 1.4f)) return colGold;
            if (y >= 8 && y <= 38 && x >= 11 && x <= 17 && ((x - 11) % 2 == 0))
            {
                float topStringY = 26f + (x - 10) * 2.2f;
                float botStringY = 4f + (x - 10) * 0.4f;
                if (y >= botStringY && y <= topStringY) return oddBeat ? colBatonGlow : colWoodStrings;
            }

            // [바이올린 연주자]
            float vBow = oddBeat ? 2.0f : -2.0f;
            if (InCapsule(x, y, 31f, 0f, 33f, 20f, 1.7f) || InCapsule(x, y, 36f, 0f, 34f, 20f, 1.7f))
                return colPlayerDark;
            if (InCapsule(x, y, 33.5f, 19f, 33.5f, 34f, 3.8f)) return colPlayerDark;
            if (InEllipse(x, y, 33.5f, 40f, 3.1f, 3.6f)) return colPlayerDark;
            if (InEllipse(x, y, 27.5f, 35f, 3.6f, 2.3f) || InCapsule(x, y, 27f, 35f, 21f, 39f, 0.9f))
                return colStrings;
            if (InCapsule(x, y, 22f, 29f + vBow, 37f, 42f + vBow, 0.85f)) return colGold;

            // [첼리스트 + 첼로]
            float cBow = oddBeat ? 2.5f : -2.5f;
            if (InBox(x, y, 50, 0, 56, 11) && (x <= 51 || x >= 55 || y >= 9)) return colPlayerDark;
            if (InCapsule(x, y, 53f, 11f, 53f, 32f, 3.9f)) return colPlayerDark;
            if (InEllipse(x, y, 52.5f, 38f, 3.0f, 3.5f)) return colPlayerDark;
            if (InCapsule(x, y, 60f, 0f, 60f, 11f, 0.7f)) return colIvory; // 엔드핀
            if (InEllipse(x, y, 60f, 16f, 4.9f, 5.2f) || InEllipse(x, y, 60f, 25f, 3.6f, 3.9f))
                return colStrings;
            if (InCapsule(x, y, 60f, 26f, 59.5f, 40f, 1.0f)) return colWoodStrings; // 넥 & 스크롤
            if (InCapsule(x, y, 49f + cBow, 21f, 67f + cBow, 23f, 0.85f)) return colGold;

            // 3. 우측 날개: 타악 섹션 — 팀파니 쌍(x 124..151), 콘서트 베이스드럼(x 153..172), 공(x 175..187)
            // [팀파니 주자 + 쌍 케틀드럼]
            if (InCapsule(x, y, 138f, 12f, 138f, 35f, 4.1f)) return colPlayerDark;
            if (InEllipse(x, y, 138f, 41f, 3.2f, 3.7f)) return colPlayerDark;
            // 왼쪽 드럼 (중심 x=131) & 오른쪽 드럼 (중심 x=145)
            for (int d = 0; d < 2; d++)
            {
                float dcx = d == 0 ? 130.5f : 145.5f;
                if (InCapsule(x, y, dcx - 3f, 0f, dcx, 11f, 0.9f) || InCapsule(x, y, dcx + 3f, 0f, dcx, 11f, 0.9f))
                    return colGold;
                if (y <= 19 && y >= 10 && InEllipse(x, y, dcx, 19f, 5.6f, 9.0f))
                    return y >= 18 ? colDrumHead : colPerc;
            }
            // 팀파니 교차 말렛 (프레임마다 좌우 교차 타격)
            float lmY = !oddBeat ? 20.5f : 31f;
            float rmY = oddBeat ? 20.5f : 31f;
            if (InCapsule(x, y, 135f, 31f, 130.5f, lmY, 0.9f) || InEllipse(x, y, 130.5f, lmY, 1.8f, 1.8f))
                return colGold;
            if (InCapsule(x, y, 141f, 31f, 145.5f, rmY, 0.9f) || InEllipse(x, y, 145.5f, rmY, 1.8f, 1.8f))
                return colGold;

            // [콘서트 베이스 드럼]
            if (InCapsule(x, y, 156f, 0f, 160f, 13f, 1.1f) || InCapsule(x, y, 170f, 0f, 166f, 13f, 1.1f))
                return colGold;
            if (InEllipse(x, y, 163f, 20f, 8.8f, 8.8f))
                return InEllipse(x, y, 163f, 20f, 6.7f, 6.7f) ? colDrumHead : colPerc;

            // [매달린 오케스트라 공/징]
            if (InBox(x, y, 175, 0, 187, 45))
            {
                bool framePost = InBox(x, y, 175, 0, 176, 44) || InBox(x, y, 186, 0, 187, 44) || InBox(x, y, 174, 43, 188, 45);
                if (framePost) return colGold;
                if (InCapsule(x, y, 181.5f, 31f, 181.5f, 43f, 0.7f)) return colGold;
                if (InEllipse(x, y, 181.5f, 25f, 4.4f, 4.4f))
                    return (beat == 0 || beat == 2) ? colGold : colPerc;
            }

            // 4. 상단 아치 무대 + 성당 파이프 오르간 + 금관 연주자 (지휘자 머리 위 빈 공간 유지)
            // 좌우 수직 무대 아치 기둥 (얇고 세련되게)
            if (InBox(x, y, 69, 0, 71, 56) || InBox(x, y, 121, 0, 123, 56))
                return (y % 8 < 2) ? colGold : colPodium;

            // 파이프 오르간 윙 (좌 x=56..83, 우 x=109..136, 중앙 상부 크라운 x=85..107 & y=74..95)
            // 파이프 폭 3px + 빈틈 2px(피치 5px)로 나노봇 엣지 림 라이트가 파이프마다 분리되어 빛남
            int[] leftPipeHeights = { 76, 82, 88, 92, 85, 79 };
            for (int pi = 0; pi < leftPipeHeights.Length; pi++)
            {
                int px0 = 56 + pi * 5;
                int topY = leftPipeHeights[pi] + ((beat == 2) ? 1 : 0);
                if (InBox(x, y, px0, 58, px0 + 2, topY))
                    return (y >= topY - 2 || y <= 59) ? colGold : colOrganPipe;
            }
            for (int pi = 0; pi < leftPipeHeights.Length; pi++)
            {
                int px0 = 110 + pi * 5;
                int topY = leftPipeHeights[leftPipeHeights.Length - 1 - pi] + ((beat == 2) ? 1 : 0);
                if (InBox(x, y, px0, 58, px0 + 2, topY))
                    return (y >= topY - 2 || y <= 59) ? colGold : colOrganPipe;
            }
            // 중앙 하이 아치 (y=74..95, 지휘자 4박 ↑ 지휘봉(tipY=67)과 명확한 네거티브 스페이스 분리)
            for (int ci = 0; ci < 5; ci++)
            {
                int px0 = 86 + ci * 5;
                int archTop = (ci == 2) ? 95 : (ci == 1 || ci == 3 ? 91 : 86);
                if (InBox(x, y, px0, 74, px0 + 2, archTop))
                    return (y >= archTop - 2 || y <= 75) ? colGold : colOrganPipe;
            }
            if (InBox(x, y, 55, 55, 84, 57) || InBox(x, y, 108, 55, 137, 57)) return colPodium;

            // 상단 좌측 금관 연주자 + 호른 코일 (x=18..48, y=54..84)
            if (InBox(x, y, 18, 54, 49, 56)) return colPodium;
            if (InCapsule(x, y, 27f, 57f, 27f, 72f, 3.6f) || InEllipse(x, y, 27f, 77f, 3.0f, 3.5f))
                return colPlayerDark;
            if (InRing(x, y, 38f, 68f, 5.2f, 2.7f) || InCapsule(x, y, 30f, 73f, 35f, 70f, 1.2f)
                || InCapsule(x, y, 42f, 65f, 47f, 61f, 2.2f))
                return colBrass;

            // 상단 우측 금관 연주자 + 팡파르 트럼펫 (x=143..174, y=54..86)
            if (InBox(x, y, 143, 54, 174, 56)) return colPodium;
            if (InCapsule(x, y, 164f, 57f, 164f, 72f, 3.6f) || InEllipse(x, y, 164f, 77f, 3.0f, 3.5f))
                return colPlayerDark;
            float hornLift = beat == 3 ? 2.0f : (oddBeat ? 1.0f : 0f);
            if (InCapsule(x, y, 161f, 74f, 146f, 79f + hornLift, 1.3f)
                || InCapsule(x, y, 146f, 79f + hornLift, 142f, 81f + hornLift, 2.6f))
                return colBrass;

            return default;
        }

        /// <summary>
        /// 2페이즈 분리형 섹션(현악/타악/금관) 2프레임 실루엣(64x56) 픽셀 샘플러.
        /// 연주자 + 대형 악기 + 연주 모션이 뚜렷하게 보이도록 1,000봇 내외 밀도로 구성.
        /// </summary>
        public static Color32 SampleSectionEnsemblePixel(KyoshoSectionType type, int frame, int x, int y, int w, int h)
        {
            Color32 colPlayer = new Color32(78, 88, 114, 255);
            Color32 colGold = new Color32(246, 202, 98, 255);
            bool f1 = (frame & 1) == 1;

            switch (type)
            {
                case KyoshoSectionType.Strings:
                {
                    Color32 colBody = KyoshoConstants.StringsMintColor;
                    Color32 colWood = Color32.Lerp(colBody, Color.white, 0.35f);
                    // 첼리스트 실루엣 + 의자
                    if (InBox(x, y, 10, 2, 18, 15) && (x <= 11 || x >= 17 || y >= 13)) return colPlayer;
                    if (InCapsule(x, y, 17f, 14f, 20f, 35f, 4.2f)) return colPlayer;
                    if (InEllipse(x, y, 20f, 42f, 3.5f, 4.2f)) return colPlayer;
                    // 대형 첼로 몸통 (좌우 굴곡 + 넥/스크롤 + 엔드핀)
                    if (InCapsule(x, y, 36f, 2f, 35f, 12f, 0.9f)) return colGold;
                    if (InEllipse(x, y, 35f, 18f, 8.5f, 7.2f) || InEllipse(x, y, 34f, 29f, 6.2f, 5.5f))
                    {
                        // f-홀 네거티브 스페이스 2개 + 중앙 브릿지
                        if ((InBox(x, y, 30, 17, 31, 21) || InBox(x, y, 38, 17, 39, 21)) && y != 19)
                            return default;
                        if (InBox(x, y, 33, 18, 36, 20)) return colGold;
                        return colWood;
                    }
                    if (InCapsule(x, y, 34f, 33f, 33f, 51f, 1.5f) || InEllipse(x, y, 32f, 51f, 2.5f, 2.3f))
                        return colGold;
                    // 활 왕복 애니메이션
                    float bowShift = f1 ? 4f : -4f;
                    if (InCapsule(x, y, 20f + bowShift, 22f, 50f + bowShift, 26f, 1.1f)) return colGold;
                    return default;
                }
                case KyoshoSectionType.Percussion:
                {
                    Color32 colCopper = KyoshoConstants.PercussionAmberColor;
                    Color32 colHead = new Color32(250, 230, 185, 255);
                    // 팀파니 주자
                    if (InCapsule(x, y, 32f, 14f, 32f, 37f, 4.8f)) return colPlayer;
                    if (InEllipse(x, y, 32f, 44f, 3.8f, 4.4f)) return colPlayer;
                    // 쌍 케틀 드럼 + 림
                    for (int d = 0; d < 2; d++)
                    {
                        float dcx = d == 0 ? 18f : 46f;
                        if (InCapsule(x, y, dcx - 5f, 2f, dcx, 14f, 1.1f) || InCapsule(x, y, dcx + 5f, 2f, dcx, 14f, 1.1f))
                            return colGold;
                        if (y <= 24 && y >= 13 && InEllipse(x, y, dcx, 24f, 9.5f, 10.5f))
                            return y >= 22 ? colHead : colCopper;
                    }
                    // 교차 말렛
                    float ly = f1 ? 37f : 26f;
                    float ry = f1 ? 26f : 37f;
                    if (InCapsule(x, y, 27f, 36f, 18f, ly, 1.1f) || InEllipse(x, y, 18f, ly, 2.3f, 2.3f)) return colGold;
                    if (InCapsule(x, y, 37f, 36f, 46f, ry, 1.1f) || InEllipse(x, y, 46f, ry, 2.3f, 2.3f)) return colGold;
                    // 크래시 심벌 + 삼각 스탠드 (우측)
                    if (InCapsule(x, y, 57f, 3f, 57f, 37f, 0.85f)
                        || InCapsule(x, y, 53f, 2f, 57f, 8f, 0.85f)
                        || InCapsule(x, y, 61f, 2f, 57f, 8f, 0.85f)) return colGold;
                    if (InCapsule(x, y, 52f, 37f, 62f, 39f, 1.3f)) return colGold;
                    return default;
                }
                default:
                {
                    Color32 colBrass = KyoshoConstants.BrassMagentaColor;
                    Color32 colTrim = Color32.Lerp(colBrass, Color.white, 0.35f);
                    // 호른/트럼펫 주자
                    if (InCapsule(x, y, 16f, 3f, 18f, 22f, 2.2f) || InCapsule(x, y, 23f, 3f, 20f, 22f, 2.2f)) return colPlayer;
                    if (InCapsule(x, y, 19f, 20f, 20f, 37f, 4.6f)) return colPlayer;
                    if (InEllipse(x, y, 20f, 44f, 3.6f, 4.2f)) return colPlayer;
                    // 프렌치 호른 원형 코일 + 큰 나팔구 + 프레임 2 음파 호
                    float lift = f1 ? 1.5f : 0f;
                    if (InRing(x, y, 37f, 26f + lift, 10.5f, 6.2f)) return colTrim;
                    if (InRing(x, y, 37f, 26f + lift, 5.2f, 3.6f)) return colGold;
                    if (InCapsule(x, y, 24f, 40f, 31f, 33f + lift, 1.5f)) return colBrass;
                    if (InCapsule(x, y, 42f, 21f + lift, 54f, 13f + lift, 3.6f)) return colBrass;
                    if (f1 && InRing(x, y, 52f, 16f, 9f, 7.6f) && x > 54) return colGold;
                    return default;
                }
            }
        }
    }
}
