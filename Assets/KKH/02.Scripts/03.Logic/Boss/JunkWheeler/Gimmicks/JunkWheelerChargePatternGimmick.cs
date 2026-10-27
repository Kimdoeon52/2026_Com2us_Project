using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 돌진 패턴 기믹 (JunkWheelerChargePatternGimmick)]
    /// 기획서 §6.18 및 §3.2 (3):
    /// 1. 2.0초 전조 동안 300 DPS 저지 체크 (저지 성공 시 3초 스턴).
    /// 2. 본 액션: 맵 한쪽 끝에서 반대편 끝까지 가로지르는 고속 횡단 돌진.
    /// 3. 타이어 부위 파괴 시 돌진 속도 영구 감속 (11.0 m/s ➔ 5.5 m/s).
    /// 4. 플레이어 충돌 시 Space 회피 무적 판정: 무적 중이면 통과, 아니면 피해.
    /// 5. 폭발 화물 상자 충돌 시 상자 자폭 및 보스 2초 스턴 즉시 인터럽트.
    /// 6. 충돌 종료 후 등 뒤의 플레이어를 향해 스프라이트 반전.
    /// </summary>
    public class JunkWheelerChargePatternGimmick : BossPatternGimmickBase
    {
        [Header("돌진 파라미터")]
        [SerializeField] private float chargeSpeed = 11.0f;
        [SerializeField] private float brokenTireSpeed = 5.5f;
        [SerializeField] private float anticipatedDuration = 0.2f;
        [SerializeField] private float wallStunDelay = 0.3f;
        [SerializeField] private float maxMapDistance = 5.0f;

        private Tweener _currentTween;
        private Tweener _flashTween;
        private Tweener _shakeTween;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor = Color.white;

        private bool _isTireBrokenDebuffed = false;
        private bool _hasHitPlayer = false;
        private float _chargeDirection = 1.0f; // 1.0: 우측, -1.0: 좌측
        private Transform _playerTransform;

        public void ApplyTireBrokenDebuff(bool debuffed)
        {
            _isTireBrokenDebuffed = debuffed;
            Debug.Log($"<color=yellow>[JunkWheelerChargePattern] 타이어 파괴 디버프 적용 상태: {_isTireBrokenDebuffed} (속도: {(debuffed ? brokenTireSpeed : chargeSpeed)}m/s)</color>");
        }

        protected override void OnPreExecute()
        {
            _hasHitPlayer = false;
            _playerTransform = GameObject.FindWithTag("Player")?.transform;

            DetermineChargeDirection();
            FaceDirection(_chargeDirection);
        }

        protected override void OnTellStarted()
        {
            // 전조 연출: 엔진 과열 붉은색 점멸 및 바퀴 헛돔 진동
            _spriteRenderer = OwnerBoss != null ? OwnerBoss.GetComponentInChildren<SpriteRenderer>() : null;
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
                _flashTween = _spriteRenderer.DOColor(Color.red, 0.15f).SetLoops(-1, LoopType.Yoyo);
            }

            if (OwnerBoss != null)
            {
                float tellDur = patternData != null ? patternData.tellDuration : 2.0f;
                _shakeTween = OwnerBoss.transform.DOShakePosition(tellDur, new Vector3(0.08f, 0f, 0f), 20);
            }
        }

        protected override void OnTellEnded()
        {
            StopTellVisuals();
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            Vector3 startPosition = OwnerBoss.transform.position;

            // 1단계: 돌진 전 살짝 뒤로 당기기 (예열 웅크림)
            Vector3 pullBackPosition = startPosition - new Vector3(_chargeDirection * 0.3f, 0f, 0f);
            _currentTween = OwnerBoss.transform.DOMoveX(pullBackPosition.x, anticipatedDuration).SetEase(Ease.OutQuad);
            await _currentTween.ToUniTask(cancellationToken: token);

            // 2단계: 타이어 파괴 상태 확인 -> 돌진 속도 결정
            bool isTireBroken = _isTireBrokenDebuffed;
            var tireGimmick = OwnerBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick != null && tireGimmick.IsPermanentlyBroken)
            {
                isTireBroken = true;
            }
            float finalSpeed = isTireBroken ? brokenTireSpeed : chargeSpeed;

            // 3단계: 반대편 벽 X좌표 산출 및 돌진 주행
            float targetX = CalculateTargetWallX(startPosition.x, _chargeDirection);
            float distance = Mathf.Abs(targetX - OwnerBoss.transform.position.x);
            float duration = distance / Mathf.Max(1.0f, finalSpeed);

            Debug.Log($"<color=red>[JunkWheelerChargePattern] ▶ 맵 횡단 돌진 개시! (방향: {_chargeDirection}, 속도: {finalSpeed}m/s, 거리: {distance:F1}m)</color>");

            _currentTween = OwnerBoss.transform.DOMoveX(targetX, duration).SetEase(Ease.Linear);

            bool hitExplosive = false;
            while (_currentTween != null && _currentTween.IsActive() && !_currentTween.IsComplete())
            {
                token.ThrowIfCancellationRequested();

                if (CheckCollisionsDuringCharge())
                {
                    hitExplosive = true;
                    break;
                }
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }

            if (hitExplosive)
            {
                return;
            }

            // 4단계: 반대편 벽 충돌 임팩트 연출
            _currentTween = OwnerBoss.transform.DOPunchScale(new Vector3(-0.25f, 0.25f, 0f), 0.2f);
            await _currentTween.ToUniTask(cancellationToken: token);
            Debug.Log("<color=orange>[JunkWheelerChargePattern] 반대편 벽 충돌 완료! (쿵!)</color>");

            // 벽 충돌 반동 대기
            await UniTask.Delay(TimeSpan.FromSeconds(wallStunDelay), cancellationToken: token);

            // 5단계: 충돌 종료 확인 후 등 뒤의 플레이어를 향해 스프라이트 반전
            await WaitForCollisionToEndAsync(token);
            TryFlipAfterCollision();
        }

        private bool CheckCollisionsDuringCharge()
        {
            if (OwnerBoss == null) return false;

            Vector2 center = OwnerBoss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.2f);

            foreach (var hit in hits)
            {
                if (hit.gameObject == OwnerBoss.gameObject) continue;

                // 1. 폭발 화물 상자 충돌 검사
                var explosive = hit.GetComponent<ExplosiveBoxFieldGimmick>() ?? hit.GetComponentInParent<ExplosiveBoxFieldGimmick>();
                if (explosive != null || hit.CompareTag("ExplosiveBox") || hit.name.IndexOf("Explosive", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("<color=red>[JunkWheelerChargePattern] ▶ 폭발 화물 상자 충돌! 2초 스턴 인터럽트 발생!</color>");
                    KillCurrentTween();

                    if (explosive != null)
                    {
                        explosive.TriggerInteraction(OwnerBoss.gameObject);
                    }
                    else
                    {
                        Destroy(hit.gameObject);
                        OwnerBoss.InterruptToStun(2.0f);
                    }

                    InterruptPattern(InterruptReason.ExplosiveStun);
                    return true;
                }

                // 2. 플레이어 충돌 검사
                if (!_hasHitPlayer && hit.CompareTag("Player"))
                {
                    _hasHitPlayer = true;
                    ApplyDamageToPlayer(hit.gameObject);
                }
            }
            return false;
        }

        private void DetermineChargeDirection()
        {
            if (_playerTransform != null)
            {
                _chargeDirection = _playerTransform.position.x >= OwnerBoss.transform.position.x ? 1.0f : -1.0f;
            }
            else
            {
                _chargeDirection = OwnerBoss.transform.localScale.x >= 0 ? -1.0f : 1.0f;
            }
        }

        private void FaceDirection(float dirX)
        {
            if (OwnerBoss == null) return;
            Vector3 scale = OwnerBoss.transform.localScale;
            float targetSign = dirX < 0 ? 1.0f : -1.0f;
            OwnerBoss.transform.localScale = new Vector3(Mathf.Abs(scale.x) * targetSign, scale.y, scale.z);
        }

        private float CalculateTargetWallX(float currentX, float directionX)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                OwnerBoss.transform.position,
                Vector2.right * directionX,
                maxMapDistance,
                LayerMask.GetMask("Wall", "Ground", "Default"));

            if (hit.collider != null && hit.collider.gameObject != OwnerBoss.gameObject)
            {
                return hit.point.x - (directionX * 0.5f);
            }
            return currentX + (directionX * maxMapDistance);
        }

        private async UniTask WaitForCollisionToEndAsync(CancellationToken token)
        {
            float timeout = 0.3f;
            while (timeout > 0f && IsTouchingPlayer())
            {
                timeout -= Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
            }
        }

        private bool IsTouchingPlayer()
        {
            if (_playerTransform == null || OwnerBoss == null) return false;
            Vector2 center = OwnerBoss.transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.2f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) return true;
            }
            return false;
        }

        private void TryFlipAfterCollision()
        {
            if (_playerTransform == null || OwnerBoss == null) return;

            float diffX = (OwnerBoss.transform.position.x - _playerTransform.position.x) * _chargeDirection;
            if (diffX > 0.05f)
            {
                float toPlayerDirection = _playerTransform.position.x >= OwnerBoss.transform.position.x ? 1.0f : -1.0f;
                FaceDirection(toPlayerDirection);
                Debug.Log("<color=cyan>[JunkWheelerChargePattern] 돌진 후 플레이어를 향해 스프라이트 반전 완료!</color>");
            }
        }

        protected override void OnInterrupted(InterruptReason reason)
        {
            base.OnInterrupted(reason);
            KillCurrentTween();
            StopTellVisuals();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            KillCurrentTween();
            StopTellVisuals();
        }

        private void KillCurrentTween()
        {
            if (_currentTween != null && _currentTween.IsActive())
            {
                _currentTween.Kill();
                _currentTween = null;
            }
        }

        private void StopTellVisuals()
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
    }
}
