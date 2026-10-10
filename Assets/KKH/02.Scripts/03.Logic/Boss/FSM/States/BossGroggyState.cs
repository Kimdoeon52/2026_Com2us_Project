using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 기믹 파훼 그로기 상태 (BossGroggyState)]
    /// 고철 못판 착지(바퀴 펑크) 등 전장 기믹 파훼 성공 시 진입함.
    /// 기획서 §6.18 기준: 4.0초간 행동 불가 무력화 및 받는 피해 1.5배(크리티컬 취약) 적용.
    /// </summary>
    public class BossGroggyState : BossStateBase
    {
        private readonly float _duration;
        private readonly float _damageMultiplier;

        public override string StateName => $"BossGroggyState ({_duration}초 그로기, 피해 {_damageMultiplier}배)";

        public BossGroggyState(BossController boss, CancellationToken token, float duration = 4.0f, float damageMultiplier = 1.5f)
            : base(boss, token)
        {
            _duration = duration;
            _damageMultiplier = damageMultiplier;
        }

        public override void Enter()
        {
            // 스냅샷 그로기 플래그 및 피해 증폭 배율 활성화
            boss.Snapshot.isGroggy = true;
            boss.Snapshot.currentDamageMultiplier = _damageMultiplier;
            boss.SetGroggyView(true, _duration);

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
            // 그로기 및 피해 배율 정상 원복
            boss.Snapshot.isGroggy = false;
            boss.Snapshot.currentDamageMultiplier = 1.0f;
            boss.SetGroggyView(false, 0f);
        }
    }
}
