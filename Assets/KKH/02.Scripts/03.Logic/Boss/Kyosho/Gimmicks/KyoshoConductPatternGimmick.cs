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
        private readonly List<KyoshoTrackedBullet> _fanBullets = new List<KyoshoTrackedBullet>();
        private GameObject _strikeTelegraphGo;
        private GameObject _shockwaveTelegraphGo;

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
            postDelayBeats = 2f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            bool isPhase3 = Kyosho.IsPhase3;
            bool isCompound68 = isPhase3 && Kyosho.NextPhase3ConductUsesCompound();
            bool isPhase2 = !isPhase3 && Kyosho.Snapshot != null && Kyosho.Snapshot.currentPhase >= 2;
            bool hasPercussion = isPhase2 && Kyosho.SectionManager != null && Kyosho.SectionManager.IsPercussionAlive;
            float face = Kyosho.GetFacingToPlayerSign();

            var tube = Swarm.TubeA;
            Color tipColor = isPhase3
                ? (isCompound68 ? KyoshoConstants.ConductP3CompoundColor : KyoshoConstants.ConductP3TripleColor)
                : (isPhase2 ? KyoshoConstants.ConductP2TipColor : KyoshoConstants.ConductP1TipColor);
            tube.r0 = 0.42f;
            tube.r1 = (isPhase2 || isPhase3) ? 0.02f : 0.14f;
            tube.stiffness = isPhase3 ? 540f : 460f;
            tube.damping = 28f;
            tube.maxSpeed = isPhase3 ? 110f : 90f;
            tube.tint = tipColor;
            tube.glow = 0.6f;
            tube.danger = false;

            Vector3 Root()
            {
                if (isPhase3)
                {
                    return Swarm.Knight.ChestWorld + new Vector3(face * 0.85f, 0.32f, -0.15f);
                }
                var b = Swarm.Blob;
                return b.center + new Vector3(face * b.radius.x * 0.55f, b.radius.y * 0.25f, -0.15f);
            }

            if (isPhase3) Kyosho.SetMaestroPose(KnightMove.Guard1, 0);

            string hintTitle = isPhase3
                ? (isCompound68 ? "3페이즈 변박 지휘 (6/8)" : "3페이즈 고속 지휘 (3/4)")
                : (isPhase2 ? "2페이즈 강박 지휘 (2/4)" : "1페이즈 기본 지휘 (4/4)");
            string hintDetail = isPhase3
                ? "궤적선을 보고 연속 회피 · 충격파(점프) + 막타 탄막 주의!"
                : (isPhase2
                    ? (hasPercussion ? "강박 지휘봉 회피 · 1/3박 착지 충격파 점프 회피!" : "지휘봉 궤적을 보고 타이밍 회피!")
                    : "지휘봉 예고 궤적선을 보고 Space 무적 회피!");
            Kyosho.ScorePreviewHUD?.ShowPatternHint(hintTitle, hintDetail, tipColor, 0f, "예비");

            double startBeat = Clock.TotalBeats;
            float initAngle = 90f;
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
            if (isPhase3 || hasPercussion)
            {
                SpawnShockwaveTelegraph(Root(), firstStrikeAngle, batonLength);
            }

            while (Clock.TotalBeats < startBeat + 1.0 - leadBeats)
            {
                token.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01((float)((Clock.TotalBeats - startBeat) / Math.Max(0.1, 1.0 - leadBeats)));
                curLength = Mathf.Lerp(0.5f, batonLength, Smooth(t));
                curAngle = Mathf.LerpAngle(initAngle, cockAngle, Smooth(t));
                tube.glow = Mathf.Lerp(0.4f, 1.8f, t);
                UpdateStrikeTelegraph(Root(), firstStrikeAngle, batonLength);
                UpdateShockwaveTelegraph(Root(), firstStrikeAngle, batonLength);
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(t * 0.25f, "타격 임박");
                StepBaton(tube, Root(), curAngle, curLength, Time.deltaTime);
                StepShockwaves(Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ClearStrikeTelegraph();
            ClearShockwaveTelegraph();

            // 2) 박자별 연속 타격 (1페 4/4: ↓←→↑, 2페 2/4: ↓↑↓↑, 3페 3/4: ↓→↑, 3페 6/8: ↓←←→→↑)
            float Ang(float rightDeg, float leftDeg) => face >= 0f ? rightDeg : leftDeg;
            float[] strikeTargets;
            double[] beatOffsets;
            bool[] spawnsShockwave;
            KnightMove[] maestroPoses;

            if (isPhase3 && isCompound68)
            {
                strikeTargets = new[]
                {
                    Ang(-35f, 215f), Ang(-160f, -20f), Ang(-160f, -20f),
                    Ang(15f, 165f), Ang(15f, 165f), Ang(110f, 70f)
                };
                beatOffsets = new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5 };
                spawnsShockwave = new[] { true, false, false, true, false, false };
                maestroPoses = new[] { KnightMove.Light1, KnightMove.Light2, KnightMove.Light3, KnightMove.Light4, KnightMove.DashAtk, KnightMove.FrontHeavy };
            }
            else if (isPhase3)
            {
                strikeTargets = new[] { Ang(-35f, 215f), Ang(15f, 165f), Ang(110f, 70f) };
                beatOffsets = new[] { 1.0, 2.0, 3.0 };
                spawnsShockwave = new[] { true, false, false };
                maestroPoses = new[] { KnightMove.Light1, KnightMove.Light2, KnightMove.FrontHeavy };
            }
            else if (isPhase2)
            {
                strikeTargets = new[] { Ang(-35f, 215f), Ang(105f, 75f), Ang(-35f, 215f), Ang(105f, 75f) };
                beatOffsets = new[] { 1.0, 2.0, 3.0, 4.0 };
                spawnsShockwave = new[] { true, false, true, false };
                maestroPoses = null;
            }
            else
            {
                strikeTargets = new[] { Ang(-35f, 215f), Ang(-160f, -20f), Ang(15f, 165f), Ang(110f, 70f) };
                beatOffsets = new[] { 1.0, 2.0, 3.0, 4.0 };
                spawnsShockwave = new[] { false, false, false, false };
                maestroPoses = null;
            }

            tube.danger = true;
            int stepCount = strikeTargets.Length;

            for (int step = 0; step < stepCount; step++)
            {
                double beatTarget = startBeat + beatOffsets[step];
                double nextStepWaitEnd = step + 1 < stepCount
                    ? (startBeat + beatOffsets[step + 1] - leadBeats)
                    : (startBeat + (isPhase3 ? 4.25 : 5.0));
                double swingStart = beatTarget - leadBeats;
                double swingEnd = beatTarget + (isCompound68 ? 0.24 : 0.32);
                float startAng = curAngle;
                float targetAng = strikeTargets[step];
                bool hitDealtThisBeat = false;
                bool shockwaveSpawnedThisBeat = false;

                if (isPhase3 && maestroPoses != null) Kyosho.SetMaestroPose(maestroPoses[step], 0);
                KyoshoSfx.PlayCue(KyoshoSfx.Data?.conductWhoosh, "conduct_whoosh", Root());
                SpawnStrikeTelegraph(Root(), targetAng, batonLength, tipColor);
                if (spawnsShockwave[step] && (isPhase3 || hasPercussion))
                {
                    SpawnShockwaveTelegraph(Root(), targetAng, batonLength);
                }
                else
                {
                    ClearShockwaveTelegraph();
                }
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress((float)step / stepCount, $"{step + 1}/{stepCount}타");

                while (Clock.TotalBeats < nextStepWaitEnd)
                {
                    token.ThrowIfCancellationRequested();
                    double nowBeat = Clock.TotalBeats;
                    float swingFrac = Mathf.Clamp01((float)((nowBeat - swingStart) / Math.Max(0.05, swingEnd - swingStart)));
                    curAngle = Mathf.LerpAngle(startAng, targetAng, EaseOutQuad(swingFrac));
                    StepBaton(tube, Root(), curAngle, batonLength, Time.deltaTime);

                    if (!shockwaveSpawnedThisBeat)
                    {
                        UpdateStrikeTelegraph(Root(), targetAng, batonLength);
                        UpdateShockwaveTelegraph(Root(), targetAng, batonLength);
                    }

                    // 정박 판정 창
                    if (!hitDealtThisBeat && nowBeat >= beatTarget - 0.08 && nowBeat <= beatTarget + 0.25)
                    {
                        var player = FindPlayerObject();
                        if (player != null && KyoshoNanobotAttackHelper.CheckSegmentHitsPlayer(tube.nodes[3], tube.Tip, player.transform, 0.32f))
                        {
                            hitDealtThisBeat = true;
                            ApplyOriginalPatternHit(player, isPhase3 ? 210 : 190, BodyPart.RightArm, 25);
                        }
                    }

                    // 정박 통과 순간 시각/청각 강조 및 타악 충격파 + 3페 막타 부채꼴 음표 탄막
                    if (!shockwaveSpawnedThisBeat && nowBeat >= beatTarget)
                    {
                        shockwaveSpawnedThisBeat = true;
                        ClearStrikeTelegraph();
                        ClearShockwaveTelegraph();
                        KyoshoNanobotAttackHelper.SpawnPulseRingEffect(
                            new Vector3(tube.Tip.x, Mathf.Max(Kyosho.ArenaFloorY + 0.14f, tube.Tip.y), 0f),
                            0.25f, 1.35f, tipColor, 0.24f);
                        Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress((step + 1f) / stepCount, $"{step + 1}/{stepCount}타");
                        KyoshoSfx.PlayConductBeatHarmony(step, stepCount, isPhase2, isPhase3, isCompound68, tube.Tip);

                        if (spawnsShockwave[step] && (isPhase3 || hasPercussion))
                        {
                            SpawnPercussionShockwavePair(new Vector3(tube.Tip.x, Kyosho.ArenaFloorY, 0f));
                        }
                        if (isPhase3 && step == stepCount - 1)
                        {
                            SpawnPhase3FanBurst(tube.Tip, face, tipColor);
                        }
                    }

                    StepShockwaves(Time.deltaTime);
                    StepFanBullets(Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }

            ClearStrikeTelegraph();
            ClearShockwaveTelegraph();
            tube.danger = false;
            tube.glow = 0.2f;

            // 3) 회수 & 다음 패턴 예고
            double recoverStart = Clock.TotalBeats;
            float telegraphAngle = curAngle;
            bool showNextTelegraph = isPhase2 || isPhase3;
            if (showNextTelegraph)
            {
                var upcoming = Kyosho.PeekNextPhase2PatternType();
                telegraphAngle = upcoming == typeof(KyoshoStaccatoPatternGimmick) ? 180f : 90f;
                string nextName = upcoming == typeof(KyoshoStaccatoPatternGimmick) ? "현악 스타카토 (좌측 지시)" : "금관 페르마타 (상단 지시)";
                Color nextCol = upcoming == typeof(KyoshoStaccatoPatternGimmick) ? KyoshoConstants.StringsMintColor : KyoshoConstants.BrassMagentaColor;
                Kyosho.ScorePreviewHUD?.ShowPatternHint("다음 악장 예고", nextName, nextCol, 0f, "예고");
            }
            else
            {
                Kyosho.ScorePreviewHUD?.HidePatternHint();
            }
            if (isPhase3) Kyosho.SetMaestroPose(KnightMove.Guard2, 0);

            while (Clock.TotalBeats < recoverStart + 1.0)
            {
                token.ThrowIfCancellationRequested();
                float k = Mathf.Clamp01((float)(Clock.TotalBeats - recoverStart));
                if (showNextTelegraph)
                {
                    curAngle = Mathf.LerpAngle(curAngle, telegraphAngle, Time.deltaTime * 10f);
                    curLength = Mathf.Lerp(batonLength, 2.8f, Smooth(k));
                    tube.tint = Color.Lerp(tipColor,
                        telegraphAngle > 135f ? KyoshoConstants.StringsMintColor : KyoshoConstants.BrassMagentaColor,
                        0.65f);
                    Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(k, "전환");
                }
                else
                {
                    curLength = Mathf.Lerp(batonLength, 0.5f, Smooth(k));
                }
                StepBaton(tube, Root(), curAngle, curLength, Time.deltaTime);
                StepShockwaves(Time.deltaTime);
                StepFanBullets(Time.deltaTime);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            Kyosho.ScorePreviewHUD?.HidePatternHint();
            ReleaseBatonBots();
        }

        private void TakeBotsToTubeA(float frac, Vector3 near)
        {
            _batonBotIds.Clear();
            int srcGuide = (Kyosho != null && Kyosho.IsPhase3 && Swarm.guideCount[RSNanoSwarm.G_Halo] > 0)
                ? RSNanoSwarm.G_Halo
                : RSNanoSwarm.G_Blob;
            int n = Mathf.Clamp(Mathf.RoundToInt(Swarm.Count * frac), 100, Mathf.Max(100, Swarm.guideCount[srcGuide] / 2));
            Swarm.Collect(srcGuide, near, n, _batonBotIds);
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
                int returnGuide = (Kyosho != null && Kyosho.IsPhase3 && Swarm.guideCount[RSNanoSwarm.G_Halo] > 0)
                    ? RSNanoSwarm.G_Halo
                    : RSNanoSwarm.G_Blob;
                var bots = Swarm.Bots;
                foreach (int i in _batonBotIds)
                {
                    if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Bound && bots[i].guide == RSNanoSwarm.G_TubeA)
                    {
                        Swarm.Bind(i, returnGuide);
                    }
                }
                Swarm.TubeA.danger = false;
                Swarm.TubeA.glow = 0f;
                Swarm.TubeA.tint = Color.white;
            }
            _batonBotIds.Clear();
        }

        private void SpawnPhase3FanBurst(Vector3 origin, float face, Color color)
        {
            KyoshoSfx.PlayPhase3FanBurstHarmony(origin);
            Vector3 toPlayer = (Kyosho.GetPlayerAimCenter() - origin);
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = new Vector3(face, -0.25f, 0f);
            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float[] spread = { -22f, 0f, 22f };

            for (int i = 0; i < spread.Length; i++)
            {
                float rad = (baseAngle + spread[i]) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                    Swarm, origin, origin, 0f, dir * 13.5f, 1.8f,
                    12, 0.24f, color, 1.3f, 130, BodyPart.Core, 0, snapToHold: true);
                _fanBullets.Add(b);
            }
        }

        private void StepFanBullets(float dt)
        {
            if (_fanBullets.Count == 0) return;
            KyoshoNanobotAttackHelper.StepTrackedBullets(_fanBullets, Swarm, Kyosho.PlayerTransform, dt, b =>
            {
                var p = FindPlayerObject();
                if (p != null) ApplyCoreOnlyHit(p, b.Damage);
            });
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            ClearStrikeTelegraph();
            ClearShockwaveTelegraph();
            Kyosho?.ScorePreviewHUD?.HidePatternHint();
            ReleaseBatonBots();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_fanBullets, Swarm);
            foreach (var sw in _shockwaves)
            {
                if (sw.VisualGo != null) Destroy(sw.VisualGo);
            }
            _shockwaves.Clear();
        }

        private void SpawnStrikeTelegraph(Vector3 rootPos, float angleDeg, float length, Color color)
        {
            ClearStrikeTelegraph();
            _strikeTelegraphGo = KyoshoNanobotAttackHelper.SpawnGlowingRayHint(
                "KyoshoConductStrikeTelegraph", color, 0.36f, 0.10f, 16);
            UpdateStrikeTelegraph(rootPos, angleDeg, length);
        }

        private void UpdateStrikeTelegraph(Vector3 rootPos, float angleDeg, float length)
        {
            KyoshoNanobotAttackHelper.UpdateGlowingRayHint(_strikeTelegraphGo, rootPos, angleDeg, length);
        }

        private void ClearStrikeTelegraph()
        {
            if (_strikeTelegraphGo != null)
            {
                Destroy(_strikeTelegraphGo);
                _strikeTelegraphGo = null;
            }
        }

        private void SpawnShockwaveTelegraph(Vector3 rootPos, float angleDeg, float length)
        {
            ClearShockwaveTelegraph();
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 impactPos = new Vector3(rootPos.x + Mathf.Cos(rad) * length * 0.85f, Kyosho.ArenaFloorY + 0.08f, 0f);
            _shockwaveTelegraphGo = KyoshoNanobotAttackHelper.SpawnRingHint(
                "KyoshoConductShockwaveHint",
                impactPos,
                0.72f,
                KyoshoConstants.PercussionAmberColor,
                0.16f,
                0.90f,
                16,
                "SHOCK ▲",
                2.6f);
        }

        private void UpdateShockwaveTelegraph(Vector3 rootPos, float angleDeg, float length)
        {
            if (_shockwaveTelegraphGo == null) return;
            float rad = angleDeg * Mathf.Deg2Rad;
            _shockwaveTelegraphGo.transform.position = new Vector3(
                rootPos.x + Mathf.Cos(rad) * length * 0.85f,
                Kyosho.ArenaFloorY + 0.08f,
                0f);
        }

        private void ClearShockwaveTelegraph()
        {
            if (_shockwaveTelegraphGo != null)
            {
                Destroy(_shockwaveTelegraphGo);
                _shockwaveTelegraphGo = null;
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    }
}