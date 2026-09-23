using UnityEngine;

/// <summary>
/// [순수 C# 전투 수치 판정기 (CombatCalculator)]
/// 기획서 공식에 입각해서 유니티 물리/애니메이션과 독립적으로
/// 대미지 감쇄, 가드 분산 소모, 위빙 페널티, 부위 파손에 따른 행동 가능 여부를 연산하는 순수 엔진 클래스임.
/// </summary>
public static class CombatCalculator
{
    /// <summary>
    /// 기술 시전 가능 여부 검사함 (전투 행동 파트 ActionExecutor 연동용)
    /// - CoreFixed(잽, 가드, 위빙, 이동): 항상 시전 가능함
    /// - Part(훅, 스트레이트, 어퍼컷, 백스핀 등): 해당 부위 파손 시 시전 차단함
    /// </summary>
    public static bool CanExecuteAction(CombatantSnapshot actor, ActionData action)
    {
        if (actor == null || action == null)
            return false;

        // 1. 코어 고정 기술은 부위 파손과 무관하게 항상 실행 가능함
        if (action.Source == ActionSource.CoreFixed)
            return true;

        // 2. 파츠 소속 기술인 경우 해당 요구 부위 파손 여부 검사함
        if (actor.IsPartBroken(action.RequiredPart))
        {
            Debug.Log($"[CombatCalculator] '{action.ActionName}' 시전 불가: 요구 부위({action.RequiredPart}) 파손 또는 미장착 상태임");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 타격 적중 시 대미지 감쇄 및 부위 피격 처리함
    /// 기획서 공식: FinalDamage = RawDamage * (100 / (Defense + 100))
    /// </summary>
    public static HitResolutionResult EvaluateHit(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        ActionData attackAction,
        bool isGuarding,
        bool isWeaving,
        BodyPart hitPart = BodyPart.Core)
    {
        var result = new HitResolutionResult();

        if (attacker == null || defender == null || attackAction == null)
            return result;

        // 1. 위빙(회피) 성공 시 완전 무적: 대미지 0임
        if (isWeaving)
        {
            result.IsEvaded = true;
            result.DamageToHp = 0;
            result.StaggerAdded = 0;
            return result;
        }

        // 2. 공격력 연산함 (RawDamage = TotalAttackPower * (Damage 계수 / 100))
        float damageRatio = attackAction.Damage > 0f ? attackAction.Damage / 100f : 1f;
        float rawAtk = attacker.totalAttackPower * damageRatio;

        // 3. 가드 판정함
        // - 백스핀 엘보우 등 IsGuardable == false 인 기술은 가드를 무시하고 코어에 직격함
        if (isGuarding && attackAction.IsGuardable)
        {
            result.IsGuarded = true;
            int totalDef = defender.baseDefense + defender.guardDefBonus;
            float finalDmg = rawAtk * (100f / (totalDef + 100f));

            // 가드 성공 시 잔여 피해를 양팔 내구도에 5:5 분산 차감함 (코어 HP 피해 0임)
            int armDmg = Mathf.RoundToInt(finalDmg * 0.5f);
            defender.ConsumeDurability(BodyPart.LeftArm, armDmg);
            defender.ConsumeDurability(BodyPart.RightArm, armDmg);

            result.LeftArmDurabilityDamage = armDmg;
            result.RightArmDurabilityDamage = armDmg;
            result.DamageToHp = 0;
        }
        else
        {
            // 4. 유효타 피격 (코어 본체 HP + 피격 파츠 내구도 동시 감쇄)
            float finalDmg = rawAtk * (100f / (defender.baseDefense + 100f));

            // 머리 파츠 피격 또는 치명타 발생 판정
            float baseCritChance = 0.15f;
            float effectiveCritChance = Mathf.Max(0f, baseCritChance - defender.critResistance);

            if (hitPart == BodyPart.Head || Random.value < effectiveCritChance)
            {
                result.IsCritical = true;
                float critMultiplier = Mathf.Max(1.0f, 1.5f - defender.critDamageReduction);
                finalDmg *= critMultiplier;

                // 머리 피격 시 머리 내구도 차감
                int headDmg = Mathf.Max(5, Mathf.RoundToInt(finalDmg * 0.25f));
                defender.ConsumeDurability(BodyPart.Head, headDmg);
            }
            else if (hitPart != BodyPart.Core)
            {
                // 팔 또는 다리 부위 피격 시 해당 파츠 내구도 차감
                int partDmg = Mathf.Max(5, Mathf.RoundToInt(finalDmg * 0.25f));
                defender.ConsumeDurability(hitPart, partDmg);

                if (hitPart == BodyPart.LeftArm) result.LeftArmDurabilityDamage = partDmg;
                else if (hitPart == BodyPart.RightArm) result.RightArmDurabilityDamage = partDmg;
            }

            // [핵심] 기체 본체(코어) 체력 차감 (파츠 피격 시에도 본체 생명력에 피해가 반영됨)
            result.DamageToHp = Mathf.Max(1, Mathf.RoundToInt(finalDmg));
            defender.currentHp = Mathf.Max(0, defender.currentHp - result.DamageToHp);
        }

        // 5. 경직도 및 다운/넉백 수치 세팅함
        result.StaggerAdded = attackAction.StaggerValue;
        result.CausesKnockdown = attackAction.CausesKnockdown;
        result.KnockbackDistance = attackAction.KnockbackDistance;

        return result;
    }

    /// <summary>
    /// 위빙(회피) 시도 시 다리 내구도 소모 및 상태별 성공/실패 판정함 (기획서 §5.7.4)
    /// - 시도 시 좌/우 다리 중 무작위 1개 내구도 5 고정 차감함
    /// - 양다리 모두 파괴 상태: 100% 회피 불가임
    /// - 다리 1개 파괴 상태: 50% 확률로 회피 실패함
    /// - 정상 상태: 100% 회피 성공함
    /// </summary>
    public static bool EvaluateWeavingAttempt(CombatantSnapshot actor)
    {
        if (actor == null) return false;

        // 1. 양다리 모두 파손 시 회피 완전 불가임
        if (actor.IsBothLegsBroken())
        {
            Debug.Log($"[CombatCalculator] {actor.fighterID} 위빙 실패: 양다리 모두 파괴된 상태임");
            return false;
        }

        // 2. 다리 내구도 5 소모함 (좌/우 다리 중 무작위 1개, 파괴되지 않은 쪽 우선 차감)
        BodyPart legToConsume;
        bool leftBroken = actor.IsPartBroken(BodyPart.LeftLeg);
        bool rightBroken = actor.IsPartBroken(BodyPart.RightLeg);

        if (leftBroken)
        {
            legToConsume = BodyPart.RightLeg;
        }
        else if (rightBroken)
        {
            legToConsume = BodyPart.LeftLeg;
        }
        else
        {
            legToConsume = (Random.value < 0.5f) ? BodyPart.LeftLeg : BodyPart.RightLeg;
        }

        actor.ConsumeDurability(legToConsume, 5);

        // 3. 다리 1개 파괴 상태: 50% 확률로 실패 반환함
        if (actor.IsOneLegBroken())
        {
            bool isSuccess = Random.value >= 0.5f;
            if (!isSuccess)
            {
                Debug.Log($"[CombatCalculator] {actor.fighterID} 위빙 실패: 다리 한쪽 파손 페널티 (50% 확률 미달임)");
            }
            return isSuccess;
        }

        // 4. 정상 상태: 100% 성공함
        return true;
    }
}
