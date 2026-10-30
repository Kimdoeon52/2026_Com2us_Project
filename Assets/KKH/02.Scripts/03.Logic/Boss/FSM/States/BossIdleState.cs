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
    /// 딜타임 대기 중에는 실시간으로 플레이어를 주시(FacePlayer)하며,
    /// 딜타임 만료 시 다음 공격 패턴을 선택하여 실행/전조로 전이함.
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
            // 진입 즉시 지면 높이 보장, 플레이어 응시 및 Idle 모션 재생
            boss.SnapToGroundY();
            boss.FacePlayer();

            if (boss.Animation != null)
            {
                boss.Animation.PlayIdle();
            }

            WaitPostDelayAndSelectNextPatternAsync().Forget();
        }

        private async UniTaskVoid WaitPostDelayAndSelectNextPatternAsync()
        {
            try
            {
                // 기획서 명시 확정 딜타임 대기 (대기 중 플레이어가 뒤로 이동하면 자연스럽게 몸을 돌려 응시)
                float elapsed = 0f;
                float interval = 0.1f;
                while (elapsed < _delaySeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    boss.FacePlayer();

                    float step = Mathf.Min(interval, _delaySeconds - elapsed);
                    await UniTask.Delay(TimeSpan.FromSeconds(step), cancellationToken: cancellationToken);
                    elapsed += step;
                }

                if (!boss.Snapshot.IsAlive) return;

                boss.FacePlayer();

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
                var retryToken = boss.RenewStateCancellationToken();
                boss.StateMachine.ChangeState(new BossIdleState(boss, retryToken, 1.0f));
            }
            catch (OperationCanceledException)
            {
                // 외부 인터럽트 시 정상 종료
            }
        }
    }
}
