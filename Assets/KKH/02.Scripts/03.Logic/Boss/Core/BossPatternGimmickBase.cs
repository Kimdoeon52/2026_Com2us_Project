using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// [공격 패턴 기믹 템플릿 기본 클래스]
    /// 템플릿 메서드 패턴을 적용하여 전조, 저지판정, 본 액션 시전, 확정 딜타임 복귀를 규격화 함
    /// </summary>
    public abstract class BossPatternGimmickBase : BossGimmickBase
    {
        [Header("패턴 기본 데이터")]
        [SerializeField] protected BossPatternData patternData;
        public BossPatternData PatternData => patternData;

        public override string GimmickID => patternData != null ? patternData.patternID : "UNKNOWN_PATTERN";
        public override string GimmickName => patternData != null ? patternData.patternName : "미지정 패턴";

        protected int _accumulatedTellDamage = 0;
        protected bool _isTellPhase = false;
        protected CancellationTokenSource _patternCTS;
        private bool _isInterrupted = false;
        private bool _isExecuting = false;
        public bool IsExecuting => _isExecuting && !_isInterrupted;

        /// <summary>
        /// [공격 패턴 전체 실행 시퀀스 골격]
        /// </summary>
        public async UniTask<PatternExecutionResult> ExecutePatternSequenceAsync(CancellationToken token)
        {
            _isInterrupted = false;
            _isExecuting = true;
            _patternCTS = CancellationTokenSource.CreateLinkedTokenSource(token);
            var linkedToken = _patternCTS.Token;

            try
            {
                // step1 : 사전 준비 (방향정렬, 웅크림 등등)
                OnPreExecute();
                if (token.IsCancellationRequested || OwnerBoss == null || OwnerBoss.IsCombatStopped || OwnerBoss.Snapshot == null || !OwnerBoss.Snapshot.IsAlive)
                {
                    return PatternExecutionResult.Cancelled;
                }
                if (_isInterrupted)
                {
                    return PatternExecutionResult.Interrupted;
                }

                // step2 : 전조 & DPS 체크 실행 (아주 쉬움 난이도는 tellDurationMultiplier 1.5배 적용)
                bool tellSuccess = await ExecuteTellPhaseAsync(linkedToken);
                if (token.IsCancellationRequested || OwnerBoss == null || OwnerBoss.IsCombatStopped || OwnerBoss.Snapshot == null || !OwnerBoss.Snapshot.IsAlive)
                {
                    return PatternExecutionResult.Cancelled;
                }
                if (!tellSuccess)
                {
                    // 300 누적 저지 성공 -> 3초 스턴 인터럽트
                    InterruptPattern(InterruptReason.DpsCheckStun);
                    return PatternExecutionResult.Interrupted;
                }
                if (_isInterrupted)
                {
                    return PatternExecutionResult.Interrupted;
                }

                // step3 : 본 공격 액션 시전 (완료 후 확정 딜타임 후딜은 BossIdleState가 전담)
                await ExecuteActionAsync(linkedToken);

                if (token.IsCancellationRequested || OwnerBoss == null || OwnerBoss.IsCombatStopped || OwnerBoss.Snapshot == null || !OwnerBoss.Snapshot.IsAlive)
                {
                    return PatternExecutionResult.Cancelled;
                }
                if (_isInterrupted)
                {
                    return PatternExecutionResult.Interrupted;
                }

                return PatternExecutionResult.Completed;
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested || OwnerBoss == null || OwnerBoss.IsCombatStopped || OwnerBoss.Snapshot == null || !OwnerBoss.Snapshot.IsAlive)
                {
                    return PatternExecutionResult.Cancelled;
                }
                return _isInterrupted ? PatternExecutionResult.Interrupted : PatternExecutionResult.Cancelled;
            }
            finally
            {
                _isExecuting = false;
                _isTellPhase = false;
                CleanupEffects();
                if (_patternCTS != null)
                {
                    _patternCTS.Dispose();
                    _patternCTS = null;
                }
            }
        }

        private async UniTask<bool> ExecuteTellPhaseAsync(CancellationToken token)
        {
            if (UseExternalTellPhase)
            {
                _isTellPhase = false;
                return true;
            }

            _isTellPhase = true;
            _accumulatedTellDamage = 0;   
            OnTellStarted();

            float baseDuration = patternData != null ? patternData.tellDuration : 2.0f;
            float multiplier = (OwnerBoss != null && OwnerBoss.Snapshot != null) ? OwnerBoss.Snapshot.tellDurationMultiplier : 1.0f;
            float duration = baseDuration * multiplier;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 외부 취소는 저지 성공(false)이 아니므로 예외로 전파하여 Cancelled 처리 (불필요한 스턴 덮어쓰기 방지)
                token.ThrowIfCancellationRequested();
                if (patternData != null && patternData.hasDpsCheck && _accumulatedTellDamage >= patternData.requiredDamageToInterrupt)
                {
                    OnTellEnded();
                    _isTellPhase = false;
                    return false; // 저지 성공 => 공격 취소
                }
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            OnTellEnded();
            _isTellPhase = false;
            return true; // 저지 실패 => 공격 계속 진행
        }

        protected override void OnDamageTaken(int damage)
        {
            if (_isTellPhase && patternData != null && patternData.hasDpsCheck)
            {
                _accumulatedTellDamage += damage;
            }
        }

        public virtual void InterruptPattern(InterruptReason reason)
        {
            if (_isExecuting && _isInterrupted) return;
            _isInterrupted = true;
            if (_patternCTS != null && !_patternCTS.IsCancellationRequested)
            {
                try
                {
                    _patternCTS.Cancel();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[{GetType().Name}] 패턴 토큰 취소 중 예외 격리: {e.Message}");
                }
            }
            OnInterrupted(reason);
            if (reason == InterruptReason.DpsCheckStun && OwnerBoss != null && !OwnerBoss.IsCombatStopped && OwnerBoss.Snapshot != null && OwnerBoss.Snapshot.IsAlive)
            {
                float stunDur = patternData != null ? patternData.interruptStunDuration : 3.0f;
                OwnerBoss.InterruptToStun(stunDur);
            }
        }

        protected static async UniTask WaitTweenSafeAsync(Tween tween, CancellationToken token)
        {
            if (tween == null) return;
            try
            {
                while (tween.IsActive() && !tween.IsComplete())
                {
                    token.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                if (tween.IsActive())
                {
                    tween.Kill(false);
                }
            }
        }

        //===============================================================================================
        // 하위 클래스 확장용 훅 메서드

        protected virtual bool UseExternalTellPhase => false;
        protected virtual void OnPreExecute() { }
        public virtual void OnTellStarted() { }
        public virtual void OnTellEnded() { }
        protected abstract UniTask ExecuteActionAsync(CancellationToken token);
        protected virtual void OnInterrupted(InterruptReason reason) { }
        protected virtual void CleanupEffects() { }

        //===============================================================================================
        // 공통 연동 헬퍼

        /// <summary>
        /// 지정 위치에 이펙트 프리팹을 생성하고 수명 종료 시 자동 파괴 처리함.
        /// </summary>
        protected GameObject SpawnVfx(
            GameObject vfxPrefab,
            Vector3 position,
            Quaternion rotation = default,
            Vector3? scale = null,
            float defaultLifetime = 2.0f,
            Transform parent = null)
        {
            return BossVfxHelper.SpawnVfx(vfxPrefab, position, rotation, scale, defaultLifetime, parent);
        }

        public bool IsPlayerInvincible(GameObject playerObj)
        {
            if (playerObj == null)
            {
                return false;
            }
            var executor = playerObj.GetComponent<ActionExecutor>() ?? playerObj.GetComponentInParent<ActionExecutor>();
            if (executor == null || executor.State == null) return false;

            // 1. HitDetection 공식 무적 판정 (Active 페이즈 여부 일치)
            if (HitDetection.IsInvincibleNow(executor)) return true;

            // 2. ActionData 무적 플래그 폴백
            return executor.State.CurrentAction != null && executor.State.CurrentAction.IsInvincibleDuringActive;
        }

        public bool ApplyDamageToPlayer(GameObject playerObj)
        {
            if (playerObj == null) return false;

            // Space 무적 회피 중인지 검사
            if (IsPlayerInvincible(playerObj))
            {
                return false;
            }

            int dmg = patternData != null ? patternData.baseDamage : 50;
            BodyPart targetPart = patternData != null ? patternData.targetPlayerPart : BodyPart.Core;
            int partDmg = patternData != null ? patternData.partDurabilityDamage : 0;

            // 기획서 §6.10.4: 난이도별 보스 공격 배율(attackMultiplier) 반영
            if (OwnerBoss != null && OwnerBoss.Snapshot != null)
            {
                float mult = OwnerBoss.Snapshot.attackMultiplier;
                dmg = Mathf.RoundToInt(dmg * mult);
                partDmg = Mathf.RoundToInt(partDmg * mult);
            }

            ApplyPlayerPartHit(dmg, targetPart, partDmg);
            return true;
        }

        protected void ApplyPlayerPartHit(int baseDmg, BodyPart targetPart, int partDmg)
        {
            var hub = CombatDataHub.Instance;
            if (hub == null) return;

            string attackerId = OwnerBoss != null && OwnerBoss.Snapshot != null ? OwnerBoss.Snapshot.bossID : "Boss";

            // 보스전 모드가 활성화되어 있으면 ProcessHit을 경유하여 플레이어 방어력(baseDefense) 감쇄 연산 적용
            if (hub.CurrentBattleMode == BattleMode.RobotVsBoss && hub.BossSnapshot != null && hub.PlayerSnapshot != null)
            {
                hub.ProcessHit(attackerId, "Player", baseDmg, targetPart, partDmg);
                return;
            }

            // 폴백: 보스전 모드가 아닌 단독 테스트 환경일 때 직접 차감
            if (targetPart != BodyPart.Core && partDmg > 0)
            {
                hub.ApplyPartHit("Player", targetPart, partDmg, baseDmg);
            }
            else
            {
                hub.ApplyCoreDamage("Player", baseDmg);
            }
        }
    }
}
