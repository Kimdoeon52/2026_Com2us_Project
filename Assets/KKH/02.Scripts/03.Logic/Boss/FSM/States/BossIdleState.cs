using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;


namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 딜타임 대기 상태 (BossIdleState)]
    /// 기획서 §6.18 공통 규칙: 모든 공격 패턴 종료 후 최소 1.0초간 확정 딜타임(대기)을 제공함.
    /// 딜타임 만료 시 다음 공격 패턴을 선택하여 전조(BossPatternTellState)로 전이함.
    /// </summary>
    public class BossIdleState : BossStateBase
    {
        private readonly float _delaySeconds;

        public override string StateName => "BossIdleState (딜타임)";

        public BossIdleState(BossControllerBase boss, CancellationToken token, float delaySeconds = 1.0f)
            : base(boss, token)
        {
            _delaySeconds = delaySeconds;
        }

        public override void Enter()
        {
            WaitPostDelayAndSelectNextPatternAsync().Forget();
        }

        private async UniTaskVoid WaitPostDelayAndSelectNextPatternAsync()
        {
            try
            {
                // 기획서 명시 확정 딜타임 대기
                await UniTask.Delay(TimeSpan.FromSeconds(_delaySeconds), cancellationToken: cancellationToken);

                if (!boss.Snapshot.IsAlive) return;

                // 1. 모듈식 패턴 기믹 조회
                var nextGimmick = boss.GetNextPatternGimmick();
                if (nextGimmick != null)
                {
                    var token = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossPatternExecutingState(boss, token, nextGimmick));
                    return;
                }

                // 2. 레거시 호환용 폴백
                var legacyBoss = boss as BossController;
                var nextPattern = legacyBoss?.GetNextPattern();
                if (nextPattern != null && legacyBoss != null)
                {
                    var token = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossPatternTellState(legacyBoss, token, nextPattern));
                    return;
                }

                Debug.LogWarning("[BossIdleState] 사용 가능한 보스 공격 패턴이 없습니다. 1초 재대기합니다.");
                Enter();
            }
            catch (OperationCanceledException)
            {
                // 인터럽트(기믹 파훼 등)로 인한 상태 취소 시 정상 종료
            }
        }
    }
}
