using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 1: 지휘 도형 (KyoshoConductPatternGimmick)]
    /// 1페이즈(4/4: ↓←→↑), 2페이즈(2/4: ↓↑↓↑) 박자 도형 궤적대로 나노봇 지휘봉(TubeA)을 휘두름.
    /// - 사슬 끝 늦음(0.08초) 선행 보정으로 지휘봉 끝(Tip)이 정확히 정박에 타격점 통과.
    /// - 타격 부위: 오른팔(BodyPart.RightArm), 내구도 피해 25.
    /// - 2페이즈 타악 섹션 생존 시 바닥 착지 박(1박/3박)마다 좌우 앰버 오렌지 충격파(코어 전용) 발생.
    /// - 2페이즈 5박 회수 시 다음 패턴 예고(좌=스타카토, 상단=페르마타) 방향 지시.
    /// </summary>
    public class KyoshoConductPatternGimmick : KyoshoPatternGimmickBase
    {
        [Header("지휘 도형 설정")]
        [SerializeField] private float batonLength = 4.0f;
        [SerializeField] private float tipLagLeadSeconds = 0.08f;
        [SerializeField] private float shockwaveSpeed = 7.5f;
        [SerializeField] private float shockwaveHeight = 0.48f;
        [SerializeField] private int shockwaveCoreDamage = 135;

        private readonly List<int> _batonBotIds = new List<int>();
        private readonly List<ActiveShockwave> _shockwaves = new List<ActiveShockwave>();
        private GameObject _strikeTelegraphGo;

        private class ActiveShockwave
        {
            public Vector3 Position;
            public float DirX;
            public float RemainingLife;
            public bool HitPlayer;
            public GameObject VisualGo;
            public SpriteRenderer Renderer;
        }

        private void Awake()
        {
            tellBeats = 1f;
            postDelayBeats = 2f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            bool isPhase2 = Kyosho.Snapshot != null && Kyosho.Snapshot.currentPhase >= 2;
            bool hasPercussion = isPhase2 && Kyosho.SectionManager != null && Kyosho.SectionManager.IsPercussionAlive;
            float face = Kyosho.GetFacingToPlayerSign();

            var tube = Swarm.TubeA;
            Color tipColor = isPhase2 ? KyoshoConstants.ConductP2TipColor : KyoshoConstants.ConductP1TipColor;
            tube.r0 = 0.42f;
            tube.r1 = isPhase2 ? 0.02f : 0.14f;
            tube.stiffness = 460f;
            tube.damping = 28f;
            tube.maxSpeed = 90f;
            tube.tint = tipColor;
            tube.glow = 0.6f;
            tube.danger = false;

            Vector3 Root()
            {
                var b = Swarm.Blob;
                return b.center + new Vector3(face * b.radius.x * 0.55f, b.radius.y * 0.25f, -0.15f);
            }

            double startBeat = Clock.TotalBeats;
            float initAngle = face >= 0f ? 90f : 90f;
            tube.ResetChain(initAngle);
            tube.ChainStep(Root(), initAngle, 0.5f, 0.016f);
            TakeBotsToTubeA(0.12f, Root());

            // 1) 0박 예비(Tell): 지휘봉이 솟아오르며 발광 + 1박 타격 방향 예고선 표시
            float leadBeats = Mathf.Min(0.35f, tipLagLeadSeconds / Mathf.Max(0.1f, Clock.BeatSec));
            float cockAngle = face >= 0f ? 115f : 65f;
            float firstStrikeAngle = face >= 0f ? -35f : 215f;
            float curAngle = initAngle;
            float curLength = 0.5f;

            SpawnStrikeTelegraph(Root(), firstStrikeAngle, batonLength, tipColor);

            while (Clock.TotalBeats < startBeat + 1.0 - leadBeats)
            {
                token.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01((float)((Clock.TotalBeats - startBeat) / Math.Max(0.1, 1.0 - leadBeats)));
                curLength = Mathf.Lerp(0.5f, batonLength, Smooth(t));
                curAngle = Mathf.LerpAngle(initAngle, cockAngle, Smooth(t));
                tube.glow = Mathf.Lerp(0.4f, 1.8f, t);
                UpdateStrikeTelegraph(Root(), firstStrikeAngle, batonLength);
                StepBaton(tube, Root(), curAngle, curLength, Time.deltaTime);
                StepShockwaves(Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ClearStrikeTelegraph();

            // 2) 1~4박 4연격 (4/4: ↓←→↑  /  2/4: ↓↑↓↑)
            float[] strikeTargets = isPhase2
                ? new[]
                {
                    face >= 0f ? -35f : 215f,   // 1박: ↓
                    face >= 0f ? 105f : 75f,    // 2박: ↑
                    face >= 0f ? -35f : 215f,   // 3박: ↓
                    face >= 0f ? 105f : 75f     // 4박: ↑
                }
                : new[]
                {
                    face >= 0f ? -35f : 215f,   // 1박: ↓
                    face >= 0f ? -160f : -20f,  // 2박: ←
                    face >= 0f ? 15f : 165f,    // 3박: →
                    face >= 0f ? 110f : 70f     // 4박: ↑
                };

            bool[] spawnsShockwave = isPhase2
                ? new[] { true, false, true, false }
                : new[] { false, false, false, false };

            tube.danger = true;

            for (int step = 0; step < 4; step++)
            {
                double beatTarget = startBeat + 1.0 + step;
                double swingStart = beatTarget - leadBeats;
                double swingEnd = beatTarget + 0.32;
                float startAng = curAngle;
                float targetAng = strikeTargets[step];
                bool hitDealtThisBeat = false;
                bool shockwaveSpawnedThisBeat = false;

                KyoshoSfx.PlayCue(KyoshoSfx.Data?.conductWhoosh, "conduct_whoosh", Root());

                while (Clock.TotalBeats < startBeat + 2.0 + step - (step < 3 ? leadBeats : 0.0))
                {
                    token.ThrowIfCancellationRequested();
                    double nowBeat = Clock.TotalBeats;
                    float swingFrac = Mathf.Clamp01((float)((nowBeat - swingStart) / Math.Max(0.05, swingEnd - swingStart)));
                    curAngle = Mathf.LerpAngle(startAng, targetAng, EaseOutQuad(swingFrac));
                    StepBaton(tube, Root(), curAngle, batonLength, Time.deltaTime);

                    // 정박 판정 창
                    if (!hitDealtThisBeat && nowBeat >= beatTarget - 0.08 && nowBeat <= beatTarget + 0.25)
                    {
                        var player = FindPlayerObject();
                        if (player != null && KyoshoNanobotAttackHelper.CheckSegmentHitsPlayer(tube.nodes[3], tube.Tip, player.transform, 0.32f))
                        {
                            hitDealtThisBeat = true;
                            ApplyOriginalPatternHit(player, 190, BodyPart.RightArm, 25);
                        }
                    }

                    // 정박 통과 순간 시각/청각 강조 및 타악 충격파
                    if (!shockwaveSpawnedThisBeat && nowBeat >= beatTarget)
                    {
                        shockwaveSpawnedThisBeat = true;
                        KyoshoSfx.PlayCue(KyoshoSfx.Data?.conductTipFlash, "conduct_flash", tube.Tip);

                        if (hasPercussion && spawnsShockwave[step])
                        {
                            SpawnPercussionShockwavePair(new Vector3(tube.Tip.x, Kyosho.ArenaFloorY, 0f));
                        }
                    }

                    StepShockwaves(Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            tube.danger = false;
            tube.glow = 0.2f;

            // 3) 5박 회수 & 다음 패턴 예고(2페: 좌=스타카토, 상=페르마타)
            double recoverStart = Clock.TotalBeats;
            float telegraphAngle = curAngle;
            if (isPhase2)
            {
                var upcoming = Kyosho.PeekNextPhase2PatternType();
                telegraphAngle = upcoming == typeof(KyoshoStaccatoPatternGimmick) ? 180f : 90f;
            }

            while (Clock.TotalBeats < recoverStart + 1.0)
            {
                token.ThrowIfCancellationRequested();
                float k = Mathf.Clamp01((float)(Clock.TotalBeats - recoverStart));
                if (isPhase2)
                {
                    curAngle = Mathf.LerpAngle(curAngle, telegraphAngle, Time.deltaTime * 10f);
                    curLength = Mathf.Lerp(batonLength, 2.8f, Smooth(k));
                    tube.tint = Color.Lerp(tipColor,
                        telegraphAngle > 135f ? KyoshoConstants.StringsMintColor : KyoshoConstants.BrassMagentaColor,
                        0.65f);
                }
                else
                {
                    curLength = Mathf.Lerp(batonLength, 0.5f, Smooth(k));
                }
                StepBaton(tube, Root(), curAngle, curLength, Time.deltaTime);
                StepShockwaves(Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ReleaseBatonBots();
        }

        private void TakeBotsToTubeA(float frac, Vector3 near)
        {
            _batonBotIds.Clear();
            int n = Mathf.Clamp(Mathf.RoundToInt(Swarm.Count * frac), 120, Mathf.Max(120, Swarm.guideCount[RSNanoSwarm.G_Blob] / 2));
            Swarm.Collect(RSNanoSwarm.G_Blob, near, n, _batonBotIds);
            var lat = NanoLattice.Taper(Mathf.Max(1, _batonBotIds.Count), Swarm.TubeA.r0, Swarm.TubeA.r1, batonLength);
            var bots = Swarm.Bots;
            for (int k = 0; k < _batonBotIds.Count; k++)
            {
                int idx = _batonBotIds[k];
                bots[idx].u = lat[k].x;
                bots[idx].v = new Vector3(lat[k].y, lat[k].z, 0f);
                Swarm.Bind(idx, RSNanoSwarm.G_TubeA);
            }
        }

        private void StepBaton(TubeGuide tube, Vector3 root, float angleDeg, float len, float dt)
        {
            if (dt <= 0f) return;
            tube.ChainStep(root, angleDeg, len, dt * 0.5f, 260f);
            tube.ChainStep(root, angleDeg, len, dt * 0.5f, 260f);
            float floorMin = Kyosho != null ? Kyosho.ArenaFloorY + 0.15f : 0.15f;
            for (int i = 0; i < TubeGuide.K; i++)
            {
                if (tube.nodes[i].y < floorMin) tube.nodes[i].y = floorMin;
            }
        }

        private void SpawnPercussionShockwavePair(Vector3 origin)
        {
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.percussionShockwave, "percussion_shock", origin);
            SpawnSingleShockwave(origin, -1f);
            SpawnSingleShockwave(origin, 1f);
        }

        private void SpawnSingleShockwave(Vector3 origin, float dirX)
        {
            var go = new GameObject($"KyoshoPercussionShockwave_{(dirX > 0 ? "R" : "L")}");
            go.transform.position = new Vector3(origin.x, origin.y + shockwaveHeight * 0.5f, -0.1f);
            go.transform.localScale = new Vector3(0.75f, shockwaveHeight, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = KyoshoConstants.PercussionAmberColor;
            sr.sortingOrder = 22;

            _shockwaves.Add(new ActiveShockwave
            {
                Position = go.transform.position,
                DirX = dirX,
                RemainingLife = 1.6f,
                VisualGo = go,
                Renderer = sr
            });
        }

        private void StepShockwaves(float dt)
        {
            if (_shockwaves.Count == 0) return;

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            var player = FindPlayerObject();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            float floorY = Kyosho.ArenaFloorY;

            for (int i = _shockwaves.Count - 1; i >= 0; i--)
            {
                var sw = _shockwaves[i];
                sw.Position.x += sw.DirX * shockwaveSpeed * dt;
                sw.RemainingLife -= dt;
                if (sw.VisualGo != null) sw.VisualGo.transform.position = sw.Position;

                if (!sw.HitPlayer && player != null)
                {
                    bool xOverlap = Mathf.Abs(playerPos.x - sw.Position.x) <= 0.55f;
                    bool yOverlap = playerPos.y <= floorY + shockwaveHeight;
                    if (xOverlap && yOverlap)
                    {
                        sw.HitPlayer = true;
                        // 타악 섹션 충격파는 코어 체력만 차감 (파츠 내구도 0)
                        ApplyCoreOnlyHit(player, shockwaveCoreDamage);
                    }
                }

                if (sw.RemainingLife <= 0f || sw.Position.x < minX - 1f || sw.Position.x > maxX + 1f)
                {
                    if (sw.VisualGo != null) Destroy(sw.VisualGo);
                    _shockwaves.RemoveAt(i);
                }
            }
        }

        private void ReleaseBatonBots()
        {
            if (Swarm != null)
            {
                var bots = Swarm.Bots;
                foreach (int i in _batonBotIds)
                {
                    if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Bound && bots[i].guide == RSNanoSwarm.G_TubeA)
                    {
                        Swarm.Bind(i, RSNanoSwarm.G_Blob);
                    }
                }
                Swarm.TubeA.danger = false;
                Swarm.TubeA.glow = 0f;
                Swarm.TubeA.tint = Color.white;
            }
            _batonBotIds.Clear();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            ClearStrikeTelegraph();
            ReleaseBatonBots();
            foreach (var sw in _shockwaves)
            {
                if (sw.VisualGo != null) Destroy(sw.VisualGo);
            }
            _shockwaves.Clear();
        }

        private void SpawnStrikeTelegraph(Vector3 rootPos, float angleDeg, float length, Color color)
        {
            ClearStrikeTelegraph();
            _strikeTelegraphGo = new GameObject("KyoshoConductStrikeTelegraph");
            var sr = _strikeTelegraphGo.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = new Color(color.r, color.g, color.b, 0.28f);
            sr.sortingOrder = 16;
            UpdateStrikeTelegraph(rootPos, angleDeg, length);
        }

        private void UpdateStrikeTelegraph(Vector3 rootPos, float angleDeg, float length)
        {
            if (_strikeTelegraphGo == null) return;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            _strikeTelegraphGo.transform.position = rootPos + dir * (length * 0.5f);
            _strikeTelegraphGo.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            _strikeTelegraphGo.transform.localScale = new Vector3(length, 0.28f, 1f);
        }

        private void ClearStrikeTelegraph()
        {
            if (_strikeTelegraphGo != null)
            {
                Destroy(_strikeTelegraphGo);
                _strikeTelegraphGo = null;
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    }
}
