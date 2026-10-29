using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using RealSteel.Boss.FSM;
using RealSteel.Boss.Visual;

namespace RealSteel.Boss.Core
{
    public abstract class BossControllerBase : MonoBehaviour
    {
        [Header("1. 보스 데이터 원장 에셋")]
        [SerializeField] protected BossMasterData masterData;

        [Header("2. 디버그 모니터링")]
        [SerializeField] protected string currentStateName;
        [SerializeField] protected int currentHpView;
        [SerializeField] protected bool isGroggyView;

        [Header("3. Camera Bounds (X-Axis)")]
        [SerializeField] protected bool clampToCameraBounds = true;
        [SerializeField] protected float cameraEdgePadding = 0.65f;
        [SerializeField] protected Camera targetCamera;

        public Camera TargetCamera
        {
            get => targetCamera != null ? targetCamera : Camera.main;
            set => targetCamera = value;
        }

        public BossMasterData MasterData => masterData;
        public BossSnapshot Snapshot { get; protected set; }
        public BossStateMachine StateMachine { get; protected set; }
        public BossAnimationController Animation => animationController;

        [Header("4. 애니메이션 컨트롤러 (미할당 시 자동 탐색)")]
        [SerializeField] protected BossAnimationController animationController;

        protected readonly Dictionary<string, BossGimmickBase> gimmickRegistry = new();
        protected readonly List<BossPatternGimmickBase> activePatternPool = new();
        protected CancellationTokenSource _stateCts;
        protected int _patternIndex = 0;

        // 대외 브로드캐스팅 이벤트
        public event Action<string> OnStateChanged;
        public event Action<int, int> OnHpChanged;
        public event Action<bool, float> OnGroggyChanged;
        public event Action<bool, float> OnStunChanged;
        public event Action<int, int> OnDpsCheckProgressChanged;
        public event Action<BossPatternData> OnPatternStarted;

        protected virtual void Awake()
        {
            InitializeBoss();
        }

        protected virtual void Update()
        {
            StateMachine?.Update();

            if (Snapshot != null)
            {
                currentHpView = Snapshot.currentHp;
                isGroggyView = Snapshot.isGroggy;
            }
            if (StateMachine?.CurrentState != null)
            {
                currentStateName = StateMachine.CurrentState.StateName;
            }
        }

        protected virtual void LateUpdate()
        {
            if (clampToCameraBounds)
            {
                // Y축(점프/급강하)은 완전히 자유롭게 유지하고, X축 위치만 카메라 화면 내로 보정
                float clampedX = ClampToCameraX(transform.position.x);
                if (!Mathf.Approximately(clampedX, transform.position.x))
                {
                    transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
                }
            }
        }

        public bool TryGetCameraBoundsX(out float minX, out float maxX, float padding = -1f)
        {
            float pad = padding >= 0f ? padding : cameraEdgePadding;
            Camera cam = TargetCamera;

            if (cam == null)
            {
                minX = float.MinValue;
                maxX = float.MaxValue;
                return false;
            }

            float distance = Mathf.Abs(cam.transform.position.z - transform.position.z);
            Vector3 leftPoint = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
            Vector3 rightPoint = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));

