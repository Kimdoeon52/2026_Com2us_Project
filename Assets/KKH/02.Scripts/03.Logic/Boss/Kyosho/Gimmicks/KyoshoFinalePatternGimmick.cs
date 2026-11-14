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
        private int _pressureSlotIndex;

        private class SafeZoneEntry
        {
            public float CenterX;
            public GameObject Root;
            public SpriteRenderer Fill;
            public TextMeshPro Glyph;
        }

        private void Awake()
        {
            tellBeats = 12f;
            postDelayBeats = 1f;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            Kyosho.SetPhaseState(KyoshoPhaseState.Finale);
            Kyosho.SectionManager?.SetTemporarilyGatheredForFinale(true);

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            float floorY = Kyosho.ArenaFloorY;

            // 1마디(1~4박): 전 봇 본체 집결 & 바닥 쉼표(𝄽) 모래 안전지대 생성
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.finaleGather, "finale_gather", Swarm.Blob.center);
            Kyosho.ScorePreviewHUD?.ShowVariationStatus("피날레! 쉼표(𝄽) 모래 안전지대로 대피하세요!");

            SpawnSafeZones(KyoshoConstants.GetFinaleSafeZoneCount(Kyosho.SelectedDifficulty), minX, maxX, floorY);

            Vector3 gatherStart = Swarm.Blob.center;
            Vector3 gatherTarget = new Vector3(centerX, floorY + 2.8f, 0f);
            double bar1Start = Clock.TotalBeats;
            while (Clock.TotalBeats < bar1Start + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float k = Mathf.Clamp01((float)((Clock.TotalBeats - bar1Start) / 4.0));
                Swarm.Blob.center = Vector3.Lerp(gatherStart, gatherTarget, k);
                Swarm.Blob.glow = Mathf.Lerp(0.2f, 2.0f, k);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 2~3마디(5~12박): 소형 음표 압박 탄막 + 헬 난이도 3마디 첫 박 안전지대 이동
            double pressureStart = Clock.TotalBeats;
            double nextFireBeat = pressureStart + 0.5;
            bool movedForHell = false;
            bool isHell = Kyosho.SelectedDifficulty == BossDifficulty.Hell;

            while (Clock.TotalBeats < pressureStart + 8.0)
            {
                token.ThrowIfCancellationRequested();
                double elapsedBeats = Clock.TotalBeats - pressureStart;

                if (isHell && !movedForHell && elapsedBeats >= 4.0)
                {
                    movedForHell = true;
                    RelocateSingleSafeZoneToOpposite(minX, maxX);
                    Kyosho.ScorePreviewHUD?.ShowVariationStatus("안전지대 이동! 반대편 쉼표(// SAFE)로 이동하세요!");
                }

                if (Clock.TotalBeats >= nextFireBeat)
                {
                    nextFireBeat += isHell ? 0.85 : 1.25;
                    FirePressureNote(gatherTarget);
                }

                KyoshoNanobotAttackHelper.StepTrackedBullets(_pressureBullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandlePressureHit);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 4마디 첫 박(13박): 전 화면 화음 대폭발!
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_pressureBullets, Swarm);
            Kyosho.ScorePreviewHUD?.ShowVariationStatus(null);
            Swarm.Blob.glow = 0f;

            KyoshoSfx.PlayCue(KyoshoSfx.Data?.finaleFullBlast, "finale_blast", gatherTarget);
            KyoshoSfx.DuckBgm(0.5f, 0.25f);
            SpawnFullScreenBlastVisual(centerX, floorY + 2.5f, (maxX - minX) + 6f);

            var player = FindPlayerObject();
            bool survived = IsPlayerSafe(player);

            ClearSafeZones();
            await Clock.WaitBeatsAsync(0.5f, token);

            if (survived)
            {
                // 버팀 성공 ➔ 마지막 4박 그로기(최후의 일격 대기)
                Debug.Log("<color=green>[KyoshoFinale] 안전지대 생존 성공! 4박 마지막 그로기 ➔ 최후의 일격 대기!</color>");
                Kyosho.EnterFinalBlowGroggy();
            }
            else
            {
                // 버팀 실패 ➔ 코어 + 왼팔 내구도 35 차감 후 2페 1순환 뒤 피날레 재발동
                if (player != null)
                {
                    ApplyOriginalPatternHit(player, 260, BodyPart.LeftArm, 35);
                }
                Kyosho.SectionManager?.SetTemporarilyGatheredForFinale(false);
                Kyosho.ScheduleFinaleRetryAfterOneCycle();
                Debug.Log("<color=red>[KyoshoFinale] 피날레 피격! 2페이즈 1순환 후 피날레 재발동 예정!</color>");
            }
        }

        private bool IsPlayerSafe(GameObject player)
        {
            if (player == null) return true;
            if (IsPlayerInvincible(player)) return true;

            Vector3 p = player.transform.position;
            float halfW = safeZoneWidth * 0.5f;
            float floorY = Kyosho.ArenaFloorY;
            if (p.y > floorY + 2.2f) return false;

            for (int i = 0; i < _safeZones.Count; i++)
            {
                if (Mathf.Abs(p.x - _safeZones[i].CenterX) <= halfW)
                {
                    return true;
                }
            }
            return false;
        }

        private void SpawnSafeZones(int count, float minX, float maxX, float floorY)
        {
            ClearSafeZones();
            float playerX = Kyosho.PlayerTransform != null ? Kyosho.PlayerTransform.position.x : 0f;
            float leftX = minX + 2.2f;
            float rightX = maxX - 2.2f;

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

        private void RelocateSingleSafeZoneToOpposite(float minX, float maxX)
        {
            if (_safeZones.Count == 0) return;
            float mid = (minX + maxX) * 0.5f;
            float newX = _safeZones[0].CenterX < mid ? (maxX - 2.2f) : (minX + 2.2f);
            _safeZones[0].CenterX = newX;
            if (_safeZones[0].Root != null)
            {
                Vector3 pos = _safeZones[0].Root.transform.position;
                pos.x = newX;
                _safeZones[0].Root.transform.position = pos;
            }
        }

        private void CreateSafeZone(float x, float floorY)
        {
            var root = new GameObject($"FinaleSafeZone_{x:F1}");
            root.transform.position = new Vector3(x, floorY + 0.15f, -0.05f);

            var pillar = new GameObject("Pillar");
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            pillar.transform.localScale = new Vector3(safeZoneWidth, 2.2f, 1f);
            var sr = pillar.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = new Color(KyoshoConstants.SafeZoneGoldColor.r, KyoshoConstants.SafeZoneGoldColor.g, KyoshoConstants.SafeZoneGoldColor.b, 0.34f);
            sr.sortingOrder = 14;

            var glyphGo = new GameObject("RestGlyph");
            glyphGo.transform.SetParent(root.transform, false);
            glyphGo.transform.localPosition = new Vector3(0f, 1.35f, -0.05f);
            var tmp = glyphGo.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 4.2f;
            tmp.color = KyoshoConstants.SafeZoneGoldColor;
            tmp.text = "//\nSAFE";

            _safeZones.Add(new SafeZoneEntry { CenterX = x, Root = root, Fill = sr, Glyph = tmp });
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
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            if (Swarm != null) Swarm.Blob.glow = 0f;
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            ClearSafeZones();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_pressureBullets, Swarm);
        }
    }
}
