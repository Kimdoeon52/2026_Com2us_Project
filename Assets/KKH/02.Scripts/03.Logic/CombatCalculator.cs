using UnityEngine;

/// <summary>
/// [순수 C# 전투 수치 판정기 (CombatCalculator)]
/// 기획서 공식에 입각하여 유니티 물리와 독립적으로 동작하는 순수 수치 연산 코어임.
/// 1:1 대전 모드 이원화(Robot vs Robot / Robot vs Boss), 방어력 감쇄, D 기본기 실린더 충전,
/// 부위 피격 시 스킬 봉인 및 보스 기믹 파훼를 총괄 연산함.
/// </summary>
public static class CombatCalculator
{
    /// <summary>
    /// 방어력 감쇄 공통 계산식:
    /// FinalDamage = RawDamage * (100 / (Defense + 100))
    /// 최소 1 대미지 보장함.
    /// </summary>
    public static int CalculateDamage(float rawDamage, int defense)
    {
        float multiplier = 100f / (Mathf.Max(0, defense) + 100f);
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
    }

    // ========================================================================
    // 1. [모드 1] 로봇 vs 로봇 1:1 대전 판정 (플레이어 vs 일반 적 NPC A·B·C)
    // ========================================================================
    public static HitResolutionResult EvaluateRobotAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        float rawSkillDamage,
        BodyPart targetPart,
        int partDamageAmount,
        bool isDefenderInvincible,
        bool isBasicAttackD)
    {
        var result = new HitResolutionResult();

        if (attacker == null || defender == null)
            return result;

        // 1) Space 회피(0.3초) 무적 회피 판정
        if (isDefenderInvincible)
        {
            result.isEvaded = true;
            return result;
        }

        // 2) D 기본기 적중 시 공격자 실린더 탄환 +1 장전 (NYH 실린더 연동)
        if (isBasicAttackD)
        {
            attacker.AddCylinder(1);
            result.cylinderGained = true;
        }

        // 3) 대미지 계산 및 방어자 코어 HP 차감 (모든 유효 공격 공통)
        // D 기본기는 고정 피해 10, 스킬은 기본 피해 * (공격자 총 공격력 / 100)
        float actualDamage = isBasicAttackD ? 10f : rawSkillDamage * (attacker.totalAttackPower / 100f);
        result.coreHpDamage = CalculateDamage(actualDamage, defender.baseDefense);
        defender.currentHp = Mathf.Max(0, defender.currentHp - result.coreHpDamage);

        // 4) 특정 부위 타격 스킬인 경우에만 해당 부위 내구도 차감 (기획서 확정)
        if (targetPart != BodyPart.Core && defender.partStates.TryGetValue(targetPart, out var partState))
        {
            result.targetBodyPart = targetPart;
            result.partDurabilityDamage = partDamageAmount;
            result.isPartDestroyed = defender.ConsumePartDurability(targetPart, partDamageAmount);

            if (result.isPartDestroyed)
            {
                CombatDataHub.Instance?.BroadcastPartBroken(defender.fighterID, targetPart);
            }
        }

        return result;
    }

    // ========================================================================
    // 1-1. [NYH HitDetection 전용 연동 오버로드]
    // ========================================================================
    public static HitResolutionResult EvaluateRobotAttack(
        CombatantSnapshot attacker,
        CombatantSnapshot defender,
        ActionData attackAction,
        bool isGuarding,
        bool isDefenderInvincible,
        BodyPart targetPart = BodyPart.Core)
    {
        if (attackAction == null) return new HitResolutionResult();

        bool isBasicAttackD = attackAction.ActionName == "D" || attackAction.Source == ActionSource.CoreFixed;
        float rawDamage = attackAction.Damage > 0f ? attackAction.Damage : 10f;

        var result = EvaluateRobotAttack(
            attacker,
            defender,
            rawDamage,
            targetPart,
            partDamageAmount: (targetPart != BodyPart.Core) ? 15 : 0,
            isDefenderInvincible: isDefenderInvincible,
            isBasicAttackD: isBasicAttackD
        );

        result.KnockbackDistance = attackAction.KnockbackDistance;
        result.isGuarded = isGuarding;
        return result;
    }

    // ========================================================================
    // 2. [모드 2-A] 보스 -> 플레이어 공격 판정 (보스전)
    // ========================================================================
    public static HitResolutionResult EvaluateBossAttack(
        BossSnapshot boss,
        CombatantSnapshot player,
        float skillRawDamage,
        BodyPart targetPart,
        int partDamageAmount,
        bool isPlayerInvincible)
    {
        var result = new HitResolutionResult();

        if (boss == null || player == null)
            return result;

        // 1) Space 회피(0.3초) 무적 판정
        if (isPlayerInvincible)
        {
            result.isEvaded = true;
            return result;
        }

        // 2) 코어 HP 차감 (모든 공격 공통 방어력 감쇄 적용)
        result.coreHpDamage = CalculateDamage(skillRawDamage, player.baseDefense);
        player.currentHp = Mathf.Max(0, player.currentHp - result.coreHpDamage);

        // 3) 보스 특정 부위 타격 스킬인 경우에만 지정 부위 내구도 차감
        if (targetPart != BodyPart.Core && player.partStates.TryGetValue(targetPart, out var partState))
        {
            result.targetBodyPart = targetPart;
            result.partDurabilityDamage = partDamageAmount;
            result.isPartDestroyed = player.ConsumePartDurability(targetPart, partDamageAmount);

            if (result.isPartDestroyed)
            {
                CombatDataHub.Instance?.BroadcastPartBroken(player.fighterID, targetPart);
            }
        }

        return result;
    }

    // ========================================================================
    // 3. [모드 2-B] 플레이어 -> 보스 공격 판정 (보스전 및 기믹 파훼)
    // ========================================================================
    public static HitResolutionResult EvaluatePlayerAttackOnBoss(
        CombatantSnapshot player,
        BossSnapshot boss,
        float skillBaseDamage,
        bool isBasicAttackD,
        GimmickTag attackTag = GimmickTag.None,
        string targetGimmickID = null)
    {
        var result = new HitResolutionResult();

        if (player == null || boss == null)
            return result;

        // 1) D 기본기 적중 시 실린더 탄환 +1 장전
        if (isBasicAttackD)
        {
            player.AddCylinder(1);
            result.cylinderGained = true;
        }

        // 2) 그로기 상태 1.5배 피해 배율 적용
        float damageMultiplier = boss.isGroggy ? 1.5f : 1.0f;
        float actualDamage = isBasicAttackD ? 10f : skillBaseDamage * (player.totalAttackPower / 100f);
        result.coreHpDamage = CalculateDamage(actualDamage * damageMultiplier, boss.baseDefense);
        boss.currentHp = Mathf.Max(0, boss.currentHp - result.coreHpDamage);

        // 3) 기믹 타격 처리
        if (!string.IsNullOrEmpty(targetGimmickID) && boss.gimmickStates.TryGetValue(targetGimmickID, out var gimmick))
        {
            bool wasBroken = gimmick.isBroken;
            gimmick.currentDurability = Mathf.Max(0, gimmick.currentDurability - Mathf.RoundToInt(actualDamage));
        
            if (!wasBroken && gimmick.isBroken)
            {
                result.isGimmickTriggered = true;
                gimmick.triggerCount++;
            }
        }

        return result;
    }

    // ========================================================================
    // 행동 실행 가능 게이트 (기획서 §6.12.4, §6.12.9 & NYH ActionExecutor 연동)
    // ========================================================================
    /// <summary>
    /// ActionData 기반 행동 가능 여부 검사 (NYH ActionExecutor 호출용)
    /// </summary>
    public static bool CanExecuteAction(CombatantSnapshot actor, ActionData action)
    {
        if (actor == null || action == null)
            return true;

        // 1. 코어 고정 기본기(D, 회피, 이동)는 항상 시전 가능
        if (action.Source == ActionSource.CoreFixed)
            return true;

        // 2. 파츠 스킬: 해당 부위 파손 여부 검사 (부위 파괴 시 스킬 봉인)
        if (actor.IsPartBroken(action.RequiredPart))
        {
            Debug.Log($"[CombatCalculator] '{action.ActionName}' 시전 불가: 요구 부위({action.RequiredPart}) 파손 상태임 (스킬 봉인)");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 원시 파라미터 기반 행동 가능 여부 검사 (실린더 소모량 포함)
    /// </summary>
    public static bool CanExecuteAction(CombatantSnapshot player, ActionSource source, BodyPart requiredPart, int cylinderCost = 0)
    {
        if (player == null) return true;

        if (source == ActionSource.CoreFixed)
            return true;

        if (source == ActionSource.Part)
        {
            if (player.IsPartBroken(requiredPart)) return false;
            if (cylinderCost > 0 && !player.CanSpendCylinder(cylinderCost)) return false;
        }

        return true;
    }

   
}
