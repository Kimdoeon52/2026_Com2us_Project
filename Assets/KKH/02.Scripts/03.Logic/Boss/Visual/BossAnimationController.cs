using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace RealSteel.Boss.Visual
{
    /// <summary>
    /// [보스 애니메이션 & 스프라이트 연출 총괄 컨트롤러 (BossAnimationController)]
    /// 1. 2D 액션 보스의 Animator 상태 전이를 스파게티 트랜지션 없이 C# 코드로 직접 제어함.
    /// 2. Animator의 State Name Hash를 캐싱하여 가비지 없이 고속 재생.
    /// 3. 보스 이동/타겟팅 방향에 따른 스프라이트 X축 자동 좌우 반전(FaceDirection) 지원.
    /// 4. Animator 미부착 또는 애니메이션 클립 미생성 상태에서도 NullReference 없이 안전하게 폴백 동작.
    /// </summary>
    public class BossAnimationController : MonoBehaviour
    {
        [Header("컴포넌트 참조 (미지정 시 자동 탐색)")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("스프라이트 기본 설정")]
        [Tooltip("기본 스프라이트가 바라보는 방향 (우측 기본이면 1, 좌측 기본이면 -1)")]
        [SerializeField] private float baseFacingSign = 1f;

        // Animator State Name Hash 캐싱
        public static readonly int StateIdle = Animator.StringToHash("Boss_Idle");
        public static readonly int StateChargeStart = Animator.StringToHash("Boss_Charge_Start");
        public static readonly int StateChargeLoop = Animator.StringToHash("Boss_Charge_Loop");
        public static readonly int StateChargeRelease = Animator.StringToHash("Boss_Charge_Release");
        public static readonly int StateChargeRun = Animator.StringToHash("Boss_Charge_Run");
        public static readonly int StateRollBarrage = Animator.StringToHash("Boss_Roll_Barrage");
        public static readonly int StateSlamJump = Animator.StringToHash("Boss_Slam_Jump");
        public static readonly int StateSlamFall = Animator.StringToHash("Boss_Slam_Fall");
        public static readonly int StateSlamLand = Animator.StringToHash("Boss_Slam_Land");
        public static readonly int StateGroggy = Animator.StringToHash("Boss_Groggy");
        public static readonly int StateStun = Animator.StringToHash("Boss_Stun");

        private int _currentHash = 0;
        private float _baseScaleX = 1f;
        private float _baseScaleY = 1f;
        private float _baseScaleZ = 1f;
        private int _squashVersion = 0;
        private Tweener _scaleTween;

        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public int CurrentStateHash => _currentHash;
        public float BaseScaleX => _baseScaleX;
        public float BaseScaleY => _baseScaleY;
        public float BaseScaleZ => _baseScaleZ;

        private void Reset() => AutoBindReferences();
        private void OnValidate() => AutoBindReferences();

        public void AutoBindReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void Awake()
        {
            AutoBindReferences();

            _baseScaleX = Mathf.Abs(transform.localScale.x);
            _baseScaleY = Mathf.Abs(transform.localScale.y);
            _baseScaleZ = Mathf.Abs(transform.localScale.z);
            if (Mathf.Approximately(_baseScaleX, 0f)) _baseScaleX = 1f;
            if (Mathf.Approximately(_baseScaleY, 0f)) _baseScaleY = 1f;
            if (Mathf.Approximately(_baseScaleZ, 0f)) _baseScaleZ = 1f;
        }

        private void KillScaleTween()
        {
            if (_scaleTween != null && _scaleTween.IsActive())
            {
                _scaleTween.Kill();
            }
            _scaleTween = null;
        }

        /// <summary>
        /// 보스의 스케일을 원래의 불변 기본 스케일로 즉시 복원함.
        /// (target.DOKill() 대신 스케일 전용 트윈만 종료하여 보스의 이동 트윈 DOMoveX/Y가 끊기지 않도록 보호)
        /// </summary>
        public void ResetScale(Transform target = null)
        {
            _squashVersion++;
            KillScaleTween();
            Transform t = target != null ? target : transform;
            if (t == null) return;
            float signX = Mathf.Sign(t.localScale.x);
            if (Mathf.Approximately(signX, 0f)) signX = 1f;
            t.localScale = new Vector3(_baseScaleX * signX, _baseScaleY, _baseScaleZ);
        }

        /// <summary>
        /// 보스가 바라보는 방향(direction > 0 우측, direction < 0 좌측)으로 스프라이트를 반전시킴.
        /// </summary>
        public void FaceDirection(float direction)
        {
            if (Mathf.Approximately(direction, 0f)) return;

            float targetSign = Mathf.Sign(direction) * Mathf.Sign(baseFacingSign);
            Vector3 scale = transform.localScale;
            scale.x = _baseScaleX * targetSign;
            transform.localScale = scale;
        }

        /// <summary>
        /// 특정 애니메이션 상태를 즉각 재생함.
        /// </summary>
        /// <param name="forceRestart">false면 이미 같은 상태를 재생 중일 때 처음으로 되감지 않음 (루프 끊김 방지)</param>
        public void Play(int stateHash, float normalizedTime = 0f, bool forceRestart = false)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy) return;

            if (!animator.enabled) animator.enabled = true;
            animator.speed = 1.0f;

            if (!forceRestart && _currentHash == stateHash
                && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash)
            {
                return;
            }

            _currentHash = stateHash;
            try
            {
                animator.Play(stateHash, 0, normalizedTime);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BossAnimationController] 애니메이션 상태 재생 실패 (Hash: {stateHash}): {ex.Message}");
            }
        }

        public void PlayIdle() => Play(StateIdle);
        public void PlayChargeStart() => Play(StateChargeStart);
        public void PlayChargeLoop() => Play(StateChargeLoop);
        public void PlayChargeRelease() => Play(StateChargeRelease);
        public void PlayChargeRun() => Play(StateChargeRun);
        public void PlayRollBarrage() => Play(StateRollBarrage);
        public void PlaySlamJump() => Play(StateSlamJump, 0f, true);
        public void PlaySlamFall() => Play(StateSlamFall);
        public void PlaySlamLand() => Play(StateSlamLand, 0f, true);
        public void PlayGroggy() => Play(StateGroggy);
        public void PlayStun() => Play(StateStun);

        /// <summary>
        /// 좌우 반전(음수 scale.x) 상태에서도 Squash/Stretch 방향이 뒤집히지 않도록 펀치 벡터의 X 부호를 보정함.
        /// </summary>
        public static Vector3 FacingSafePunch(Transform target, Vector3 punch)
        {
            punch.x *= Mathf.Sign(target.localScale.x);
            return punch;
        }

        /// <summary>
        /// 비대칭 비선형 펀치감 연출 (Fast-In 초고속 압착 -> Elastic-Out 탄성 복원)
        /// 좌우 반전 상태(scale.x < 0)에서도 대칭적인 Squash & Stretch를 보장함.
        /// </summary>
        public async UniTask PlayImpactSquashAndStretchAsync(
            Vector2 squashScaleRatio,
            float inDuration = 0.04f,
            float outDuration = 0.28f,
            CancellationToken token = default)
        {
            await PlayImpactSquashAndStretchAsync(transform, squashScaleRatio, inDuration, outDuration, token);
        }

        /// <summary>
        /// 비대칭 비선형 펀치감 연출 (특정 대상 Transform 지정)
        /// 1. 불변 기본 스케일(_baseScaleX, _baseScaleY)을 기준으로 연산하여 연속 호출 시에도 스케일 왜곡 누적 방지.
        /// 2. target.DOKill() 대신 _scaleTween만 단독 제어하여 보스의 이동 트윈(DOMoveX/DOMoveY)을 절대 끊지 않음.
        /// 3. 버전 카운터(_squashVersion)를 통해 후속 스쿼시가 이전 스쿼시를 덮어썼을 때 예외가 상위 패턴으로 오전파되는 현상 차단.
        /// </summary>
        public async UniTask PlayImpactSquashAndStretchAsync(
            Transform target,
            Vector2 squashScaleRatio,
            float inDuration = 0.04f,
            float outDuration = 0.28f,
            CancellationToken token = default)
        {
            if (target == null) return;

            int currentVersion = ++_squashVersion;

            // 진행 중인 이전 스케일 트윈만 즉시 중단 (이동 트윈 DOMove 간섭 원천 방지)
            KillScaleTween();

            float signX = Mathf.Sign(target.localScale.x);
            if (Mathf.Approximately(signX, 0f)) signX = 1f;

            // 영구 불변 기본 스케일을 기준으로 원본 스케일 및 압착 스케일 산출
            Vector3 squashedScale = new Vector3(
                _baseScaleX * squashScaleRatio.x * signX,
                _baseScaleY * squashScaleRatio.y,
                _baseScaleZ
            );

            try
            {
                // 1단계: 초고속 압착 (Fast In)
                _scaleTween = target.DOScale(squashedScale, inDuration).SetEase(Ease.InQuad);
                await _scaleTween.ToUniTask(cancellationToken: token);

                if (_squashVersion != currentVersion || target == null) return;

                // 도중에 FaceDirection으로 좌우가 반전되었을 수 있으므로 현재 부호 재확인
                float outSignX = Mathf.Sign(target.localScale.x);
                if (Mathf.Approximately(outSignX, 0f)) outSignX = signX;
                Vector3 originalScale = new Vector3(_baseScaleX * outSignX, _baseScaleY, _baseScaleZ);

                // 2단계: 탄성 로그 감쇠 복원 (Elastic Out)
                _scaleTween = target.DOScale(originalScale, outDuration).SetEase(Ease.OutElastic);
                await _scaleTween.ToUniTask(cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    if (target != null && _squashVersion == currentVersion)
                    {
                        KillScaleTween();
                        float currentSignX = Mathf.Sign(target.localScale.x);
                        if (Mathf.Approximately(currentSignX, 0f)) currentSignX = signX;
                        target.localScale = new Vector3(_baseScaleX * currentSignX, _baseScaleY, _baseScaleZ);
                    }
                    throw;
                }
                return;
            }
            finally
            {
                if (target != null && _squashVersion == currentVersion)
                {
                    KillScaleTween();
                    float currentSignX = Mathf.Sign(target.localScale.x);
                    if (Mathf.Approximately(currentSignX, 0f)) currentSignX = signX;
                    target.localScale = new Vector3(_baseScaleX * currentSignX, _baseScaleY, _baseScaleZ);
                }
            }
        }

        /// <summary>
        /// 지면 구름(Roll) 감속 모션 재생 (지정 시간 동안 Animator speed를 서서히 줄여 자연스러운 회전 감속 표현)
        /// </summary>
        public async UniTask PlayRollDecelAsync(float duration, float startSpeed = 1.2f, float endSpeed = 0.3f, CancellationToken token = default)
        {
            PlayRollBarrage();
            if (animator == null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token);
                return;
            }

            float elapsed = 0f;
            try
            {
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    float t = elapsed / Mathf.Max(0.001f, duration);
                    animator.speed = Mathf.Lerp(startSpeed, endSpeed, t);
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
            }
            finally
            {
                if (animator != null)
                {
                    animator.speed = 1.0f;
                }
            }
        }
    }
}
