using System;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 박자 동기화 패턴 추상 기본 클래스]
    /// BeatClock 및 RSNanoSwarm 참조를 제공하고,
    /// 원본 패턴 타격(파츠 내구도 차감) vs 섹션 강화 추가타(코어 HP만 차감)를 명확히 분리함.
    /// </summary>
    public abstract class KyoshoPatternGimmickBase : BossPatternGimmickBase
    {
        [Header("박자 기반 타이밍 설정 (박 단위)")]
        [SerializeField] protected float tellBeats = 1f;
        [SerializeField] protected float postDelayBeats = 1f;

        public KyoshoBossController Kyosho => OwnerBoss as KyoshoBossController;
        public KyoshoBeatClock Clock => Kyosho != null ? Kyosho.BeatClock : null;
        public RSNanoSwarm Swarm => Kyosho != null ? Kyosho.Swarm : null;
        public float PostDelayBeats => postDelayBeats;

        protected override void OnPreExecute()
        {
            base.OnPreExecute();
            // 쿄쇼의 모든 전조는 ExecuteActionAsync 내부에서 BeatClock 정박에 맞춰 수행하므로 BossPatternData의 초 단위 전조 루프는 0으로 유지
            if (patternData != null && patternData.tellDuration > 0f && !patternData.hasDpsCheck)
            {
                patternData.tellDuration = 0f;
            }
        }

        /// <summary>
        /// 원본 패턴 피격 처리 (파츠 내구도 차감 포함, 난이도 attackMultiplier 적용).
        /// </summary>
        protected bool ApplyOriginalPatternHit(GameObject playerObj, int overrideBaseDamage = -1, BodyPart? overridePart = null, int overridePartDmg = -1)
        {
            if (playerObj == null) return false;
            if (IsPlayerInvincible(playerObj))
            {
                Debug.Log($"<color=cyan>[{GimmickName}] 플레이어가 Space 무적 회피로 원본 공격을 회피했습니다!</color>");
                return false;
            }

            int baseDmg = overrideBaseDamage >= 0 ? overrideBaseDamage : (patternData != null ? patternData.baseDamage : 180);
            BodyPart part = overridePart ?? (patternData != null ? patternData.targetPlayerPart : BodyPart.Core);
            int partDmg = overridePartDmg >= 0 ? overridePartDmg : (patternData != null ? patternData.partDurabilityDamage : 25);

            float mult = (OwnerBoss != null && OwnerBoss.Snapshot != null) ? OwnerBoss.Snapshot.attackMultiplier : 1f;
            float testScale = Kyosho != null ? Kyosho.TestDamageScale : 1f;
            int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * mult * testScale));
            int scaledPartDmg = partDmg > 0 ? Mathf.Max(1, Mathf.RoundToInt(partDmg * mult * testScale)) : 0;

            ApplyPlayerPartHit(scaledDmg, part, scaledPartDmg);
            return true;
        }

        /// <summary>
        /// 악기 섹션 강화 추가타 및 변주곡 실패 페널티 전용:
        /// 반드시 코어 HP만 차감하고 플레이어 파츠 내구도는 절대 깎지 않음 (§2.2).
        /// </summary>
        protected bool ApplyCoreOnlyHit(GameObject playerObj, int rawBaseDamage, bool respectSpaceDodge = true)
        {
            if (playerObj == null) return false;
            if (respectSpaceDodge && IsPlayerInvincible(playerObj))
            {
                Debug.Log($"<color=cyan>[{GimmickName}] 플레이어가 Space 무적 회피로 추가 공격을 회피했습니다!</color>");
                return false;
            }

            float mult = (OwnerBoss != null && OwnerBoss.Snapshot != null) ? OwnerBoss.Snapshot.attackMultiplier : 1f;
            float testScale = Kyosho != null ? Kyosho.TestDamageScale : 1f;
            int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(rawBaseDamage * mult * testScale));

            ApplyPlayerPartHit(scaledDmg, BodyPart.Core, 0);
            return true;
        }

        protected GameObject FindPlayerObject()
        {
            Transform pt = Kyosho != null ? Kyosho.PlayerTransform : null;
            return pt != null ? pt.gameObject : GameObject.FindWithTag("Player");
        }
    }
}
