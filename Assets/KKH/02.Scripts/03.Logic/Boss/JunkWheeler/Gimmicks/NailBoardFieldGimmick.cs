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
    /// [전장 기물: 고철 못판 / 스파이크 트랩 (NailBoardFieldGimmick)]
    /// 기획서 §6.18, §3.2 (6), §4.1:
    /// 1. 맵에 사전 배치되는 고철 못판(Spike.png) 전장 기물.
    /// 2. 평상시: 지면에 납작하게 숨겨진 대기 상태 (Frame 0).
    /// 3. 보스 내려찍기(Slam) 낙하 또는 밟음(Step) 발생 시:
    ///    - 못판이 솟구치는 4단계 애니메이션(Frame 0 -> 1 -> 2 -> 3) 실시간 재생.
    ///    - 바닥 접지면(Y좌표)을 자동 고정하여 스프라이트가 가라앉지 않고 위로 날카롭게 솟구침.
    /// 4. 효과 적용:
    ///    - 보스 착지/접촉 시: 폐타이어(TIRE_PART) 내구도 1 관통 피해(펑크) + 4초 그로기(받는 피해 1.5배) 유발.
    ///    - 플레이어 접촉 시: 관통 피해(20) 및 경직 적용 (Space 회피 무적 중에는 회피).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class NailBoardFieldGimmick : BossFieldGimmickBase
    {
        public override string FieldGimmickID => "GIMMICK_NAIL_BOARD";
        public override string GimmickName => "고철 못판 (스파이크 트랩)";

        public enum SpikeState
        {
            Retracted, // 지면에 납작하게 숨겨진 상태 (Frame 0)
            Emerging,  // 못이 솟구쳐 나오는 중 (Frame 0 -> 1 -> 2 -> 3)
            Extended,  // 완전히 돌출된 위험 상태 (Frame 3)
            Retracting // 다시 지면으로 들어가는 중 (Frame 4 -> 5 -> 0)
        }

        [Header("1. 스파이크 프레임 애니메이션 (Spike.png)")]
        [Tooltip("Spike.png의 슬라이스된 6개 프레임 (미할당 시 자동 탐색 및 바인딩)")]
        [SerializeField] private Sprite[] spikeFrames;
        [Tooltip("스파이크 솟구침(Emerge) 재생 속도 (FPS, 기본 14 = 약 0.21초 동안 솟구침)")]
        [SerializeField] private float emergeFps = 14.0f;
        [Tooltip("스파이크 복귀(Retract) 재생 속도 (FPS, 기본 10.0)")]
        [SerializeField] private float retractFps = 10.0f;

        [Header("2. 파훼 효과 파라미터")]
        [SerializeField] private float groggyDuration = 4.0f;
        [SerializeField] private float damageMultiplier = 1.5f;
        [SerializeField] private int tirePierceDamage = 1;
        [Tooltip("일회용 트랩 여부: 기믹 효과 발동 시 영구 소모되어 재사용 불가")]
        [SerializeField] private bool isSingleUse = true;
        [Tooltip("일회용 소모 후 오브젝트 파괴 지연 시간 (초, 0.8초 권장)")]
        [SerializeField] private float destroyDelay = 0.8f;
        [Tooltip("파훼 성공 후 오브젝트 영구 파괴 여부 (하위 호환)")]
        [SerializeField] private bool destroyOnPuncture = true;
        [Tooltip("그로기 지속 후 스파이크가 지면으로 다시 들어가는 대기 시간 (초, isSingleUse=false일 때만 적용)")]
        [SerializeField] private float retractDelayAfterGroggy = 4.0f;

        [Header("3. 발동 조건 제어")]
        [Tooltip("보스의 내려찍기(Slam) 패턴 중에만 못판이 작동하도록 엄격히 제한 (일반 보스 이동/돌진 및 플레이어 밟음 시 발동 차단)")]
        [SerializeField] private bool onlyActivateOnSlam = true;

        [Header("4. 밟음(Step) 판정 파라미터 (onlyActivateOnSlam=false 시 적용)")]
        [Tooltip("밟았을 때 스파이크 자동 발동 여부")]
        [SerializeField] private bool triggerOnStep = true;
        [Tooltip("플레이어가 솟아오른 못판을 밟았을 때 입는 피해")]
        [SerializeField] private int playerStepDamage = 20;
        [Tooltip("플레이어 밟음 피해 쿨다운 (초)")]
        [SerializeField] private float playerDamageCooldown = 1.0f;
        [Tooltip("플레이어가 밟았을 때도 일회용으로 즉시 소모할지 여부 (기본: false, 보스 타이어 펑크 시에만 소모)")]
        [SerializeField] private bool consumeOnPlayerStep = false;

        [Header("5. 시각 효과(VFX) 프리팹 (선택)")]
        [Tooltip("못판 파열/스파크 VFX (예: FX_Flipbook_StarShock)")]
        [SerializeField] private GameObject breakVfxPrefab;
        [SerializeField] private ParticleSystem breakParticle;

        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private float _lastPlayerDamageTime;
        private CancellationTokenSource _animCts;

        public SpikeState CurrentSpikeState { get; private set; } = SpikeState.Retracted;
        public bool IsSpikeExtended => CurrentSpikeState == SpikeState.Extended;
        public bool IsConsumed { get; private set; } = false;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();

            if (_collider == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.75f, 0.45f);
                box.offset = new Vector2(0f, 0.2f);
                _collider = box;
            }
            else
            {
                _collider.isTrigger = true;
            }

            EnsureSpikeFramesLoaded();

            if (spikeFrames != null && spikeFrames.Length > 0 && spikeFrames[0] != null)
            {
                SetFrame(0);
            }
        }

        private void OnDestroy()
        {
            CancelAnimationToken();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            EnsureSpikeFramesLoaded();
            if (GetComponent<Collider2D>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.75f, 0.45f);
                box.offset = new Vector2(0f, 0.2f);
            }
        }

        private void OnValidate()
        {
            if (spikeFrames == null || spikeFrames.Length == 0)
            {
                EnsureSpikeFramesLoaded();
            }
        }