            minX = Mathf.Min(leftPoint.x, rightPoint.x) + pad;
            maxX = Mathf.Max(leftPoint.x, rightPoint.x) - pad;
            return true;
        }

        public float ClampToCameraX(float desiredX, float padding = -1f)
        {
            if (TryGetCameraBoundsX(out float minX, out float maxX, padding))
            {
                return Mathf.Clamp(desiredX, minX, maxX);
            }
            return desiredX;
        }

        protected virtual void OnDestroy()
        {
            CancelCurrentStateToken();
        }

        // ========================================================================
        // 템플릿 메서드: 보스 초기화 시퀀스
        // ========================================================================
        public void InitializeBoss()
        {
            SetupSnapshot();
            SetupAnimationController();

            // CombatDataHub에 1:1 보스전 자동 바인딩
            if (CombatDataHub.Instance != null && Snapshot != null)
            {
                CombatDataHub.Instance.InitializeBossBattle(CombatDataHub.Instance.PlayerSnapshot, Snapshot);
            }

            SetupStateMachine();
            RegisterGimmicks(); // 구체 보스 훅
            BuildPhasePatternPool(Snapshot.currentPhase);
            OnBossInitialized(); // 구체 보스 훅

            StartCombatLoop();
        }

        protected virtual void SetupAnimationController()
        {
            if (animationController == null)
            {
                animationController = GetComponentInChildren<BossAnimationController>(true);
            }
            if (animationController == null)
            {
                animationController = gameObject.AddComponent<BossAnimationController>();
            }
        }

        protected virtual void SetupSnapshot()
        {
            if (masterData == null)
            {
                Debug.LogError($"[{GetType().Name}] BossMasterData가 지정되지 않았습니다!");
                return;
            }

            Snapshot = new BossSnapshot
            {
                bossID = masterData.bossID,
                bossName = masterData.bossName,
                maxHp = masterData.maxHp,
                currentHp = masterData.maxHp,
                baseDefense = masterData.baseDefense,
                currentPhase = 1,
                isGroggy = false,
                currentDamageMultiplier = 1.0f
            };

            foreach (var gimmick in masterData.gimmickParts)
            {
                Snapshot.gimmickStates[gimmick.gimmickID] = new BossGimmickRuntimeState(
                    gimmick.gimmickID, gimmick.gimmickName, gimmick.isPhysicalBossPart, gimmick.maxDurability
                );
            }
        }

        protected virtual void SetupStateMachine()
        {
            StateMachine = new BossStateMachine();
            StateMachine.OnStateChanged += (prev, next) =>
            {
                OnStateChanged?.Invoke(next);
            };
        }

        protected virtual void StartCombatLoop()
        {
            var token = RenewStateCancellationToken();
            float postDelay = masterData != null ? masterData.patternPostDelay : 1.0f;
            StateMachine.Initialize(new BossIdleState(this, token, postDelay));
        }

        // ========================================================================
        // 템플릿 메서드: 피격 처리 (SSOT 연동)
        // ========================================================================
        public virtual void TakeDamage(int rawDamage, bool isAlreadyCalculated = false)
        {
            if (Snapshot == null || !Snapshot.IsAlive) return;

            int adjustedDamage = OnPreTakeDamage(rawDamage);
            int finalDamage;
            if (isAlreadyCalculated)
            {
                finalDamage = adjustedDamage;
            }
            else
            {
                float multiplier = Snapshot.isGroggy ? Snapshot.currentDamageMultiplier : 1.0f;
                finalDamage = CombatCalculator.CalculateDamage(adjustedDamage * multiplier, Snapshot.baseDefense);
            }

            Snapshot.currentHp = Mathf.Max(0, Snapshot.currentHp - finalDamage);
            OnHpChanged?.Invoke(Snapshot.currentHp, Snapshot.maxHp);

            StateMachine?.OnDamageTaken(finalDamage);
            NotifyGimmicksDamageTaken(finalDamage);

            CheckPhaseTransition();

            if (Snapshot.currentHp <= 0)
            {
                OnBossDefeated();
                BattleManager.Instance?.OnFighterKilled(Snapshot.bossID);
            }
        }

        /// <summary>
        /// CombatDataHub.ProcessHit 등 외부에서 이미 방어력/배율 연산이 완료된 대미지를 수신할 때 사용함 (이중 차감 방지)
        /// </summary>
        public void NotifyDamageFromHub(int finalDamage)
        {
            TakeDamage(finalDamage, isAlreadyCalculated: true);
        }

        // ========================================================================
        // 기믹 레지스트리 관리
        // ========================================================================
        public void RegisterGimmick(BossGimmickBase gimmick)
        {
            if (gimmick == null) return;
            gimmickRegistry[gimmick.GimmickID] = gimmick;
            gimmick.Attach(this);

            if (gimmick is BossPatternGimmickBase patternGimmick)
            {
                activePatternPool.Add(patternGimmick);
            }
        }

        public T GetGimmick<T>(string gimmickId) where T : BossGimmickBase
        {
            return gimmickRegistry.TryGetValue(gimmickId, out var g) ? g as T : null;
        }

        public void NotifyGimmicksDamageTaken(int damage)
        {
            foreach (var kvp in gimmickRegistry)
            {
                kvp.Value.NotifyDamageTaken(damage);
            }
        }

        // ========================================================================
        // 패턴 선택 및 인터럽트 제어
        // ========================================================================
        public virtual BossPatternGimmickBase GetNextPatternGimmick()
        {
            if (activePatternPool.Count == 0) return null;

            var validPool = activePatternPool.FindAll(p => p != null && !Snapshot.IsSkillSealed(p.GimmickID));
            if (validPool.Count == 0) validPool = activePatternPool;

            var pattern = validPool[_patternIndex % validPool.Count];
            _patternIndex++;
            return pattern;
        }

        public virtual void InterruptToGroggy(float duration = 4.0f, float damageMultiplier = 1.5f)
        {
            var token = RenewStateCancellationToken();
            StateMachine.ChangeState(new BossGroggyState(this, token, duration, damageMultiplier));
        }

        public virtual void InterruptToStun(float duration = 3.0f)
        {
            var token = RenewStateCancellationToken();
            StateMachine.ChangeState(new BossStunState(this, token, duration));
        }

        public virtual void NotifyDPSCheckProgress(int currentDamage, int requiredDamage)
        {
            // DPS 체크 저지 게이지 UI 연동 및 하위 보스 클래스 오버라이드용 훅
            // Debug.Log($"[DPS Check] {currentDamage} / {requiredDamage}");
            NotifyDpsCheckProgress(currentDamage, requiredDamage);
        }
        public CancellationToken RenewStateCancellationToken()
        {
            CancelCurrentStateToken();
            _stateCts = new CancellationTokenSource();
            return _stateCts.Token;
        }

        public void CancelCurrentStateToken()
        {
            if (_stateCts != null)
            {
                if (!_stateCts.IsCancellationRequested) _stateCts.Cancel();
                _stateCts.Dispose();
                _stateCts = null;
            }
        }

        // ========================================================================
        // 페이즈 전환 템플릿
        // ========================================================================
        protected virtual void CheckPhaseTransition()
        {
            if (masterData == null || masterData.phases == null) return;

            float currentRatio = (float)Snapshot.currentHp / Snapshot.maxHp;
            foreach (var phase in masterData.phases)
            {
                if (phase.phaseIndex > Snapshot.currentPhase && currentRatio <= phase.hpThresholdRatio)
                {
                    TransitionPhase(phase.phaseIndex);
                    break;
                }
            }
        }

        public virtual void TransitionPhase(int nextPhase)
        {
            int prevPhase = Snapshot.currentPhase;
            Snapshot.currentPhase = nextPhase;
            BuildPhasePatternPool(nextPhase);
            OnPhaseChanged(prevPhase, nextPhase);
        }

        protected virtual void BuildPhasePatternPool(int phase) { }

        // ========================================================================
        // 하위 클래스 확장용 훅(Hook) 메서드
        // ========================================================================
        protected abstract void RegisterGimmicks();
        protected virtual void OnBossInitialized() { }
        protected virtual int OnPreTakeDamage(int rawDamage) => rawDamage;
        protected virtual void OnPhaseChanged(int prevPhase, int newPhase) { }
        protected virtual void OnBossDefeated() { }

        // 대외 통보 헬퍼
        public void NotifyDpsCheckProgress(int cur, int req) => OnDpsCheckProgressChanged?.Invoke(cur, req);
        public void NotifyPatternStarted(BossPatternData p) => OnPatternStarted?.Invoke(p);
        public void SetGroggyView(bool groggy, float dur) { Snapshot.isGroggy = groggy; OnGroggyChanged?.Invoke(groggy, dur); }
        public void SetStunView(bool stun, float dur) => OnStunChanged?.Invoke(stun, dur);
    }
}
