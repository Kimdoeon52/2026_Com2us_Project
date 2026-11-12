using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.FSM;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 정박 대기/전환 상태]
    /// 패턴 종료 후 박(Beat) 단위로 회복 딜타임을 대기하고,
    /// 모든 악기 섹션 파괴 시 +1박 보너스 딜타임을 부여하며, 다음 마디 첫 박(Downbeat)에 정확히 패턴을 개시함.
    /// </summary>
    public class KyoshoBeatIdleState : BossStateBase
    {
        private readonly KyoshoBossController _kyosho;
        private readonly float _baseRecoveryBeats;

        public override string StateName => $"KyoshoBeatIdle ({_baseRecoveryBeats:F1}박 대기)";

        public KyoshoBeatIdleState(KyoshoBossController kyosho, CancellationToken token, float recoveryBeats = 1f)
            : base(kyosho, token)
        {
            _kyosho = kyosho;
            _baseRecoveryBeats = Mathf.Max(0.5f, recoveryBeats);
        }

        public override void Enter()
        {
            _kyosho.FacePlayer();
            _kyosho.SetDriftEnabled(true);
            RunWaitAndNextPatternAsync().Forget();
        }

        public override void Exit()
        {
            _kyosho.SetDriftEnabled(false);
        }

        private async UniTaskVoid RunWaitAndNextPatternAsync()
        {
            try
            {
                var clock = _kyosho.BeatClock;
                float bonusBeats = _kyosho.GetAllSectionsBrokenPostDelayBonusBeats();
                float waitBeats = _baseRecoveryBeats + bonusBeats;

                if (clock != null && clock.IsRunning)
                {
                    await clock.WaitBeatsAsync(waitBeats, cancellationToken);
                    // 모든 패턴은 반드시 다음 마디 첫 박(Downbeat)에서 시작하여 리듬 정렬 보장
                    if (clock.BeatInBar != 0 || clock.BeatProgress01 > 0.15f)
                    {
                        await clock.WaitUntilNextBarAsync(cancellationToken);
                    }
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(waitBeats * 0.5f), cancellationToken: cancellationToken);
                }

                if (_kyosho.IsCombatStopped || _kyosho.Snapshot == null || !_kyosho.Snapshot.IsAlive) return;

                _kyosho.FacePlayer();
                var nextPattern = _kyosho.GetNextPatternGimmick();
                if (nextPattern != null)
                {
                    var token = _kyosho.RenewStateCancellationToken();
                    _kyosho.StateMachine.ChangeState(new KyoshoPatternExecutingState(_kyosho, token, nextPattern));
                    return;
                }

                // 폴백 재대기
                var retryToken = _kyosho.RenewStateCancellationToken();
                _kyosho.StateMachine.ChangeState(new KyoshoBeatIdleState(_kyosho, retryToken, 1f));
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    /// <summary>
    /// [쿄쇼 정박 패턴 실행 상태]
    /// 완료 후 BossIdleState 대신 KyoshoBeatIdleState로 복귀하여 박자 동기화를 유지함.
    /// </summary>
    public class KyoshoPatternExecutingState : BossStateBase
    {
        private readonly KyoshoBossController _kyosho;
        private readonly RealSteel.Boss.Core.BossPatternGimmickBase _gimmick;

        public override string StateName => _gimmick != null ? $"KyoshoPattern ({_gimmick.GimmickName})" : "KyoshoPattern";

        public KyoshoPatternExecutingState(
            KyoshoBossController kyosho,
            CancellationToken token,
            RealSteel.Boss.Core.BossPatternGimmickBase gimmick)
            : base(kyosho, token)
        {
            _kyosho = kyosho;
            _gimmick = gimmick;
        }

        public override void Enter()
        {
            if (_gimmick == null)
            {
                ReturnToBeatIdle();
                return;
            }

            _kyosho.NotifyPatternStarted(_gimmick.PatternData);
            ExecuteAsync().Forget();
        }

        private async UniTaskVoid ExecuteAsync()
        {
            RealSteel.Boss.Core.PatternExecutionResult result;
            try
            {
                result = await _gimmick.ExecutePatternSequenceAsync(cancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = RealSteel.Boss.Core.PatternExecutionResult.Cancelled;
            }

            if (result == RealSteel.Boss.Core.PatternExecutionResult.Completed)
            {
                ReturnToBeatIdle();
                return;
            }

            if (_kyosho.StateMachine.CurrentState == this && _kyosho.Snapshot != null && _kyosho.Snapshot.IsAlive && !_kyosho.IsCombatStopped)
            {
                ReturnToBeatIdle();
            }
        }

        private void ReturnToBeatIdle()
        {
            float beats = (_gimmick is KyoshoPatternGimmickBase kp) ? kp.PostDelayBeats : 1f;
            var token = _kyosho.RenewStateCancellationToken();
            _kyosho.StateMachine.ChangeState(new KyoshoBeatIdleState(_kyosho, token, beats));
        }
    }
}
