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
    /// 난이도별 패시브 및 페이즈 연동:
    /// - 어려움(Hard) 2페이즈: 과열 가속 패시브로 딜타임 20% 감소 (1.0s -> 0.8s)
    /// - 헬(Hell) 2페이즈: 패턴 연속 연계로 딜타임 0.4초 단축
    /// </summary>
    public class BossIdleState : BossStateBase
    {
        private readonly float _delaySeconds;

        public override string StateName => $"BossIdleState (딜타임 {_delaySeconds:F2}s)";

        public BossIdleState(BossControllerBase boss, CancellationToken token, float delaySeconds = 1.0f)
            : base(boss, token)
        {
            float finalDelay = delaySeconds;
            if (boss != null && boss.Snapshot != null && boss.Snapshot.currentPhase >= 2)
            {
                // 기획서 §6.10.4: 헬 난이도 2페이즈 패턴 연계 후딜 0.4초
                if (boss.Snapshot.allowPatternChain)
                {
                    finalDelay = 0.4f;
                }
                // 기획서 §6.10.4: 어려움 난이도 1종 패시브 "과열 가속" (후딜 -20%)
                else if (boss.Snapshot.hasOverheatAcceleration)
                {
                    finalDelay = delaySeconds * 0.8f;
                }
            }
            _delaySeconds = finalDelay;
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
                // 확정 딜타임 대기 (대기 중 플레이어가 뒤로 이동하면 자연스럽게 몸을 돌려 응시)
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
