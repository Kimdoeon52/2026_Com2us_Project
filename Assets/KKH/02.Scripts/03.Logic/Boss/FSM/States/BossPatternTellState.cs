using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 공격 패턴 전조 및 저지 체크 상태 (BossPatternTellState)]
    /// 기획서 명세 준수:
    /// 1. 약 2초간 전조 연출 (바퀴 헛돔 진동, 붉은색 점멸, 머리 위 300 게이지)
    /// 2. 전조 중 300 대미지 누적 시 패턴 차단 및 3초 스턴(BossStunState)으로 즉시 인터럽트
    /// 3. 저지 실패(2초 만료) 시 패턴 ID에 따라 해당 공격 상태(BossChargeState 등)로 자동 전이
    /// </summary>
    public class BossPatternTellState : BossStateBase
    {
        private readonly BossPatternData _pattern;
        private int _accumulatedDamage = 0;
        private bool _isInterrupted = false;

        private SpriteRenderer _spriteRenderer;
        private Color _originalColor = Color.white;
        private Tweener _flashTween;
        private Tweener _shakeTween;

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

            // 머리 위 저지 게이지 및 이벤트 통보
            boss.NotifyPatternStarted(_pattern);
            if (_pattern.hasDpsCheck)
            {
                boss.NotifyDpsCheckProgress(0, _pattern.requiredDamageToInterrupt);
            }

            // 기획서 연출: 바퀴 헛돔 진동 및 붉은색 점멸 개시
            StartTellVisualEffects();

            Debug.Log($"<color=yellow>[BossPatternTell] {_pattern.patternName} 2초 전조 시작! (저지 요구 피해량: {_pattern.requiredDamageToInterrupt})</color>");

            ExecuteTellTimerAsync().Forget();
        }

        public override void OnDamageTaken(int damage)
        {
            if (!_pattern.hasDpsCheck || _isInterrupted) return;

            _accumulatedDamage += damage;
            boss.NotifyDpsCheckProgress(_accumulatedDamage, _pattern.requiredDamageToInterrupt);

            Debug.Log($"[BossPatternTell] 저지 누적 대미지: {_accumulatedDamage} / {_pattern.requiredDamageToInterrupt}");

            // 2초 내 300 대미지 누적 달성 시 -> 3초 스턴 저지 성공!
            if (_accumulatedDamage >= _pattern.requiredDamageToInterrupt)
            {
                _isInterrupted = true;
                StopTellVisualEffects();

                Debug.Log("<color=cyan>[BossPatternTell] ▶ [저지 성공!] 누적 대미지 300 돌파! 돌진 차단 및 3초 스턴 발생!</color>");
                boss.InterruptToStun(_pattern.interruptStunDuration);
            }
        }

        private async UniTaskVoid ExecuteTellTimerAsync()
        {
            try
            {
                // 2초 전조 타이머 대기
                await UniTask.Delay(TimeSpan.FromSeconds(_pattern.tellDuration), cancellationToken: cancellationToken);

                if (_isInterrupted || !boss.Snapshot.IsAlive) return;

                StopTellVisualEffects();

                Debug.Log($"<color=red>[BossPatternTell] 2초 전조 만료 (저지 실패) ➔ {_pattern.patternName} 실행 개시!</color>");

                var nextToken = boss.RenewStateCancellationToken();

                // 패턴 ID별 구체 실행 State 분기 전이
                if (_pattern.patternID == "PATTERN_CHARGE")
                {
                    // 패턴 2: 맵 횡단 돌진 실행
                    boss.StateMachine.ChangeState(new BossChargeState(boss, nextToken, _pattern));
                }
                else if (_pattern.patternID == "PATTERN_JUMP_BARRAGE")
                {
                    // 패턴 1: 튀어오르기 탄막 실행
                    boss.StateMachine.ChangeState(new BossJumpBarrageState(boss, nextToken, _pattern));
                }
                else if (_pattern.patternID == "PATTERN_SLAM")
                {
                    // 패턴 3: 내려찍기 (아직 미구현이므로 기본 딜타임 복귀)
                    Debug.Log("[BossPatternTell] 내려찍기 패턴은 구현 후 연결됩니다.");
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, _pattern.postDelay));
                }
                else
                {
                    // 기타 기본 후딜 복귀
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, _pattern.postDelay));
                }
            }
            catch (OperationCanceledException)
            {
                StopTellVisualEffects();
            }
        }

        private void StartTellVisualEffects()
        {
            _spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
                // 붉은색 점멸 (엔진 과열)
                _flashTween = _spriteRenderer.DOColor(Color.red, 0.15f).SetLoops(-1, LoopType.Yoyo);
            }

            // 바퀴 헛돔 미세 진동 연출
            _shakeTween = boss.transform.DOShakePosition(_pattern.tellDuration, new Vector3(0.08f, 0f, 0f), 20);
        }

        private void StopTellVisualEffects()
        {
            if (_flashTween != null && _flashTween.IsActive())
            {
                _flashTween.Kill();
                _flashTween = null;
            }

            if (_shakeTween != null && _shakeTween.IsActive())
            {
                _shakeTween.Kill();
                _shakeTween = null;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalColor;
            }
        }

        public override void Exit()
        {
            StopTellVisualEffects();
            if (_pattern.hasDpsCheck)
            {
                boss.NotifyDpsCheckProgress(0, 0); // 머리 위 게이지 리셋
            }
        }
    }
}