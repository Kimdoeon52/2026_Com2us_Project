using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스패턴 2: 돌진 상태 (BossChargeState)]
    /// 1. 맵 한쪽 끝에서 반대편 끝까지 가로지르는 고속 횡단 돌진
    /// 2. 타이어 부위 파괴(펑크 2회 누적) 시 돌진 속도 영구 감속 (18m/s -> 11m/s)
    /// 3. 플레이어 충돌 시 Space 회피 무적(0.3s) 판정: 무적 중이면 통과, 아니면 큰 피해
    /// 4. 폭발 화물 상자 충돌 시 상자 자폭 및 보스 2초 스턴 즉시 인터럽트
    /// 5. 반대편 벽 충돌 후 기획서 공통 1.0초 확정 딜타임(BossIdleState)으로 자동 복귀
    /// </summary>
    public class BossChargeState : BossStateBase
    {
        private readonly BossPatternData _pattern; // 돌진 패턴 데이터
        private readonly float _chargeSpeed; // 돌진 속도 (m/s)
        private readonly float _brokenTireSpeed; // 타이어 부위 파괴 시 감속 속도 (m/s)
        private readonly float _anticipatedDuration; // 예상 돌진 지속 시간 (기본 11.0f초)
        private readonly float _wallStunDelay; // 벽 충돌 후 스턴 딜레이 (기본 0.2f)
        private readonly float _maxMapDistance; // 맵 가로 길이 (기본 22.0f)
        private readonly int _chargeDamage; // 돌진 충돌 시 플레이어에게 입히는 피해량 (기본 50)

        private Tweener _currentTween; // 현재 진행 중인 DOTween Tween 객체
        private bool _hasHitPlayer = false; // 플레이어 충돌 여부
        private float _chargeDirection = 1.0f; // 돌진 방향 (1.0f: 오른쪽, -1.0f: 왼쪽)
        
        public override string StateName => "BossChargeState (돌진 상태)";

        public BossChargeState(
            BossController boss, 
            CancellationToken token, 
            BossPatternData pattern, 
            float chargeSpeed = 18.0f,
            float brokenTireSpeed = 11.0f,
            float anticipatedDuration = 0.2f,
            float wallStunDelay = 0.3f,
            float maxMapDistance = 22.0f,
            int chargeDamage = 50) : base(boss, token)
        {
            _pattern = pattern;
            _chargeSpeed = chargeSpeed;
            _brokenTireSpeed = brokenTireSpeed;
            _anticipatedDuration = anticipatedDuration;
            _wallStunDelay = wallStunDelay;
            _maxMapDistance = maxMapDistance;
            _chargeDamage = chargeDamage;
        }
        public override void Enter()
        {
            _hasHitPlayer = false;
            DetermineChargeDirection();
            ExecuteChargeSequenceAsync().Forget();
        }
        public override void Exit()
        {
            base.Exit();
            KillCurrentTween();
        }

        private void DetermineChargeDirection()
        {
            Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (playerTransform != null)
            {
                _chargeDirection = playerTransform.position.x >= boss.transform.position.x ? 1.0f : -1.0f;
            }
            else
            {
                _chargeDirection = boss.transform.localScale.x >= 0 ? 1.0f : -1.0f; // 기본값: 오른쪽
            }
            // 보스 모델/스프라이트 x축 반전 정렬
            Vector3 localScale = boss.transform.localScale;
            boss.transform.localScale = new Vector3(Mathf.Abs(localScale.x) * _chargeDirection, localScale.y, localScale.z);
        }

        private async UniTaskVoid ExecuteChargeSequenceAsync()
        {
            try
            {
                Vector3 startPosition = boss.transform.position;

                // 1단계: 돌진 전 약간 뒤로 당기기 (0.4m)
                Vector3 pullBackPosition = startPosition - new Vector3(_chargeDirection * 0.4f, 0f, 0f);
                _currentTween = boss.transform.DOMoveX(pullBackPosition.x,_anticipatedDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);

                // 2단계: 타이어 부위 파괴(2회) 여부 검사 -> 돌진 속도 결정
                bool isTireBroken = false; // TODO: 실제 타이어 부위 파괴 여부 확인 로직 필요
                var tireGimmick = boss.Snapshot?.GetGimmick("TIRE_PART");
                if (tireGimmick != null && (tireGimmick.isBroken || tireGimmick.triggerCount >= 2))
                {
                    isTireBroken = true;
                }
                float finalSpeed = isTireBroken ? _brokenTireSpeed : _chargeSpeed;
                if (isTireBroken)
                {
                    Debug.Log($"[BossChargeState] 타이어 부위 파괴 감지됨. 돌진 속도 감속: {_chargeSpeed} -> {_brokenTireSpeed}");
                }

                // 3단계: 반대편 맵 끝(벽) x좌표 및 이동시간 계산
                float targetX = CalculateTargetWallX(startPosition.x, _chargeDirection);
                float distance = Mathf.Abs(targetX - boss.transform.position.x);
                float duration = distance / Mathf.Max(1.0f, finalSpeed);
                Debug.Log($"<color=red>[BossChargeState] ▶ 맵 횡단 돌진 개시! (방향: {_chargeDirection}, 속도: {finalSpeed}m/s, 거리: {distance:F1}m)</color>");

                // 4단계: 맵 끝까지 등속 고속 주행 (DOMoveX Linear) & 실시간 충돌 루프
                _currentTween = boss.transform.DOMoveX(targetX,duration).SetEase(Ease.Linear);

                bool hitExplosiveBox = false;

            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BossChargeState] 돌진 시퀀스 취소됨");
            }
        }

        private float CalculateTargetWallX(float x, float chargeDirection)
        {
            throw new NotImplementedException();
        }

        private void KillCurrentTween()
        {
            _currentTween?.Kill();
        }
    }

}
