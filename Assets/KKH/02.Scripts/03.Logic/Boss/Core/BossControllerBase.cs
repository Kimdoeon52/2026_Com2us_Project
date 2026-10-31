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
        [SerializeField] protected float cameraEdgePadding = 0.50f;
        [SerializeField] protected Camera targetCamera;
        protected CombatCamera _cachedCombatCamera;
        private float _initialGroundY;
        private bool _hasInitialGroundY = false;

        /// <summary>
        /// 보스의 기본 지면 Y 좌표 (공중 패턴 종료/캔슬 후 바닥 복귀 기준점)
        /// </summary>
        public float GroundY
        {
            get
            {
                if (!_hasInitialGroundY)
                {
                    _initialGroundY = transform.position.y;
                    _hasInitialGroundY = true;
                }
                return _initialGroundY;
            }
            protected set
            {
                _initialGroundY = value;
                _hasInitialGroundY = true;
            }
        }

        /// <summary>
        /// 보스가 공중에 떠 있는 경우 기본 지면 높이(GroundY)로 즉시 스냅 복귀시킴.
        /// </summary>
        public void SnapToGroundY()
        {
            float gy = GroundY;
            if (Mathf.Abs(transform.position.y - gy) > 0.001f)
            {
                transform.position = new Vector3(transform.position.x, gy, transform.position.z);
            }
        }

        public Camera TargetCamera
        {
            get => targetCamera != null ? targetCamera : Camera.main;
            set => targetCamera = value;
        }

        public CombatCamera TargetCombatCamera
        {
            get
            {
                if (_cachedCombatCamera == null)
                {
                    Camera cam = TargetCamera;
                    if (cam != null) _cachedCombatCamera = cam.GetComponent<CombatCamera>();
                    if (_cachedCombatCamera == null) _cachedCombatCamera = FindFirstObjectByType<CombatCamera>();
                }
                return _cachedCombatCamera;
            }
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
        private CombatDataHub _subscribedHub;
        private bool _isDefeatedHandled = false;

        /// <summary>
        /// StopBossCombat()으로 전투가 의도적으로 정지되었는지 여부 (FSM 자동 복구 로직이 루프를 되살리지 않도록 구분)
        /// </summary>
        public bool IsCombatStopped { get; private set; }

        // 대외 브로드캐스팅 이벤트
        public event Action<string> OnStateChanged;
        public event Action<int, int> OnHpChanged;
        public event Action<bool, float> OnGroggyChanged;
        public event Action<bool, float> OnStunChanged;
        public event Action<int, int> OnDpsCheckProgressChanged;
        public event Action<BossPatternData> OnPatternStarted;

        protected virtual void Reset()
        {
            AutoBindInspectorReferences();
        }

        protected virtual void OnValidate()
        {
            AutoBindInspectorReferences();
        }

        public virtual void AutoBindInspectorReferences()
        {
#if UNITY_EDITOR
            if (masterData == null)
            {
                masterData = UnityEditor.AssetDatabase.LoadAssetAtPath<BossMasterData>("Assets/KKH/03.SOData/Boss/BossMasterData_JunkWheeler.asset");
            }
#endif
            if (animationController == null)
            {
                animationController = GetComponentInChildren<BossAnimationController>(true);
            }
            else
            {
                animationController.AutoBindReferences();
            }

            if (targetCamera == null && gameObject.scene.IsValid())
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }
        }

        protected virtual void Awake()
        {
            InitializeBoss();
        }

        protected virtual void Update()
        {
            EnsureHubSubscription();
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
                // CombatCamera보다 먼저 LateUpdate가 실행되더라도 카메라 줌아웃을 막지 않도록 스테이지 최대 카메라 경계로 보정
                float clampedX = ClampToStageWallX(transform.position.x);
                if (!Mathf.Approximately(clampedX, transform.position.x))
                {
                    transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
                }
            }
        }

        /// <summary>
        /// 현재 프레임의 순간 카메라 뷰포트 좌/우 X 경계를 반환함 (기울어진 3D 카메라 왜곡을 배제한 평면 투영).
        /// </summary>
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

            float halfWidth;
            if (cam.orthographic)
            {
                halfWidth = cam.orthographicSize * cam.aspect;
            }
            else
            {
                float distance = Mathf.Abs(cam.transform.position.z - transform.position.z);
                halfWidth = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * cam.aspect;
            }

            float centerX = cam.transform.position.x;
            minX = centerX - halfWidth + pad;
            maxX = centerX + halfWidth - pad;
            return true;
        }

        /// <summary>
        /// CombatCamera가 줌아웃(farZ) 및 스테이지 이동 한계(minX~maxX)에 도달했을 때의 '최대 전장 카메라 벽(Stage Wall)' 좌/우 X 경계를 반환함.
        /// 돌진이나 당구 바운스처럼 카메라를 밀어붙이며 화면 끝까지 가야 하는 패턴에서 사용함.
        /// </summary>
        public bool TryGetStageWallBoundsX(out float minX, out float maxX, float padding = -1f)
        {
            float pad = padding >= 0f ? padding : cameraEdgePadding;
            Camera cam = TargetCamera;
            var combatCam = TargetCombatCamera;

            if (cam != null && combatCam != null)
            {
                var minXField = typeof(CombatCamera).GetField("minX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxXField = typeof(CombatCamera).GetField("maxX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var farZField = typeof(CombatCamera).GetField("farZ", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxOrthoField = typeof(CombatCamera).GetField("maxOrthoSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                float camMinCenter = minXField != null ? (float)minXField.GetValue(combatCam) : -3f;
                float camMaxCenter = maxXField != null ? (float)maxXField.GetValue(combatCam) : 3f;

                float maxHalfWidth;
                if (cam.orthographic)
                {
                    float maxOrtho = maxOrthoField != null ? (float)maxOrthoField.GetValue(combatCam) : cam.orthographicSize;
                    maxHalfWidth = Mathf.Max(cam.orthographicSize, maxOrtho) * cam.aspect;
                }
                else
                {
                    float farZ = farZField != null ? (float)farZField.GetValue(combatCam) : cam.transform.position.z;
                    float maxDist = Mathf.Max(Mathf.Abs(cam.transform.position.z - transform.position.z), Mathf.Abs(farZ - transform.position.z));
                    maxHalfWidth = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * maxDist * cam.aspect;
                }

                // 현재 순간 카메라 경계와 최대 스테이지 경계 중 더 넓은 범위를 허용하여 카메라가 줌아웃하며 따라올 수 있게 보장
                if (TryGetCameraBoundsX(out float curMinX, out float curMaxX, pad))
                {
                    minX = Mathf.Min(curMinX, camMinCenter - maxHalfWidth + pad);
                    maxX = Mathf.Max(curMaxX, camMaxCenter + maxHalfWidth - pad);
                }
                else
                {
                    minX = camMinCenter - maxHalfWidth + pad;
                    maxX = camMaxCenter + maxHalfWidth - pad;
                }
                return true;
            }

            return TryGetCameraBoundsX(out minX, out maxX, pad);
        }

        public float ClampToStageWallX(float desiredX, float padding = -1f)
        {
            if (TryGetStageWallBoundsX(out float minX, out float maxX, padding))
            {
                return Mathf.Clamp(desiredX, minX, maxX);
            }
            return desiredX;
        }

        public float ClampToCameraX(float desiredX, float padding = -1f)
        {
            if (TryGetCameraBoundsX(out float minX, out float maxX, padding))
            {
                return Mathf.Clamp(desiredX, minX, maxX);
            }
            return desiredX;
        }

        protected Transform _cachedPlayerTransform;

        public Transform PlayerTransform
        {
            get
            {
                if (_cachedPlayerTransform == null)
                {
                    var playerObj = GameObject.FindWithTag("Player");
                    if (playerObj != null) _cachedPlayerTransform = playerObj.transform;
                }
                return _cachedPlayerTransform;
            }
        }

        /// <summary>
        /// 보스가 현재 플레이어의 X축 위치를 바라보도록 방향을 전환함.
        /// </summary>
        public void FacePlayer()
        {
            var player = PlayerTransform;
            if (player == null) return;

            float diffX = player.position.x - transform.position.x;
            if (Mathf.Abs(diffX) > 0.05f)
            {
                float dirX = diffX >= 0f ? 1f : -1f;
                FaceDirection(dirX);
            }
        }

        /// <summary>
        /// 특정 X축 방향(dirX > 0 우측, dirX < 0 좌측)으로 스프라이트를 반전시킴.
        /// </summary>
        public void FaceDirection(float dirX)
        {
            if (Mathf.Approximately(dirX, 0f)) return;

            if (Animation != null)
            {
                Animation.FaceDirection(dirX);
            }
            else
            {
                Vector3 scale = transform.localScale;
                float sign = dirX >= 0f ? 1f : -1f;
                scale.x = Mathf.Abs(scale.x) * sign;
                transform.localScale = scale;
            }
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeHub();
            CancelCurrentStateToken();
        }

        private void EnsureHubSubscription()
        {
            var hub = CombatDataHub.Instance;
            if (hub == _subscribedHub) return;
            UnsubscribeHub();
            _subscribedHub = hub;
            if (_subscribedHub != null)
            {
                _subscribedHub.OnBossDamaged += HandleHubBossDamaged;
            }
        }

        private void UnsubscribeHub()
        {
            if (_subscribedHub != null)
            {
                _subscribedHub.OnBossDamaged -= HandleHubBossDamaged;
                _subscribedHub = null;
            }
        }

        // ========================================================================
        // 템플릿 메서드: 보스 초기화 시퀀스
        // ========================================================================
        public void InitializeBoss()
        {
            _isDefeatedHandled = false;
            IsCombatStopped = false;
            if (!_hasInitialGroundY)
            {
                _initialGroundY = transform.position.y;
                _hasInitialGroundY = true;
            }
            SetupSnapshot();
            SetupAnimationController();

            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            // CombatDataHub에 1:1 보스전 자동 바인딩 및 피격 이벤트 구독
            EnsureHubSubscription();
            if (CombatDataHub.Instance != null && Snapshot != null)
            {
                CombatDataHub.Instance.InitializeBossBattle(CombatDataHub.Instance.PlayerSnapshot, Snapshot);
            }

            AutoWireSceneSystems();

            SetupStateMachine();
            RegisterGimmicks(); // 구체 보스 훅
            BuildPhasePatternPool(Snapshot.currentPhase);
            OnBossInitialized(); // 구체 보스 훅

            StartCombatLoop();
        }

        protected virtual void AutoWireSceneSystems()
        {
            // 1. BattleManager의 enemyRobotObject가 비어있거나 비활성화된 더미 Enemy를 가리키면 현재 보스로 자동 교체
            var bm = BattleManager.Instance;
            if (bm != null)
            {
                var enemyField = typeof(BattleManager).GetField("enemyRobotObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enemyField != null)
                {
                    var curEnemy = enemyField.GetValue(bm) as GameObject;
                    if (curEnemy == null || !curEnemy.activeInHierarchy)
                    {
                        enemyField.SetValue(bm, gameObject);
                    }
                }
            }

            // 2. 보스전이므로 CombatCamera의 두 번째 추적 대상(playerB)을 무조건 현재 보스 Transform으로 바인딩
            // (씬에 남아있는 더미 Enemy 로봇이 켜져 있더라도 카메라가 보스를 추적하고 줌아웃하도록 보장)
            var combatCam = TargetCombatCamera;
            if (combatCam != null)
            {
                var playerBField = typeof(CombatCamera).GetField("playerB", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (playerBField != null)
                {
                    var curB = playerBField.GetValue(combatCam) as Transform;
                    if (curB != transform)
                    {
                        if (curB != null && curB.GetComponent<BossControllerBase>() == null && curB.gameObject.activeSelf)
                        {
                            // 보스 씬의 더미 일반 적(Enemy) 오브젝트가 켜져 있으면 비활성화
                            curB.gameObject.SetActive(false);
                        }
                        playerBField.SetValue(combatCam, transform);
                    }
                }
            }
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
            CombatDataHub.Instance?.SyncBossToEnemyProxy();

            ProcessPostDamageReactions(finalDamage);
        }

        /// <summary>
        /// CombatDataHub.ProcessHit(NYH HitDetection)에서 이미 Snapshot.currentHp 차감이 끝난 후
        /// 이중 차감 없이 FSM/기믹(300 DPS 저지)/페이즈 전환/토벌 사망 처리를 수행하는 핸들러임.
        /// </summary>
        protected virtual void HandleHubBossDamaged(int finalDamage)
        {
            if (Snapshot == null) return;
            ProcessPostDamageReactions(finalDamage);
        }

        protected virtual void ProcessPostDamageReactions(int finalDamage)
        {
            if (Snapshot == null) return;

            OnHpChanged?.Invoke(Snapshot.currentHp, Snapshot.maxHp);

            StateMachine?.OnDamageTaken(finalDamage);
            NotifyGimmicksDamageTaken(finalDamage);

            CheckPhaseTransition();

            if (Snapshot.currentHp <= 0 && !_isDefeatedHandled)
            {
                _isDefeatedHandled = true;
                StopBossCombat();
                OnBossDefeated();
                BattleManager.Instance?.OnFighterKilled(Snapshot.bossID);
            }
        }

        /// <summary>
        /// 전투 종료(승리/패배/타임오버) 시 진행 중인 모든 비동기 패턴 루프와 FSM을 즉시 정지함.
        /// </summary>
        public virtual void StopBossCombat()
        {
            IsCombatStopped = true;
            CancelCurrentStateToken();
            foreach (var pattern in activePatternPool)
            {
                if (pattern != null)
                {
                    pattern.InterruptPattern(InterruptReason.PhaseTransition);
                }
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
                if (!activePatternPool.Contains(patternGimmick))
                {
                    activePatternPool.Add(patternGimmick);
                }
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
