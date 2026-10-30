using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 기믹 파훼 그로기 상태 (BossGroggyState)]
    /// 기획서 §6.18, §4.1:
    /// - 3단 내려찍기 중 고철 못판에 착지 시: 즉시 4초 그로기 전이.
    /// - 그로기 중: 보스 행동 완전 불가 + 플레이어의 공격 피해 1.5배 증폭 적용.
    /// - 지속 시간 종료 시: 기본 딜타임(IdleState)으로 복귀.
    /// </summary>
    public class BossGroggyState : BossStateBase
    {
        private readonly float _duration;
        private readonly float _damageMultiplier;

        public override string StateName => $"BossGroggyState ({_duration}초 그로기, 피해 {_damageMultiplier}배)";

        public BossGroggyState(BossControllerBase boss, CancellationToken token, float duration = 4.0f, float damageMultiplier = 1.5f)
            : base(boss, token)
        {
            _duration = duration;
            _damageMultiplier = damageMultiplier;
        }

        public override void Enter()
        {
            boss.SnapToGroundY();
            // 스냅샷 그로기 플래그 및 피해 증폭 배율 활성화
            boss.Snapshot.isGroggy = true;
            boss.Snapshot.currentDamageMultiplier = _damageMultiplier;
            boss.SetGroggyView(true, _duration);

            if (boss.Animation != null)
            {
                boss.Animation.PlayGroggy();
            }

            Debug.Log($"<color=magenta>[BossGroggyState] ▶ [기믹 파훼 성공!] 보스 {_duration}초간 그로기 상태 진입! (받는 피해 {_damageMultiplier}배 증폭)</color>");

            ExecuteGroggyTimerAsync().Forget();
        }

        private async UniTaskVoid ExecuteGroggyTimerAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_duration), cancellationToken: cancellationToken);

                if (!boss.Snapshot.IsAlive) return;

                Debug.Log("[BossGroggyState] 그로기 시간 만료 ➔ 확정 딜타임(IdleState) 복귀");
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
            boss.Snapshot.isGroggy = false;
            boss.Snapshot.currentDamageMultiplier = 1.0f;
            boss.SetGroggyView(false, 0f);
            boss.FacePlayer();
            Debug.Log("[BossGroggyState] 그로기 상태 해제 완료.");
        }
    }
}
