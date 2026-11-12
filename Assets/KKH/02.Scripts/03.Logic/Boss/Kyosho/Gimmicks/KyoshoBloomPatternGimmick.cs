using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 3: 개화 (KyoshoBloomPatternGimmick)]
    /// 1페이즈 전용.
    /// - 1박: 플레이어 최소 3m, 줄기 간 최소 2m 이격 조건을 만족하는 바닥 N곳에 씨앗 파편(♩/나팔/원반) 살포
    /// - 2~3박: 6개 풀에서 줄기 성장(2~3박 내 D 기본기 2타 / 스킬 1타 절단 가능)
    /// - 4박: 절단되지 않은 봉오리 만개 공격 (음표 3연사 / 트럼펫 4m 빔 / 심벌즈 2m 충격파) -> 왼다리(LeftLeg) 내구도 25
    /// </summary>
    public class KyoshoBloomPatternGimmick : KyoshoPatternGimmickBase
    {
        private readonly List<KyoshoBloomStemFieldGimmick> _stemPool = new List<KyoshoBloomStemFieldGimmick>(6);
        private readonly List<KyoshoTrackedBullet> _activeBullets = new List<KyoshoTrackedBullet>();
        private readonly List<GameObject> _transientVfx = new List<GameObject>();

        private void Awake()
        {
            tellBeats = 1f;
            postDelayBeats = 1f;
        }

        public void InitializeStemPool(IReadOnlyList<RSNanoLimb> poolLimbs, RSNanoSwarm swarm, Transform poolParent)
        {
            _stemPool.Clear();
            if (poolLimbs == null) return;

            for (int i = 0; i < poolLimbs.Count; i++)
            {
                var go = new GameObject($"KyoshoBloomStem_{i}");
                go.transform.SetParent(poolParent, false);
                var stem = go.AddComponent<KyoshoBloomStemFieldGimmick>();
                stem.BindPoolLimb(poolLimbs[i], swarm);
                _stemPool.Add(stem);
            }
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null || _stemPool.Count == 0) return;

            int count = Mathf.Min(_stemPool.Count, KyoshoConstants.GetBloomSeedCount(Kyosho.SelectedDifficulty));
            List<float> spawnXs = PickValidStemPositionsX(count);
            float floorY = Kyosho.ArenaFloorY;

            // 1박: 파편 살포 (모양으로 개화 종류 예고)
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomSeedToss, "bloom_toss", Swarm.Blob.center);
            var activeStems = new List<KyoshoBloomStemFieldGimmick>(count);
            for (int i = 0; i < spawnXs.Count; i++)
            {
                KyoshoBloomType bType = (KyoshoBloomType)(i % 3);
                if (i >= 3) bType = (KyoshoBloomType)UnityEngine.Random.Range(0, 3);

                Vector3 landing = new Vector3(spawnXs[i], floorY, 0f);
                var stem = _stemPool[i];
                stem.ActivateSeedPreview(landing, bType);
                activeStems.Add(stem);

                // 씨앗 투척 궤적 나노봇
                Vector3 vel = (landing - Swarm.Blob.center) / Mathf.Max(0.2f, Clock.BeatSec * 0.7f);
                var seedPacket = KyoshoNanobotAttackHelper.FireTrackedClump(
                    Swarm,
                    Swarm.Blob.center,
                    Swarm.Blob.center,
                    0f,
                    vel,
                    Clock.BeatSec * 0.75f,
                    18,
                    0.2f,
                    Color.white,
                    0.9f,
                    0,
                    BodyPart.Core,
                    0,
                    snapToHold: true);
                // 씨앗 자체는 피해를 주지 않음
                seedPacket.HitResolved = false;
            }

            await Clock.WaitBeatsAsync(1f, token);

            // 2~3박: 줄기 성장 (절단 가능 구간)
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomStemGrow, "bloom_grow", Swarm.Blob.center);
            double growStart = Clock.TotalBeats;
            bool isHell = Kyosho.SelectedDifficulty == BossDifficulty.Hell;

            while (Clock.TotalBeats < growStart + 2.0)
            {
                token.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01((float)((Clock.TotalBeats - growStart) / 2.0));
                float sway = isHell && p > 0.75f ? Mathf.Sin((p - 0.75f) * 28f) * 0.65f : Mathf.Sin(Time.time * 4f) * 0.08f;

                for (int i = 0; i < activeStems.Count; i++)
                {
                    if (activeStems[i].IsAliveAndUncut)
                    {
                        activeStems[i].SetGrowthProgress(p, sway * (i % 2 == 0 ? 1f : -1f));
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 4박: 살아남은 봉오리 만개 공격 (헬은 동시 2개 + 근접 휘두르기 판정)
            var surviving = new List<KyoshoBloomStemFieldGimmick>();
            foreach (var s in activeStems)
            {
                if (s.IsAliveAndUncut) surviving.Add(s);
            }

            int maxSimultaneous = isHell ? 2 : 1;
            int firedCount = 0;

            if (isHell && surviving.Count > 0)
            {
                var player = FindPlayerObject();
                if (player != null)
                {
                    foreach (var s in surviving)
                    {
                        if (Mathf.Abs(player.transform.position.x - s.transform.position.x) <= 1.25f)
                        {
                            ApplyOriginalPatternHit(player, 180, BodyPart.LeftLeg, 25);
                            break;
                        }
                    }
                }
            }

            foreach (var stem in surviving)
            {
                if (firedCount >= maxSimultaneous && !isHell)
                {
                    // 비-헬 난이도: 1개 개화 후 남은 봉오리는 순차 개화 또는 1개 집중 후 회수
                    TriggerBloomAttack(stem);
                }
                else
                {
                    TriggerBloomAttack(stem);
                }
                firedCount++;
                if (firedCount < surviving.Count && !isHell)
                {
                    await Clock.WaitBeatsAsync(0.35f, token);
                }
            }

            double tailStart = Clock.TotalBeats;
            while (Clock.TotalBeats < tailStart + 1.0)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_activeBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            foreach (var stem in _stemPool) stem.DeactivateStem();
        }

        private void TriggerBloomAttack(KyoshoBloomStemFieldGimmick stem)
        {
            if (stem == null || !stem.IsAliveAndUncut) return;

            Vector3 tip = stem.TipPosition;
            Vector3 playerCenter = Kyosho.GetPlayerAimCenter();
            var player = FindPlayerObject();

            switch (stem.BloomType)
            {
                case KyoshoBloomType.Note:
                {
                    KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomOpenNote, "bloom_note", tip);
                    Vector3 baseDir = (playerCenter - tip);
                    baseDir.z = 0f;
                    if (baseDir.sqrMagnitude < 0.01f) baseDir = Vector3.left;
                    baseDir.Normalize();

                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 0f, k * 12f) * baseDir;
                        var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                            Swarm, tip, tip, k * 0.08f + 0.08f, dir * 8.5f, 3.0f,
                            20, 0.35f, new Color(0.55f, 0.90f, 1.00f, 1f), 1.3f,
                            180, BodyPart.LeftLeg, 25, snapToHold: true);
                        _activeBullets.Add(b);
                    }
                    break;
                }
                case KyoshoBloomType.Trumpet:
                {
                    KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomOpenTrumpet, "bloom_trumpet", tip);
                    float dirX = playerCenter.x >= tip.x ? 1f : -1f;
                    Vector3 beamStart = new Vector3(tip.x, Kyosho.ArenaFloorY + 0.85f, -0.08f);
                    Vector3 beamEnd = beamStart + Vector3.right * (dirX * 4.0f);
                    SpawnTransientBeamVisual((beamStart + beamEnd) * 0.5f, 4.0f, 0.55f, new Color(1f, 0.72f, 0.25f, 0.88f));

                    if (player != null && KyoshoNanobotAttackHelper.CheckSegmentHitsPlayer(beamStart, beamEnd, player.transform, 0.42f))
                    {
                        ApplyOriginalPatternHit(player, 180, BodyPart.LeftLeg, 25);
                    }
                    break;
                }
                case KyoshoBloomType.Cymbal:
                {
                    KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomOpenCymbal, "bloom_cymbal", stem.transform.position);
                    Vector3 center = stem.transform.position + Vector3.up * 0.9f;
                    SpawnTransientRingVisual(center, 2.0f, new Color(1f, 0.42f, 0.75f, 0.78f));

                    if (player != null && Vector2.Distance(playerCenter, center) <= 2.0f)
                    {
                        ApplyOriginalPatternHit(player, 180, BodyPart.LeftLeg, 25);
                    }
                    break;
                }
            }

            stem.DeactivateStem();
        }

        private List<float> PickValidStemPositionsX(int count)
        {
            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float safeMin = minX + 1.0f;
            float safeMax = maxX - 1.0f;
            float playerX = Kyosho.PlayerTransform != null ? Kyosho.PlayerTransform.position.x : 0f;

            var chosen = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    float candidate = UnityEngine.Random.Range(safeMin, safeMax);
                    if (Mathf.Abs(candidate - playerX) < 3.0f) continue;

                    bool tooClose = false;
                    for (int j = 0; j < chosen.Count; j++)
                    {
                        if (Mathf.Abs(candidate - chosen[j]) < 2.0f) { tooClose = true; break; }
                    }
                    if (!tooClose)
                    {
                        chosen.Add(candidate);
                        found = true;
                        break;
                    }
                }

                // 만약 플레이어가 중앙을 점유해 6개 공간이 빡빡할 경우 최소 간격만 유지하여 배치
                if (!found)
                {
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        float candidate = UnityEngine.Random.Range(safeMin, safeMax);
                        if (Mathf.Abs(candidate - playerX) < 2.2f) continue;
                        bool tooClose = false;
                        for (int j = 0; j < chosen.Count; j++)
                        {
                            if (Mathf.Abs(candidate - chosen[j]) < 1.6f) { tooClose = true; break; }
                        }
                        if (!tooClose) { chosen.Add(candidate); found = true; break; }
                    }
                }
            }

            return chosen;
        }

        private void HandleBulletHit(KyoshoTrackedBullet b)
        {
            var player = FindPlayerObject();
            if (player != null)
            {
                ApplyOriginalPatternHit(player, b.Damage, BodyPart.LeftLeg, 25);
            }
        }

        private void SpawnTransientBeamVisual(Vector3 center, float width, float height, Color color)
        {
            var go = new GameObject("KyoshoTrumpetBeam");
            go.transform.position = center;
            go.transform.localScale = new Vector3(width, height, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = color;
            sr.sortingOrder = 22;
            _transientVfx.Add(go);
            Destroy(go, 0.35f);
        }

        private void SpawnTransientRingVisual(Vector3 center, float radius, Color color)
        {
            var go = new GameObject("KyoshoCymbalRing");
            go.transform.position = center;
            go.transform.localScale = Vector3.one * (radius * 2f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetCircleSprite();
            sr.color = color;
            sr.sortingOrder = 22;
            _transientVfx.Add(go);
            Destroy(go, 0.32f);
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            foreach (var stem in _stemPool) stem?.DeactivateStem();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_activeBullets, Swarm);
            foreach (var v in _transientVfx)
            {
                if (v != null) Destroy(v);
            }
            _transientVfx.Clear();
        }
    }
}
