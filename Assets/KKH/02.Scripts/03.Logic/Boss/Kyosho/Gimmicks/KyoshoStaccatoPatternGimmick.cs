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

        private void Awake()
        {
            tellBeats = 4f;
            postDelayBeats = 1f;
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

            scoreHud?.HideStaccatoScore();

            // 2마디(4박): 리스폰스 (동일 타이밍 발사 + 현악 16분음표 추가타)
            double fireStartBeat = Clock.TotalBeats;
            bool[] firedMain = new bool[bar.notes.Count];
            bool[] firedBonus = new bool[bar.notes.Count];

            // +50% 규칙: 휴지부가 아닌 음표 중 홀수/2개당 1개 이상 16분음표 추가
            int activeNoteOrdinal = 0;
            bool[] enableBonusForIndex = new bool[bar.notes.Count];
            for (int i = 0; i < bar.notes.Count; i++)
            {
                if (bar.notes[i].noteType == KyoshoNoteType.Rest) continue;
                enableBonusForIndex[i] = allowStringsBonus && (activeNoteOrdinal % 2 == 0);
                activeNoteOrdinal++;
            }

            while (Clock.TotalBeats < fireStartBeat + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float localBeat = (float)(Clock.TotalBeats - fireStartBeat);

                for (int i = 0; i < bar.notes.Count; i++)
                {
                    var ev = bar.notes[i];
                    if (ev.noteType == KyoshoNoteType.Rest) continue;

                    if (!firedMain[i] && localBeat >= ev.beatOffset)
                    {
                        firedMain[i] = true;
                        FireOriginalNoteBullet(ev.noteType);
                    }

                    if (enableBonusForIndex[i] && !firedBonus[i] && localBeat >= ev.beatOffset + 0.25f)
                    {
                        firedBonus[i] = true;
                        FireStringsSixteenthBullet();
                    }
                }

                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHitPlayer);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 비행 중인 잔여 탄환이 바닥/플레이어에 닿을 때까지 최대 1박 추적 유지
            double tailStart = Clock.TotalBeats;
            while (_bullets.Count > 0 && Clock.TotalBeats < tailStart + 1.5)
            {
                token.ThrowIfCancellationRequested();
                KyoshoNanobotAttackHelper.StepTrackedBullets(_bullets, Swarm, Kyosho.PlayerTransform, Time.deltaTime, HandleBulletHitPlayer);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
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

        private void FireOriginalNoteBullet(KyoshoNoteType noteType)
        {
            if (Swarm == null || Kyosho == null) return;

            int clump;
            float speed;
            float radius;
            switch (noteType)
            {
                case KyoshoNoteType.Quarter:
                    clump = 48; speed = 4.0f; radius = 0.55f;
                    break;
                case KyoshoNoteType.Triplet:
                case KyoshoNoteType.Eighth:
                default:
                    clump = 24; speed = 7.0f; radius = 0.38f;
                    break;
            }

            Vector3 origin = Swarm.Blob.center + new Vector3(Kyosho.GetFacingToPlayerSign() * 1.1f, 0.3f, 0f);
            Vector3 playerCenter = Kyosho.GetPlayerAimCenter();
            Vector3 dir = (playerCenter - origin);
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.left;
            dir.Normalize();

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

        private void FireStringsSixteenthBullet()
        {
            if (Swarm == null || Kyosho == null) return;
            var stringsSec = Kyosho.SectionManager?.StringsSection;
            Vector3 origin = stringsSec != null
                ? stringsSec.AnchorWorldPosition + new Vector3(0.4f, 0.4f, 0f)
                : new Vector3(Kyosho.ArenaMinX + 2.5f, Kyosho.ArenaFloorY + 1.2f, 0f);

            Vector3 playerCenter = Kyosho.GetPlayerAimCenter();
            Vector3 dir = (playerCenter - origin);
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            dir.Normalize();

            var b = KyoshoNanobotAttackHelper.FireTrackedClump(
                Swarm,
                origin,
                origin,
                0f,
                dir * 10.0f,
                3.5f,
                12,
                0.25f,
                KyoshoConstants.StringsMintColor,
                1.4f,
                bonusCoreDamage,
                BodyPart.Core,
                0,
                snapToHold: true);

            _bullets.Add(b);
            KyoshoSfx.PlayCue(KyoshoSfx.Data?.stringsRapidFire, "strings_fire", origin);
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
            KyoshoNanobotAttackHelper.ClearTrackedBullets(_bullets, Swarm);
        }
    }
}
