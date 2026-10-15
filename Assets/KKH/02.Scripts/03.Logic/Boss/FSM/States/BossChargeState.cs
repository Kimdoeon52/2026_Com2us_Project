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
    /// 2. 타이어 부위 파괴(펑크 2회 누적) 시 돌진 속도 영구 감속
    /// 3. 플레이어 충돌 시 Space 회피 무적(0.3s) 판정: 무적 중이면 통과, 아니면 피해
    /// 4. 폭발 화물 상자 충돌 시 상자 자폭 및 보스 2초 스턴 즉시 인터럽트
    /// 5. 충돌(플레이어/벽)이 완전히 끝나고 겹침이 해제되었을 때 등 뒤의 플레이어를 향해 스프라이트 반전(Flip)
    /// </summary>
    public class BossChargeState : BossStateBase
    {
        private readonly BossPatternData _pattern; // 돌진 패턴 데이터
        private readonly float _chargeSpeed; // 돌진 속도 (m/s)
        private readonly float _brokenTireSpeed; // 타이어 부위 파괴 시 감속 속도 (m/s)
        private readonly float _anticipatedDuration; // 예열 시간
        private readonly float _wallStunDelay; // 벽 충돌 후 스턴 딜레이
        private readonly float _maxMapDistance; // 맵 횡단 거리
        private readonly int _chargeDamage; // 돌진 피해량

        private Tweener _currentTween;
        private bool _hasHitPlayer = false;
        private float _chargeDirection = 1.0f; // 1.0f: 오른쪽, -1.0f: 왼쪽
        private Transform _playerTransform;

        public override string StateName => "BossChargeState (돌진 상태)";

        public BossChargeState(
            BossController boss, 
            CancellationToken token, 
            BossPatternData pattern, 
            float chargeSpeed = 11.0f,
            float brokenTireSpeed = 5.5f,
            float anticipatedDuration = 0.2f,
            float wallStunDelay = 0.3f,
            float maxMapDistance = 5.0f,
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
            _playerTransform = GameObject.FindWithTag("Player")?.transform;

            // 1. 돌진 방향 산출 및 돌진 주행 방향 정면 주시
            DetermineChargeDirection();
            SetFacingDirection(_chargeDirection);

            ExecuteChargeSequenceAsync().Forget();
        }

        public override void Exit()
        {
            base.Exit();
            KillCurrentTween();
        }

        private void DetermineChargeDirection()
        {
            if (_playerTransform != null)
            {
                _chargeDirection = _playerTransform.position.x >= boss.transform.position.x ? 1.0f : -1.0f;
            }
            else
            {
                _chargeDirection = boss.transform.localScale.x >= 0 ? 1.0f : -1.0f;
            }
        }

        /// <summary>
        /// 보스가 dirX 방향(1: 우측, -1: 좌측)을 정면으로 바라보도록 localScale 설정
        /// </summary>
        private void SetFacingDirection(float dirX)
        {
            Vector3 localScale = boss.transform.localScale;
            boss.transform.localScale = new Vector3(Mathf.Abs(localScale.x) * Mathf.Sign(dirX), localScale.y, localScale.z);
        }

        private async UniTaskVoid ExecuteChargeSequenceAsync()
        {
            try
            {
                Vector3 startPosition = boss.transform.position;

                // 1단계: 돌진 전 살짝 뒤로 당기기 (예열 웅크림)
                Vector3 pullBackPosition = startPosition - new Vector3(_chargeDirection * 0.3f, 0f, 0f);
                _currentTween = boss.transform.DOMoveX(pullBackPosition.x, _anticipatedDuration).SetEase(Ease.OutQuad);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);

                // 2단계: 타이어 부위 파괴(2회) 여부 검사 -> 돌진 속도 결정
                bool isTireBroken = false;
                var tireGimmick = boss.Snapshot?.GetGimmick("TIRE_PART");
                if (tireGimmick != null && (tireGimmick.isBroken || tireGimmick.triggerCount >= 2))
                {
                    isTireBroken = true;
                }
                float finalSpeed = isTireBroken ? _brokenTireSpeed : _chargeSpeed;
                if (isTireBroken)
                {
                    Debug.Log($"<color=yellow>[BossChargeState] 타이어 부위 파괴 감지됨. 돌진 속도 감속: {_chargeSpeed} -> {_brokenTireSpeed}</color>");
                }

                // 3단계: 반대편 맵 끝(벽) x좌표 및 이동시간 계산
                float targetX = CalculateTargetWallX(startPosition.x, _chargeDirection);
                float distance = Mathf.Abs(targetX - boss.transform.position.x);
                float duration = distance / Mathf.Max(1.0f, finalSpeed);
                Debug.Log($"<color=red>[BossChargeState] ▶ 맵 횡단 돌진 개시! (방향: {_chargeDirection}, 속도: {finalSpeed}m/s, 거리: {distance:F1}m)</color>");

                // 4단계: 맵 끝까지 등속 고속 주행 (주행 중에는 앞을 보고 전속력 질주)
                _currentTween = boss.transform.DOMoveX(targetX, duration).SetEase(Ease.Linear);

                bool hitExplosiveBox = false;
                while (_currentTween != null && _currentTween.IsActive() && !_currentTween.IsComplete())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 충돌 감지 (폭발 상자 자폭 / 플레이어 피격 및 Space 무적 회피)
                    if (CheckChargeCollisions())
                    {
                        hitExplosiveBox = true;
                        break;
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
                }

                if (hitExplosiveBox)
                {
                    return;
                }

                // 5단계: 반대편 벽 충돌 임팩트 연출
                _currentTween = boss.transform.DOPunchScale(new Vector3(-0.25f, 0.25f, 0f), 0.2f);
                await _currentTween.ToUniTask(cancellationToken: cancellationToken);
                Debug.Log("<color=orange>[BossChargeState] 맵 반대편 벽 충돌! 쿵! (반동 경직)</color>");

                // 짧은 벽 충돌 반동 대기
                await UniTask.Delay(TimeSpan.FromSeconds(_wallStunDelay), cancellationToken: cancellationToken);

                // ⭐ 6단계: [충돌 종료 조건 체크 후 반전]
                // 벽 충돌 반동이 끝나고 플레이어와의 콜라이더 겹침이 완전히 해제(충돌 종료)되었을 때,
                // 플레이어를 지나친 상태라면 등 뒤의 플레이어를 향해 홱 몸을 반전!
                await WaitForCollisionToEndAsync();
                TryFlipAfterCollision();

                // 7단계: 기획서 공통규칙 1.0초 확정 딜타임(BossIdleState) 복귀
                if (!cancellationToken.IsCancellationRequested && boss.Snapshot != null && boss.Snapshot.IsAlive)
                {
                    float postDelay = _pattern != null ? _pattern.postDelay : 1.0f;
                    var nextToken = boss.RenewStateCancellationToken();
                    boss.StateMachine.ChangeState(new BossIdleState(boss, nextToken, postDelay));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[BossChargeState] 돌진 시퀀스 취소됨");
            }
            finally
            {
                KillCurrentTween();
            }
        }

        /// <summary>
        /// 충돌(플레이어와의 겹침)이 완전히 끝날 때까지 안전 대기
        /// </summary>
        private async UniTask WaitForCollisionToEndAsync()
        {
            float timeout = 0.3f;
            while (timeout > 0f && IsTouchingPlayer())
            {
                timeout -= Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// 현재 보스와 플레이어 콜라이더가 맞닿아 있는지(충돌 중인지) 검사
        /// </summary>
        private bool IsTouchingPlayer()
        {
            if (_playerTransform == null) return false;
            Vector2 center = boss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.2f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) return true;
            }
            return false;
        }

        /// <summary>
        /// 충돌이 완전히 끝난 후 플레이어를 지나친 상태일 때 등 뒤의 플레이어를 바라보도록 반전
        /// </summary>
        private void TryFlipAfterCollision()
        {
            if (_playerTransform == null) return;

            // 보스가 돌진 방향 기준으로 플레이어를 지나쳤는지 확인
            float diffX = (boss.transform.position.x - _playerTransform.position.x) * _chargeDirection;
            bool hasPassedPlayer = diffX > 0.05f;

            if (hasPassedPlayer)
            {
                float toPlayerDirection = _playerTransform.position.x >= boss.transform.position.x ? 1.0f : -1.0f;
                SetFacingDirection(toPlayerDirection);
                Debug.Log("<color=cyan>[BossChargeState] 충돌 종료 및 플레이어 통과 확인 ➔ 플레이어를 향해 스프라이트 반전 완료!</color>");
            }
        }

        private float CalculateTargetWallX(float currentX, float directionX)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                boss.transform.position,
                Vector2.right * directionX,
                _maxMapDistance,
                LayerMask.GetMask("Wall", "Ground", "Default"));

            if (hit.collider != null && hit.collider.gameObject != boss.gameObject)
            {
                return hit.point.x - (directionX * 0.5f);
            }
            return currentX + (directionX * _maxMapDistance);
        }

        private bool CheckChargeCollisions()
        {
            Vector2 center = boss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.2f);

            foreach (var hit in hits)
            {
                if (hit.gameObject == boss.gameObject) continue;

                // 1. 전장 기물: 폭발 화물 상자 충돌 (즉시 자폭 및 보스 2초 스턴)
                if (hit.CompareTag("ExplosiveBox") || hit.name.IndexOf("Explosive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("<color=red>[BossChargeState] ▶ 폭발 화물 상자 충돌! 자폭 발생 및 보스 2초 스턴!</color>");
                    KillCurrentTween();
                    UnityEngine.Object.Destroy(hit.gameObject);
                    boss.InterruptToStun(2.0f);
                    return true;
                }

                // 2. 플레이어 충돌: Space 회피 무적 체크
                if (!_hasHitPlayer && hit.CompareTag("Player"))
                {
                    _hasHitPlayer = true;

                    // NYH ActionExecutor 연동: 회피 무적 상태인지 검사
                    bool isInvincible = false;
                    var executor = hit.GetComponent<ActionExecutor>() ?? hit.GetComponentInParent<ActionExecutor>();
                    if (executor != null && executor.State != null && executor.State.CurrentAction != null)
                    {
                        isInvincible = executor.State.CurrentAction.IsInvincibleDuringActive;
                    }

                    if (isInvincible)
                    {
                        // 기획서: Space 회피 무적으로 돌진을 회피함
                        Debug.Log("<color=cyan>[BossChargeState] ▶ [회피 성공!] 플레이어가 Space 무적 회피로 돌진을 통과했습니다!</color>");
                    }
                    else
                    {
                        // 무적 회피 실패 시 코어 피해 적용
                        Debug.Log($"<color=red>[BossChargeState] ▶ [피격!] 플레이어가 돌진에 직격당함! (피해량: {_chargeDamage})</color>");
                        CombatDataHub.Instance?.ApplyCoreDamage("Player", _chargeDamage);
                    }
                }
            }
            return false;
        }

        private void KillCurrentTween()
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
        }
    }
}
