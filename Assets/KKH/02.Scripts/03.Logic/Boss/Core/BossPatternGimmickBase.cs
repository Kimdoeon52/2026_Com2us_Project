using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
        public override string GimmickName => patternData != null?patternData.patternName : "미지정 패턴";

        protected int _accumulatedTellDamage = 0;
        protected bool _isTellPhase = false;
        protected CancellationTokenSource _patternCTS;

        /// <summary>
        /// [공격 패턴 전체 실행 시퀀스 골격]
        /// </summary>
        public async UniTask<PatternExecutionResult> ExecutePatternSequenceAsync(CancellationToken token)
        {
            _patternCTS = CancellationTokenSource.CreateLinkedTokenSource(token);
            var linkedToken = _patternCTS.Token;

            try
            {
                // step1 : 사전 준비 (방향정렬, 웅크림 등등)
                OnPreExecute();

                // step2 : 2.0초 전조 & DPS 체크 실행
                bool tellSuccess = await ExecuteTellPhaseAsync(linkedToken);
                if (!tellSuccess)
                {
                    // 300 누적 저지 성공 -> 3초 스턴 인터럽트
                    InterruptPattern(InterruptReason.DpsCheckStun);
                    return PatternExecutionResult.Interrupted;
                }
                // step3 : 본 공격 액션 시전
                await ExecuteActionAsync(linkedToken);
                // step4 : 1.0초 확정 딜타임 복귀 전 후딜
                await ExecutePostDelayPhaseAsync(linkedToken);

                return PatternExecutionResult.Completed;
            }
            catch (OperationCanceledException)
            {
                return PatternExecutionResult.Cancelled;
            }
            finally
            {
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
            _isTellPhase = true;
            _accumulatedTellDamage = 0;   
            OnTellStarted();

            float duration = patternData != null ? patternData.tellDuration : 2.0f;
            float elapsed = 0f;
            while(elapsed < duration)
            {
                if (token.IsCancellationRequested)
                {return false;}
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

        private async UniTask ExecutePostDelayPhaseAsync(CancellationToken token)
        {
            float postDelay = patternData != null ? patternData.postDelay : 1.0f;
            if (postDelay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(postDelay), cancellationToken : token);
            }
        }

        protected override void OnDamageTaken(int damage)
        {
            if(_isTellPhase && patternData != null && patternData.hasDpsCheck)
            {
                _accumulatedTellDamage += damage;
                OnDPSCheckProgress(_accumulatedTellDamage, patternData.requiredDamageToInterrupt);
                OwnerBoss?.NotifyDPSCheckProgress(_accumulatedTellDamage, patternData.requiredDamageToInterrupt);
            }
        }
        public virtual void InterruptPattern(InterruptReason reason)
        {
            if (_patternCTS != null && !_patternCTS.IsCancellationRequested)
            {
                _patternCTS.Cancel();
            }
            OnInterrupted(reason);
            if (reason == InterruptReason.DpsCheckStun)
            {
                float stunDur = patternData != null ? patternData.interruptStunDuration : 3.0f;
                OwnerBoss?.InterruptToStun(stunDur);
            }
        }

        //===============================================================================================
        // 하위 클래스 확장용 훅 메서드

        protected virtual void OnPreExecute(){ }
        public virtual void OnTellStarted(){ }
        public virtual void OnTellEnded(){ }
        protected virtual void OnDPSCheckProgress(int currentDamage, int requiredDamage){ }
        protected abstract UniTask ExecuteActionAsync(CancellationToken token);
        protected virtual void OnInterrupted(InterruptReason reason) { }
        protected virtual void CleanupEffects(){ }
        
        //===============================================================================================
        // 공통 연동 헬퍼
        public bool IsPlayerInvincible(GameObject playerObj)
        {
            if (playerObj == null)
            {
                return false;
            }
            var executor = playerObj.GetComponent<ActionExecutor>() ?? playerObj.GetComponentInParent<ActionExecutor>();
            return executor != null && executor.State != null && executor.State.CurrentAction != null && executor.State.CurrentAction.IsInvincibleDuringActive;
        }

        public void ApplyDamageToPlayer(GameObject playerObj)
        {
            if (playerObj == null) return;

            // Space 무적 회피 중인지 검사
            if (IsPlayerInvincible(playerObj))
            {
                Debug.Log($"<color=cyan>[{GimmickName}] 플레이어가 Space 무적 회피로 공격을 회피했습니다!</color>");
                return;
            }

            int dmg = patternData != null ? patternData.baseDamage : 50;
            BodyPart targetPart = patternData != null ? patternData.targetPlayerPart : BodyPart.Core;
            int partDmg = patternData != null ? patternData.partDurabilityDamage : 0;

            // 기획서 §12.7 및 §5.3: 부위 타격 스킬인 경우 파츠 내구도와 코어 HP를 동시 타격
            if (targetPart != BodyPart.Core && partDmg > 0)
            {
                Debug.Log($"<color=red>[{GimmickName}] 플레이어 {targetPart} 부위 타격! (파츠 피해: {partDmg}, 코어 피해: {dmg})</color>");
                if (CombatDataHub.Instance != null)
                {
                    CombatDataHub.Instance.ApplyPartHit("Player", targetPart, partDmg, dmg);
                }
            }
            else
            {
                Debug.Log($"<color=red>[{GimmickName}] 플레이어 코어 타격! (피해량: {dmg})</color>");
                if (CombatDataHub.Instance != null)
                {
                    CombatDataHub.Instance.ApplyCoreDamage("Player", dmg);
                }
            }
        }

        protected void ApplyPlayerPartHit(int baseDmg, BodyPart targetPart, int partDmg)
        {
            if(CombatDataHub.Instance != null)
            {
                CombatDataHub.Instance.ApplyPartHit("Player", targetPart, partDmg, baseDmg);
            }
        }
    }
}
