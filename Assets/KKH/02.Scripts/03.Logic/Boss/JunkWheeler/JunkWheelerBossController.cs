using System;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [지역 1 보스 "정크 휠러" 구체 컨트롤러 (JunkWheelerBossController)]
    /// 기획서 §6.18 및 §3.2 (1), §5.1:
    /// 1. BossController를 상속하여 기존 도구(BossFSMTester, BattleManager) 및 씬 직렬화 100% 호환 보장.
    /// 2. 폐타이어 부위 기믹, 돌진 기믹, 탄막 기믹, 3단 내려찍기 기믹, 회전 톱날 당구 돌진 기믹을 컴포지션(Composition)으로 조립/제어.
    /// 3. 정크 휠러 스프라이트 반전(FaceDirection) 및 페이즈 전환(2페이즈 엔진 과열/불꽃) 연출 담당.
    /// </summary>
    public class JunkWheelerBossController : BossController
    {
        [Header("정크 휠러 전용 연출 및 컴포넌트")]
        [SerializeField] private ParticleSystem phase2FireVFX; // 2페이즈 엔진 불꽃 파티클
        [SerializeField] private ParticleSystem defeatExplosionVFX; // 토벌 사망 폭발 파티클

        [Header("부착된 구체 기믹 참조 (인스펙터 모니터링)")]
        [SerializeField] private JunkWheelerTirePartGimmick tirePartGimmick;
        [SerializeField] private JunkWheelerChargePatternGimmick chargePatternGimmick;
        [SerializeField] private JunkWheelerBarragePatternGimmick barragePatternGimmick;
        [SerializeField] private JunkWheelerSlamPatternGimmick slamPatternGimmick;
        [SerializeField] private JunkWheelerRicochetBladePatternGimmick ricochetBladePatternGimmick;

        public JunkWheelerTirePartGimmick TirePartGimmick => tirePartGimmick;
        public JunkWheelerChargePatternGimmick ChargePatternGimmick => chargePatternGimmick;
        public JunkWheelerBarragePatternGimmick BarragePatternGimmick => barragePatternGimmick;
        public JunkWheelerSlamPatternGimmick SlamPatternGimmick => slamPatternGimmick;
        public JunkWheelerRicochetBladePatternGimmick RicochetBladePatternGimmick => ricochetBladePatternGimmick;

        public override void AutoBindInspectorReferences()
        {
            base.AutoBindInspectorReferences();
            if (tirePartGimmick == null) tirePartGimmick = GetComponentInChildren<JunkWheelerTirePartGimmick>(true);
            if (chargePatternGimmick == null) chargePatternGimmick = GetComponentInChildren<JunkWheelerChargePatternGimmick>(true);
            if (barragePatternGimmick == null) barragePatternGimmick = GetComponentInChildren<JunkWheelerBarragePatternGimmick>(true);
            if (slamPatternGimmick == null) slamPatternGimmick = GetComponentInChildren<JunkWheelerSlamPatternGimmick>(true);
            if (ricochetBladePatternGimmick == null) ricochetBladePatternGimmick = GetComponentInChildren<JunkWheelerRicochetBladePatternGimmick>(true);

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
            AutoBindInspectorReferences();
            AutoWireSceneSystems();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }

        protected override void RegisterGimmicks()
        {
            // 1. 타이어 부위 기믹 탐색 및 부착
            tirePartGimmick = GetOrAddComponent<JunkWheelerTirePartGimmick>();
            RegisterGimmick(tirePartGimmick);

            // 2. 패턴 기믹 4종 탐색 및 부착 (마스터 데이터 매칭)
            chargePatternGimmick = GetOrAddComponent<JunkWheelerChargePatternGimmick>();
            barragePatternGimmick = GetOrAddComponent<JunkWheelerBarragePatternGimmick>();
            slamPatternGimmick = GetOrAddComponent<JunkWheelerSlamPatternGimmick>();
            ricochetBladePatternGimmick = GetOrAddComponent<JunkWheelerRicochetBladePatternGimmick>();

            BindPatternDataIfAvailable();

            RegisterGimmick(chargePatternGimmick);
            RegisterGimmick(barragePatternGimmick);
            RegisterGimmick(slamPatternGimmick);
            RegisterGimmick(ricochetBladePatternGimmick);

            // 3. 기타 씬 내 추가 기믹(전장 기물 등) 자동 등록
            base.RegisterGimmicks();
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
        }

        // 좌우 반전은 BossControllerBase.FaceDirection 단일 경로로 통일 (역부호 중복 선언 제거)

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
            }
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
        }
    }
}
