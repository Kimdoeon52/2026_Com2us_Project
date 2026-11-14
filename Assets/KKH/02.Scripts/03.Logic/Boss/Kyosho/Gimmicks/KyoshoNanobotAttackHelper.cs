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
            int need = Mathf.Max(1, clumpSize);
            swarm.Collect(sourceGuide, origin, need, _collectTmp);
            if (_collectTmp.Count < need && sourceGuide != RSNanoSwarm.G_Blob)
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

                swarm.Fire(idx, holdPos + offset, waitSec, velocity, lifeSec, RSNanoSwarm.G_Blob, default, 0f, snapToHold || waitSec <= 0f, clump: 1);
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
                        if (idx >= 0 && idx < bots.Length && bots[idx].mode == BotMode.Free)
                        {
                            swarm.Bind(idx, RSNanoSwarm.G_Blob);
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
    }
}
