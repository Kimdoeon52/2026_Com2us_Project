using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 5: 피날레 (KyoshoFinalePatternGimmick)]
    /// 2페이즈 마무리 (HP 10% 도달 시 다음 마디 첫 박에 발동).
    /// - 1마디: 전 봇 집결 + 바닥 쉼표(𝄽) 모래 안전지대 생성 (폭 2.2m, 아주쉬움·쉬움 2곳 / 보통~헬 1곳)
    /// - 2~3마디: 화음 압박 탄막 + 헬 난이도는 3마디 첫 박에 안전지대 반대편 1회 이동
    /// - 4마디 첫 박: 전 화면 광역 판정 (왼팔 LeftArm 내구도 35)
    ///   · 생존: 마지막 4박 그로기 진입 ➔ 아무 공격 1회 적중 시 최후의 일격 승리!
    ///   · 피격: 코어 피해 + 왼팔 내구도 차감 ➔ HP 10% 유지한 채 2페 1순환 후 피날레 재발동
    /// </summary>
    public class KyoshoFinalePatternGimmick : KyoshoPatternGimmickBase
    {
        [Header("피날레 설정")]
        [SerializeField] private float safeZoneWidth = 2.8f;
        [SerializeField] private int pressureBulletDamage = 110;

        private readonly List<SafeZoneEntry> _safeZones = new List<SafeZoneEntry>();
        private readonly List<KyoshoTrackedBullet> _pressureBullets = new List<KyoshoTrackedBullet>();
        private GameObject _fullScreenBlastVisual;
        private GameObject _dangerFloorVisual;
        private GameObject _relocateHintVisual;
        private int _pressureSlotIndex;
        private int _melodyStepIndex;

        private class SafeZoneEntry
        {
            public float CenterX;
            public GameObject Root;
            public SpriteRenderer Fill;
            public TextMeshPro Glyph;
        }

        private void Awake()
        {
            postDelayBeats = 1f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            bool isPhase3 = Kyosho.IsPhase3;
            bool isHell = Kyosho.SelectedDifficulty == BossDifficulty.Hell;
            _melodyStepIndex = 0;
            Kyosho.SetPhaseState(KyoshoPhaseState.Finale);
            Kyosho.SetPatternPositionControl(true);
            Kyosho.TargetCombatCamera?.SetExternalFraming(true, 8.2f, 4.6f);
            if (!isPhase3) Kyosho.SectionManager?.SetTemporarilyGatheredForFinale(true);

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            float floorY = Kyosho.ArenaFloorY;
            float edgeInset = isHell ? 2.8f : 2.2f;

            // 1마디(1~4박): 전 봇/후광 집결 & 바닥 // SAFE 모래 안전지대 생성
            Vector3 initialPos = isPhase3 ? Swarm.Knight.ChestWorld : Swarm.Blob.center;
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.finaleGather, "finale_gather", initialPos);
            Kyosho.ScorePreviewHUD?.ShowPatternHint(
                isPhase3 ? "커튼콜 피날레 — 광역 대폭발!" : "피날레 — 광역 대폭발!",
                "전원 금색 // SAFE 안전지대 안으로 대피하세요!",
                KyoshoConstants.SafeZoneGoldColor,
                0f,
                "집결 중");

            SpawnDangerFloorBand(minX, maxX, floorY);
            SpawnSafeZones(KyoshoConstants.GetFinaleSafeZoneCount(Kyosho.SelectedDifficulty), minX, maxX, floorY, edgeInset);

            Vector3 gatherStart = isPhase3 ? Swarm.Knight.origin : Swarm.Blob.center;
            Vector3 gatherTarget = new Vector3(centerX, floorY + (isPhase3 ? 3.2f : 2.8f), 0f);
            Vector3 knightTargetOrigin = new Vector3(centerX, floorY, 0f);
            if (isPhase3) Kyosho.SetMaestroPose(KnightMove.Guard1, 0);

            double bar1Start = Clock.TotalBeats;
            while (Clock.TotalBeats < bar1Start + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float k = Mathf.Clamp01((float)((Clock.TotalBeats - bar1Start) / 4.0));
                if (isPhase3)
                {
                    Swarm.Knight.origin = Vector3.Lerp(gatherStart, knightTargetOrigin, k);
                    Swarm.Knight.UpdatePose();
                    Swarm.Halo.center = Vector3.Lerp(Swarm.Knight.ChestWorld, gatherTarget, k);
                    Swarm.Halo.spin += Time.deltaTime * 160f;
                    Swarm.Halo.glow = Mathf.Lerp(0.4f, 2.4f, k);
                }
                else
                {
                    Swarm.Blob.center = Vector3.Lerp(gatherStart, gatherTarget, k);
                    Swarm.Blob.glow = Mathf.Lerp(0.2f, 2.0f, k);
                }
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(k * 0.33f, "집결 중");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 2~3마디(5~12박): 소형 음표 압박 탄막 + 헬 난이도 2.0박 이동 예고 ➔ 3.0박 안전지대 이동
            double pressureStart = Clock.TotalBeats;
            double nextFireBeat = pressureStart + 0.5;
            bool warnedMoveForHell = false;
            bool movedForHell = false;

            while (Clock.TotalBeats < pressureStart + 8.0)
            {
                token.ThrowIfCancellationRequested();
                double elapsedBeats = Clock.TotalBeats - pressureStart;

                if (isHell && !warnedMoveForHell && elapsedBeats >= 2.0)
                {
                    warnedMoveForHell = true;
                    float targetSafeX = GetOppositeSafeZoneX(minX, maxX, edgeInset);
                    SpawnRelocatePreviewHint(targetSafeX, floorY);
                    Kyosho.ScorePreviewHUD?.ShowPatternHint(
                        "안전지대 이동 임박!",
                        "1박 뒤 안전지대가 점선 예고 위치로 이동합니다!",
                        new Color(1.00f, 0.55f, 0.25f),
                        0.33f + (float)(elapsedBeats / 8.0) * 0.67f,
                        "이동 예고");
                }

                if (isHell && !movedForHell && elapsedBeats >= 3.0)
                {
                    movedForHell = true;
                    ClearRelocatePreviewHint();
                    RelocateSingleSafeZoneToOpposite(minX, maxX, edgeInset);
                    Kyosho.ScorePreviewHUD?.ShowPatternHint(
                        "안전지대 반대편 이동 완료!",
                        "새 // SAFE 안전지대로 대피하세요!",
                        KyoshoConstants.SafeZoneGoldColor,
                        0.33f + (float)(elapsedBeats / 8.0) * 0.67f,
                        "대피!");
                }

                if (Clock.TotalBeats >= nextFireBeat)
                {
                    nextFireBeat += isHell ? 0.85 : 1.25;
                    FirePressureNote(gatherTarget);
                }

                float remainSec = Mathf.Max(0f, (float)((8.0 - elapsedBeats) * Clock.BeatSec));
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(
                    0.33f + Mathf.Clamp01((float)(elapsedBeats / 8.0)) * 0.67f,
                    $"폭발 {remainSec:0.0}s");

                KyoshoNanobotAttackHelper.StepTrackedBullets(_pressureBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandlePressureHit);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 4마디 첫 박(13박): 전 화면 화음 대폭발!
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_pressureBullets, Swarm);
            ClearRelocatePreviewHint();
            Kyosho.ScorePreviewHUD?.HidePatternHint();
            Swarm.Blob.glow = 0f;
            if (isPhase3)
            {
                Swarm.Halo.glow = 0.3f;
                Kyosho.SetMaestroPose(KnightMove.FrontHeavy, 2);
            }

            KyoshoSfx.PlayCue(KyoshoSfx.Data?.finaleFullBlast, "finale_blast", gatherTarget);
            KyoshoSfx.DuckBgm(0.5f, 0.25f);
            SpawnFullScreenBlastVisual(centerX, floorY + 2.5f, (maxX - minX) + 6f);

            var player = FindPlayerObject();
            bool survived = IsPlayerSafe(player);

            ClearSafeZones();
            Kyosho.SetPatternPositionControl(false);
            Kyosho.TargetCombatCamera?.SetExternalFraming(false);
            await Clock.WaitBeatsAsync(0.5f, token);

            if (survived)
            {
                if (isPhase3 && !Kyosho.IsAtFinaleHpGate)
                {
                    Kyosho.SetPhaseState(KyoshoPhaseState.Phase3);
                    Kyosho.EnterBeatGroggy(4f, 1.8f);
                }
                else
                {
                    // 버팀 성공 ➔ 마지막 4박 그로기(최후의 일격 대기)
                    Kyosho.EnterFinalBlowGroggy();
                }
            }
            else
            {
                // 버팀 실패 ➔ 코어 + 왼팔 내구도 35 차감
                if (player != null)
                {
                    ApplyOriginalPatternHit(player, 260, BodyPart.LeftArm, 35);
                }
                if (!isPhase3) Kyosho.SectionManager?.SetTemporarilyGatheredForFinale(false);

                if (!isPhase3 || Kyosho.IsAtFinaleHpGate)
                {
                    Kyosho.ScheduleFinaleRetryAfterOneCycle();
                }
                else
                {
                    Kyosho.SetPhaseState(KyoshoPhaseState.Phase3);
                }
            }
        }

        private bool IsPlayerSafe(GameObject player)
        {
            if (player == null) return true;

            Vector3 p = player.transform.position;
            float halfW = safeZoneWidth * 0.5f;
            float floorY = Kyosho.ArenaFloorY;
            if (p.y > floorY + 2.2f) return IsPlayerInvincible(player);

            for (int i = 0; i < _safeZones.Count; i++)
            {
                if (Mathf.Abs(p.x - _safeZones[i].CenterX) <= halfW)
                {
                    return true;
                }
            }

            return IsPlayerInvincible(player);
        }

        private void SpawnSafeZones(int count, float minX, float maxX, float floorY, float edgeInset)
        {
            ClearSafeZones();
            float playerX = Kyosho.PlayerTransform != null ? Kyosho.PlayerTransform.position.x : 0f;
            float leftX = minX + edgeInset;
            float rightX = maxX - edgeInset;

            if (count >= 2)
            {
                CreateSafeZone(leftX, floorY);
                CreateSafeZone(rightX, floorY);
            }
            else
            {
                // 플레이어로부터 반대쪽에 생성해 이동 유도를 유도
                float chosenX = playerX >= (minX + maxX) * 0.5f ? leftX : rightX;
                CreateSafeZone(chosenX, floorY);
            }
        }

        private float GetOppositeSafeZoneX(float minX, float maxX, float edgeInset)
        {
            if (_safeZones.Count == 0) return (minX + maxX) * 0.5f;
            float mid = (minX + maxX) * 0.5f;
            return _safeZones[0].CenterX < mid ? (maxX - edgeInset) : (minX + edgeInset);
        }

        private void RelocateSingleSafeZoneToOpposite(float minX, float maxX, float edgeInset)
        {
            if (_safeZones.Count == 0) return;
            float newX = GetOppositeSafeZoneX(minX, maxX, edgeInset);
            _safeZones[0].CenterX = newX;
            if (_safeZones[0].Root != null)
            {
                Vector3 pos = _safeZones[0].Root.transform.position;
                pos.x = newX;
                _safeZones[0].Root.transform.position = pos;
                KyoshoNanobotAttackHelper.SpawnPulseRingEffect(
                    new Vector3(newX, pos.y + 0.9f, -0.05f), 0.3f, 2.0f, KyoshoConstants.SafeZoneGoldColor, 0.30f);
            }
        }

        private void CreateSafeZone(float x, float floorY)
        {
            var root = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                $"FinaleSafeZone_{x:F1}",
                new Vector3(x, floorY + 1.15f, -0.05f),
                safeZoneWidth,
                2.30f,
                KyoshoConstants.SafeZoneGoldColor,
                0.28f,
                0.95f,
                0.055f,
                "// SAFE\n안전지대",
                0.25f,
                3.6f,
                14);
            var sr = root.transform.GetChild(0).GetComponent<SpriteRenderer>();
            var tmp = root.GetComponentInChildren<TextMeshPro>();

            _safeZones.Add(new SafeZoneEntry { CenterX = x, Root = root, Fill = sr, Glyph = tmp });
        }

        private void SpawnDangerFloorBand(float minX, float maxX, float floorY)
        {
            if (_dangerFloorVisual != null) Destroy(_dangerFloorVisual);
            _dangerFloorVisual = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                "FinaleDangerFloorBand",
                new Vector3((minX + maxX) * 0.5f, floorY + 0.08f, 0.02f),
                maxX - minX,
                0.16f,
                new Color(1.00f, 0.22f, 0.25f),
                0.28f,
                0.78f,
                0.03f,
                null,
                0f,
                2.5f,
                10);
        }

        private void SpawnRelocatePreviewHint(float targetX, float floorY)
        {
            ClearRelocatePreviewHint();
            _relocateHintVisual = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                "FinaleRelocatePreview",
                new Vector3(targetX, floorY + 1.15f, -0.03f),
                safeZoneWidth,
                2.25f,
                new Color(1.00f, 0.55f, 0.25f),
                0.10f,
                0.78f,
                0.045f,
                "이동 예정 ➔",
                0.20f,
                3.2f,
                13);
        }

        private void ClearRelocatePreviewHint()
        {
            if (_relocateHintVisual != null)
            {
                Destroy(_relocateHintVisual);
                _relocateHintVisual = null;
            }
        }

        private void FirePressureNote(Vector3 origin)
        {
            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float floorY = Kyosho.ArenaFloorY;

            // 플레이어 실시간 추적(유도) 제거: 안전지대 중심을 피해 균일 부채꼴 레인으로 교대 발사
            const int laneSlots = 7;
            float targetX = origin.x;
            for (int tries = 0; tries < laneSlots; tries++)
            {
                int slot = (_pressureSlotIndex + tries) % laneSlots;
                float candX = Mathf.Lerp(minX + 1.0f, maxX - 1.0f, slot / (float)(laneSlots - 1));
                bool insideSafeCore = false;
                foreach (var sz in _safeZones)
                {
                    if (Mathf.Abs(candX - sz.CenterX) <= safeZoneWidth * 0.42f)
                    {
                        insideSafeCore = true;
                        break;
                    }
                }
                if (!insideSafeCore)
                {
                    targetX = candX;
                    _pressureSlotIndex = (slot + 2) % laneSlots;
                    break;
                }
            }

            Vector3 dir = new Vector3(targetX - origin.x, floorY - origin.y, 0f);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.down;
            dir.Normalize();

            var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                Swarm, origin, origin, 0f, dir * 5.6f, 3.2f,
                14, 0.25f, KyoshoConstants.OriginalPatternColor, 1.1f,
                pressureBulletDamage, BodyPart.Core, 0, snapToHold: true);
            _pressureBullets.Add(b);
            KyoshoSfx.PlayFinalePressureMelody(_melodyStepIndex++, Kyosho.SelectedDifficulty == BossDifficulty.Hell, origin);
        }

        private void HandlePressureHit(KyoshoTrackedBullet b)
        {
            var player = FindPlayerObject();
            if (player != null)
            {
                ApplyCoreOnlyHit(player, b.Damage);
            }
        }

        private void SpawnFullScreenBlastVisual(float x, float y, float width)
        {
            _fullScreenBlastVisual = new GameObject("KyoshoFinaleFullBlast");
            _fullScreenBlastVisual.transform.position = new Vector3(x, y, -0.2f);
            _fullScreenBlastVisual.transform.localScale = new Vector3(width, 12f, 1f);
            var sr = _fullScreenBlastVisual.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = new Color(1f, 0.94f, 0.78f, 0.72f);
            sr.sortingOrder = 30;
            Destroy(_fullScreenBlastVisual, 0.35f);
        }

        private void ClearSafeZones()
        {
            for (int i = 0; i < _safeZones.Count; i++)
            {
                if (_safeZones[i].Root != null) Destroy(_safeZones[i].Root);
            }
            _safeZones.Clear();
            if (_dangerFloorVisual != null)
            {
                Destroy(_dangerFloorVisual);
                _dangerFloorVisual = null;
            }
            ClearRelocatePreviewHint();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            Kyosho?.SetPatternPositionControl(false);
            Kyosho?.TargetCombatCamera?.SetExternalFraming(false);
            Kyosho?.ScorePreviewHUD?.HidePatternHint();
            if (Swarm != null)
            {
                Swarm.Blob.glow = 0f;
                if (Kyosho != null && Kyosho.IsPhase3) Swarm.Halo.glow = 0.3f;
            }
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            ClearSafeZones();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_pressureBullets, Swarm);
        }
    }
}
