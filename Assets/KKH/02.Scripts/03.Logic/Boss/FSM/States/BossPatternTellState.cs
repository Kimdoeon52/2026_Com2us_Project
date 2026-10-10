using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 공격 패턴 전조 및 저지 체크 상태 (BossPatternTellState)]
    /// 공격 전 약 2초간 시각적/청각적 예고(바퀴 헛돔, 붉은 점멸)를 수행함.
    /// 돌진 등 저지 기믹(DPS 체크)이 있는 경우, 2초 동안 일정 피해(예: 300) 누적 시 3초 스턴으로 인터럽트함.
    /// </summary>
    public class BossPatternTellState : BossStateBase
    {
        private readonly BossPatternData _pattern;
        private int _accumulatedDamage = 0;
        private bool _isInterrupted = false;

        public override string StateName => $"BossPatternTellState ({_pattern.patternName} 전조)";

        public BossPatternTellState(BossController boss, CancellationToken token, BossPatternData pattern)
            : base(boss, token)
        {
            _pattern = pattern;
        }

        public override void Enter()
        {
            _accumulatedDamage = 0;
            _isInterrupted = false;

            boss.NotifyPatternStarted(_pattern);
            if (_pattern.hasDpsCheck)
            {
                boss.NotifyDpsCheckProgress(0, _pattern.requiredDamageToInterrupt);
            }

            Debug.Log($"<color=yellow>[BossPatternTell] {_pattern.patternName} 전조 시작 (예고 시간: {_pattern.tellDuration}초, 저지 기믹: {_pattern.hasDpsCheck})</color>");

            ExecuteTellTimerAsync().Forget();
        }

        public override void OnDamageTaken(int damage)
        {
            if (!_pattern.hasDpsCheck || _isInterrupted) return;

            _accumulatedDamage += damage;
            boss.NotifyDpsCheckProgress(_accumulatedDamage, _pattern.requiredDamageToInterrupt);

            Debug.Log($"[BossPatternTell] DPS 체크 누적 피해: {_accumulatedDamage}/{_pattern.requiredDamageToInterrupt}");

            // 저지 요구 대미지 달성 시 ➔ 패턴 차단 및 3초 스턴 발생!
            if (_accumulatedDamage >= _pattern.requiredDamageToInterrupt)
            {
                _isInterrupted = true;
                Debug.Log("<color=cyan>[BossPatternTell] ▶ [저지 성공!] 누적 대미지 임계치 돌파! 보스 3초 스턴 발생!</color>");
                boss.InterruptToStun(_pattern.interruptStunDuration);
            }
        }

        private async UniTaskVoid ExecuteTellTimerAsync()
        {
            try
            {
                // 전조 지속 시간 대기 (기획서: 약 2초)
                await UniTask.Delay(TimeSpan.FromSeconds(_pattern.tellDuration), cancellationToken: cancellationToken);

                if (_isInterrupted || !boss.Snapshot.IsAlive) return;

                Debug.Log($"[BossPatternTell] 전조 만료 ➔ {_pattern.patternName} 시전 개시!");

                // Phase 2 기본 플로우: 패턴 시전 후 딜타임 복귀 (Phase 3에서 세부 패턴 State로 확장)
                var token = boss.RenewStateCancellationToken();
                boss.StateMachine.ChangeState(new BossIdleState(boss, token, _pattern.postDelay));
            }
            catch (OperationCanceledException)
            {
                // 외부 인터럽트(스턴/그로기 등) 시 정상 취소
            }
        }

        public override void Exit()
        {
            if (_pattern.hasDpsCheck)
            {
                boss.NotifyDpsCheckProgress(0, 0); // 게이지 초기화
            }
        }
    }
}
