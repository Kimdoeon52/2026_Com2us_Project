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

        private void Awake()
        {
            tellBeats = 3f;
            postDelayBeats = 1.5f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            bool isFirstFermata = !Kyosho.HasCompletedFirstPhase2Fermata;
            bool hasBrass = !isFirstFermata && Kyosho.SectionManager != null && Kyosho.SectionManager.IsBrassAlive;

            int baseCount = KyoshoConstants.GetFermataBaseBulletCount(Kyosho.SelectedDifficulty);
            int extraCount = hasBrass ? 4 : 0;
            int totalCount = baseCount + extraCount;

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float floorY = Kyosho.ArenaFloorY;
            float airY = floorY + deployHeight;
            float safeMinX = minX + 0.9f;
            float safeMaxX = maxX - 0.9f;

            // 1박: 공중 배치 (RingGuide에서 음표 탄들을 상공 좌표에 흩어 배치)
            var ring = Swarm.Ring;
            ring.center = Swarm.Blob.center + Vector3.up * 1.4f;
            ring.axis = Vector3.up;
            ring.radius = 1.6f;
            ring.glow = 1.8f;

            KyoshoSfx.PlayCue(KyoshoSfx.Data?.fermataSpawn, "fermata_spawn", ring.center);

            float holdDuration = Clock.BeatsToSeconds(2.9f);
            List<float> xPositions = BuildSpreadPositionsX(totalCount, safeMinX, safeMaxX);

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

            await Clock.WaitBeatsAsync(1f, token);

            // 2~3박: 페르마타(⌒) 정지 및 예고선 유지
            Kyosho.ScorePreviewHUD?.ShowFermataHold(true);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.fermataHoldChord, "fermata_hold", Swarm.Blob.center);

            await Clock.WaitBeatsAsync(2f, token);

            Kyosho.ScorePreviewHUD?.ShowFermataHold(false);
            ClearTelegraphLines();

            // 4박: 일제 수직 낙하 (Drop)
            Swarm.Blob.bulgeDir = Vector3.down;
            Swarm.Blob.bulge = 0.85f;
            ring.glow = 0f;
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.fermataSlamDrop, "fermata_drop", Swarm.Blob.center);

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
            _brassBeamVisual = new GameObject("KyoshoBrassHorizontalBeam");
            _brassBeamVisual.transform.position = new Vector3((minX + maxX) * 0.5f, floorY + 0.52f, -0.1f);
            _brassBeamVisual.transform.localScale = new Vector3(width, 0.30f, 1f);
            var sr = _brassBeamVisual.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            Color col = KyoshoConstants.BrassMagentaColor;
            sr.color = new Color(col.r, col.g, col.b, 0.32f);
            sr.sortingOrder = 24;

            Kyosho?.ScorePreviewHUD?.ShowVariationStatus("금관 하단 빔 예고 — 점프로 회피!");
            await Clock.WaitBeatsAsync(0.85f, token);

            KyoshoSfx.PlayCue(KyoshoSfx.Data?.brassBeam, "brass_beam", lowPos);
            if (_brassBeamVisual != null)
            {
                _brassBeamVisual.transform.localScale = new Vector3(width, 0.46f, 1f);
                sr.color = col;
            }

            var player = FindPlayerObject();
            if (player != null && player.transform.position.y <= floorY + 0.68f)
            {
                // 금관 수평 빔은 코어 HP만 차감 (점프 회피)
                ApplyCoreOnlyHit(player, brassBeamCoreDamage);
            }

            await Clock.WaitBeatsAsync(0.4f, token);
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            if (_brassBeamVisual != null) { Destroy(_brassBeamVisual); _brassBeamVisual = null; }
            brass.ResetToBasePosition();
        }

        private List<float> BuildSpreadPositionsX(int count, float minX, float maxX)
        {
            var list = new List<float>(count);
            if (count <= 0) return list;

            // 최소 2개의 안전한 빈 슬롯을 보장해 플레이어가 확실한 회피 틈새를 읽을 수 있게 배치
            int totalSlots = count + 2;
            int safeSlotA = Mathf.Clamp(UnityEngine.Random.Range(1, Mathf.Max(2, totalSlots / 2)), 0, totalSlots - 1);
            int safeSlotB = Mathf.Clamp(UnityEngine.Random.Range(Mathf.Max(safeSlotA + 2, totalSlots / 2), totalSlots), 0, totalSlots - 1);
            if (safeSlotB == safeSlotA)
                safeSlotB = (safeSlotA + 2) % totalSlots;

            for (int slot = 0; slot < totalSlots && list.Count < count; slot++)
            {
                if (slot == safeSlotA || slot == safeSlotB) continue;
                float t = totalSlots > 1 ? slot / (float)(totalSlots - 1) : 0.5f;
                float jitter = UnityEngine.Random.Range(-0.14f, 0.14f);
                list.Add(Mathf.Clamp(Mathf.Lerp(minX, maxX, t) + jitter, minX, maxX));
            }
            return list;
        }

        private void SpawnDropTelegraph(float x, float floorY, float airY, Color color)
        {
            var go = new GameObject("FermataDropLine");
            go.transform.position = new Vector3(x, (floorY + airY) * 0.5f, 0.05f);
            go.transform.localScale = new Vector3(0.56f, Mathf.Max(0.5f, airY - floorY), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = new Color(color.r, color.g, color.b, 0.24f);
            sr.sortingOrder = 10;
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
            Kyosho?.ScorePreviewHUD?.ShowFermataHold(false);
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            ClearTelegraphLines();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_bullets, Swarm);
            if (_brassBeamVisual != null) { Destroy(_brassBeamVisual); _brassBeamVisual = null; }
            Kyosho?.SectionManager?.BrassSection?.ResetToBasePosition();
        }
    }
}
