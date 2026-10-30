using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 스턴 상태 (BossStunState)]
    /// 기획서 §6.18, §3.2 (3):
    /// - 돌진 패턴 2초 전조 중 300 DPS 저지 성공 시: 3.0초 스턴 전이.
    /// - 돌진 중 폭발 화물 상자 충돌 시: 2.0초 스턴 전이.
    /// - 스턴 중: 모든 패턴 실행 정지, 보스 조작 불가.
    /// - 지속 시간 종료 시: 확정 딜타임(IdleState)으로 복귀.
    /// </summary>
    public class BossStunState : BossStateBase
    {
        private readonly float _duration;

        public override string StateName => $"BossStunState ({_duration}초 스턴)";

        public BossStunState(BossControllerBase boss, CancellationToken token, float duration = 3.0f)
            : base(boss, token)
        {
            _duration = duration;
        }

        public override void Enter()
        {
            boss.SnapToGroundY();
            boss.SetStunView(true, _duration);

            if (boss.Animation != null)
            {
                boss.Animation.PlayStun();
            }

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
            boss.FacePlayer();
            Debug.Log("[BossStunState] 스턴 상태 해제 완료.");
        }
    }
}
