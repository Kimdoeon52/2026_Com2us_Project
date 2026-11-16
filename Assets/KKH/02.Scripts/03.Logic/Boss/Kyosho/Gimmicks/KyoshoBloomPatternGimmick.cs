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
        private readonly List<KyoshoTrackedBullet> _seedBullets = new List<KyoshoTrackedBullet>();
        private readonly List<GameObject> _transientVfx = new List<GameObject>();
        private readonly List<GameObject> _hellWhipTelegraphs = new List<GameObject>();

        private void Awake()
        {
            tellBeats = 1f;
            postDelayBeats = 1.5f;
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
                stem.BindPoolLimb(poolLimbs[i], swarm, Kyosho);
                _stemPool.Add(stem);
            }
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null || _stemPool.Count == 0) return;

            int count = Mathf.Min(_stemPool.Count, KyoshoConstants.GetBloomSeedCount(Kyosho.SelectedDifficulty));
            List<float> spawnXs = PickValidStemPositionsX(count);
            float floorY = Kyosho.ArenaFloorY;

            bool isHell = Kyosho.SelectedDifficulty == BossDifficulty.Hell;
            bool isHardOrHell = Kyosho.SelectedDifficulty >= BossDifficulty.Hard;
            Kyosho.ScorePreviewHUD?.ShowPatternHint(
                "1페이즈 개화 — 씨앗 줄기 절단!",
                isHell
                    ? "만개 전 줄기를 공격해 절단! (후반 줄기 근처 채찍 경보 주의)"
                    : "만개 전 줄기를 공격(D 2타 / 스킬 1타)해 절단하세요!",
                KyoshoConstants.StringsMintColor,
                0f,
                "살포");

            // 1박: 파편 살포 (모양으로 개화 종류 예고 + 펜타토닉 하프 아르페지오)
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_seedBullets, Swarm);
            ClearHellWhipTelegraphs();
            var activeStems = new List<KyoshoBloomStemFieldGimmick>(count);
            for (int i = 0; i < spawnXs.Count; i++)
            {
                KyoshoBloomType bType = (KyoshoBloomType)(i % 3);
                if (i >= 3) bType = (KyoshoBloomType)UnityEngine.Random.Range(0, 3);

                Vector3 landing = new Vector3(spawnXs[i], floorY, 0f);
                var stem = _stemPool[i];
                stem.ActivateSeedPreview(landing, bType, Kyosho);
                activeStems.Add(stem);
                KyoshoSfx.PlayBloomSeedHarmony(i, landing);

                // 씨앗 투척 궤적 나노봇 (목적지 도달 후 회수해 클럼프 누수 방지)
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
                seedPacket.HitResolved = true;
                _seedBullets.Add(seedPacket);
            }

            double seedStart = Clock.TotalBeats;
            while (Clock.TotalBeats < seedStart + 1.0)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_seedBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, null);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_seedBullets, Swarm);

            // 2~4박: 줄기 성장 (절단 가능 구간 — 3박으로 여유 확보)
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.bloomStemGrow, "bloom_grow", Swarm.Blob.center);
            double growStart = Clock.TotalBeats;
            bool hellWhipWarned = false;

            while (Clock.TotalBeats < growStart + 3.0)
            {
                token.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01((float)((Clock.TotalBeats - growStart) / 3.0));
                float sway = isHell && p > 0.75f ? Mathf.Sin((p - 0.75f) * 28f) * 0.65f : Mathf.Sin(Time.time * 4f) * 0.08f;
                Vector3? playerPos = Kyosho.PlayerTransform != null ? (Vector3?)Kyosho.PlayerTransform.position : null;

                for (int i = 0; i < activeStems.Count; i++)
                {
                    if (activeStems[i].IsAliveAndUncut)
                    {
                        activeStems[i].SetGrowthProgress(p, sway * (i % 2 == 0 ? 1f : -1f), playerPos);
                    }
                }

                if (isHell && !hellWhipWarned && p >= 0.72f)
                {
                    hellWhipWarned = true;
                    SpawnHellWhipTelegraphs(activeStems);
                }

                float remainSec = Mathf.Max(0f, (1f - p) * 3f * Clock.BeatSec);
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(p, $"{remainSec:0.0}s");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 만개: 살아남은 봉오리 중 동시 상한(Hard/Hell 2개, 이하 1개)만 개화하고 초과분은 회수
            var surviving = new List<KyoshoBloomStemFieldGimmick>();
            foreach (var s in activeStems)
            {
                if (s.IsAliveAndUncut) surviving.Add(s);
            }

            int maxSimultaneous = isHardOrHell ? 2 : 1;
            int firedCount = 0;

            ClearHellWhipTelegraphs();
            if (isHell && surviving.Count > 0)
            {
                var player = FindPlayerObject();
                if (player != null)
                {
                    foreach (var s in surviving)
                    {
                        KyoshoNanobotAttackHelper.SpawnPulseRingEffect(
                            s.transform.position + Vector3.up * 0.35f, 0.25f, 1.30f, new Color(1f, 0.25f, 0.35f), 0.24f);
                        if (Mathf.Abs(player.transform.position.x - s.transform.position.x) <= 1.25f)
                        {
                            ApplyOriginalPatternHit(player, 180, BodyPart.LeftLeg, 25);
                            break;
                        }
                    }
                }
            }

            Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(1f, surviving.Count > 0 ? "만개!" : "절단 완료!");

            foreach (var stem in surviving)
            {
                if (firedCount >= maxSimultaneous)
                {
                    stem.DeactivateStem();
                    continue;
                }

                await TriggerBloomAttackAsync(stem, firedCount, token);
                firedCount++;

                if (firedCount < maxSimultaneous && firedCount < surviving.Count)
                {
                    double stepWaitEnd = Clock.TotalBeats + 0.45;
                    while (Clock.TotalBeats < stepWaitEnd)
                    {
                        token.ThrowIfCancellationRequested();
                        KyoshoNanobotAttackHelper.StepTrackedBullets(_activeBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                }
            }

            double tailStart = Clock.TotalBeats;
            while (_activeBullets.Count > 0 && Clock.TotalBeats < tailStart + 1.4)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_activeBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            Kyosho.ScorePreviewHUD?.HidePatternHint();
            foreach (var stem in _stemPool) stem.DeactivateStem();
        }

        private async UniTask TriggerBloomAttackAsync(KyoshoBloomStemFieldGimmick stem, int openIndex, CancellationToken token)
        {
            if (stem == null || !stem.IsAliveAndUncut) return;

            Vector3 tip = stem.TipPosition;
            Vector3 playerCenter = Kyosho.GetPlayerAimCenter();
            KyoshoBloomType bloomType = stem.BloomType;

            switch (bloomType)
            {
                case KyoshoBloomType.Note:
                {
                    KyoshoSfx.PlayBloomOpenHarmony(KyoshoBloomType.Note, openIndex, tip);
                    KyoshoNanobotAttackHelper.SpawnPulseRingEffect(tip, 0.18f, 0.90f, new Color(0.55f, 0.90f, 1.00f), 0.24f);
                    Vector3 baseDir = (playerCenter - tip);
                    baseDir.z = 0f;
                    if (baseDir.sqrMagnitude < 0.01f) baseDir = Vector3.left;
                    baseDir.Normalize();

                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 dir = Quaternion.Euler(0f, 0f, k * 16f) * baseDir;
                        var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                            Swarm, tip, tip, k * 0.08f + 0.08f, dir * 6.8f, 3.0f,
                            18, 0.26f, new Color(0.55f, 0.90f, 1.00f, 1f), 1.3f,
                            180, BodyPart.LeftLeg, 25, snapToHold: true);
                        _activeBullets.Add(b);
                    }
                    stem.DeactivateStem();
                    break;
                }
                case KyoshoBloomType.Trumpet:
                {
                    KyoshoSfx.PlayBloomOpenHarmony(KyoshoBloomType.Trumpet, openIndex, tip);
                    float dirX = playerCenter.x >= tip.x ? 1f : -1f;
                    Vector3 beamStart = new Vector3(tip.x, Kyosho.ArenaFloorY + 0.85f, -0.08f);
                    Vector3 beamEnd = beamStart + Vector3.right * (dirX * 3.2f);
                    SpawnTransientBeamVisual((beamStart + beamEnd) * 0.5f, 3.2f, 0.44f, new Color(1f, 0.72f, 0.25f, 0.90f));
                    stem.DeactivateStem();

                    float elapsed = 0f;
                    bool hitDealt = false;
                    while (elapsed < 0.28f)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!hitDealt)
                        {
                            var pObj = FindPlayerObject();
                            if (pObj != null && KyoshoNanobotAttackHelper.CheckSegmentHitsPlayer(beamStart, beamEnd, pObj.transform, 0.30f))
                            {
                                hitDealt = true;
                                ApplyOriginalPatternHit(pObj, 180, BodyPart.LeftLeg, 25);
                            }
                        }
                        KyoshoNanobotAttackHelper.StepTrackedBullets(_activeBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                        elapsed += Time.deltaTime;
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                    break;
                }
                case KyoshoBloomType.Cymbal:
                {
                    KyoshoSfx.PlayBloomOpenHarmony(KyoshoBloomType.Cymbal, openIndex, stem.transform.position);
                    Vector3 center = stem.transform.position + Vector3.up * 0.9f;
                    SpawnTransientRingVisual(center, 1.55f, new Color(1f, 0.42f, 0.75f, 0.80f));
                    KyoshoNanobotAttackHelper.SpawnPulseRingEffect(center, 0.30f, 1.65f, new Color(1f, 0.42f, 0.75f), 0.30f);
                    stem.DeactivateStem();

                    float elapsed = 0f;
                    bool hitDealt = false;
                    while (elapsed < 0.28f)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!hitDealt)
                        {
                            var pObj = FindPlayerObject();
                            if (pObj != null && Vector2.Distance(Kyosho.GetPlayerAimCenter(), center) <= 1.55f)
                            {
                                hitDealt = true;
                                ApplyOriginalPatternHit(pObj, 180, BodyPart.LeftLeg, 25);
                            }
                        }
                        KyoshoNanobotAttackHelper.StepTrackedBullets(_activeBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                        elapsed += Time.deltaTime;
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                    break;
                }
            }
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

        private void SpawnHellWhipTelegraphs(List<KyoshoBloomStemFieldGimmick> stems)
        {
            ClearHellWhipTelegraphs();
            for (int i = 0; i < stems.Count; i++)
            {
                var s = stems[i];
                if (s == null || !s.IsAliveAndUncut) continue;
                var warn = KyoshoNanobotAttackHelper.SpawnRingHint(
                    "KyoshoHellWhipWarn",
                    s.transform.position + Vector3.up * 0.35f,
                    1.25f,
                    new Color(1f, 0.25f, 0.35f),
                    0.14f,
                    0.92f,
                    15,
                    "! WHIP !",
                    2.6f);
                _hellWhipTelegraphs.Add(warn);
            }
        }

        private void ClearHellWhipTelegraphs()
        {
            for (int i = _hellWhipTelegraphs.Count - 1; i >= 0; i--)
            {
                if (_hellWhipTelegraphs[i] != null) Destroy(_hellWhipTelegraphs[i]);
            }
            _hellWhipTelegraphs.Clear();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            Kyosho?.ScorePreviewHUD?.HidePatternHint();
            ClearHellWhipTelegraphs();
            foreach (var stem in _stemPool) stem?.DeactivateStem();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_seedBullets, Swarm);
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_activeBullets, Swarm);
            foreach (var v in _transientVfx)
            {
                if (v != null) Destroy(v);
            }
            _transientVfx.Clear();
        }
    }
}
