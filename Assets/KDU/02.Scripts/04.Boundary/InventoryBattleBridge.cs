// 인벤토리 장착 상태를 전투 스냅샷 부위에 덮어쓴다. 정산 결과가 파츠 ID로 인벤토리에 돌아오게 하는 연결점
// 정식 조립기가 생기기 전까지 스탯은 스냅샷 값을 그대로 쓰고, 코어 레벨·경험치와 파츠 ID·내구도만 바꾼다
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
