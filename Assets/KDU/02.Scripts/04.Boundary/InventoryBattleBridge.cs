using UnityEngine;

// 인벤토리 코어·장착 파츠를 전투 스냅샷에 덮어쓴다. 스탯 공식은 스탯창과 같은 CombatCalculator를 쓴다
public static class InventoryBattleBridge
{
    private static CombatantSnapshot _applied;

    // 같은 스냅샷은 한 번만 적용한다. 전투 중 재등록돼도 깎인 내구도가 되돌아가지 않아야 한다
    public static void ApplyTo(CombatantSnapshot snapshot)
    {
        TryApplyTo(snapshot);
    }

    public static bool TryApplyTo(CombatantSnapshot snapshot)
    {
        InventoryHost host = InventoryHost.Instance;
        if (snapshot == null || host == null || ReferenceEquals(snapshot, _applied) || snapshot.partStates == null)
            return false;

        _applied = snapshot;

        // 정산이 스냅샷 값을 세이브에 덮어쓰므로 세이브 값으로 시작한다
        snapshot.coreLevel = host.CoreLevel;
        snapshot.currentCoreExp = host.Save.Core.Exp;

        Apply(snapshot, host, Slot.Head, BodyPart.Head, 42);
        Apply(snapshot, host, Slot.LeftArm, BodyPart.LeftArm, 70);
        Apply(snapshot, host, Slot.RightArm, BodyPart.RightArm, 70);
        Apply(snapshot, host, Slot.LeftLeg, BodyPart.LeftLeg, 56);
        Apply(snapshot, host, Slot.RightLeg, BodyPart.RightLeg, 56);

        ApplyStats(snapshot, host);
        return true;
    }

    // ActionState.Begin()에서 매 행동 시작 시 호출되는 장비 보정치 리졸버 (쿨타임 감소 · 사거리/히트박스 증가)
    public static void ApplyModifiers(ActionData action, ResolvedModifiers resolved)
    {
        InventoryHost host = InventoryHost.Instance;
        if (host == null || resolved == null)
            return;

        float cooldownCap = host.Core != null ? host.Core.CooldownReductionCap : 30f;
        float cooldownReduction = Mathf.Min(SumCooldownReduction(host), cooldownCap);
        if (cooldownReduction > 0f)
        {
            resolved.Multiply(SkillParamId.Cooldown, Mathf.Max(0.1f, 1f - cooldownReduction * 0.01f));
        }

        float rangeIncrease = SumRangeIncrease(host);
        if (rangeIncrease > 0f)
        {
            float rangeMul = 1f + rangeIncrease * 0.01f;
            resolved.Multiply(SkillParamId.Range, rangeMul);
            resolved.Multiply(SkillParamId.HitBoxWidth, rangeMul);
        }
    }

    // 코어 체력·방어력, 팔 공격력, 다리 이동속도 스냅샷 동기화 (실제 무버 배율은 PartMoveSpeed가 별도 적용)
    private static void ApplyStats(CombatantSnapshot snapshot, InventoryHost host)
    {
        CoreDefinition core = host.Core;
        if (core == null)
            return;

        CoreLevel level = core.GetLevel(host.CoreLevel);
        snapshot.maxHp = level.Hp;
        snapshot.currentHp = level.Hp;
        snapshot.baseDefense = level.Defense + Mathf.RoundToInt(SumDefenseBonus(host));
        snapshot.totalAttackPower = CombatCalculator.CalculateTotalAttackPower(core.BaseAttack, Attack(host, Slot.LeftArm), Attack(host, Slot.RightArm));

        float moveSpeedIndex = CombatCalculator.CalculateMoveSpeed(MoveSpeedIndex(host, Slot.LeftLeg), MoveSpeedIndex(host, Slot.RightLeg));
        if (moveSpeedIndex > 0f)
        {
            snapshot.finalMoveSpeed = 3f * (moveSpeedIndex / 100f);
        }
    }

    private static int Attack(InventoryHost host, Slot slot)
    {
        PartsDefinition part = host.GetEquippedOrStarting(slot);
        return part != null ? part.AttackPower : 0;
    }

    private static float MoveSpeedIndex(InventoryHost host, Slot slot)
    {
        PartsDefinition part = host.GetEquippedOrStarting(slot);
        return part != null ? part.MoveSpeedIndex : 0f;
    }

    private static float SumDefenseBonus(InventoryHost host)
    {
        float total = 0f;
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            PartsDefinition part = host.GetEquippedOrStarting((Slot)i);
            if (part != null)
                total += part.DefenseBonus;
        }

        return total;
    }

    private static float SumCooldownReduction(InventoryHost host)
    {
        float total = 0f;
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            PartsDefinition part = host.GetEquippedOrStarting((Slot)i);
            if (part != null)
                total += part.CooldownReduction;
        }

        return total;
    }

    private static float SumRangeIncrease(InventoryHost host)
    {
        float total = 0f;
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            PartsDefinition part = host.GetEquippedOrStarting((Slot)i);
            if (part != null)
                total += part.RangeIncrease;
        }

        return total;
    }

    private static void Apply(CombatantSnapshot snapshot, InventoryHost host, Slot slot, BodyPart part, int defaultDurability)
    {
        if (!snapshot.partStates.TryGetValue(part, out PartRuntimeState state) || state == null)
        {
            state = new PartRuntimeState($"DEFAULT_{part}", part, defaultDurability);
            snapshot.partStates[part] = state;
        }

        PartsDefinition equipped = host.GetEquipped(slot);
        if (equipped != null)
        {
            state.partID = equipped.Id;
            state.maxDurability = equipped.Durability;
            state.currentDurability = host.GetDurability(slot);
            return;
        }

        // 빈 슬롯은 기본 파츠. 정산기가 DEFAULT_ 접두사로 파괴 대상에서 뺀다
        state.partID = $"DEFAULT_{part}";
        state.maxDurability = defaultDurability;
        state.currentDurability = defaultDurability;
    }
}
