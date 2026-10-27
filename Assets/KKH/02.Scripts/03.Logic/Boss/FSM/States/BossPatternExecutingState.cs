using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [단일 범용 패턴 실행 상태 (BossPatternExecutingState)]
    /// 전달받은 BossPatternGimmickBase의 템플릿 시퀀스를 실행하고 완료 시 1.0s 확정 딜타임(IdleState)으로 자동 복귀함.
    /// </summary>
    public class BossPatternExecutingState : BossStateBase
    {
        public override string StateName => _gimmick != null ? $"PatternExecuting ({_gimmick.GimmickName})" : "PatternExecuting";

        private readonly BossPatternGimmickBase _gimmick;

        public BossPatternExecutingState(BossControllerBase boss, CancellationToken token, BossPatternGimmickBase gimmick)
            : base(boss, token)
        {
            _gimmick = gimmick;
        }

        public override void Enter()
        {
            if (_gimmick == null)
            {
                Debug.LogWarning("[BossPatternExecutingState] 실행할 패턴 기믹이 null입니다! Idle 상태로 복귀합니다.");
                ReturnToIdle();
                return;
            }

            boss.NotifyPatternStarted(_gimmick.PatternData);
            ExecuteAsync().Forget();
        }

        private async UniTaskVoid ExecuteAsync()
        {
            var result = await _gimmick.ExecutePatternSequenceAsync(cancellationToken);

            // 정상 완료된 경우 1.0초 확정 딜타임(BossIdleState)으로 복귀
            if (result == PatternExecutionResult.Completed)
            {
                ReturnToIdle();
            }
        }

        private void ReturnToIdle()
        {
            var token = boss.RenewStateCancellationToken();
            float postDelay = boss.MasterData != null ? boss.MasterData.patternPostDelay : 1.0f;
            boss.StateMachine.ChangeState(new BossIdleState(boss, token, postDelay));
        }

        public override void OnDamageTaken(int damage)
        {
            _gimmick?.NotifyDamageTaken(damage);
        }
    }
}