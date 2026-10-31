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
            PatternExecutionResult result;
            try
            {
                result = await _gimmick.ExecutePatternSequenceAsync(cancellationToken);
            }
            catch (System.Exception e)
            {
                // 패턴 내부 예외로 UniTaskVoid가 중단되면 FSM이 영구 정지되므로 로그 후 복구
                Debug.LogException(e);
                result = PatternExecutionResult.Cancelled;
            }

            // 정상 완료된 경우 1.0초 확정 딜타임(BossIdleState)으로 복귀
            if (result == PatternExecutionResult.Completed)
            {
                ReturnToIdle();
                return;
            }

            // 인터럽트/취소로 끝났는데 스턴·그로기 등 다른 상태로 전이되지 않고 여전히 이 상태에 머물러 있다면
            // (예: 이미 퓨즈가 켜진 폭발 상자에 돌진 충돌 → 스턴 미발생) 루프가 멈추므로 Idle로 강제 복구
            if (boss.StateMachine.CurrentState == this && boss.Snapshot.IsAlive && !boss.IsCombatStopped)
            {
                Debug.LogWarning($"[BossPatternExecutingState] {_gimmick.GimmickName} 패턴이 {result}로 종료되었으나 후속 상태 전이가 없어 Idle로 복구합니다.");
                ReturnToIdle();
            }
        }

        private void ReturnToIdle()
        {
            var token = boss.RenewStateCancellationToken();
            float postDelay = boss.MasterData != null ? boss.MasterData.patternPostDelay : 1.0f;
            boss.StateMachine.ChangeState(new BossIdleState(boss, token, postDelay));
        }

        // 피격 대미지는 BossControllerBase.NotifyGimmicksDamageTaken()이 등록된 모든 기믹(실행 중 패턴 포함)에 전달함.
        // 여기서 _gimmick에 다시 전달하면 DPS 저지 게이지가 2배로 누적되므로 별도 전달하지 않음.
    }
}