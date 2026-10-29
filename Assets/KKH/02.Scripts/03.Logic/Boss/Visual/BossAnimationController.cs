using System;
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

        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public int CurrentStateHash => _currentHash;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _baseScaleX = Mathf.Abs(transform.localScale.x);
            if (Mathf.Approximately(_baseScaleX, 0f))
            {
                _baseScaleX = 1f;
            }
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
        public void Play(int stateHash, float normalizedTime = 0f)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy) return;

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
        public void PlaySlamJump() => Play(StateSlamJump);
        public void PlaySlamFall() => Play(StateSlamFall);
        public void PlaySlamLand() => Play(StateSlamLand);
        public void PlayGroggy() => Play(StateGroggy);
        public void PlayStun() => Play(StateStun);
    }
}

