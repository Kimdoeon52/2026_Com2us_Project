using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.FSM;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 박(Beat) 기반 그로기 상태]
    /// 변주곡 파훼 성공 또는 피날레 생존 후 4박 그로기를 처리하며,
    /// 마지막 일격 대기(isFinalBlowWindow) 여부 및 완료 후 콜백을 지원함.
    /// </summary>
    public class KyoshoGroggyState : BossStateBase
    {
        private readonly KyoshoBossController _kyosho;
        private readonly float _beats;
        private readonly float _damageMultiplier;
        private readonly bool _isFinalBlowWindow;
        private readonly Action _onGroggyExpired;

        public override string StateName => _isFinalBlowWindow
            ? $"KyoshoFinalGroggy ({_beats:F0}박 최후의 일격 대기)"
            : $"KyoshoGroggy ({_beats:F0}박, 피해 {_damageMultiplier:F1}x)";

        public KyoshoGroggyState(
            KyoshoBossController kyosho,
            CancellationToken token,
            float beats = 4f,
            float damageMultiplier = 1.5f,
            bool isFinalBlowWindow = false,
            Action onGroggyExpired = null)
            : base(kyosho, token)
        {
            _kyosho = kyosho;
            _beats = Mathf.Max(1f, beats);
            _damageMultiplier = damageMultiplier;
            _isFinalBlowWindow = isFinalBlowWindow;
            _onGroggyExpired = onGroggyExpired;
        }

        public override void Enter()
        {
            float durationSec = _kyosho.BeatClock != null ? _kyosho.BeatClock.BeatsToSeconds(_beats) : _beats * 0.54f;

            _kyosho.Snapshot.isGroggy = true;
            _kyosho.Snapshot.currentDamageMultiplier = _damageMultiplier;
            _kyosho.IsWaitingFinalBlow = _isFinalBlowWindow;
            _kyosho.SetGroggyView(true, durationSec);
            _kyosho.SetBlobGroggyVisual(true);

            WaitBeatsAndExitAsync().Forget();
        }

        private async UniTaskVoid WaitBeatsAndExitAsync()
        {
            try
            {
                if (_kyosho.BeatClock != null && _kyosho.BeatClock.IsRunning)
                {
                    await _kyosho.BeatClock.WaitBeatsAsync(_beats, cancellationToken);
                }
                else
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_beats * 0.54f), cancellationToken: cancellationToken);
                }

                if (_kyosho.IsCombatStopped || _kyosho.Snapshot == null || !_kyosho.Snapshot.IsAlive) return;

                _onGroggyExpired?.Invoke();

                // 콜백에서 상태를 이미 전환하지 않았다면 BeatIdle로 복귀
                if (_kyosho.StateMachine.CurrentState == this)
                {
                    var token = _kyosho.RenewStateCancellationToken();
                    _kyosho.StateMachine.ChangeState(new KyoshoBeatIdleState(_kyosho, token, 1f));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public override void Exit()
        {
            if (_kyosho.Snapshot != null)
            {
                _kyosho.Snapshot.isGroggy = false;
                _kyosho.Snapshot.currentDamageMultiplier = 1.0f;
            }
            _kyosho.IsWaitingFinalBlow = false;
            _kyosho.SetGroggyView(false, 0f);
            _kyosho.SetBlobGroggyVisual(false);
            _kyosho.FacePlayer();
        }
    }
}
