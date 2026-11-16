using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
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
        private static Sprite _hollowRingSprite;
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

        // ══════════════════════════════════════════════════════════════
        // [Phase 3 Pixel Maestro 스프라이트 & RSNanoKnightMoves 런타임/에디터 생성기]
        // ══════════════════════════════════════════════════════════════
        public const int MaestroCellWidth = 96;
        public const int MaestroCellHeight = 64;
        public const int MaestroRootX = 48;
        public const int MaestroRootY = 4;

        private static RSNanoKnightMoves _runtimeMaestroMoves;

        private struct MaestroPose
        {
            public float CenterX;
            public float FootY;
            public float BodyLeanDeg;
            public float Stride;
            public float BatonElbowX, BatonElbowY;
            public float BatonHandX, BatonHandY;
            public float BatonTipX, BatonTipY;
            public float OffHandX, OffHandY;
            public float TailLift;
            public bool ShowFxArc;
            public float FxArcRadius;
            public float FxArcAngleDeg;
            public float FxCenterX, FxCenterY;
            public float FxSpread;
        }

        public static int GetMaestroFrameCount(KnightMove move)
        {
            return move switch
            {
                KnightMove.RunIn => 3,
                KnightMove.Running => 6,
                KnightMove.RunOut => 3,
                KnightMove.Dash => 3,
                KnightMove.BackDash => 3,
                KnightMove.Light1 => 4,
                KnightMove.Light2 => 4,
                KnightMove.Light3 => 4,
                KnightMove.Light4 => 4,
                KnightMove.FrontHeavy => 5,
                KnightMove.DashAtk => 4,
                KnightMove.DashHeavy => 4,
                KnightMove.Rush => 6,
                KnightMove.Jump => 4,
                KnightMove.AirIn => 3,
                KnightMove.AirLoop => 4,
                KnightMove.AirLand => 3,
                KnightMove.Guard1 => 4,
                KnightMove.Guard2 => 4,
                KnightMove.Guard3 => 6,
                _ => 4
            };
        }

        public static RSNanoKnightMoves GetOrCreateMaestroMoves()
        {
            if (_runtimeMaestroMoves != null && _runtimeMaestroMoves.Ready)
            {
                return _runtimeMaestroMoves;
            }

            var asset = ScriptableObject.CreateInstance<RSNanoKnightMoves>();
            asset.name = "Kyosho_Maestro_Moves_Runtime";
            asset.cellSize = new Vector2Int(MaestroCellWidth, MaestroCellHeight);
            asset.root = new Vector2Int(MaestroRootX, MaestroRootY);
            asset.alphaCut = 0.5f;
            asset.pixelScale = 3;
            asset.darkSilhouette = false;
            asset.bladeGlow = 1.4f;
            asset.fxGlow = 2.6f;
            asset.bodySnap = 42000f;
            asset.snapSpeed = 420f;
            asset.idle = KnightMove.Guard1;

            foreach (KnightMove move in RSNanoKnightMoves.All)
            {
                NanoKnightClip clip = asset.Get(move);
                BakeRuntimeMaestroClip(move, clip);
            }

            _runtimeMaestroMoves = asset;
            return _runtimeMaestroMoves;
        }

        private static void BakeRuntimeMaestroClip(KnightMove move, NanoKnightClip clip)
        {
            int frameCount = GetMaestroFrameCount(move);
            clip.frames = frameCount;
            clip.fps = (move == KnightMove.Dash || move == KnightMove.Light1 || move == KnightMove.Light2) ? 18f : 14f;
            clip.start = new int[frameCount + 1];
            clip.counts = new int[frameCount * 3];

            var pxList = new List<int>(frameCount * 850);
            var colList = new List<int>(frameCount * 850);
            var framePixels = new List<(int hilbert, int px, int col, int kind)>(900);

            for (int f = 0; f < frameCount; f++)
            {
                clip.start[f] = pxList.Count;
                framePixels.Clear();

                for (int y = 0; y < MaestroCellHeight; y++)
                {
                    for (int x = 0; x < MaestroCellWidth; x++)
                    {
                        SampleMaestroFramePixel(move, f, x, y, out byte alpha, out Color32 col);
                        if (alpha < 128) continue;

                        int kind = ClassifyMaestroPixelKind(col, move);
                        int packedPx = (x + 2048) | ((y + 2048) << 12) | (kind << 24);
                        int packedCol = col.r | (col.g << 8) | (col.b << 16);
                        framePixels.Add((HilbertEncode(128, x, y), packedPx, packedCol, kind));
                        clip.counts[f * 3 + kind]++;
                    }
                }

                framePixels.Sort((a, b) =>
                {
                    int cmp = a.kind.CompareTo(b.kind);
                    return cmp != 0 ? cmp : a.hilbert.CompareTo(b.hilbert);
                });

                for (int i = 0; i < framePixels.Count; i++)
                {
                    pxList.Add(framePixels[i].px);
                    colList.Add(framePixels[i].col);
                }
            }

            clip.start[frameCount] = pxList.Count;
            clip.px = pxList.ToArray();
            clip.color = colList.ToArray();
            clip.bakedKey = $"runtime_{move}_{frameCount}";
        }

        private static int ClassifyMaestroPixelKind(Color32 c, KnightMove move)
        {
            bool isBaseMove = move == KnightMove.RunIn || move == KnightMove.Running || move == KnightMove.RunOut
                || move == KnightMove.Dash || move == KnightMove.BackDash;
            if (!isBaseMove && IsMaestroFxColor(c)) return 2;

            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            if (c.b >= c.r + 15 && lum >= 0.6f) return 1;
            return 0;
        }

        private static bool IsMaestroFxColor(Color32 c)
        {
            // BaseMoves 팔레트에 존재하지 않는 Fx 전용 RGB 판별
            return (c.r == 255 && c.g == 224 && c.b == 64)
                || (c.r == 64 && c.g == 245 && c.b == 255)
                || (c.r == 255 && c.g == 72 && c.b == 136);
        }

        private static int HilbertEncode(int n, int x, int y)
        {
            int d = 0;
            for (int s = n >> 1; s > 0; s >>= 1)
            {
                int rx = (x & s) != 0 ? 1 : 0;
                int ry = (y & s) != 0 ? 1 : 0;
                d += s * s * ((3 * rx) ^ ry);
                if (ry == 0)
                {
                    if (rx == 1)
                    {
                        x = (n - 1) - x;
                        y = (n - 1) - y;
                    }
                    int t = x;
                    x = y;
                    y = t;
                }
            }
            return d;
        }

        public static void SampleMaestroFramePixel(KnightMove move, int frame, int x, int y, out byte a, out Color32 col)
        {
            a = 0;
            col = default;

            // Body / Blade(지휘봉) 팔레트 - BaseMoves와 100% 동일해야 RSNanoKnightBaker가 올바르게 분류한다.
            Color32 colCoat = new Color32(34, 38, 52, 255);
            Color32 colVest = new Color32(62, 70, 92, 255);
            Color32 colShirt = new Color32(230, 232, 240, 255);   // b-r=10 < 15 -> Body
            Color32 colTrim = new Color32(218, 172, 68, 255);     // b < r -> Body
            Color32 colBaton = new Color32(120, 205, 255, 255);   // b-r=135>=15 & lum=0.73>=0.6 -> Blade

            // Fx 팔레트 - BaseMoves 이외의 동작에서만 등장
            Color32 colFxGold = new Color32(255, 224, 64, 255);
            Color32 colFxCyan = new Color32(64, 245, 255, 255);
            Color32 colFxCrimson = new Color32(255, 72, 136, 255);

            int total = Mathf.Max(1, GetMaestroFrameCount(move));
            MaestroPose p = BuildMaestroPose(move, Mathf.Clamp(frame, 0, total - 1), total);

            float rad = p.BodyLeanDeg * Mathf.Deg2Rad;
            float sinL = Mathf.Sin(rad);
            float cosL = Mathf.Cos(rad);

            // 골반 중심 및 상체 좌표 계산 (몸통~머리 축)
            float hipX = p.CenterX;
            float hipY = p.FootY + 14.5f;
            float chestX = hipX + sinL * 11f;
            float chestY = hipY + cosL * 11f;
            float shoulderX = hipX + sinL * 16.5f;
            float shoulderY = hipY + cosL * 16.5f;
            float headX = hipX + sinL * 23.5f;
            float headY = hipY + cosL * 23.5f;

            // 1. 효과 픽셀(음파 호/지휘 궤적) 우선 또는 외곽 레이어 판정
            if (p.ShowFxArc)
            {
                float dx = x - p.FxCenterX;
                float dy = y - p.FxCenterY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(dist - p.FxArcRadius) <= 2.3f)
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float diff = Mathf.Abs(Mathf.DeltaAngle(ang, p.FxArcAngleDeg));
                    if (diff <= p.FxSpread)
                    {
                        a = 255;
                        col = move == KnightMove.FrontHeavy || move == KnightMove.DashHeavy ? colFxCrimson
                            : (Mathf.Abs(dist - p.FxArcRadius) < 1.1f ? colFxGold : colFxCyan);
                        return;
                    }
                }
                // 보조 공명 파문 호
                if (p.FxSpread > 50f && Mathf.Abs(dist - (p.FxArcRadius - 6.5f)) <= 1.25f)
                {
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(ang, p.FxArcAngleDeg)) <= p.FxSpread * 0.78f)
                    {
                        a = 255;
                        col = colFxCyan;
                        return;
                    }
                }
            }

            // 2. 빛나는 지휘봉(Blade, kind=1) — 손에서 끝까지 선명한 굵기
            if (InCapsule(x, y, p.BatonHandX, p.BatonHandY, p.BatonTipX, p.BatonTipY, 1.05f))
            {
                a = 255;
                col = colBaton;
                return;
            }
            // 손잡이 금장 링
            if (InEllipse(x, y, p.BatonHandX, p.BatonHandY, 1.6f, 1.6f))
            {
                a = 255;
                col = colTrim;
                return;
            }

            // 3. 흰 장갑/손
            if (InEllipse(x, y, p.BatonHandX, p.BatonHandY, 1.4f, 1.4f)
                || InEllipse(x, y, p.OffHandX, p.OffHandY, 1.5f, 1.5f))
            {
                a = 255;
                col = colShirt;
                return;
            }

            // 4. 머리 & 휘날리는 지휘자 헤어 실루엣
            if (InEllipse(x, y, headX, headY, 3.8f, 4.3f))
            {
                a = 255;
                col = colCoat;
                return;
            }
            if (InCapsule(x, y, headX - 2f, headY + 2f, headX - 5.2f - sinL * 2f, headY + 3.8f, 1.6f))
            {
                a = 255;
                col = colVest;
                return;
            }

            // 5. 앞팔(지휘팔) & 뒷팔(왼팔)
            float rShX = shoulderX + 2.8f * cosL;
            float rShY = shoulderY - 2.8f * sinL;
            float lShX = shoulderX - 3.2f * cosL;
            float lShY = shoulderY + 3.2f * sinL;
            if (InCapsule(x, y, rShX, rShY, p.BatonElbowX, p.BatonElbowY, 1.75f)
                || InCapsule(x, y, p.BatonElbowX, p.BatonElbowY, p.BatonHandX, p.BatonHandY, 1.45f))
            {
                a = 255;
                col = colCoat;
                return;
            }
            if (InCapsule(x, y, lShX, lShY, (lShX + p.OffHandX) * 0.5f - 1.5f, (lShY + p.OffHandY) * 0.5f - 1.2f, 1.65f)
                || InCapsule(x, y, (lShX + p.OffHandX) * 0.5f - 1.5f, (lShY + p.OffHandY) * 0.5f - 1.2f, p.OffHandX, p.OffHandY, 1.4f))
            {
                a = 255;
                col = colVest;
                return;
            }

            // 6. 몸통(연미복 코트 + 중앙 셔츠 + 금장 견장)
            if (InEllipse(x, y, shoulderX, shoulderY - 0.5f, 5.4f, 2.2f))
            {
                a = 255;
                col = (Mathf.Abs(x - shoulderX) > 3.6f) ? colTrim : colCoat;
                return;
            }
            if (InCapsule(x, y, chestX, chestY, hipX, hipY, 4.1f))
            {
                // V자 흰 셔츠 가슴 + 단추 금장 라인
                float torsoDist = Mathf.Abs((x - chestX) * cosL - (y - chestY) * sinL);
                a = 255;
                if (y > chestY - 2f && torsoDist < 1.5f) col = colShirt;
                else if (torsoDist < 2.6f) col = colVest;
                else col = colCoat;
                return;
            }

            // 7. 연미복 뒷자락(Coattails) — 역동적으로 휘날리는 좌우 제비꼬리
            float tailTip1X = hipX - 6.8f - sinL * 4f - p.TailLift * 2.5f;
            float tailTip1Y = hipY - 9.5f + p.TailLift * 4.5f;
            float tailTip2X = hipX - 4.0f - sinL * 3f - p.TailLift * 1.5f;
            float tailTip2Y = hipY - 11.0f + p.TailLift * 3.5f;
            if (InCapsule(x, y, hipX - 2.2f, hipY + 1f, tailTip1X, tailTip1Y, 2.0f)
                || InCapsule(x, y, hipX - 1.0f, hipY + 0.5f, tailTip2X, tailTip2Y, 1.8f))
            {
                a = 255;
                col = colCoat;
                return;
            }

            // 8. 다리 & 부츠
            float frontFootX = hipX + 2.4f + p.Stride;
            float backFootX = hipX - 2.4f - p.Stride * 0.85f;
            if (InCapsule(x, y, hipX + 1.4f, hipY, frontFootX, p.FootY + 1f, 2.0f)
                || InCapsule(x, y, hipX - 1.4f, hipY, backFootX, p.FootY + 1f, 1.9f))
            {
                a = 255;
                col = (y < p.FootY + 5f) ? colCoat : colVest;
                return;
            }
        }

        private static MaestroPose BuildMaestroPose(KnightMove move, int frame, int total)
        {
            float t = total > 1 ? (float)frame / (total - 1) : 0f;
            float cx = 48f;
            float fy = MaestroRootY;

            var p = new MaestroPose
            {
                CenterX = cx,
                FootY = fy,
                BodyLeanDeg = 6f,
                Stride = 1.6f,
                BatonElbowX = cx + 8f,
                BatonElbowY = fy + 29f,
                BatonHandX = cx + 14f,
                BatonHandY = fy + 34f,
                BatonTipX = cx + 28f,
                BatonTipY = fy + 47f,
                OffHandX = cx - 8f,
                OffHandY = fy + 29f,
                TailLift = 0.25f
            };

            switch (move)
            {
                case KnightMove.RunIn:
                case KnightMove.Running:
                case KnightMove.RunOut:
                {
                    float phase = t * Mathf.PI * 2f;
                    p.BodyLeanDeg = 18f;
                    p.Stride = Mathf.Sin(phase) * 4.5f;
                    p.FootY = fy + Mathf.Abs(Mathf.Cos(phase)) * 1.6f;
                    p.BatonElbowX = cx - 4f;
                    p.BatonElbowY = p.FootY + 26f;
                    p.BatonHandX = cx - 10f;
                    p.BatonHandY = p.FootY + 24f;
                    p.BatonTipX = cx - 25f;
                    p.BatonTipY = p.FootY + 32f + Mathf.Sin(phase) * 3f;
                    p.OffHandX = cx + 10f;
                    p.OffHandY = p.FootY + 31f;
                    p.TailLift = 0.85f + Mathf.Abs(Mathf.Sin(phase)) * 0.35f;
                    break;
                }
                case KnightMove.Dash:
                {
                    p.BodyLeanDeg = 28f;
                    p.Stride = 6.2f;
                    p.BatonElbowX = cx - 6f;
                    p.BatonElbowY = fy + 24f;
                    p.BatonHandX = cx - 12f;
                    p.BatonHandY = fy + 22f;
                    p.BatonTipX = cx - 30f;
                    p.BatonTipY = fy + 24f;
                    p.OffHandX = cx + 14f;
                    p.OffHandY = fy + 28f;
                    p.TailLift = 1.25f;
                    break;
                }
                case KnightMove.BackDash:
                {
                    p.BodyLeanDeg = -14f;
                    p.Stride = 5.0f;
                    p.BatonElbowX = cx + 9f;
                    p.BatonElbowY = fy + 31f;
                    p.BatonHandX = cx + 15f;
                    p.BatonHandY = fy + 34f;
                    p.BatonTipX = cx + 28f;
                    p.BatonTipY = fy + 40f;
                    p.OffHandX = cx - 9f;
                    p.OffHandY = fy + 32f;
                    p.TailLift = 0.95f;
                    break;
                }
                case KnightMove.Light1:
                case KnightMove.Light2:
                case KnightMove.Light3:
                case KnightMove.Light4:
                {
                    float swing = Mathf.Lerp(78f, -52f, t);
                    if (move == KnightMove.Light2 || move == KnightMove.Light4) swing = -swing;
                    p.BodyLeanDeg = Mathf.Lerp(-6f, 20f, t);
                    p.Stride = 3.5f;
                    p.BatonElbowX = cx + 10f;
                    p.BatonElbowY = fy + 31f;
                    p.BatonHandX = cx + 16f + Mathf.Cos(swing * Mathf.Deg2Rad) * 3f;
                    p.BatonHandY = fy + 32f + Mathf.Sin(swing * Mathf.Deg2Rad) * 5f;
                    p.BatonTipX = p.BatonHandX + Mathf.Cos(swing * Mathf.Deg2Rad) * 18f;
                    p.BatonTipY = p.BatonHandY + Mathf.Sin(swing * Mathf.Deg2Rad) * 18f;
                    p.OffHandX = cx - 10f;
                    p.OffHandY = fy + 34f;
                    p.TailLift = 0.7f;
                    if (frame >= 1 && frame <= 2)
                    {
                        p.ShowFxArc = true;
                        p.FxCenterX = cx + 14f;
                        p.FxCenterY = fy + 31f;
                        p.FxArcRadius = 21f;
                        p.FxArcAngleDeg = 8f;
                        p.FxSpread = 68f;
                    }
                    break;
                }
                case KnightMove.FrontHeavy:
                {
                    // 프레임 0-1: 지휘봉을 머리 위로 높이 들어올린 예고 / 프레임 2-3: 강렬한 내리꽂기 (ActiveFx!)
                    float swing = frame < 2 ? Mathf.Lerp(108f, 92f, frame) : Mathf.Lerp(-12f, -48f, (frame - 2) / 2f);
                    p.BodyLeanDeg = frame < 2 ? -12f : 26f;
                    p.Stride = frame < 2 ? 2.0f : 5.4f;
                    p.BatonElbowX = cx + 8f;
                    p.BatonElbowY = fy + (frame < 2 ? 38f : 28f);
                    p.BatonHandX = cx + (frame < 2 ? 6f : 17f);
                    p.BatonHandY = fy + (frame < 2 ? 46f : 26f);
                    p.BatonTipX = p.BatonHandX + Mathf.Cos(swing * Mathf.Deg2Rad) * 19f;
                    p.BatonTipY = p.BatonHandY + Mathf.Sin(swing * Mathf.Deg2Rad) * 19f;
                    p.OffHandX = cx - 11f;
                    p.OffHandY = fy + 36f;
                    p.TailLift = frame < 2 ? 0.5f : 1.15f;
                    if (frame >= 2 && frame <= 3)
                    {
                        p.ShowFxArc = true;
                        p.FxCenterX = cx + 13f;
                        p.FxCenterY = fy + 28f;
                        p.FxArcRadius = 24f;
                        p.FxArcAngleDeg = 0f;
                        p.FxSpread = 82f;
                    }
                    break;
                }
                case KnightMove.DashAtk:
                case KnightMove.DashHeavy:
                case KnightMove.Rush:
                {
                    float osc = Mathf.Sin(t * Mathf.PI * 2f);
                    p.BodyLeanDeg = 24f;
                    p.Stride = 5.6f;
                    p.BatonElbowX = cx + 10f;
                    p.BatonElbowY = fy + 30f;
                    p.BatonHandX = cx + 17f;
                    p.BatonHandY = fy + 30f + osc * 4f;
                    p.BatonTipX = p.BatonHandX + 19f;
                    p.BatonTipY = p.BatonHandY + osc * 8f;
                    p.OffHandX = cx - 9f;
                    p.OffHandY = fy + 31f;
                    p.TailLift = 1.2f;
                    if (frame >= 1)
                    {
                        p.ShowFxArc = true;
                        p.FxCenterX = cx + 16f;
                        p.FxCenterY = fy + 30f;
                        p.FxArcRadius = 22f;
                        p.FxArcAngleDeg = 0f;
                        p.FxSpread = 74f;
                    }
                    break;
                }
                case KnightMove.Jump:
                case KnightMove.AirIn:
                case KnightMove.AirLoop:
                {
                    float bob = Mathf.Sin(t * Mathf.PI * 2f) * 2.5f;
                    p.FootY = fy + 5f + bob;
                    p.BodyLeanDeg = -8f;
                    p.Stride = 1.2f;
                    p.BatonElbowX = cx + 9f;
                    p.BatonElbowY = p.FootY + 32f;
                    p.BatonHandX = cx + 15f;
                    p.BatonHandY = p.FootY + 41f;
                    p.BatonTipX = cx + 26f;
                    p.BatonTipY = p.FootY + 52f;
                    p.OffHandX = cx - 10f;
                    p.OffHandY = p.FootY + 38f;
                    p.TailLift = 1.0f;
                    break;
                }
                case KnightMove.AirLand:
                {
                    p.BodyLeanDeg = 16f;
                    p.Stride = 4.0f;
                    p.BatonElbowX = cx + 10f;
                    p.BatonElbowY = fy + 28f;
                    p.BatonHandX = cx + 16f;
                    p.BatonHandY = fy + 25f;
                    p.BatonTipX = cx + 31f;
                    p.BatonTipY = fy + 18f;
                    p.TailLift = 0.9f;
                    if (frame >= 1)
                    {
                        p.ShowFxArc = true;
                        p.FxCenterX = cx + 12f;
                        p.FxCenterY = fy + 22f;
                        p.FxArcRadius = 21f;
                        p.FxArcAngleDeg = -8f;
                        p.FxSpread = 70f;
                    }
                    break;
                }
                case KnightMove.Guard2:
                {
                    p.BodyLeanDeg = -4f;
                    p.Stride = 2.0f;
                    p.BatonElbowX = cx + 7f;
                    p.BatonElbowY = fy + 30f;
                    p.BatonHandX = cx + 11f;
                    p.BatonHandY = fy + 33f;
                    p.BatonTipX = cx - 4f;
                    p.BatonTipY = fy + 44f;
                    p.OffHandX = cx + 8f;
                    p.OffHandY = fy + 35f;
                    break;
                }
                case KnightMove.Guard3:
                {
                    // 커튼콜 정중한 인사 (프레임 0 직립 -> 프레임 5 깊이 숙여 인사)
                    float bow = t;
                    p.BodyLeanDeg = Mathf.Lerp(2f, 38f, bow);
                    p.Stride = Mathf.Lerp(1.5f, 3.0f, bow);
                    p.BatonElbowX = Mathf.Lerp(cx + 8f, cx + 14f, bow);
                    p.BatonElbowY = Mathf.Lerp(fy + 31f, fy + 23f, bow);
                    p.BatonHandX = Mathf.Lerp(cx + 15f, cx + 20f, bow);
                    p.BatonHandY = Mathf.Lerp(fy + 37f, fy + 17f, bow);
                    p.BatonTipX = p.BatonHandX + Mathf.Lerp(11f, 16f, bow);
                    p.BatonTipY = p.BatonHandY + Mathf.Lerp(12f, -8f, bow);
                    // 왼손은 정중히 가슴 위에 얹는다
                    p.OffHandX = Mathf.Lerp(cx - 8f, cx + 4f, bow);
                    p.OffHandY = Mathf.Lerp(fy + 30f, fy + 27f, bow);
                    p.TailLift = Mathf.Lerp(0.2f, 1.15f, bow);
                    break;
                }
                default: // Guard1: 우아한 지휘 대기 자세
                {
                    float sway = Mathf.Sin(t * Mathf.PI * 2f);
                    p.BodyLeanDeg = sway * 3.5f;
                    p.BatonElbowX = cx + 8f;
                    p.BatonElbowY = fy + 31f + sway * 1.5f;
                    p.BatonHandX = cx + 14f;
                    p.BatonHandY = fy + 37f + sway * 3.0f;
                    p.BatonTipX = cx + 26f + sway * 3.0f;
                    p.BatonTipY = fy + 48f + sway * 4.5f;
                    p.OffHandX = cx - 9f;
                    p.OffHandY = fy + 31f - sway * 2.5f;
                    p.TailLift = 0.3f + Mathf.Abs(sway) * 0.15f;
                    break;
                }
            }

            return p;
        }

        // ========================================================================
        // 공용 월드 힌트/전조 시각 빌더 (경계 박스, 발광 광선, 중공 링, 펄스 이펙트)
        // ========================================================================

        public static Sprite GetHollowRingSprite()
        {
            if (_hollowRingSprite != null) return _hollowRingSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.10f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            }
            tex.Apply();
            _hollowRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _hollowRingSprite;
        }

        public static GameObject SpawnBorderedZoneHint(
            string name,
            Vector3 center,
            float width,
            float height,
            Color color,
            float fillAlpha,
            float borderAlpha,
            float borderThickness = 0.06f,
            string badgeText = null,
            float badgeLocalY = 0f,
            float badgeFontSize = 3.2f,
            int sortingOrder = 14)
        {
            var root = new GameObject(name);
            root.transform.position = center;

            var sq = GetSquareSprite();
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localScale = new Vector3(width, height, 1f);
            var fillSr = fillGo.AddComponent<SpriteRenderer>();
            fillSr.sprite = sq;
            fillSr.color = new Color(color.r, color.g, color.b, fillAlpha);
            fillSr.sortingOrder = sortingOrder;

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            CreateZoneEdge(root.transform, "EdgeTop", sq, new Vector3(0f, halfH, -0.01f), new Vector3(width, borderThickness, 1f), color, borderAlpha, sortingOrder + 1);
            CreateZoneEdge(root.transform, "EdgeBot", sq, new Vector3(0f, -halfH, -0.01f), new Vector3(width, borderThickness, 1f), color, borderAlpha, sortingOrder + 1);
            CreateZoneEdge(root.transform, "EdgeLeft", sq, new Vector3(-halfW, 0f, -0.01f), new Vector3(borderThickness, height, 1f), color, borderAlpha, sortingOrder + 1);
            CreateZoneEdge(root.transform, "EdgeRight", sq, new Vector3(halfW, 0f, -0.01f), new Vector3(borderThickness, height, 1f), color, borderAlpha, sortingOrder + 1);

            if (!string.IsNullOrEmpty(badgeText))
            {
                var txtGo = new GameObject("Badge");
                txtGo.transform.SetParent(root.transform, false);
                txtGo.transform.localPosition = new Vector3(0f, badgeLocalY, -0.03f);
                var tmp = txtGo.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = badgeFontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.Lerp(color, Color.white, 0.35f);
                tmp.sortingOrder = sortingOrder + 2;
                tmp.text = badgeText;
            }

            return root;
        }

        private static void CreateZoneEdge(Transform parent, string name, Sprite sq, Vector3 localPos, Vector3 scale, Color color, float alpha, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sq;
            sr.color = new Color(color.r, color.g, color.b, alpha);
            sr.sortingOrder = sortingOrder;
        }

        public static GameObject SpawnRingHint(
            string name,
            Vector3 center,
            float radius,
            Color color,
            float fillAlpha = 0.14f,
            float ringAlpha = 0.88f,
            int sortingOrder = 16,
            string label = null,
            float labelFontSize = 2.8f)
        {
            var root = new GameObject(name);
            root.transform.position = center;

            float diameter = Mathf.Max(0.1f, radius * 2f);
            if (fillAlpha > 0.001f)
            {
                var fillGo = new GameObject("InnerFill");
                fillGo.transform.SetParent(root.transform, false);
                fillGo.transform.localScale = new Vector3(diameter, diameter, 1f);
                var fillSr = fillGo.AddComponent<SpriteRenderer>();
                fillSr.sprite = GetCircleSprite();
                fillSr.color = new Color(color.r, color.g, color.b, fillAlpha);
                fillSr.sortingOrder = sortingOrder;
            }

            var rimGo = new GameObject("OuterRing");
            rimGo.transform.SetParent(root.transform, false);
            rimGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            rimGo.transform.localScale = new Vector3(diameter, diameter, 1f);
            var rimSr = rimGo.AddComponent<SpriteRenderer>();
            rimSr.sprite = GetHollowRingSprite();
            rimSr.color = new Color(color.r, color.g, color.b, ringAlpha);
            rimSr.sortingOrder = sortingOrder + 1;

            if (!string.IsNullOrEmpty(label))
            {
                var txtGo = new GameObject("RingLabel");
                txtGo.transform.SetParent(root.transform, false);
                txtGo.transform.localPosition = new Vector3(0f, radius * 0.35f, -0.02f);
                var tmp = txtGo.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = labelFontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.Lerp(color, Color.white, 0.4f);
                tmp.sortingOrder = sortingOrder + 2;
                tmp.text = label;
            }

            return root;
        }

        public static GameObject SpawnGlowingRayHint(string name, Color color, float outerThickness = 0.34f, float coreThickness = 0.09f, int sortingOrder = 16)
        {
            var root = new GameObject(name);
            var sq = GetSquareSprite();

            var outer = new GameObject("OuterBeam");
            outer.transform.SetParent(root.transform, false);
            outer.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            outer.transform.localScale = new Vector3(1f, outerThickness, 1f);
            var outerSr = outer.AddComponent<SpriteRenderer>();
            outerSr.sprite = sq;
            outerSr.color = new Color(color.r, color.g, color.b, 0.24f);
            outerSr.sortingOrder = sortingOrder;

            var core = new GameObject("CoreBeam");
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = new Vector3(0.5f, 0f, -0.01f);
            core.transform.localScale = new Vector3(1f, coreThickness, 1f);
            var coreSr = core.AddComponent<SpriteRenderer>();
            coreSr.sprite = sq;
            coreSr.color = new Color(Mathf.Lerp(color.r, 1f, 0.55f), Mathf.Lerp(color.g, 1f, 0.55f), Mathf.Lerp(color.b, 1f, 0.55f), 0.90f);
            coreSr.sortingOrder = sortingOrder + 1;

            var tip = new GameObject("TipMarker");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(1f, 0f, -0.02f);
            tip.transform.localScale = Vector3.one * (outerThickness * 1.45f);
            var tipSr = tip.AddComponent<SpriteRenderer>();
            tipSr.sprite = GetHollowRingSprite();
            tipSr.color = new Color(color.r, color.g, color.b, 0.95f);
            tipSr.sortingOrder = sortingOrder + 2;

            return root;
        }

        public static void UpdateGlowingRayHint(GameObject rayGo, Vector3 origin, float angleDeg, float length)
        {
            if (rayGo == null) return;
            rayGo.transform.position = origin;
            rayGo.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            rayGo.transform.localScale = new Vector3(Mathf.Max(0.1f, length), 1f, 1f);
            var tip = rayGo.transform.Find("TipMarker");
            if (tip != null)
            {
                float invX = 1f / Mathf.Max(0.1f, length);
                tip.localScale = new Vector3(0.45f * invX, 0.45f, 1f);
            }
        }

        public static void SpawnPulseRingEffect(Vector3 center, float startRadius, float endRadius, Color color, float duration = 0.30f, int sortingOrder = 22)
        {
            var go = new GameObject("KyoshoPulseRingVfx");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetHollowRingSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            AnimatePulseRingAsync(go, sr, startRadius, endRadius, color, duration).Forget();
        }

        private static async UniTaskVoid AnimatePulseRingAsync(GameObject go, SpriteRenderer sr, float startRadius, float endRadius, Color baseColor, float duration)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, duration);
            while (go != null && sr != null && elapsed < dur)
            {
                float t = Mathf.Clamp01(elapsed / dur);
                float r = Mathf.Lerp(startRadius, endRadius, 1f - (1f - t) * (1f - t));
                go.transform.localScale = Vector3.one * (r * 2f);
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t));
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (go != null) UnityEngine.Object.Destroy(go);
        }
    }
}
