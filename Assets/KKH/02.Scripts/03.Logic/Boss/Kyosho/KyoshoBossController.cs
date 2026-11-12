using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.Visual;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [지역 3 보스 '쿄쇼 (Kyosho)' 메인 컨트롤러]
    /// BossControllerBase 프레임워크와 RSNanoSwarm 군집 엔진을 결합하여
    /// 1페이즈 ~ 2페이즈 피날레 격파까지의 박자 동기화 전투 시퀀스를 총괄함.
    /// </summary>
    [RequireComponent(typeof(RSNanoSwarm))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class KyoshoBossController : BossControllerBase
    {
        private enum PendingGateType { None, Variation1, Variation2, Finale }

        [Header("쿄쇼 전용 컴포넌트 및 에셋")]
        [SerializeField] private RSNanoSwarm swarm = default;
        [SerializeField] private KyoshoRhythmTableSO rhythmTable = default;
        [SerializeField] private KyoshoSfxData sfxData = default;
        [SerializeField] private Vector3 blobShapeRatio = new Vector3(3.4f, 2.7f, 0.9f);

        [Header("아레나 및 이동 설정")]
        [SerializeField] private float fallbackArenaMinX = -7.75f;
        [SerializeField] private float fallbackArenaMaxX = 7.75f;
        [SerializeField] private float preferredDistance = 5.5f;
        [SerializeField] private float driftSpeed = 2.0f;

        [Header("패턴 기믹 참조 (자동 탐색/부착)")]
        [SerializeField] private KyoshoConductPatternGimmick conductPattern;
        [SerializeField] private KyoshoStaccatoPatternGimmick staccatoPattern;
        [SerializeField] private KyoshoBloomPatternGimmick bloomPattern;
        [SerializeField] private KyoshoFermataPatternGimmick fermataPattern;
        [SerializeField] private KyoshoVariationPatternGimmick variationPattern;
        [SerializeField] private KyoshoFinalePatternGimmick finalePattern;

        [Header("테스트 밸런스 설정 (본 밸런스 복원 시 1.0)")]
        [Tooltip("테스트용 전체 공격력/파츠 피해 배율 (기획 Lv.21~30 기준 원복 시 1.0으로 설정)")]
        [SerializeField, Range(0.05f, 2f)] private float testDamageScale = 0.2f;

        private KyoshoBeatClock _beatClock;
        private KyoshoSectionManager _sectionManager;
        private KyoshoBeatIndicatorHUD _beatIndicatorHud;
        private KyoshoScorePreviewHUD _scorePreviewHud;
        private KyoshoSectionStatusHUD _sectionStatusHud;
        private KyoshoStageBeatPulse _stageBeatPulse;

        private RSNanoRig _masterRig;
        private CircleCollider2D _hurtCollider;
        private GameObject _rigHostGo;

        private KyoshoPhaseState _phaseState = KyoshoPhaseState.Intro;
        private Vector3 _blobBaseSize;
        private float _arenaFloorY;
        private bool _hasFloorY;
        private bool _driftEnabled;
        private float _beatPulseAmount;

        private int _lastAuthoritativeHp;
        private bool _variation1Triggered;
        private bool _variation1Completed;
        private bool _variation2Triggered;
        private bool _finaleTriggered;
        private int _finaleRetryRemainingPatterns;
        private PendingGateType _pendingGate = PendingGateType.None;
        private bool _isTransitionRunning;
        private int _cycleStepIndex;
        private CancellationTokenSource _gateTransitionCts;

        public RSNanoSwarm Swarm => swarm;
        public KyoshoBeatClock BeatClock => _beatClock;
        public KyoshoSectionManager SectionManager => _sectionManager;
        public KyoshoScorePreviewHUD ScorePreviewHUD => _scorePreviewHud;
        public KyoshoPhaseState PhaseState => _phaseState;
        public float ArenaFloorY => _arenaFloorY;
        public float ArenaMinX { get { GetArenaBounds(out float minX, out _); return minX; } }
        public float ArenaMaxX { get { GetArenaBounds(out _, out float maxX); return maxX; } }

        public bool IsInvincible { get; set; }
        public bool IsWaitingFinalBlow { get; set; }
        public bool HasCompletedFirstPhase2Fermata { get; private set; }
        public float TestDamageScale => Mathf.Clamp(testDamageScale, 0.05f, 2f);

        public override void AutoBindInspectorReferences()
        {
            base.AutoBindInspectorReferences();
#if UNITY_EDITOR
            if (masterData == null || masterData.bossID != KyoshoConstants.BossID)
            {
                var kMaster = UnityEditor.AssetDatabase.LoadAssetAtPath<BossMasterData>("Assets/KKH/03.SOData/Boss/Kyosho/BossMasterData_Kyosho.asset");
                if (kMaster != null) masterData = kMaster;
            }
            if (rhythmTable == null)
            {
                rhythmTable = UnityEditor.AssetDatabase.LoadAssetAtPath<KyoshoRhythmTableSO>("Assets/KKH/03.SOData/Boss/Kyosho/KyoshoRhythmTable.asset");
            }
            if (sfxData == null)
            {
                sfxData = UnityEditor.AssetDatabase.LoadAssetAtPath<KyoshoSfxData>("Assets/KKH/03.SOData/Boss/Kyosho/KyoshoSfxData.asset");
            }
#endif
            if (swarm == null) swarm = GetComponent<RSNanoSwarm>();
            if (conductPattern == null) conductPattern = GetComponentInChildren<KyoshoConductPatternGimmick>(true);
            if (staccatoPattern == null) staccatoPattern = GetComponentInChildren<KyoshoStaccatoPatternGimmick>(true);
            if (bloomPattern == null) bloomPattern = GetComponentInChildren<KyoshoBloomPatternGimmick>(true);
            if (fermataPattern == null) fermataPattern = GetComponentInChildren<KyoshoFermataPatternGimmick>(true);
            if (variationPattern == null) variationPattern = GetComponentInChildren<KyoshoVariationPatternGimmick>(true);
            if (finalePattern == null) finalePattern = GetComponentInChildren<KyoshoFinalePatternGimmick>(true);
            BindPatternDataIfAvailable();
        }

        [ContextMenu("[자동 설정] 쿄쇼 컴포넌트 및 SO 일괄 연결")]
        public void EditorEnsureAllComponentsAndReferences()
        {
            swarm = GetComponent<RSNanoSwarm>() ?? gameObject.AddComponent<RSNanoSwarm>();
            ConfigureDefaultSwarmSettings();
            _hurtCollider = GetComponent<CircleCollider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
            _hurtCollider.isTrigger = true;
            _hurtCollider.radius = 2.0f;

            conductPattern = GetOrAddComponent<KyoshoConductPatternGimmick>();
            staccatoPattern = GetOrAddComponent<KyoshoStaccatoPatternGimmick>();
            bloomPattern = GetOrAddComponent<KyoshoBloomPatternGimmick>();
            fermataPattern = GetOrAddComponent<KyoshoFermataPatternGimmick>();
            variationPattern = GetOrAddComponent<KyoshoVariationPatternGimmick>();
            finalePattern = GetOrAddComponent<KyoshoFinalePatternGimmick>();
            AutoBindInspectorReferences();
            AutoWireSceneSystems();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        private void ConfigureDefaultSwarmSettings()
        {
            if (swarm == null) return;
            swarm.botCount = 16000;
            swarm.grain = 1;
            swarm.botWorldSize = 0.25f;
            swarm.fill = 1.3f;
            swarm.bulletClump = 24;
            swarm.floorFromTransform = false;
            if (swarm.shader == null)
            {
                swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
            }
        }

        protected override void SetupSnapshot()
        {
            if (masterData == null)
            {
                masterData = CreateRuntimeFallbackMasterData();
            }
            base.SetupSnapshot();
        }

        private static BossMasterData CreateRuntimeFallbackMasterData()
        {
            var data = ScriptableObject.CreateInstance<BossMasterData>();
            data.bossID = KyoshoConstants.BossID;
            data.bossName = KyoshoConstants.BossDisplayName;
            data.regionIndex = 3;
            data.regionBasePrice = 1300;
            data.regionLevelCap = 30;
            data.maxHp = 10000;
            data.baseDefense = 80;
            data.patternPostDelay = 0.54f;
            data.rewardGold = 650;
            data.rewardCoreExp = 312;
            foreach (BossDifficulty diff in Enum.GetValues(typeof(BossDifficulty)))
            {
                var layer = BossDifficultyLayer.GetDefault(diff);
                layer.allowPhase2 = true;
                layer.maxAllowedPatterns = 6;
                data.difficultyLayers.Add(layer);
            }
            data.gimmickParts.Add(new BossGimmickPartData { gimmickID = KyoshoConstants.SectionStringsID, gimmickName = "현악 섹션", isPhysicalBossPart = false, maxDurability = 800 });
            data.gimmickParts.Add(new BossGimmickPartData { gimmickID = KyoshoConstants.SectionPercussionID, gimmickName = "타악 섹션", isPhysicalBossPart = false, maxDurability = 800 });
            data.gimmickParts.Add(new BossGimmickPartData { gimmickID = KyoshoConstants.SectionBrassID, gimmickName = "금관 섹션", isPhysicalBossPart = false, maxDurability = 800 });
            return data;
        }

        protected override void CheckPhaseTransition()
        {
            // 쿄쇼는 변주곡 ① 완료를 통해 2페이즈로 전환하므로 BossControllerBase의 단순 HP 비율 자동 전환을 비활성화함
        }

        public override void InterruptToGroggy(float duration = 4.0f, float damageMultiplier = 1.5f)
        {
            float beats = (_beatClock != null && _beatClock.BeatSec > 0.01f) ? Mathf.Max(1f, Mathf.Round(duration / _beatClock.BeatSec)) : 4f;
            EnterBeatGroggy(beats, damageMultiplier);
        }

        protected override void SetupAnimationController()
        {
            // 쿄쇼는 RSNanoSwarm 절차적 렌더링을 사용하므로 2D Sprite BossAnimationController를 강제 추가하지 않음
            if (animationController == null)
            {
                animationController = GetComponentInChildren<BossAnimationController>(true);
            }
        }

        protected override void RegisterGimmicks()
        {
            if (swarm == null) swarm = GetComponent<RSNanoSwarm>();
            if (!_hasFloorY)
            {
                _arenaFloorY = transform.position.y;
                _hasFloorY = true;
            }
            if (swarm != null)
            {
                swarm.floorFromTransform = false;
                swarm.floorY = _arenaFloorY;
                if (swarm.shader == null) swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
            }

            _hurtCollider = GetComponent<CircleCollider2D>() ?? gameObject.AddComponent<CircleCollider2D>();
            _hurtCollider.isTrigger = true;

            conductPattern = GetOrAddComponent<KyoshoConductPatternGimmick>();
            staccatoPattern = GetOrAddComponent<KyoshoStaccatoPatternGimmick>();
            bloomPattern = GetOrAddComponent<KyoshoBloomPatternGimmick>();
            fermataPattern = GetOrAddComponent<KyoshoFermataPatternGimmick>();
            variationPattern = GetOrAddComponent<KyoshoVariationPatternGimmick>();
            finalePattern = GetOrAddComponent<KyoshoFinalePatternGimmick>();

            BindPatternDataIfAvailable();

            RegisterGimmick(conductPattern);
            RegisterGimmick(staccatoPattern);
            RegisterGimmick(bloomPattern);
            RegisterGimmick(fermataPattern);
            RegisterGimmick(variationPattern);
            RegisterGimmick(finalePattern);

            SetupBeatClockAndUI();
            SetupMasterRigAndSections();
        }

        private void SetupBeatClockAndUI()
        {
            _beatClock = GetOrAddComponent<KyoshoBeatClock>();
            _beatIndicatorHud = GetOrAddComponent<KyoshoBeatIndicatorHUD>();
            _scorePreviewHud = GetOrAddComponent<KyoshoScorePreviewHUD>();
            _sectionStatusHud = GetOrAddComponent<KyoshoSectionStatusHUD>();
            _stageBeatPulse = GetOrAddComponent<KyoshoStageBeatPulse>();

            _scorePreviewHud.Initialize(this);
        }

        private void SetupMasterRigAndSections()
        {
            if (_rigHostGo != null) Destroy(_rigHostGo);
            _rigHostGo = new GameObject("[KyoshoMasterRig]");
            _rigHostGo.transform.position = Vector3.zero;
            _rigHostGo.transform.rotation = Quaternion.identity;
            _rigHostGo.transform.localScale = Vector3.one;

            _masterRig = _rigHostGo.AddComponent<RSNanoRig>();
            _masterRig.swarm = swarm;

            if (rhythmTable == null)
            {
                rhythmTable = ScriptableObject.CreateInstance<KyoshoRhythmTableSO>();
            }
            rhythmTable.EnsureDefaults();
            staccatoPattern?.SetRhythmTable(rhythmTable);

            // 1. 개화 줄기 풀 6개 (Chain)
            var bloomLimbs = new List<RSNanoLimb>(6);
            for (int i = 0; i < 6; i++)
            {
                var lGo = new GameObject($"Limb_BloomStem_{i}");
                lGo.transform.SetParent(_rigHostGo.transform, false);
                var limb = lGo.AddComponent<RSNanoLimb>();
                limb.type = LimbType.Chain;
                limb.radius = 0.24f;
                limb.amount = 0f;
                limb.grow = 0f;
                limb.bind = 0.9f;
                bloomLimbs.Add(limb);
            }
            bloomPattern?.InitializeStemPool(bloomLimbs, swarm, _rigHostGo.transform);

            // 2. 변주곡 본체 변신 림 (Sprite)
            var morphGo = new GameObject("Limb_VariationMorph");
            morphGo.transform.SetParent(_rigHostGo.transform, false);
            morphGo.transform.localScale = Vector3.one * 2.2f;
            var morphLimb = morphGo.AddComponent<RSNanoLimb>();
            morphLimb.type = LimbType.Sprite;
            morphLimb.amount = 0f;
            morphLimb.bind = 1f;
            morphLimb.artColor = true;
            variationPattern?.Configure(morphLimb, rhythmTable);

            // 3. 2페이즈 오케스트라 악기 섹션 3종 (Sprite)
            RSNanoLimb stringsLimb = CreateSectionSpriteLimb("Limb_Section_Strings", KyoshoSectionType.Strings);
            RSNanoLimb percLimb = CreateSectionSpriteLimb("Limb_Section_Percussion", KyoshoSectionType.Percussion);
            RSNanoLimb brassLimb = CreateSectionSpriteLimb("Limb_Section_Brass", KyoshoSectionType.Brass);

            var secMgrGo = new GameObject("SectionManager");
            secMgrGo.transform.SetParent(_rigHostGo.transform, false);
            _sectionManager = secMgrGo.AddComponent<KyoshoSectionManager>();
            _sectionManager.Initialize(this, swarm, stringsLimb, percLimb, brassLimb, _sectionStatusHud);
        }

        private RSNanoLimb CreateSectionSpriteLimb(string goName, KyoshoSectionType type)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(_rigHostGo.transform, false);
            go.transform.localScale = Vector3.one * 1.6f;
            var limb = go.AddComponent<RSNanoLimb>();
            limb.type = LimbType.Sprite;
            limb.amount = 0f;
            limb.bind = 1f;
            limb.artColor = true;
            limb.picture = rhythmTable != null ? rhythmTable.GetSectionShape(type) : null;
            if (limb.picture == null)
            {
                limb.picture = CreateFallbackInstrumentShape(type);
            }
            return limb;
        }

        private static RSNanoSpriteShape CreateFallbackInstrumentShape(KyoshoSectionType type)
        {
            Color col = type switch
            {
                KyoshoSectionType.Strings => KyoshoConstants.StringsMintColor,
                KyoshoSectionType.Percussion => KyoshoConstants.PercussionAmberColor,
                KyoshoSectionType.Brass => KyoshoConstants.BrassMagentaColor,
                _ => Color.white
            };
            Color32 c32 = col;

            return KyoshoNanobotAttackHelper.CreateRuntimeFallbackShape($"FallbackSection_{type}", 28, 28, (x, y) =>
            {
                float nx = (x - 13.5f) / 12f;
                float ny = (y - 13.5f) / 12f;
                bool draw = type switch
                {
                    KyoshoSectionType.Strings => (Mathf.Abs(nx) < 0.10f && ny > -0.8f)
                        || ((nx * nx * 2.2f + (ny + 0.25f) * (ny + 0.25f) * 1.4f) < 0.35f)
                        || ((nx * nx * 3.0f + (ny - 0.25f) * (ny - 0.25f) * 2.0f) < 0.25f),
                    KyoshoSectionType.Percussion => (ny < 0.2f && ny > -0.65f && Mathf.Abs(nx) < 0.72f * (0.6f + 0.4f * (ny + 0.65f)))
                        || (Mathf.Abs(ny - 0.25f) < 0.10f && Mathf.Abs(nx) < 0.76f),
                    _ => (Mathf.Abs(Mathf.Sqrt(nx * nx + ny * ny) - 0.52f) < 0.16f)
                        || (nx > 0.2f && Mathf.Abs(ny + 0.35f) < 0.28f && nx < 0.85f)
                };
                return draw ? c32 : (Color32?)null;
            });
        }

        protected override void OnBossInitialized()
        {
            base.OnBossInitialized();

            _lastAuthoritativeHp = Snapshot != null ? Snapshot.maxHp : 10000;
            _variation1Triggered = false;
            _variation1Completed = false;
            _variation2Triggered = false;
            _finaleTriggered = false;
            _finaleRetryRemainingPatterns = 0;
            _pendingGate = PendingGateType.None;
            _isTransitionRunning = false;
            _cycleStepIndex = 0;
            IsInvincible = false;
            IsWaitingFinalBlow = false;
            HasCompletedFirstPhase2Fermata = false;
            _phaseState = KyoshoPhaseState.Intro;

            if (swarm != null)
            {
                GetArenaBounds(out float minX, out float maxX);
                float centerX = (minX + maxX) * 0.5f;
                swarm.Blob.aspect = blobShapeRatio.x / Mathf.Max(0.01f, blobShapeRatio.y);
                _blobBaseSize = swarm.BlobSize(swarm.botCount, blobShapeRatio);
                swarm.Blob.radius = _blobBaseSize;
                Vector3 center = new Vector3(centerX, _arenaFloorY + _blobBaseSize.y * 0.78f, 0f);
                swarm.Blob.center = center;
                swarm.Build(center);
                swarm.ResetBotLooks();
                transform.position = center;
                GroundY = center.y;
            }

            if (_masterRig != null && !_masterRig.Active)
            {
                _masterRig.Begin(RSNanoSwarm.G_Blob, () => PlayerTransform != null ? PlayerTransform.position : Vector3.zero, 1f, Vector3.zero);
            }

            float bpm = KyoshoConstants.GetBpmForDifficulty(SelectedDifficulty);
            _beatClock.StartClock(bpm, 4);
            _beatClock.OnBeat -= HandleBeatVisualPulse;
            _beatClock.OnBeat += HandleBeatVisualPulse;

            _beatIndicatorHud?.Bind(_beatClock, SelectedDifficulty);
            _stageBeatPulse?.Bind(_beatClock);

            if (sfxData != null)
            {
                KyoshoSfx.Bind(sfxData);
                if (sfxData.bgmMain != null) KyoshoSfx.PlayBgm(sfxData.bgmMain);
            }
        }

        public override void SetDifficulty(BossDifficulty difficulty)
        {
            base.SetDifficulty(difficulty);
            if (_beatClock != null)
            {
                float bpm = KyoshoConstants.GetBpmForDifficulty(difficulty);
                _beatClock.SetBpm(bpm);
                _beatIndicatorHud?.Bind(_beatClock, difficulty);
            }
            if (Snapshot != null)
            {
                _lastAuthoritativeHp = Snapshot.currentHp;
            }
        }

        protected override void StartCombatLoop()
        {
            // 인트로 모래 집결 연출 후 Phase 1 박자 루프 개시
            var token = RenewStateCancellationToken();
            PlayIntroAndStartLoopAsync(token).Forget();
        }

        private async UniTaskVoid PlayIntroAndStartLoopAsync(CancellationToken token)
        {
            try
            {
                IsInvincible = true;
                _phaseState = KyoshoPhaseState.Intro;

                if (swarm != null)
                {
                    GetArenaBounds(out float minX, out float maxX);
                    float cx = (minX + maxX) * 0.5f;
                    swarm.ScatterOnFloor(cx - 5.5f, cx + 5.5f);
                    await _beatClock.WaitBeatsAsync(1f, token);

                    var debris = swarm.AllIn(BotMode.Debris, new List<int>());
                    debris.Sort((a, b) => Mathf.Abs(swarm.Bots[a].pos.x - cx).CompareTo(Mathf.Abs(swarm.Bots[b].pos.x - cx)));
                    double riseStart = _beatClock.TotalBeats;
                    int next = 0;
                    while (next < debris.Count && _beatClock.TotalBeats < riseStart + 3.0)
                    {
                        token.ThrowIfCancellationRequested();
                        float p = Mathf.Clamp01((float)((_beatClock.TotalBeats - riseStart) / 3.0));
                        int upto = Mathf.Min(debris.Count, Mathf.CeilToInt(debris.Count * p));
                        for (; next < upto; next++) swarm.Bind(debris[next], RSNanoSwarm.G_Blob);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                    }
                    for (; next < debris.Count; next++) swarm.Bind(debris[next], RSNanoSwarm.G_Blob);
                }
                else
                {
                    await _beatClock.WaitBeatsAsync(4f, token);
                }

                if (_beatClock != null && _beatClock.BeatInBar != 0)
                {
                    await _beatClock.WaitUntilNextBarAsync(token);
                }

                IsInvincible = false;
                _phaseState = KyoshoPhaseState.Phase1;
                StateMachine.Initialize(new KyoshoBeatIdleState(this, token, 0.5f));
                Debug.Log($"<color=cyan>[KyoshoBossController] 쿄쇼 1페이즈 전투 개시! (난이도: {SelectedDifficulty}, BPM: {_beatClock.Bpm})</color>");
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void Update()
        {
            base.Update();
            if (IsCombatStopped || Snapshot == null || !Snapshot.IsAlive || swarm == null) return;

            UpdateBlobBodyAndCollider(Time.deltaTime);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (swarm != null && !IsCombatStopped)
            {
                // BossControllerBase.LateUpdate의 X클램프를 Blob.center에도 동기화
                swarm.Blob.center.x = transform.position.x;
            }
        }

        private void UpdateBlobBodyAndCollider(float dt)
        {
            var blob = swarm.Blob;
            Vector3 size = swarm.BlobSize(Mathf.Max(60, swarm.guideCount[RSNanoSwarm.G_Blob]), blobShapeRatio);
            _blobBaseSize = Vector3.Lerp(_blobBaseSize, size, 1f - Mathf.Exp(-6f * dt));
            blob.radius = _blobBaseSize;
            blob.center.y = _arenaFloorY + blob.radius.y * 0.78f;
            blob.center.z = Mathf.MoveTowards(blob.center.z, 0f, dt * 6f);
            blob.bulge = Mathf.MoveTowards(blob.bulge, 0f, dt * 1.5f);

            if (_beatPulseAmount > 0f)
            {
                _beatPulseAmount = Mathf.MoveTowards(_beatPulseAmount, 0f, dt * 5f);
                blob.glow = Mathf.Max(blob.glow, _beatPulseAmount * 0.55f);
            }
            else if (_phaseState != KyoshoPhaseState.Finale)
            {
                blob.glow = Mathf.MoveTowards(blob.glow, 0f, dt * 4f);
            }

            if (_driftEnabled && !IsInvincible && Snapshot.isGroggy == false)
            {
                GetArenaBounds(out float minX, out float maxX);
                float halfW = blob.radius.x * 0.65f + 0.3f;
                float playerX = PlayerTransform != null ? PlayerTransform.position.x : 0f;
                float side = blob.center.x >= playerX ? 1f : -1f;
                float wantX = Mathf.Clamp(playerX + side * preferredDistance, minX + halfW, maxX - halfW);
                float v = Mathf.Clamp(wantX - blob.center.x, -1f, 1f) * driftSpeed;
                blob.center.x += v * dt;
                if (Mathf.Abs(v) > 0.1f)
                {
                    blob.bulgeDir = new Vector3(Mathf.Sign(v), 0f, 0f);
                    blob.bulge = Mathf.Max(blob.bulge, Mathf.Abs(v) * 0.14f);
                }
            }

            transform.position = blob.center;

            if (_hurtCollider != null)
            {
                bool allBroken = _sectionManager != null && _sectionManager.AreAllActiveSectionsBroken;
                float baseRadius = Mathf.Max(1.25f, Mathf.Max(blob.radius.x, blob.radius.y) * 0.85f);
                _hurtCollider.radius = allBroken ? baseRadius * 1.30f : baseRadius;
                _hurtCollider.enabled = !IsInvincible && Snapshot.IsAlive;
            }
        }

        private void HandleBeatVisualPulse(int beatInBar, int totalBeat)
        {
            _beatPulseAmount = beatInBar == 0 ? 1.0f : 0.45f;
        }

        // ========================================================================
        // 피격 및 HP 게이트 고정 (60% 변주곡①, 30% 변주곡②, 10% 피날레, 마지막 일격)
        // ========================================================================

        protected override int OnPreTakeDamage(int rawDamage)
        {
            if (IsInvincible) return 0;
            return base.OnPreTakeDamage(rawDamage);
        }

        public override void TakeDamage(int rawDamage, bool isAlreadyCalculated = false)
        {
            if (Snapshot == null || !Snapshot.IsAlive) return;
            if (IsInvincible) return;

            if (IsWaitingFinalBlow)
            {
                ExecuteFinalBlowVictory();
                return;
            }

            base.TakeDamage(rawDamage, isAlreadyCalculated);
            EnforceHpGateAfterDamage();
        }

        protected override void HandleHubBossDamaged(int finalDamage)
        {
            if (Snapshot == null) return;

            if (IsInvincible)
            {
                Snapshot.currentHp = _lastAuthoritativeHp;
                SyncHpToHubAndViews();
                return;
            }

            if (IsWaitingFinalBlow)
            {
                ExecuteFinalBlowVictory();
                return;
            }

            EnforceHpGateAfterDamage();
            SpawnBotHitSplash();
            base.HandleHubBossDamaged(finalDamage);
        }

        private void EnforceHpGateAfterDamage()
        {
            if (Snapshot == null || Snapshot.maxHp <= 0) return;

            int gate60 = Mathf.RoundToInt(Snapshot.maxHp * KyoshoConstants.Variation1HpRatio);
            int gate30 = Mathf.RoundToInt(Snapshot.maxHp * KyoshoConstants.Variation2HpRatio);
            int gate10 = Mathf.Max(1, Mathf.RoundToInt(Snapshot.maxHp * KyoshoConstants.FinaleHpRatio));

            bool clamped = false;

            if (!_variation1Triggered && Snapshot.currentHp <= gate60)
            {
                Snapshot.currentHp = gate60;
                _variation1Triggered = true;
                _pendingGate = PendingGateType.Variation1;
                clamped = true;
            }
            else if (_variation1Completed && !_variation2Triggered
                     && KyoshoConstants.SupportsVariation2(SelectedDifficulty)
                     && Snapshot.currentHp <= gate30)
            {
                Snapshot.currentHp = gate30;
                _variation2Triggered = true;
                _pendingGate = PendingGateType.Variation2;
                clamped = true;
            }
            else if (_variation1Completed && Snapshot.currentHp <= gate10)
            {
                // 피날레를 버티고 마지막 일격이 성립되기 전까지는 HP 10% 아래로 내려가지 않음
                Snapshot.currentHp = gate10;
                clamped = true;
                if (!_finaleTriggered)
                {
                    _finaleTriggered = true;
                    _pendingGate = PendingGateType.Finale;
                }
            }

            _lastAuthoritativeHp = Snapshot.currentHp;

            if (clamped)
            {
                SyncHpToHubAndViews();
                if (_pendingGate != PendingGateType.None && !_isTransitionRunning)
                {
                    TriggerPendingGateAtNextBarAsync().Forget();
                }
            }
        }

        private void SyncHpToHubAndViews()
        {
            if (Snapshot == null) return;
            CombatDataHub.Instance?.SyncBossToEnemyProxy();
        }

        private void SpawnBotHitSplash()
        {
            if (swarm == null) return;
            Vector3 p = swarm.Blob.center + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(-0.5f, 0.5f), 0f);
            swarm.Hit(p, 0.7f, new Vector3(UnityEngine.Random.Range(-2f, 2f), 2.5f, 0f), 0.08f, 1 << RSNanoSwarm.G_Blob);
        }

        private async UniTaskVoid TriggerPendingGateAtNextBarAsync()
        {
            _isTransitionRunning = true;
            _gateTransitionCts?.Cancel();
            _gateTransitionCts?.Dispose();
            _gateTransitionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            var token = _gateTransitionCts.Token;

            try
            {
                if (_beatClock != null && _beatClock.IsRunning)
                {
                    await _beatClock.WaitUntilNextBarAsync(token);
                }

                if (IsCombatStopped || Snapshot == null || !Snapshot.IsAlive) return;

                PendingGateType gate = _pendingGate;
                _pendingGate = PendingGateType.None;
                BossPatternGimmickBase gatePattern = null;

                switch (gate)
                {
                    case PendingGateType.Variation1:
                        variationPattern?.SetVariationMode(false);
                        gatePattern = variationPattern;
                        break;
                    case PendingGateType.Variation2:
                        variationPattern?.SetVariationMode(true);
                        gatePattern = variationPattern;
                        break;
                    case PendingGateType.Finale:
                        gatePattern = finalePattern;
                        break;
                }

                if (gatePattern != null)
                {
                    // 진행 중이던 일반 패턴이 있다면 정박 전환을 위해 깔끔히 정리 후 즉시 기믹 실행
                    InterruptActivePatterns();
                    var stateToken = RenewStateCancellationToken();
                    StateMachine.ChangeState(new KyoshoPatternExecutingState(this, stateToken, gatePattern));
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isTransitionRunning = false;
            }
        }

        // ========================================================================
        // 페이즈 · 순환 패턴 선택 · 그로기 · 피날레 분기 제어
        // ========================================================================

        public override BossPatternGimmickBase GetNextPatternGimmick()
        {
            if (_finaleTriggered && _finaleRetryRemainingPatterns > 0)
            {
                _finaleRetryRemainingPatterns--;
                if (_finaleRetryRemainingPatterns == 0)
                {
                    // 2페이즈 1순환 완료 후 다시 피날레 발동
                    return finalePattern;
                }
            }

            bool isPhase2 = Snapshot != null && Snapshot.currentPhase >= 2;
            int step = _cycleStepIndex % 4;
            _cycleStepIndex++;

            if (!isPhase2)
            {
                // 1페이즈 순환: ① 지휘 도형 ➔ ② 스타카토 ➔ ③ 지휘 도형 ➔ ④ 개화
                return step switch
                {
                    0 => conductPattern,
                    1 => staccatoPattern,
                    2 => conductPattern,
                    _ => bloomPattern
                };
            }
            else
            {
                // 2페이즈 순환: ① 지휘 도형 ➔ ② 스타카토 ➔ ③ 지휘 도형 ➔ ④ 페르마타
                return step switch
                {
                    0 => conductPattern,
                    1 => staccatoPattern,
                    2 => conductPattern,
                    _ => fermataPattern
                };
            }
        }

        public Type PeekNextPhase2PatternType()
        {
            int nextStep = _cycleStepIndex % 4;
            return (nextStep == 1) ? typeof(KyoshoStaccatoPatternGimmick) : typeof(KyoshoFermataPatternGimmick);
        }

        public void SetPhaseState(KyoshoPhaseState newState)
        {
            _phaseState = newState;
        }

        public void PreparePhase2AfterVariation1()
        {
            if (_variation1Completed) return;
            _variation1Completed = true;
            _cycleStepIndex = 0;

            if (Snapshot != null)
            {
                Snapshot.currentPhase = 2;
            }
            _phaseState = KyoshoPhaseState.Phase2;

            if (swarm != null)
            {
                // 2페 나노봇 유동감 강화
                swarm.Blob.wobble = 0.14f;
                swarm.Blob.flow = 0.32f;
                swarm.Blob.bind = 0.78f;
            }

            _sectionManager?.ActivatePhase2InitialSections();

            if (sfxData != null && sfxData.bgmPhase2 != null)
            {
                KyoshoSfx.PlayBgm(sfxData.bgmPhase2, 0.6f);
            }

            Debug.Log("<color=yellow>[KyoshoBossController] ▶ 2페이즈 개막! 현악(좌)·타악(우) 악기 섹션 분리 배치 완료!</color>");
        }

        public void MarkFirstPhase2FermataCompleted()
        {
            HasCompletedFirstPhase2Fermata = true;
        }

        public void EnterBeatGroggy(float beats = 4f, float damageMultiplier = 1.5f)
        {
            var token = RenewStateCancellationToken();
            StateMachine.ChangeState(new KyoshoGroggyState(this, token, beats, damageMultiplier));
        }

        public void EnterFinalBlowGroggy()
        {
            _phaseState = KyoshoPhaseState.FinalGroggy;
            IsWaitingFinalBlow = true;
            var token = RenewStateCancellationToken();
            StateMachine.ChangeState(new KyoshoGroggyState(
                this,
                token,
                beats: 4f,
                damageMultiplier: 1.5f,
                isFinalBlowWindow: true,
                onGroggyExpired: OnFinalBlowWindowExpired));
        }

        private void OnFinalBlowWindowExpired()
        {
            // 만약 4박 그로기 동안 플레이어가 한 번도 공격하지 못했다면, 2페 1순환 후 피날레 재진입으로 소프트락 방지
            if (IsWaitingFinalBlow && Snapshot != null && Snapshot.IsAlive && !IsCombatStopped)
            {
                IsWaitingFinalBlow = false;
                _phaseState = KyoshoPhaseState.Phase2;
                _sectionManager?.SetTemporarilyGatheredForFinale(false);
                ScheduleFinaleRetryAfterOneCycle();
            }
        }

        public void ScheduleFinaleRetryAfterOneCycle()
        {
            _phaseState = KyoshoPhaseState.Phase2;
            _cycleStepIndex = 0;
            _finaleRetryRemainingPatterns = 5; // 지휘→스타카토→지휘→페르마타(4패턴) 후 5번째에 피날레
        }

        private void ExecuteFinalBlowVictory()
        {
            if (Snapshot == null || !IsWaitingFinalBlow) return;
            IsWaitingFinalBlow = false;
            _phaseState = KyoshoPhaseState.Defeated;
            Snapshot.currentHp = 0;
            SyncHpToHubAndViews();

            Debug.Log("<color=green>[KyoshoBossController] ▶ 최후의 일격 적중! 쿄쇼 2페이즈 토벌 완료!</color>");
            ProcessPostDamageReactions(Snapshot.maxHp);
        }

        public void SetBlobGroggyVisual(bool groggy)
        {
            if (swarm == null) return;
            swarm.Blob.bind = groggy ? 0.35f : (_variation1Completed ? 0.78f : 1f);
            swarm.Blob.glow = groggy ? 0.9f : 0f;
        }

        public void SetDriftEnabled(bool enabled) => _driftEnabled = enabled;

        public float GetAllSectionsBrokenPostDelayBonusBeats()
        {
            return (_sectionManager != null && _sectionManager.AreAllActiveSectionsBroken) ? 1f : 0f;
        }

        public void GetArenaBounds(out float minX, out float maxX)
        {
            var combatCam = TargetCombatCamera;
            if (combatCam != null && combatCam.UseArenaBounds)
            {
                minX = combatCam.ArenaMinX;
                maxX = combatCam.ArenaMaxX;
                return;
            }
            minX = fallbackArenaMinX;
            maxX = fallbackArenaMaxX;
        }

        public Vector3 GetHeadAnchorWorldPosition()
        {
            if (swarm != null)
            {
                return swarm.Blob.center + new Vector3(0f, swarm.Blob.radius.y + 1.0f, 0f);
            }
            return transform.position + Vector3.up * 2.6f;
        }

        public Vector3 GetPlayerAimCenter()
        {
            if (PlayerTransform != null)
            {
                return PlayerTransform.position + Vector3.up * 0.65f;
            }
            return new Vector3(-4f, _arenaFloorY + 0.65f, 0f);
        }

        public float GetFacingToPlayerSign()
        {
            if (PlayerTransform == null) return -1f;
            float diff = PlayerTransform.position.x - transform.position.x;
            return diff >= 0f ? 1f : -1f;
        }

        private void InterruptActivePatterns()
        {
            foreach (var p in activePatternPool)
            {
                p?.InterruptPattern(InterruptReason.PhaseTransition);
            }
        }

        public override void StopBossCombat()
        {
            base.StopBossCombat();
            _gateTransitionCts?.Cancel();
            _beatClock?.StopClock();
            _scorePreviewHud?.HideAll();
            _sectionStatusHud?.SetVisible(false);
            KyoshoSfx.StopBgm(0.6f);
        }

        protected override void OnBossDefeated()
        {
            base.OnBossDefeated();
            _phaseState = KyoshoPhaseState.Defeated;
            if (_masterRig != null && _masterRig.Active) _masterRig.End();

            if (swarm != null)
            {
                GetArenaBounds(out float minX, out float maxX);
                swarm.ScatterOnFloor(minX + 0.5f, maxX - 0.5f);
            }
            if (_hurtCollider != null) _hurtCollider.enabled = false;
        }

        protected override void OnDestroy()
        {
            _gateTransitionCts?.Cancel();
            _gateTransitionCts?.Dispose();
            if (_beatClock != null) _beatClock.OnBeat -= HandleBeatVisualPulse;
            if (_rigHostGo != null) Destroy(_rigHostGo);
            KyoshoSfx.Unbind();
            base.OnDestroy();
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            var c = GetComponentInChildren<T>(true);
            if (c == null) c = gameObject.AddComponent<T>();
            return c;
        }

        private void BindPatternDataIfAvailable()
        {
            if (masterData == null || masterData.patterns == null) return;
            foreach (var p in masterData.patterns)
            {
                if (p == null) continue;
                switch (p.patternID)
                {
                    case KyoshoConstants.PatternConductID: SetPatternDataPrivate(conductPattern, p); break;
                    case KyoshoConstants.PatternStaccatoID: SetPatternDataPrivate(staccatoPattern, p); break;
                    case KyoshoConstants.PatternBloomID: SetPatternDataPrivate(bloomPattern, p); break;
                    case KyoshoConstants.PatternFermataID: SetPatternDataPrivate(fermataPattern, p); break;
                    case KyoshoConstants.PatternVariationID: SetPatternDataPrivate(variationPattern, p); break;
                    case KyoshoConstants.PatternFinaleID: SetPatternDataPrivate(finalePattern, p); break;
                }
            }
        }

        private static void SetPatternDataPrivate(BossPatternGimmickBase gimmick, BossPatternData data)
        {
            if (gimmick == null || data == null) return;
            var field = typeof(BossPatternGimmickBase).GetField("patternData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.GetValue(gimmick) == null)
            {
                field.SetValue(gimmick, data);
            }
        }
    }
}