#endif

        public void EnsureSpikeFramesLoaded()
        {
            if (spikeFrames != null && spikeFrames.Length >= 6 && spikeFrames[0] != null) return;

#if UNITY_EDITOR
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/KKH/04.Prefab/Spike.png");
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s) list.Add(s);
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            if (list.Count > 0)
            {
                spikeFrames = list.ToArray();
                if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer != null && _spriteRenderer.sprite == null && spikeFrames.Length > 0)
                {
                    _spriteRenderer.sprite = spikeFrames[0];
                }
            }
#endif
        }

        private void SetFrame(int frameIndex)
        {
            if (_spriteRenderer == null || spikeFrames == null || frameIndex < 0 || frameIndex >= spikeFrames.Length) return;
            var sprite = spikeFrames[frameIndex];
            if (sprite == null) return;

            _spriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// [못판 솟구침 애니메이션 트리거]
        /// 보스가 내려찍기 낙하를 시작할 때 즉시 호출되어 못이 지면에서 솟구침 (Frame 0 -> 1 -> 2 -> 3)
        /// </summary>
        public void TriggerEmerge(Action onEmergeCompleted = null)
        {
            if (IsConsumed) return;
            if (CurrentSpikeState == SpikeState.Emerging || CurrentSpikeState == SpikeState.Extended)
            {
                onEmergeCompleted?.Invoke();
                return;
            }

            CancelAnimationToken();
            _animCts = new CancellationTokenSource();
            RunEmergeSequenceAsync(_animCts.Token, onEmergeCompleted).Forget();
        }

        private async UniTaskVoid RunEmergeSequenceAsync(CancellationToken token, Action onCompleted)
        {
            CurrentSpikeState = SpikeState.Emerging;
            if (JunkWheelerSfx.Data != null) JunkWheelerSfx.PlayOrFallback(JunkWheelerSfx.Data.fieldNailBoardRise, JunkWheelerSfx.Data.bladeDeploy, transform.position, 1f, 1.3f, 0.7f);
            float frameDur = 1.0f / Mathf.Max(1.0f, emergeFps);

            try
            {
                // Frame 0 -> 1 -> 2 -> 3
                for (int i = 0; i <= 3 && i < spikeFrames.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    SetFrame(i);
                    await UniTask.Delay(TimeSpan.FromSeconds(frameDur), cancellationToken: token);
                }

                CurrentSpikeState = SpikeState.Extended;
                onCompleted?.Invoke();

                // 내려찍기 전용 모드에서 보스가 낙하하지 않고 빗나간 경우, 2.5초 후 자동으로 지면으로 복귀
                if (onlyActivateOnSlam && !IsConsumed)
                {
                    ScheduleRetractAsync(2.5f).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                // 애니메이션 중단
            }
        }

        /// <summary>
        /// [못판 복귀 애니메이션]
        /// 돌출된 못이 다시 지면 속으로 들어감 (Frame 4 -> 5 -> 0)
        /// </summary>
        public async UniTask PlayRetractAsync(CancellationToken token = default)
        {
            if (IsConsumed || CurrentSpikeState == SpikeState.Retracted) return;

            CurrentSpikeState = SpikeState.Retracting;
            float frameDur = 1.0f / Mathf.Max(1.0f, retractFps);

            try
            {
                int[] retractSequence = new int[] { 4, 5, 0 };
                foreach (int idx in retractSequence)
                {
                    token.ThrowIfCancellationRequested();
                    if (idx < spikeFrames.Length)
                    {
                        SetFrame(idx);
                        await UniTask.Delay(TimeSpan.FromSeconds(frameDur), cancellationToken: token);
                    }
                }
                CurrentSpikeState = SpikeState.Retracted;
                if (!IsConsumed) IsInteractable = true;
            }
            catch (OperationCanceledException)
            {
                // 중단
            }
        }

        public override void ResetGimmick()
        {
            if (isSingleUse && IsConsumed) return; // 일회용 트랩은 리셋 불가
            base.ResetGimmick();
            CurrentSpikeState = SpikeState.Retracted;
            SetFrame(0);
        }

        /// <summary>
        /// [내려찍기 전용 발동 메서드]
        /// 보스가 내려찍기 패턴(Slam) 중 못판 위치로 낙하하여 착지했을 때 패턴 측에서 직접 호출함
        /// </summary>
        public void TriggerBySlam(GameObject bossActor)
        {
            if (IsConsumed || !IsInteractable || bossActor == null) return;
            var boss = bossActor.GetComponent<BossControllerBase>() ?? bossActor.GetComponentInParent<BossControllerBase>();
            if (boss != null)
            {
                var slam = boss.GetGimmick<JunkWheelerSlamPatternGimmick>("PATTERN_SLAM");
                if (slam != null && !slam.CanTriggerNailBoard) return;
            }
            ExecuteGimmickEffect(bossActor);
            OnPostInteract();
        }

        protected override bool CanInteract(GameObject actor)
        {
            if (IsConsumed || !IsInteractable || actor == null) return false;

            var boss = actor.GetComponent<BossControllerBase>() ?? actor.GetComponentInParent<BossControllerBase>();
            if (boss == null) return false;

            // 회전 칼날 당구 패턴(Ricochet) 중에는 못판 상호작용 완전 배제
            var ricochet = boss.GetGimmick<JunkWheelerRicochetBladePatternGimmick>(JunkWheelerRicochetBladePatternGimmick.PatternRicochetBladeID);
            if (ricochet != null && ricochet.IsActionRunning) return false;

            if (onlyActivateOnSlam)
            {
                var slam = boss.GetGimmick<JunkWheelerSlamPatternGimmick>("PATTERN_SLAM");
                if (slam != null && !slam.CanTriggerNailBoard)
                {
                    return false;
                }
            }

            return true;
        }

        protected override void ExecuteGimmickEffect(GameObject actor)
        {
            if (IsConsumed) return;
            var boss = actor.GetComponent<BossControllerBase>() ?? actor.GetComponentInParent<BossControllerBase>();
            if (boss == null) return;

            // 1. 상태 즉시 일회용 소모 확정 및 콜라이더 비활성화 (다중 충돌 및 중복 인터럽트 원천 차단)
            IsConsumed = true;
            IsInteractable = false;
            if (_collider != null) _collider.enabled = false;

            Debug.Log($"<color=magenta>[NailBoardFieldGimmick] ▶ [기믹 파훼 성공 - 일회용 소모] '{gameObject.name}' 보스 펑크 발생 및 4초 그로기 유발!</color>");

            // 2. 스파이크가 아직 다 솟지 않았다면 즉시 최대 돌출 프레임(Frame 3)으로 강제 고정
            SetFrame(Mathf.Min(3, spikeFrames.Length - 1));
            if (JunkWheelerSfx.Data != null)
            {
                JunkWheelerSfx.Play(JunkWheelerSfx.Data.nailPuncture, transform.position);
                JunkWheelerSfx.Play(JunkWheelerSfx.Data.tireBlowout, transform.position);
            }
            CurrentSpikeState = SpikeState.Extended;

            // 3. 타이어 부위 기믹 내구도 차감 (관통)
            var tireGimmick = boss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick != null)
            {
                tireGimmick.TakePartDamage(tirePierceDamage, GimmickTag.Pierce);
            }

            // 4. 보스 4초 그로기 (피해 1.5배 증폭) 인터럽트
            boss.InterruptToGroggy(groggyDuration, damageMultiplier);

            // 5. [VFX] 파열 및 스파크 이펙트
            if (breakVfxPrefab != null)
            {
                BossVfxHelper.SpawnVfx(breakVfxPrefab, transform.position + Vector3.up * 0.3f);
            }

            if (breakParticle != null)
            {
                breakParticle.transform.SetParent(null);
                breakParticle.Play();
                Destroy(breakParticle.gameObject, 2.0f);
            }

            // 6. 일회용 소멸 수명 주기
            if (isSingleUse || destroyOnPuncture)
            {
                CancelAnimationToken();
                float delay = Mathf.Max(0.1f, destroyDelay);
                if (_spriteRenderer != null && delay > 0.1f)
                {
                    _ = _spriteRenderer.DOFade(0f, delay).SetEase(Ease.InQuad);
                }
                Destroy(gameObject, delay);
            }
            else
            {
                // 재사용 가능 트랩 설정일 때만 지면 복귀 스케줄 가동
                ScheduleRetractAsync(retractDelayAfterGroggy).Forget();
            }
        }

        private async UniTaskVoid ScheduleRetractAsync(float delay)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            if (canceled) return;

            await PlayRetractAsync(this.GetCancellationTokenOnDestroy());
        }

        // ========================================================================
        // 밟음(Step) 판정
        // ========================================================================

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleStepped(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleStepped(other);
        }

        private void HandleStepped(Collider2D other)
        {
            if (other == null || IsConsumed) return;

            // 보스가 회전 칼날 당구(Ricochet) 바운스 비행 중이면 지면 못판 트리거 무시
            var bossCtrl = other.GetComponent<BossControllerBase>() ?? other.GetComponentInParent<BossControllerBase>();
            if (bossCtrl != null)
            {
                var ricochet = bossCtrl.GetGimmick<JunkWheelerRicochetBladePatternGimmick>(JunkWheelerRicochetBladePatternGimmick.PatternRicochetBladeID);
                if (ricochet != null && ricochet.IsActionRunning) return;
            }

            // [내려찍기(Slam) 전용 모드]
            if (onlyActivateOnSlam)
            {
                // 보스가 밟았고 현재 내려찍기(Slam) 패턴 중일 때만 반응
                var bossObj = (other.GetComponent<BossControllerBase>() ?? other.GetComponentInParent<BossControllerBase>());
                if (bossObj != null && IsInteractable && !IsConsumed)
                {
                    var slam = bossObj.GetGimmick<JunkWheelerSlamPatternGimmick>("PATTERN_SLAM");
                    if (slam != null && slam.CanTriggerNailBoard)
                    {
                        TriggerEmerge(() => ExecuteGimmickEffect(bossObj.gameObject));
                    }
                }
                // 일반 이동, 돌진, 플레이어 보행 중에는 못판이 전혀 반응하지 않음 (가시 숨김 유지)
                return;
            }

            // [일반 밟음 모드 (onlyActivateOnSlam == false)]
            // 1. 보스가 밟거나 접촉한 경우 (일회용 기믹 파훼)
            if (other.CompareTag("Boss") || other.GetComponent<BossControllerBase>() != null || other.GetComponentInParent<BossControllerBase>() != null)
            {
                var boss = (other.GetComponent<BossControllerBase>() ?? other.GetComponentInParent<BossControllerBase>())?.gameObject;
                if (boss != null && IsInteractable && !IsConsumed)
                {
                    // 솟구침 애니메이션과 함께 효과 즉각 발동
                    TriggerEmerge(() => ExecuteGimmickEffect(boss));
                }
                return;
            }

            // 2. 플레이어가 밟은 경우
            if (triggerOnStep && !IsConsumed && (other.CompareTag("Player") || other.GetComponentInParent<ActionExecutor>() != null))
            {
                var playerObj = other.CompareTag("Player") ? other.gameObject : other.GetComponentInParent<ActionExecutor>()?.gameObject;
                if (playerObj == null) return;

                // 스파이크가 숨겨져 있다면 솟구치게 유도
                if (CurrentSpikeState == SpikeState.Retracted)
                {
                    TriggerEmerge();
                }

                // 솟아있는 상태에서 접촉 시 플레이어 피해
                if (CurrentSpikeState == SpikeState.Extended || CurrentSpikeState == SpikeState.Emerging)
                {
                    if (Time.time - _lastPlayerDamageTime >= playerDamageCooldown)
                    {
                        _lastPlayerDamageTime = Time.time;

                        // Space 무적 회피 중인지 검사
                        var executor = playerObj.GetComponent<ActionExecutor>() ?? playerObj.GetComponentInParent<ActionExecutor>();
                        if (executor != null && executor.State != null && executor.State.CurrentAction != null && executor.State.CurrentAction.IsInvincibleDuringActive)
                        {
                            return; // 회피 성공
                        }

                        CombatDataHub.Instance?.ApplyCoreDamage("Player", playerStepDamage);
                        Debug.Log($"<color=orange>[NailBoardFieldGimmick] 플레이어가 솟아오른 못판(Spike)을 밟아 피해 {playerStepDamage}를 입었습니다!</color>");

                        if (consumeOnPlayerStep)
                        {
                            IsConsumed = true;
                            IsInteractable = false;
                            if (_collider != null) _collider.enabled = false;
                            Destroy(gameObject, 0.4f);
                        }
                    }
                }
            }
        }

        private void CancelAnimationToken()
        {
            if (_animCts != null)
            {
                if (!_animCts.IsCancellationRequested) _animCts.Cancel();
                _animCts.Dispose();
                _animCts = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.75f);
            Vector3 center = transform.position + Vector3.up * 0.2f;
            Gizmos.DrawWireCube(center, new Vector3(1.75f, 0.45f, 0.2f));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.35f);
            Vector3 center = transform.position + Vector3.up * 0.2f;
            Gizmos.DrawWireCube(center, new Vector3(1.75f, 0.45f, 0.2f));
        }
#endif
    }
}
