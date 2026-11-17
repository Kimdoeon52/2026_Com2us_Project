using System;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러 폐타이어 부위 기믹 (JunkWheelerTirePartGimmick)]
    /// 기획서 §6.18 및 §3.2 (2):
    /// 1. 폐타이어 부위(TIRE_PART) 내구도 관리 (최대 내구도 2, 못판 착지 시 1씩 차감).
    /// 2. 펑크 2회 누적 시 타이어가 영구 파괴됨.
    /// 3. 스킬을 봉인하지 않고 스펙 약화(Degradation)를 유발함:
    ///    - 돌진 속도 영구 감속: 11.0 m/s ➔ 5.5 m/s (50% 감속)
    ///    - 탄막 사출 높이 영구 감축: 1.7 m ➔ 1.19 m (30% 감축)
    /// </summary>
    public class JunkWheelerTirePartGimmick : BossPartGimmickBase
    {
        [Header("타이어 기믹 설정")]
        [SerializeField] private ParticleSystem flatTireParticle; // 펑크 시 연기/스파크 파티클 (선택)

        public bool IsPermanentlyBroken => BreakCount >= 2;

        private void Awake()
        {
            gimmickID = "TIRE_PART";
            gimmickName = "폐타이어 부위";
            maxDurability = 2;
            targetSkillIDToSeal = ""; // 공백 유지: 스킬 영구 봉인 대신 스펙 약화 적용
        }

        protected override void OnPartDamaged(int amount)
        {
            base.OnPartDamaged(amount);
            if (JunkWheelerSfx.Data != null && OwnerBoss != null)
            {
                JunkWheelerSfx.Play(JunkWheelerSfx.Data.hitRubber, OwnerBoss.transform.position);
            }
        }

        protected override void OnPartBroken()
        {
            if (flatTireParticle != null)
            {
                flatTireParticle.Play();
            }

            var d = JunkWheelerSfx.Data;
            if (d != null && OwnerBoss != null)
            {
                JunkWheelerSfx.PlayOrFallback(d.tireBreakFinal, d.driftDonut, OwnerBoss.transform.position, 1f, 1.28f, 1.0f);
            }

            if (IsPermanentlyBroken)
            {
                ApplyDegradation();
            }
        }

        /// <summary>
        /// 부착된 돌진 및 탄막 패턴 기믹에 스펙 약화 디버프 통보
        /// </summary>
        public void ApplyDegradation()
        {
            if (OwnerBoss == null) return;

            var chargePattern = OwnerBoss.GetGimmick<JunkWheelerChargePatternGimmick>("PATTERN_CHARGE");
            if (chargePattern != null)
            {
                chargePattern.ApplyTireBrokenDebuff(true);
            }

            var barragePattern = OwnerBoss.GetGimmick<JunkWheelerBarragePatternGimmick>("PATTERN_JUMP_BARRAGE");
            if (barragePattern != null)
            {
                barragePattern.ApplyTireBrokenDebuff(true);
            }
        }
    }
}
