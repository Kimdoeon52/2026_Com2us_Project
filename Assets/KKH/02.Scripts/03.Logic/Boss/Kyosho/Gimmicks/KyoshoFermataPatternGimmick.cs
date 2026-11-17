using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 4: 페르마타 (KyoshoFermataPatternGimmick)]
    /// 2페이즈 주력 탄막 패턴.
    /// - 1박: 상공(floorY + 5.5m)에 N발(기본 6~12발, 금관 생존 시 +4발 마젠타 코어 전용) 정지 배치
    /// - 2~3박: 늘임표(⌒) 점등 + 완전 정지 + 직하방 바닥 수직 낙하 예고선 표시
    /// - 4박: 일제 수직 낙하 (28 m/s) -> 원본 탄 오른다리(RightLeg) 내구도 25, 금관 추가 탄 코어 전용
    /// - 학습 순서 규칙: 2페 첫 페르마타는 무조건 기본판 ➔ 종료 후 떨어진 탄이 모여 금관 섹션(SECTION_BRASS) 생성
    /// - 이후 금관 생존 시 4.5박에 금관 섹션이 하강해 지면 수평 빔 1줄 발사 (점프 회피, 코어 전용)
    /// </summary>
    public class KyoshoFermataPatternGimmick : KyoshoPatternGimmickBase
    {
        [Header("페르마타 설정")]
        [SerializeField] private float deployHeight = 5.5f;
        [SerializeField] private float slamDropSpeed = 18f;
        [SerializeField] private int brassExtraCoreDamage = 135;
        [SerializeField] private int brassBeamCoreDamage = 155;

        private readonly List<KyoshoTrackedBullet> _bullets = new List<KyoshoTrackedBullet>();
        private readonly List<GameObject> _telegraphLines = new List<GameObject>();
        private GameObject _brassBeamVisual;
        private GameObject _brassBeamTelegraph;

        private void Awake()
        {
            postDelayBeats = 1.5f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            bool isPhase3 = Kyosho.IsPhase3;
            bool isFirstFermata = !isPhase3 && !Kyosho.HasCompletedFirstPhase2Fermata;
            bool hasBrass = !isPhase3 && !isFirstFermata && Kyosho.SectionManager != null && Kyosho.SectionManager.IsBrassAlive;

            int baseCount = KyoshoConstants.GetFermataBaseBulletCount(Kyosho.SelectedDifficulty) + (isPhase3 ? 3 : 0);
            int extraCount = hasBrass ? 4 : 0;
            int totalCount = baseCount + extraCount;

            Kyosho.TargetCombatCamera?.SetExternalFraming(true, 7.6f, deployHeight + 1.0f);
            Kyosho.ScorePreviewHUD?.ShowPatternHint(
                "2페이즈 페르마타 (⌒) — 낙하 틈새 대피!",
                hasBrass
                    ? "녹색 SAFE 틈새로 이동! · 낙하 후 금관 수평 빔 점프 주의"
                    : "녹색 SAFE 틈새로 이동해 수직 음표 낙하를 피하세요!",
                hasBrass ? KyoshoConstants.BrassMagentaColor : KyoshoConstants.OriginalPatternColor,
                0f,
                "배치 중");

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float floorY = Kyosho.ArenaFloorY;
            float airY = floorY + deployHeight;
            float safeMinX = minX + 0.9f;
            float safeMaxX = maxX - 0.9f;

            // 1박: 공중 배치 (2페: Ring / 3페: Halo 후광에서 음표 탄들을 상공 좌표에 흩어 배치)
            var ring = Swarm.Ring;
            ring.center = isPhase3
                ? (Swarm.Halo.center + Vector3.up * 0.6f)
                : (Swarm.Blob.center + Vector3.up * 1.4f);
            ring.axis = Vector3.up;
            ring.radius = 1.6f;
            ring.glow = 1.8f;
            if (isPhase3)
            {
                Kyosho.SetMaestroPose(KnightMove.AirLoop, 0);
                Swarm.Halo.glow = 1.6f;
            }

            KyoshoSfx.PlayFermataSpawnHarmony(totalCount, ring.center);

            float holdDuration = Clock.BeatsToSeconds(2.9f);
            List<float> safeXs;
            List<float> xPositions = BuildSpreadPositionsX(totalCount, safeMinX, safeMaxX, out safeXs);

            ClearTelegraphLines();
            for (int i = 0; i < totalCount; i++)
            {
                bool isBrassBonus = i >= baseCount;
                Vector3 holdPos = new Vector3(xPositions[i], airY, 0f);
                Color col = isBrassBonus ? KyoshoConstants.BrassMagentaColor : KyoshoConstants.OriginalPatternColor;
                int dmg = isBrassBonus ? brassExtraCoreDamage : 200;
                BodyPart part = isBrassBonus ? BodyPart.Core : BodyPart.RightLeg;
                int partDmg = isBrassBonus ? 0 : 25;

                var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                    Swarm,
                    ring.center,
                    holdPos,
                    holdDuration,
                    Vector3.down * slamDropSpeed,
                    2.5f,
                    20,
                    0.28f,
                    col,
                    isBrassBonus ? 1.6f : 1.0f,
                    dmg,
                    part,
                    partDmg,
                    snapToHold: false);

                _bullets.Add(b);
                SpawnDropTelegraph(xPositions[i], floorY, airY, col);
            }
            for (int i = 0; i < safeXs.Count; i++)
            {
                SpawnSafeGapTelegraph(safeXs[i], floorY);
            }

            double phaseStartBeat = Clock.TotalBeats;
            while (Clock.TotalBeats < phaseStartBeat + 1.0)
            {
                token.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01((float)((Clock.TotalBeats - phaseStartBeat) / 3.0));
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(p, "배치");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 2~3박: 페르마타(⌒) 정지 및 예고선 유지
            Kyosho.ScorePreviewHUD?.ShowFermataHold(true);
            KyoshoSfx.PlayFermataHoldHarmony(ring.center);

            while (Clock.TotalBeats < phaseStartBeat + 3.0)
            {
                token.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01((float)((Clock.TotalBeats - phaseStartBeat) / 3.0));
                float remainSec = Mathf.Max(0f, (float)((phaseStartBeat + 3.0 - Clock.TotalBeats) * Clock.BeatSec));
                Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(p, $"{remainSec:0.0}s");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            Kyosho.ScorePreviewHUD?.ShowFermataHold(false);
            Kyosho.ScorePreviewHUD?.UpdatePatternHintProgress(1f, "낙하!");
            ClearTelegraphLines();

            // 4박: 일제 수직 낙하 (Drop)
            if (isPhase3)
            {
                Kyosho.SetMaestroPose(KnightMove.FrontHeavy, 2);
                Swarm.Halo.glow = 0.25f;
            }
            else
            {
                Swarm.Blob.bulgeDir = Vector3.down;
                Swarm.Blob.bulge = 0.85f;
            }
            ring.glow = 0f;
            KyoshoSfx.PlayFermataDropResolution(ring.center);

            // 대기 타이머 즉시 0으로 맞춰 정박 동시 낙하 동기화
            foreach (var b in _bullets)
            {
                b.WaitTimer = 0f;
                foreach (int idx in b.BotIndices)
                {
                    if (idx >= 0 && idx < Swarm.Count && Swarm.Bots[idx].mode == BotMode.Free)
                    {
                        Swarm.Bots[idx].wait = 0f;
                        Swarm.Bots[idx].vel = Vector3.down * slamDropSpeed;
                    }
                }
            }

            double dropStart = Clock.TotalBeats;
            while (_bullets.Count > 0 && Clock.TotalBeats < dropStart + 1.5)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHit);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 락하 완료 후 금관 첫 생성 or 금관 수평 빔 (동시 체크메이트/공중 정지 방지)
            if (isFirstFermata)
            {
                Kyosho.MarkFirstPhase2FermataCompleted();
                Kyosho.SectionManager?.ActivateBrassSection();
            }
            else if (hasBrass && Kyosho.SectionManager != null && Kyosho.SectionManager.IsBrassAlive)
            {
                await FireBrassHorizontalBeamAsync(token);
            }
            else
            {
                await Clock.WaitBeatsAsync(0.45f, token);
            }

            CleanupEffects();
        }

        private async UniTask FireBrassHorizontalBeamAsync(CancellationToken token)
        {
            var brass = Kyosho.SectionManager?.BrassSection;
            if (brass == null || brass.IsBroken) return;

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float floorY = Kyosho.ArenaFloorY;
            Vector3 lowPos = new Vector3((minX + maxX) * 0.5f, floorY + 0.58f, 0f);
            brass.SetWorldPosition(lowPos);

            float width = (maxX - minX);
            Color col = KyoshoConstants.BrassMagentaColor;
            if (_brassBeamTelegraph != null) { Destroy(_brassBeamTelegraph); _brassBeamTelegraph = null; }
            _brassBeamTelegraph = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                "KyoshoBrassBeamTelegraph",
                new Vector3((minX + maxX) * 0.5f, floorY + 0.36f, -0.06f),
                width,
                0.68f,
                col,
                0.15f,
                0.85f,
                0.04f,
                "금관 수평 빔! 점프 회피 ▲",
                0f,
                3.0f,
                22);

            Kyosho?.ScorePreviewHUD?.ShowPatternHint(
                "금관 섹션 — 하단 수평 빔 예고!",
                "지금 점프(W / ↑)로 지면 수평 빔을 뛰어넘으세요!",
                col,
                0f,
                "발사 임박");
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus("금관 하단 빔 예고 — 점프로 회피!");

            double warnStart = Clock.TotalBeats;
            while (Clock.TotalBeats < warnStart + 0.85)
            {
                token.ThrowIfCancellationRequested();
                float p = Mathf.Clamp01((float)((Clock.TotalBeats - warnStart) / 0.85));
                Kyosho?.ScorePreviewHUD?.UpdatePatternHintProgress(p, "점프 ▲");
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (_brassBeamTelegraph != null) { Destroy(_brassBeamTelegraph); _brassBeamTelegraph = null; }

            _brassBeamVisual = new GameObject("KyoshoBrassHorizontalBeam");
            _brassBeamVisual.transform.position = new Vector3((minX + maxX) * 0.5f, floorY + 0.52f, -0.1f);
            _brassBeamVisual.transform.localScale = new Vector3(width, 0.48f, 1f);
            var sr = _brassBeamVisual.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = col;
            sr.sortingOrder = 24;

            KyoshoNanobotAttackHelper.SpawnPulseRingEffect(lowPos, 0.3f, 2.2f, col, 0.28f);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.brassBeam, "brass_beam", lowPos);
            Kyosho?.ScorePreviewHUD?.UpdatePatternHintProgress(1f, "빔 발사!");

            bool hitDealt = false;
            double beamEndBeat = Clock.TotalBeats + 0.40;
            while (Clock.TotalBeats < beamEndBeat)
            {
                token.ThrowIfCancellationRequested();
                if (!hitDealt)
                {
                    var player = FindPlayerObject();
                    if (player != null && player.transform.position.y <= floorY + 0.68f)
                    {
                        hitDealt = true;
                        // 금관 수평 빔은 코어 HP만 차감 (점프 회피)
                        ApplyCoreOnlyHit(player, brassBeamCoreDamage);
                    }
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            if (_brassBeamVisual != null) { Destroy(_brassBeamVisual); _brassBeamVisual = null; }
            brass.ResetToBasePosition();
        }

        private List<float> BuildSpreadPositionsX(int count, float minX, float maxX, out List<float> safeXs)
        {
            var list = new List<float>(count);
            safeXs = new List<float>(2);
            if (count <= 0) return list;

            // 최소 2개의 안전한 빈 슬롯을 보장해 플레이어가 확실한 회피 틈새를 읽을 수 있게 배치
            int totalSlots = count + 2;
            int safeSlotA = Mathf.Clamp(UnityEngine.Random.Range(1, Mathf.Max(2, totalSlots / 2)), 0, totalSlots - 1);
            int safeSlotB = Mathf.Clamp(UnityEngine.Random.Range(Mathf.Max(safeSlotA + 2, totalSlots / 2), totalSlots), 0, totalSlots - 1);
            if (safeSlotB == safeSlotA)
                safeSlotB = (safeSlotA + 2) % totalSlots;

            for (int slot = 0; slot < totalSlots; slot++)
            {
                float t = totalSlots > 1 ? slot / (float)(totalSlots - 1) : 0.5f;
                if (slot == safeSlotA || slot == safeSlotB)
                {
                    safeXs.Add(Mathf.Clamp(Mathf.Lerp(minX, maxX, t), minX, maxX));
                    continue;
                }
                if (list.Count >= count) continue;
                float jitter = UnityEngine.Random.Range(-0.14f, 0.14f);
                list.Add(Mathf.Clamp(Mathf.Lerp(minX, maxX, t) + jitter, minX, maxX));
            }
            return list;
        }

        private void SpawnDropTelegraph(float x, float floorY, float airY, Color color)
        {
            float height = Mathf.Max(0.5f, airY - floorY);
            var go = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                "FermataDropLine",
                new Vector3(x, (floorY + airY) * 0.5f, 0.05f),
                0.56f,
                height,
                color,
                0.15f,
                0.58f,
                0.025f,
                "▼",
                -(height * 0.5f - 0.35f),
                2.8f,
                10);
            _telegraphLines.Add(go);
        }

        private void SpawnSafeGapTelegraph(float x, float floorY)
        {
            var go = KyoshoNanobotAttackHelper.SpawnBorderedZoneHint(
                "FermataSafeZone",
                new Vector3(x, floorY + 1.15f, 0.04f),
                0.88f,
                2.20f,
                new Color(0.25f, 0.95f, 0.58f),
                0.14f,
                0.82f,
                0.045f,
                "SAFE 틈새",
                -0.65f,
                2.6f,
                12);
            _telegraphLines.Add(go);
        }

        private void HandleBulletHit(KyoshoTrackedBullet b)
        {
            var player = FindPlayerObject();
            if (player == null) return;

            if (b.TargetPart == BodyPart.Core || b.PartDurabilityDamage <= 0)
            {
                ApplyCoreOnlyHit(player, b.Damage);
            }
            else
            {
                ApplyOriginalPatternHit(player, b.Damage, b.TargetPart, b.PartDurabilityDamage);
            }
        }

        private void ClearTelegraphLines()
        {
            for (int i = 0; i < _telegraphLines.Count; i++)
            {
                if (_telegraphLines[i] != null) Destroy(_telegraphLines[i]);
            }
            _telegraphLines.Clear();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            Kyosho?.TargetCombatCamera?.SetExternalFraming(false);
            Kyosho?.ScorePreviewHUD?.HidePatternHint();
            Kyosho?.ScorePreviewHUD?.ShowFermataHold(false);
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            ClearTelegraphLines();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_bullets, Swarm);
            if (_brassBeamTelegraph != null) { Destroy(_brassBeamTelegraph); _brassBeamTelegraph = null; }
            if (_brassBeamVisual != null) { Destroy(_brassBeamVisual); _brassBeamVisual = null; }
            Kyosho?.SectionManager?.BrassSection?.ResetToBasePosition();
        }
    }
}
