using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 스턴 무력화 상태 (BossStunState)]
    /// 돌진 전조 중 누적 피해 300 달성 시 또는 폭발 상자 자폭 충돌 시 진입함.
    /// 기획서 기준 3.0초간 행동 불가 상태가 되며, 만료 시 1.0초 확정 딜타임(IdleState)으로 복귀함.
    /// </summary>
    public class BossStunState : BossStateBase
    {
        private readonly float _duration;

        public override string StateName => $"BossStunState ({_duration}초 스턴)";

        public BossStunState(BossController boss, CancellationToken token, float duration = 3.0f)
            : base(boss, token)
        {
            _duration = duration;
        }

        public override void Enter()
        {
            boss.SetStunView(true, _duration);
            Debug.Log($"<color=cyan>[BossStunState] 보스 {_duration}초간 스턴 상태 진입!</color>");

            ExecuteStunTimerAsync().Forget();
        }

        private async UniTaskVoid ExecuteStunTimerAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_duration), cancellationToken: cancellationToken);

                if (!boss.Snapshot.IsAlive) return;

                Debug.Log("[BossStunState] 스턴 시간 만료 ➔ 확정 딜타임(IdleState) 복귀");
                var token = boss.RenewStateCancellationToken();
                boss.StateMachine.ChangeState(new BossIdleState(boss, token, boss.MasterData != null ? boss.MasterData.patternPostDelay : 1.0f));
            }
            catch (OperationCanceledException)
            {
                // 외부 인터럽트 시 정상 종료
            }
        }

        public override void Exit()
        {
            boss.SetStunView(false, 0f);
        }
    }
}
