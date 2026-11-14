using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 2: 스타카토 (KyoshoStaccatoPatternGimmick)]
    /// 1마디 콜(머리 위 악보 UI + 가이드 음) ➔ 2마디 리스폰스(동일 리듬 직선 음표 탄 발사).
    /// - 4분음표(♩): 48봇, 4.0 m/s, 0.55m / 8분음표(♪): 24봇, 7.0 m/s, 0.38m -> 머리(Head) 내구도 25 차감
    /// - 2페이즈 현악 섹션 생존 시: 16분음표(♬, 12봇, 10.0 m/s, 0.25m, 민트색) 좌측 현악 위치에서 +50% 추가 발사 (코어 HP 전용)
    /// </summary>
    public class KyoshoStaccatoPatternGimmick : KyoshoPatternGimmickBase
    {
        [Header("스타카토 데이터 & 설정")]
        [SerializeField] private KyoshoRhythmTableSO rhythmTable;
        [SerializeField] private int bonusCoreDamage = 115;

        private readonly List<KyoshoTrackedBullet> _bullets = new List<KyoshoTrackedBullet>();
        private readonly List<GameObject> _telegraphVisuals = new List<GameObject>();

        private void Awake()
        {
            tellBeats = 4f;
            postDelayBeats = 1.5f;
        }

        public void SetRhythmTable(KyoshoRhythmTableSO table) => rhythmTable = table;

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            if (rhythmTable == null)
            {
                rhythmTable = ScriptableObject.CreateInstance<KyoshoRhythmTableSO>();
            }

            bool isPhase2 = Kyosho.Snapshot != null && Kyosho.Snapshot.currentPhase >= 2;
            bool hasStrings = isPhase2 && Kyosho.SectionManager != null && Kyosho.SectionManager.IsStringsAlive;
            var bar = rhythmTable.PickRandomBar(Kyosho.SelectedDifficulty);

            await ExecuteCallAndResponseAsync(bar, hasStrings, token);
        }

        /// <summary>
        /// 정규 스타카토 및 헬 변주곡 동시 발사에서도 재사용 가능한 콜 & 리스폰스 2마디 루틴.
        /// </summary>
        public async UniTask ExecuteCallAndResponseAsync(KyoshoRhythmBar bar, bool allowStringsBonus, CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null || bar == null) return;

            var scoreHud = Kyosho.ScorePreviewHUD;
            scoreHud?.ShowStaccatoScore(bar, allowStringsBonus);

            // 1마디(4박): 콜 (악보 예고)
            double callStartBeat = Clock.TotalBeats;
            bool[] called = new bool[bar.notes.Count];

            while (Clock.TotalBeats < callStartBeat + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float localBeat = (float)(Clock.TotalBeats - callStartBeat);
                scoreHud?.UpdateStaccatoCursor(localBeat);

                for (int i = 0; i < bar.notes.Count; i++)
                {
                    if (called[i]) continue;
                    var ev = bar.notes[i];
                    if (localBeat >= ev.beatOffset)
                    {
                        called[i] = true;
                        if (ev.noteType != KyoshoNoteType.Rest)
                        {
                            KyoshoSfx.PlayStaccatoCall(ev.pitchIndex, Kyosho.transform.position);
                            Swarm.Blob.bulgeDir = Vector3.up;
                            Swarm.Blob.bulge = 0.45f;
                        }
                    }
                }

                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHitPlayer);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            scoreHud?.SetStaccatoPhase(true, allowStringsBonus);

            // 2마디(4박): 발사 방향 고정 (패턴 도중 재조준/유도 방지) + 상/하 고정 레인 예고선 표시
            float lockedDirX = Kyosho.GetFacingToPlayerSign();
            float floorY = Kyosho.ArenaFloorY;
            float lowLaneY = floorY + 0.56f;
            float highLaneY = floorY + 1.72f;
            float originX = Swarm.Blob.center.x + lockedDirX * 1.1f;
            Kyosho.GetArenaBounds(out float arenaMinX, out float arenaMaxX);
            float endX = lockedDirX > 0f ? arenaMaxX + 0.5f : arenaMinX - 0.5f;

            ClearTelegraphs();
            SpawnLaneTelegraph(originX, endX, lowLaneY, new Color(1.00f, 0.78f, 0.28f, 0.22f));
            SpawnLaneTelegraph(originX, endX, highLaneY, new Color(0.35f, 0.85f, 1.00f, 0.16f));

            var stringsSec = Kyosho.SectionManager?.StringsSection;
            Vector3 stringsOrigin = stringsSec != null
                ? new Vector3(stringsSec.AnchorWorldPosition.x + 0.4f, Mathf.Max(stringsSec.AnchorWorldPosition.y + 0.5f, floorY + 1.55f), 0f)
                : new Vector3(arenaMinX + 2.5f, floorY + 1.55f, 0f);
            float stringsDirX = (Kyosho.PlayerTransform != null && Kyosho.PlayerTransform.position.x < stringsOrigin.x) ? -1f : 1f;

            double fireStartBeat = Clock.TotalBeats;
            bool[] firedMain = new bool[bar.notes.Count];
            bool[] firedBonus = new bool[bar.notes.Count];
            bool[] rapidUseHighLane = new bool[bar.notes.Count];
            bool[] enableBonusForIndex = new bool[bar.notes.Count];

            // HUD 카드 배치와 정확히 일치하도록 8분/3연음표 교차 레인 및 16분 보너스(Quarter/Eighth +0.5박) 계산
            int rapidIndex = 0;
            for (int i = 0; i < bar.notes.Count; i++)
            {
                var n = bar.notes[i];
                if (n.noteType == KyoshoNoteType.Rest) continue;

                if (n.noteType == KyoshoNoteType.Eighth || n.noteType == KyoshoNoteType.Triplet)
                {
                    rapidUseHighLane[i] = (rapidIndex % 2) == 1;
                    rapidIndex++;
                }

                enableBonusForIndex[i] = allowStringsBonus
                    && (n.noteType == KyoshoNoteType.Quarter || n.noteType == KyoshoNoteType.Eighth)
                    && (n.beatOffset + 0.5f < 3.95f);
            }

            while (Clock.TotalBeats < fireStartBeat + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float localBeat = (float)(Clock.TotalBeats - fireStartBeat);
                scoreHud?.UpdateStaccatoCursor(localBeat);

                for (int i = 0; i < bar.notes.Count; i++)
                {
                    var ev = bar.notes[i];
                    if (ev.noteType == KyoshoNoteType.Rest) continue;

                    if (!firedMain[i] && localBeat >= ev.beatOffset)
                    {
                        firedMain[i] = true;
                        FireOriginalNoteBullet(ev.noteType, lockedDirX, rapidUseHighLane[i]);
                    }

                    if (enableBonusForIndex[i] && !firedBonus[i] && localBeat >= ev.beatOffset + 0.5f)
                    {
                        firedBonus[i] = true;
                        FireStringsSixteenthBullet(stringsDirX);
                    }
                }

                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHitPlayer);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 비행 중인 잔여 탄환이 바닥/플레이어에 닿을 때까지 최대 1.5박 추적 유지
            double tailStart = Clock.TotalBeats;
            while (_bullets.Count > 0 && Clock.TotalBeats < tailStart + 1.5)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHitPlayer);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            scoreHud?.HideStaccatoScore();
            ClearTelegraphs();
        }

        /// <summary>
        /// 헬 난이도 변주곡 중 백그라운드 기본판 스타카토 1회 발사용.
        /// </summary>
        public void FireSingleImmediateNote(KyoshoNoteType noteType)
        {
            FireOriginalNoteBullet(noteType);
        }

        public void StepActiveBullets(float dt)
        {
            KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho != null ? Kyosho.PlayerTransform : null, dt, HandleBulletHitPlayer);
        }

        private void FireOriginalNoteBullet(KyoshoNoteType noteType, float? fixedDirX = null, bool useHighLane = false)
        {
            if (Swarm == null || Kyosho == null) return;

            int clump;
            float speed;
            float radius;
            switch (noteType)
            {
                case KyoshoNoteType.Quarter:
                    clump = 38; speed = 4.6f; radius = 0.40f;
                    break;
                case KyoshoNoteType.Triplet:
                case KyoshoNoteType.Eighth:
                default:
                    clump = 20; speed = 6.6f; radius = 0.30f;
                    break;
            }

            float dirX = fixedDirX ?? Kyosho.GetFacingToPlayerSign();
            float laneY = Kyosho.ArenaFloorY + (useHighLane ? 1.72f : 0.56f);
            Vector3 origin = new Vector3(Swarm.Blob.center.x + dirX * 1.1f, laneY, 0f);
            Vector3 dir = new Vector3(dirX, 0f, 0f);

            var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                Swarm,
                origin,
                origin,
                0f,
                dir * speed,
                4.5f,
                clump,
                radius,
                KyoshoConstants.OriginalPatternColor,
                0.8f,
                160,
                BodyPart.Head,
                25,
                snapToHold: true);

            _bullets.Add(b);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.staccatoFire, "staccato_fire", origin);
        }

        private void FireStringsSixteenthBullet(float fixedDirX)
        {
            if (Swarm == null || Kyosho == null) return;
            var stringsSec = Kyosho.SectionManager?.StringsSection;
            float floorY = Kyosho.ArenaFloorY;
            Vector3 origin = stringsSec != null
                ? new Vector3(stringsSec.AnchorWorldPosition.x + 0.4f, Mathf.Max(stringsSec.AnchorWorldPosition.y + 0.5f, floorY + 1.55f), 0f)
                : new Vector3(Kyosho.ArenaMinX + 2.5f, floorY + 1.55f, 0f);

            Vector3 dir = new Vector3(fixedDirX, 0.08f, 0f).normalized;

            var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                Swarm,
                origin,
                origin,
                0f,
                dir * 8.2f,
                3.5f,
                12,
                0.24f,
                KyoshoConstants.StringsMintColor,
                1.4f,
                bonusCoreDamage,
                BodyPart.Core,
                0,
                snapToHold: true);

            _bullets.Add(b);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.stringsRapidFire, "strings_fire", origin);
        }

        private void SpawnLaneTelegraph(float startX, float endX, float y, Color color)
        {
            float length = Mathf.Abs(endX - startX);
            if (length < 0.25f) return;

            var go = new GameObject("KyoshoStaccatoLaneTelegraph");
            go.transform.position = new Vector3((startX + endX) * 0.5f, y, 0.05f);
            go.transform.localScale = new Vector3(length, 0.22f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
            sr.color = color;
            sr.sortingOrder = 14;
            _telegraphVisuals.Add(go);
        }

        private void ClearTelegraphs()
        {
            for (int i = _telegraphVisuals.Count - 1; i >= 0; i--)
            {
                if (_telegraphVisuals[i] != null)
                    Destroy(_telegraphVisuals[i]);
            }
            _telegraphVisuals.Clear();
        }

        private void HandleBulletHitPlayer(KyoshoTrackedBullet b)
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

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            Kyosho?.ScorePreviewHUD?.HideStaccatoScore();
            ClearTelegraphs();
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_bullets, Swarm);
        }
    }
}
