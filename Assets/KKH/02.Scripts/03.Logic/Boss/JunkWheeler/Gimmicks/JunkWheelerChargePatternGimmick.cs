using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.Visual;

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
        [Header("돌진 기본 파라미터")]
        [SerializeField] private float chargeSpeed = 11.0f;
        [SerializeField] private float brokenTireSpeed = 5.5f;
        [SerializeField] private float anticipatedDuration = 0.2f;
        [SerializeField] private float wallStunDelay = 0.3f;
        [SerializeField] private float maxMapDistance = 5.0f;

        [Header("비선형 전조(Tell) 가속 파라미터")]
        [Tooltip("전조 점멸 목표 색상 (기본: 빨간색 Color.red)")]
        [SerializeField] private Color flashColor = Color.red;
        [Tooltip("전조 시작 시 점멸 주기 (초, 기본: 0.30초)")]
        [SerializeField] private float flashStartInterval = 0.30f;
        [Tooltip("전조 종료 직전 초고속 스트로보 주기 (초, 기본: 0.04초)")]
        [SerializeField] private float flashEndInterval = 0.04f;
        [Tooltip("전조 시작 시 미세 진동 세기 (m)")]
        [SerializeField] private float minShakeStrength = 0.02f;
        [Tooltip("전조 발사 직전 최대 폭발 진동 세기 (m)")]
        [SerializeField] private float maxShakeStrength = 0.20f;
        [Tooltip("전조 시작 시 진동 주파수 (Hz)")]
        [SerializeField] private float minShakeVibrato = 10f;
        [Tooltip("전조 발사 직전 진동 주파수 (Hz)")]
        [SerializeField] private float maxShakeVibrato = 50f;
        [Tooltip("진동/점멸 가속 곡선 지수 (t^k, 기본: 2.8)")]
        [SerializeField] private float shakeCurveExponent = 2.8f;

        [Header("탄성 반동(Rebound) 및 롤 감속 파라미터")]
        [Tooltip("벽 충돌 후 반대 방향으로 튕겨 나오는 후방 거리 (m)")]
        [SerializeField] private float reboundDistance = 1.4f;
        [Tooltip("벽 충돌 후 반동 이동 소요 시간 (초)")]
        [SerializeField] private float reboundDuration = 0.24f;
        [Tooltip("튕겨 나온 후 Roll 바퀴가 지면을 굴러가며 감속되는 시간 (초)")]
        [SerializeField] private float rollDecelDuration = 0.40f;
        [Tooltip("벽 충돌 순간 가로 압착 비율 (X: 가로, Y: 세로)")]
        [SerializeField] private Vector2 impactSquashScale = new Vector2(0.55f, 1.45f);
        [Tooltip("충돌 순간 초고속 압착 시간 (Fast-In)")]
        [SerializeField] private float squashDuration = 0.04f;
        [Tooltip("압착 후 탄성 진동하며 복원되는 시간 (Elastic-Out)")]
        [SerializeField] private float elasticRestoreDuration = 0.28f;

        [Header("인스펙터 전조 이벤트 바인딩")]
        [Tooltip("전조 시작 시 인스펙터에서 연동할 UnityEvent")]
        [SerializeField] private UnityEngine.Events.UnityEvent onTellStartedEvent;
        [Tooltip("전조 종료 시 인스펙터에서 연동할 UnityEvent")]
        [SerializeField] private UnityEngine.Events.UnityEvent onTellEndedEvent;

        public UnityEngine.Events.UnityEvent OnTellStartedEvent => onTellStartedEvent;
        public UnityEngine.Events.UnityEvent OnTellEndedEvent => onTellEndedEvent;

        public Color FlashColor { get => flashColor; set => flashColor = value; }
        public float FlashInterval { get => flashStartInterval; set => flashStartInterval = value; }
        public float MinShakeStrength { get => minShakeStrength; set => minShakeStrength = value; }
        public float MaxShakeStrength { get => maxShakeStrength; set => maxShakeStrength = value; }
        public float ShakeCurveExponent { get => shakeCurveExponent; set => shakeCurveExponent = value; }

        public void SetFlashParameters(Color newColor, float newInterval)
        {
            flashColor = newColor;
            flashStartInterval = newInterval;
        }

        public void SetShakeParameters(Vector3 newStrength, int newVibrato)
        {
            maxShakeStrength = Mathf.Max(newStrength.x, newStrength.y);
            maxShakeVibrato = newVibrato;
        }

        private Tweener _currentTween;
        private Tweener _flashTween;
        private Tweener _shakeTween;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor = Color.white;
        private CancellationTokenSource _tellVisualCts;
        private Vector3 _baseWorldPosBeforeTell;
        private bool _isTellVisualRunning = false;

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

        /// <summary>
        /// 전조(Tell) 연출 시작: 바퀴 공회전 모션, 비선형 로그/지수 점멸 및 본체 가속 진동 가동 (인스펙터 및 외부 호출 가능)
        /// </summary>
        [ContextMenu("[전조] 점멸 시작 (OnTellStarted)")]
        public override void OnTellStarted()
        {
            if (OwnerBoss != null && OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlayChargeStart();
            }

            _spriteRenderer = OwnerBoss != null ? OwnerBoss.GetComponentInChildren<SpriteRenderer>() : null;
            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            if (OwnerBoss != null)
            {
                float tellDur = patternData != null ? patternData.tellDuration : 2.0f;

                // 전조 시작 직후 차지 루프로 부드럽게 전이
                TransitToChargeLoopAsync(tellDur).Forget();

                // 비선형 전조(지수 가속 진동 & 스트로보 점멸) 비동기 루프 가동
                StopTellVisuals();
                _tellVisualCts = new CancellationTokenSource();
                RunNonlinearTellVisualsAsync(tellDur, _tellVisualCts.Token).Forget();
            }

            // 인스펙터 바인딩 UnityEvent 실행
            onTellStartedEvent?.Invoke();
        }

        /// <summary>
        /// 외부에서 즉시 지정한 커스텀 점멸 색상과 주기로 전조를 시작하는 오버로딩 메서드
        /// </summary>
        public void OnTellStarted(Color customFlashColor, float customInterval)
        {
            flashColor = customFlashColor;
            flashStartInterval = customInterval;
            OnTellStarted();
        }

        private async UniTaskVoid TransitToChargeLoopAsync(float delay)
        {
            // Boss_Charge_Start 클립 길이(4프레임 @12fps = 0.333초)만큼 대기 후 전이 (마지막 프레임 잘림 방지)
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(0.34f), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            if (canceled) return;
            if (OwnerBoss != null && OwnerBoss.Animation != null
                && OwnerBoss.Animation.CurrentStateHash == BossAnimationController.StateChargeStart)
            {
                OwnerBoss.Animation.PlayChargeLoop();
            }
        }

        private async UniTaskVoid RunNonlinearTellVisualsAsync(float duration, CancellationToken token)
        {
            if (OwnerBoss == null) return;

            _baseWorldPosBeforeTell = OwnerBoss.transform.position;
            _isTellVisualRunning = true;
            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();

                    float progress = Mathf.Clamp01(elapsed / duration); // 0 ~ 1
                    float curvedT = Mathf.Pow(progress, shakeCurveExponent); // 비선형 지수 가속 (t^2.8)

                    // 1. 비선형 진동 계산 (X축 바퀴 헛돔 진동 + 미세 Y축 떨림)
                    float curAmp = Mathf.Lerp(minShakeStrength, maxShakeStrength, curvedT);
                    float curFreq = Mathf.Lerp(minShakeVibrato, maxShakeVibrato, curvedT);
                    float shakeOffsetX = Mathf.Sin(elapsed * curFreq * Mathf.PI * 2f) * curAmp;
                    float shakeOffsetY = Mathf.Cos(elapsed * curFreq * Mathf.PI * 1.5f) * (curAmp * 0.25f);

                    OwnerBoss.transform.position = new Vector3(
                        _baseWorldPosBeforeTell.x + shakeOffsetX,
                        _baseWorldPosBeforeTell.y + shakeOffsetY,
                        _baseWorldPosBeforeTell.z
                    );

                    // 2. 비선형 점멸 계산 (스트로보 가속)
                    if (_spriteRenderer != null)
                    {
                        if (progress >= 0.92f)
                        {
                            // 발사 직전 임계 폭발 피크: 완전 붉은색 고정 (Peak Saturation)
                            _spriteRenderer.color = flashColor;
                        }
                        else
                        {
                            float curInterval = Mathf.Lerp(flashStartInterval, flashEndInterval, curvedT);
                            float pingPong = Mathf.PingPong(elapsed / Mathf.Max(0.01f, curInterval), 1f);
                            _spriteRenderer.color = Color.Lerp(Color.white, flashColor, pingPong);
                        }
                    }

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // 외부 인터럽트 시 정상 종료
            }
            finally
            {
                _isTellVisualRunning = false;
                if (OwnerBoss != null)
                {
                    OwnerBoss.transform.position = _baseWorldPosBeforeTell;
                }
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.color = _originalColor;
                }
            }
        }

        /// <summary>
        /// 전조(Tell) 연출 종료: 점멸 및 진동 정지 (인스펙터 및 외부 호출 가능)
        /// </summary>
        [ContextMenu("[전조] 점멸 종료 (OnTellEnded)")]
        public override void OnTellEnded()
        {
            StopTellVisuals();
            onTellEndedEvent?.Invoke();
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (OwnerBoss == null) return;

            Vector3 startPosition = OwnerBoss.transform.position;

            // 1단계: 돌진 전 살짝 뒤로 당기기 (예열 웅크림)
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlayChargeLoop();
            }

            Vector3 pullBackPosition = startPosition - new Vector3(_chargeDirection * 0.3f, 0f, 0f);
            if (OwnerBoss != null)
            {
                pullBackPosition.x = OwnerBoss.ClampToCameraX(pullBackPosition.x, 0.7f);
            }
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

            // 돌진 발사 순간 애니메이션 ➔ 주행 루프
            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlayChargeRelease();
                TransitToChargeRunAsync().Forget();
            }

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

            // 4단계: 반대편 벽 충돌 임팩트 및 탄성 반동(Elastic Rebound)
            Debug.Log("<color=orange>[JunkWheelerChargePattern] 반대편 벽 충돌 접촉! (콰앙!)</color>");

            // [Step 4-A] 벽면 극단적 가로 압착 (Squash & Stretch Fast-In / Elastic-Out)
            UniTask squashTask = UniTask.CompletedTask;
            if (OwnerBoss.Animation != null)
            {
                squashTask = OwnerBoss.Animation.PlayImpactSquashAndStretchAsync(
                    OwnerBoss.transform,
                    impactSquashScale,
                    squashDuration,
                    elasticRestoreDuration,
                    token
                );
            }

            // [Step 4-B] 탄성 튕김 이동 (벽면에서 반대 방향으로 reboundDistance만큼 튕겨 나감)
            float wallContactX = OwnerBoss.transform.position.x;
            float reboundTargetX = wallContactX - (_chargeDirection * reboundDistance);
            if (OwnerBoss != null)
            {
                reboundTargetX = OwnerBoss.ClampToCameraX(reboundTargetX, 0.7f);
            }

            _currentTween = OwnerBoss.transform.DOMoveX(reboundTargetX, reboundDuration).SetEase(Ease.OutQuad);
            await _currentTween.ToUniTask(cancellationToken: token);

            // 스쿼시 트윈 완료 대기
            await squashTask;

            // [Step 4-C] 지면 굴림(Roll) 관성 감속 (0.40s)
            // 튕겨 나온 후 바퀴가 천천히 굴러가며 잔여 관성을 자연스럽게 소모함
            if (OwnerBoss.Animation != null)
            {
                await OwnerBoss.Animation.PlayRollDecelAsync(rollDecelDuration, 1.2f, 0.3f, token);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(rollDecelDuration), cancellationToken: token);
            }

            // 5단계: 충돌 종료 확인 후 플레이어를 향해 스프라이트 반전 및 Idle 복귀
            await WaitForCollisionToEndAsync(token);
            if (OwnerBoss != null)
            {
                OwnerBoss.Animation?.ResetScale();
                OwnerBoss.FacePlayer();
            }

            if (OwnerBoss.Animation != null)
            {
                OwnerBoss.Animation.PlayIdle();
            }
        }

        private async UniTaskVoid TransitToChargeRunAsync()
        {
            // Boss_Charge_Release 클립 길이(3프레임 @15fps = 0.2초) 이후 주행 루프로 전이
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(0.2f), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            if (canceled) return;
            // 그사이 스턴/충돌 등으로 다른 모션이 재생됐다면 덮어쓰지 않음
            if (OwnerBoss != null && OwnerBoss.Animation != null
                && OwnerBoss.Animation.CurrentStateHash == BossAnimationController.StateChargeRelease)
            {
                OwnerBoss.Animation.PlayChargeRun();
            }
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

                    if (OwnerBoss.Animation != null)
                    {
                        OwnerBoss.Animation.PlayStun();
                    }

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
                // 스프라이트 기본 정면 = 우측(+X) 기준
                _chargeDirection = OwnerBoss.transform.localScale.x >= 0 ? 1.0f : -1.0f;
            }
        }

        private void FaceDirection(float dirX)
        {
            OwnerBoss?.FaceDirection(dirX);
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
                float wallHitX = hit.point.x - (directionX * 0.5f);
                return OwnerBoss != null ? OwnerBoss.ClampToCameraX(wallHitX, 0.7f) : wallHitX;
            }
            float fallbackX = currentX + (directionX * maxMapDistance);
            return OwnerBoss != null ? OwnerBoss.ClampToCameraX(fallbackX, 0.7f) : fallbackX;
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
            OwnerBoss?.Animation?.ResetScale();
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            KillCurrentTween();
            StopTellVisuals();
            OwnerBoss?.Animation?.ResetScale();
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
            if (_tellVisualCts != null)
            {
                if (!_tellVisualCts.IsCancellationRequested) _tellVisualCts.Cancel();
                _tellVisualCts.Dispose();
                _tellVisualCts = null;
            }

            if (_isTellVisualRunning && OwnerBoss != null)
            {
                OwnerBoss.transform.position = _baseWorldPosBeforeTell;
                _isTellVisualRunning = false;
            }

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
