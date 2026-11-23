using UnityEngine;

// 인벤토리 코어·장착 파츠를 전투 스냅샷에 덮어쓴다. 스탯 공식은 스탯창과 같은 CombatCalculator를 쓴다
public static class InventoryBattleBridge
{
    private static CombatantSnapshot _applied;

    // 같은 스냅샷은 한 번만 적용한다. 전투 중 재등록돼도 깎인 내구도가 되돌아가지 않아야 한다
    public static void ApplyTo(CombatantSnapshot snapshot)
    {
        InventoryHost host = InventoryHost.Instance;
        if (snapshot == null || host == null || ReferenceEquals(snapshot, _applied) || snapshot.partStates == null)
            return;

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
    }

    // 코어 체력·방어력과 파츠 공격력·이동속도. 코어가 없으면 코어 값은 스냅샷 것을 둔다
    private static void ApplyStats(CombatantSnapshot snapshot, InventoryHost host)
    {
        snapshot.finalMoveSpeed = CombatCalculator.CalculateMoveSpeed(StatPart(host, Slot.LeftLeg)?.MoveSpeedIndex ?? 0f, StatPart(host, Slot.RightLeg)?.MoveSpeedIndex ?? 0f);

        CoreDefinition core = host.Core;
        if (core == null)
            return;

        CoreLevel level = core.GetLevel(host.CoreLevel);
        snapshot.maxHp = level.Hp;
        snapshot.currentHp = level.Hp;
        snapshot.baseDefense = level.Defense + Mathf.RoundToInt(SumDefenseBonus(host));
        snapshot.totalAttackPower = CombatCalculator.CalculateTotalAttackPower(core.BaseAttack, StatPart(host, Slot.LeftArm)?.AttackPower ?? 0, StatPart(host, Slot.RightArm)?.AttackPower ?? 0);
    }

    // 스탯 계산용 파츠. 빈 부위는 시작 파츠로 본다
    private static PartsDefinition StatPart(InventoryHost host, Slot slot)
    {
        PartsDefinition equipped = host.GetEquipped(slot);
        if (equipped != null)
            return equipped;

        return host.Catalog != null ? host.Catalog.FindStarting(slot) : null;
    }

    private static float SumDefenseBonus(InventoryHost host)
    {
        float total = 0f;
        for (int i = 0; i < EquipmentSaveData.SlotCount; i++)
        {
            PartsDefinition part = StatPart(host, (Slot)i);
            if (part != null)
                total += part.DefenseBonus;
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
