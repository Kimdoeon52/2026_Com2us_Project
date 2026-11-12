using System;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.FSM;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [지역 1 보스 "정크 휠러" 구체 컨트롤러 (JunkWheelerBossController)]
    /// 기획서 §6.18, §6.10.4 및 §3.2 (1), §5.1:
    /// 1. BossController를 상속하여 기존 도구(BossFSMTester, BattleManager) 및 씬 직렬화 100% 호환 보장.
    /// 2. 폐타이어 부위 기믹, 돌진 기믹, 탄막 기믹, 3단 내려찍기 기믹, 회전 톱날 당구 돌진 기믹을 컴포지션(Composition)으로 조립/제어.
    /// 3. 난이도 레이어 시스템에 따른 필드 기믹(못판/Spike, 폭발상자) 수 활성화 제어 및 2페이즈 엔진 과열 불꽃 연출 담당.
    /// </summary>
    public class JunkWheelerBossController : BossController
    {
        [Header("정크 휠러 전용 연출 및 컴포넌트")]
        [SerializeField] private ParticleSystem phase2FireVFX; // 2페이즈 엔진 불꽃 파티클
        [SerializeField] private ParticleSystem defeatExplosionVFX; // 토벌 사망 폭발 파티클

        [Header("사운드 데이터 (SO)")]
        [SerializeField] private JunkWheelerSfxData sfxData;
        [SerializeField] private bool playIdleEngineLoop = true;

        private AudioSource _idleLoopSource;
        private AudioSource _groggyLoopSource;
        private float _lastHitSfxTime = -10f;
        private int _hitSfxCounter = 0;

        [Header("부착된 구체 기믹 참조 (인스펙터 모니터링)")]
        [SerializeField] private JunkWheelerTirePartGimmick tirePartGimmick;
        [SerializeField] private JunkWheelerChargePatternGimmick chargePatternGimmick;
        [SerializeField] private JunkWheelerBarragePatternGimmick barragePatternGimmick;
        [SerializeField] private JunkWheelerSlamPatternGimmick slamPatternGimmick;
        [SerializeField] private JunkWheelerRicochetBladePatternGimmick ricochetBladePatternGimmick;
        [SerializeField] private JunkWheelerDribbleShockwavePatternGimmick dribbleShockwavePatternGimmick;

        public JunkWheelerTirePartGimmick TirePartGimmick => tirePartGimmick;
        public JunkWheelerChargePatternGimmick ChargePatternGimmick => chargePatternGimmick;
        public JunkWheelerBarragePatternGimmick BarragePatternGimmick => barragePatternGimmick;
        public JunkWheelerSlamPatternGimmick SlamPatternGimmick => slamPatternGimmick;
        public JunkWheelerRicochetBladePatternGimmick RicochetBladePatternGimmick => ricochetBladePatternGimmick;
        public JunkWheelerDribbleShockwavePatternGimmick DribbleShockwavePatternGimmick => dribbleShockwavePatternGimmick;

        public override void AutoBindInspectorReferences()
        {
            base.AutoBindInspectorReferences();
#if UNITY_EDITOR
            if (sfxData == null)
            {
                sfxData = UnityEditor.AssetDatabase.LoadAssetAtPath<JunkWheelerSfxData>("Assets/KKH/03.SOData/Boss/JunkWheelerSfxData.asset");
            }
#endif
            if (tirePartGimmick == null) tirePartGimmick = GetComponentInChildren<JunkWheelerTirePartGimmick>(true);
            if (chargePatternGimmick == null) chargePatternGimmick = GetComponentInChildren<JunkWheelerChargePatternGimmick>(true);
            if (barragePatternGimmick == null) barragePatternGimmick = GetComponentInChildren<JunkWheelerBarragePatternGimmick>(true);
            if (slamPatternGimmick == null) slamPatternGimmick = GetComponentInChildren<JunkWheelerSlamPatternGimmick>(true);
            if (ricochetBladePatternGimmick == null) ricochetBladePatternGimmick = GetComponentInChildren<JunkWheelerRicochetBladePatternGimmick>(true);
            if (dribbleShockwavePatternGimmick == null) dribbleShockwavePatternGimmick = GetComponentInChildren<JunkWheelerDribbleShockwavePatternGimmick>(true);

            BindPatternDataIfAvailable();
        }

        [ContextMenu("[자동 설정] 인스펙터 컴포넌트 및 SO 일괄 연결")]
        public void EditorEnsureAllComponentsAndReferences()
        {
            SetupAnimationController();
            animationController?.AutoBindReferences();
            tirePartGimmick = GetOrAddComponent<JunkWheelerTirePartGimmick>();
            chargePatternGimmick = GetOrAddComponent<JunkWheelerChargePatternGimmick>();
            barragePatternGimmick = GetOrAddComponent<JunkWheelerBarragePatternGimmick>();
            slamPatternGimmick = GetOrAddComponent<JunkWheelerSlamPatternGimmick>();
            ricochetBladePatternGimmick = GetOrAddComponent<JunkWheelerRicochetBladePatternGimmick>();
            dribbleShockwavePatternGimmick = GetOrAddComponent<JunkWheelerDribbleShockwavePatternGimmick>();
            AutoBindInspectorReferences();
            AutoWireSceneSystems();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        protected override void AutoWireSceneSystems()
        {
            base.AutoWireSceneSystems();
        }

        protected override void RegisterGimmicks()
        {
            // 1. 타이어 부위 기믹 탐색 및 부착
            tirePartGimmick = GetOrAddComponent<JunkWheelerTirePartGimmick>();
            RegisterGimmick(tirePartGimmick);

            // 2. 패턴 기믹 5종 탐색 및 부착 (마스터 데이터 매칭)
            chargePatternGimmick = GetOrAddComponent<JunkWheelerChargePatternGimmick>();
            barragePatternGimmick = GetOrAddComponent<JunkWheelerBarragePatternGimmick>();
            slamPatternGimmick = GetOrAddComponent<JunkWheelerSlamPatternGimmick>();
            ricochetBladePatternGimmick = GetOrAddComponent<JunkWheelerRicochetBladePatternGimmick>();
            dribbleShockwavePatternGimmick = GetOrAddComponent<JunkWheelerDribbleShockwavePatternGimmick>();

            BindPatternDataIfAvailable();

            RegisterGimmick(chargePatternGimmick);
            RegisterGimmick(barragePatternGimmick);
            RegisterGimmick(slamPatternGimmick);
            RegisterGimmick(ricochetBladePatternGimmick);
            RegisterGimmick(dribbleShockwavePatternGimmick);
        }

        protected override void ApplyDifficultyToGimmicks()
        {
            base.ApplyDifficultyToGimmicks();
            if (Snapshot == null) return;

            // 기획서 §6.10.4: 활성 필드 기믹 수 제한 (아주쉬움: 0~1, 쉬움: 1, 보통: 2, 어려움: 3, 헬: 4)
            int maxGimmicks = Snapshot.maxActiveGimmicks;
            var sceneGimmicks = FindObjectsByType<BossFieldGimmickBase>(FindObjectsSortMode.None);
            if (sceneGimmicks != null && sceneGimmicks.Length > 0)
            {
                // 못판(NailBoard)이 우선적으로 켜지도록 정렬
                Array.Sort(sceneGimmicks, (a, b) =>
                {
                    bool aIsNail = a is NailBoardFieldGimmick;
                    bool bIsNail = b is NailBoardFieldGimmick;
                    if (aIsNail && !bIsNail) return -1;
                    if (!aIsNail && bIsNail) return 1;
                    return 0;
                });

                for (int i = 0; i < sceneGimmicks.Length; i++)
                {
                    bool enable = i < maxGimmicks;
                    sceneGimmicks[i].gameObject.SetActive(enable);
                }
                Debug.Log($"<color=cyan>[JunkWheelerBossController] 필드 기믹 수 조정: {Mathf.Min(sceneGimmicks.Length, maxGimmicks)} / {sceneGimmicks.Length}개 활성화 (난이도: {Snapshot.difficulty})</color>");
            }
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            var comp = GetComponentInChildren<T>(true);
            if (comp == null)
            {
                comp = gameObject.AddComponent<T>();
            }
            return comp;
        }

        private void BindPatternDataIfAvailable()
        {
            if (masterData == null || masterData.patterns == null) return;

            foreach (var p in masterData.patterns)
            {
                if (p == null) continue;

                if (p.patternID == "PATTERN_CHARGE" && chargePatternGimmick != null)
                {
                    SetPatternDataPrivate(chargePatternGimmick, p);
                }
                else if (p.patternID == "PATTERN_JUMP_BARRAGE" && barragePatternGimmick != null)
                {
                    SetPatternDataPrivate(barragePatternGimmick, p);
                }
                else if (p.patternID == "PATTERN_SLAM" && slamPatternGimmick != null)
                {
                    SetPatternDataPrivate(slamPatternGimmick, p);
                }
                else if (p.patternID == JunkWheelerRicochetBladePatternGimmick.PatternRicochetBladeID && ricochetBladePatternGimmick != null)
                {
                    SetPatternDataPrivate(ricochetBladePatternGimmick, p);
                }
                else if (p.patternID == JunkWheelerDribbleShockwavePatternGimmick.PatternDribbleShockwaveID && dribbleShockwavePatternGimmick != null)
                {
                    SetPatternDataPrivate(dribbleShockwavePatternGimmick, p);
                }
            }
        }

        private void SetPatternDataPrivate(BossPatternGimmickBase gimmick, BossPatternData data)
        {
            var field = typeof(BossPatternGimmickBase).GetField("patternData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.GetValue(gimmick) == null)
            {
                field.SetValue(gimmick, data);
            }
        }

        [ContextMenu("[돌진] 전조 점멸 시작 (OnTellStarted)")]
        public void TestChargeTellStart()
        {
            ChargePatternGimmick?.OnTellStarted();
        }

        [ContextMenu("[돌진] 전조 점멸 종료 (OnTellEnded)")]
        public void TestChargeTellEnd()
        {
            ChargePatternGimmick?.OnTellEnded();
        }

        protected override void OnBossInitialized()
        {
            base.OnBossInitialized();
            Debug.Log("<color=green>[JunkWheelerBossController] 정크 휠러 초기화 완료! 엔진 가동 시작!</color>");

            if (sfxData != null)
            {
                JunkWheelerSfx.Bind(sfxData);
                if (sfxData.bgmMain != null)
                {
                    JunkWheelerSfx.PlayBgm(sfxData.bgmMain);
                }
            }

            OnStateChanged -= HandleStateChangedForIdleSfx;
            OnStateChanged += HandleStateChangedForIdleSfx;
            OnGroggyChanged -= HandleGroggySfx;
            OnGroggyChanged += HandleGroggySfx;
            OnStunChanged -= HandleStunSfx;
            OnStunChanged += HandleStunSfx;

            if (StateMachine?.CurrentState is BossIdleState)
            {
                StartIdleLoop();
            }
        }

        protected override void ProcessPostDamageReactions(int finalDamage)
        {
            base.ProcessPostDamageReactions(finalDamage);
            if (finalDamage <= 0 || Snapshot == null || !Snapshot.IsAlive) return;

            if (Time.unscaledTime - _lastHitSfxTime >= 0.06f)
            {
                _lastHitSfxTime = Time.unscaledTime;
                var d = JunkWheelerSfx.Data;
                if (d != null)
                {
                    bool useArmor = (_hitSfxCounter++ % 2) == 0;
                    JunkWheelerSfx.PlayOrFallback(
                        useArmor ? d.hitArmor : d.hitRubber,
                        useArmor ? d.hitRubber : d.hitArmor,
                        transform.position);
                }
            }
        }

        protected override void OnPhaseChanged(int prevPhase, int newPhase)
        {
            base.OnPhaseChanged(prevPhase, newPhase);
            Debug.Log($"<color=red>[JunkWheelerBossController] ▶ [페이즈 전환!] Phase {prevPhase} ➔ Phase {newPhase}</color>");

            if (newPhase >= 2)
            {
                // 2페이즈 진입: 엔진 과열 불꽃 이펙트 활성화
                if (phase2FireVFX != null)
                {
                    phase2FireVFX.Play();
                }

                var d = JunkWheelerSfx.Data;
                if (d != null)
                {
                    var fallback = d.v8RevAggressive.HasValidClip ? d.v8RevAggressive : d.idleLoop;
                    JunkWheelerSfx.PlayOrFallback(d.phase2Roar, fallback, transform.position, 1f, 1.25f, 1.15f);
                    if (d.bgmPhase2 != null)
                    {
                        JunkWheelerSfx.PlayBgm(d.bgmPhase2, 0.5f);
                    }
                }
            }
        }

        public override void StopBossCombat()
        {
            base.StopBossCombat();
            JunkWheelerSfx.Stop(ref _idleLoopSource);
            JunkWheelerSfx.Stop(ref _groggyLoopSource);
            JunkWheelerSfx.StopAllLoops();
        }

        protected override void OnBossDefeated()
        {
            base.OnBossDefeated();
            Debug.Log("<color=red>[JunkWheelerBossController] ▶ 정크 휠러 코어 체력 소진! 보스 토벌 완료!</color>");

            if (phase2FireVFX != null)
            {
                phase2FireVFX.Stop();
            }

            if (defeatExplosionVFX != null)
            {
                defeatExplosionVFX.Play();
            }

            var d = JunkWheelerSfx.Data;
            if (d != null)
            {
                if (d.defeatExplosion.HasValidClip)
                {
                    JunkWheelerSfx.Play(d.defeatExplosion, transform.position);
                }
                else
                {
                    // 전용 사망 폭발음이 비어 있으면 파열음+중장비 충돌음 레이어링으로 웅장한 폭발 연출
                    JunkWheelerSfx.Play(d.tireBlowout, transform.position, 0.72f, 1.1f);
                    JunkWheelerSfx.Play(d.chargeHitPlayer, transform.position, 0.75f, 1.15f);
                }
                JunkWheelerSfx.StopBgm(0.8f);
            }
        }

        protected override void OnDestroy()
        {
            OnStateChanged -= HandleStateChangedForIdleSfx;
            OnGroggyChanged -= HandleGroggySfx;
            OnStunChanged -= HandleStunSfx;
            base.OnDestroy();
            if (sfxData != null && JunkWheelerSfx.Data == sfxData)
            {
                JunkWheelerSfx.Unbind();
            }
        }

        private void StartIdleLoop()
        {
            if (!playIdleEngineLoop || _idleLoopSource != null) return;
            var d = JunkWheelerSfx.Data;
            if (d != null && d.idleLoop.HasValidClip)
            {
                _idleLoopSource = JunkWheelerSfx.Loop(d.idleLoop, transform.position, 1f, 1f, true);
            }
        }

        private void HandleStateChangedForIdleSfx(string newStateName)
        {
            bool isIdle = StateMachine != null && StateMachine.CurrentState is BossIdleState;
            bool canPlayIdle = isIdle && !IsCombatStopped && Snapshot != null && Snapshot.IsAlive;

            if (canPlayIdle)
            {
                StartIdleLoop();
            }
            else
            {
                JunkWheelerSfx.Stop(ref _idleLoopSource);
            }
        }

        private void HandleGroggySfx(bool isGroggy, float duration)
        {
            var d = JunkWheelerSfx.Data;
            if (d == null) return;

            if (isGroggy)
            {
                if (_groggyLoopSource == null)
                {
                    _groggyLoopSource = JunkWheelerSfx.Loop(d.stateGroggy, transform.position, 1f, 1f, false);
                }
            }
            else
            {
                JunkWheelerSfx.Stop(ref _groggyLoopSource);
            }
        }

        private void HandleStunSfx(bool isStun, float duration)
        {
            if (!isStun) return;
            var d = JunkWheelerSfx.Data;
            if (d != null)
            {
                JunkWheelerSfx.PlayOrFallback(d.chargeInterrupted, d.stateGroggy, transform.position, 1f, 1.25f, 0.9f);
            }
        }
    }
}
