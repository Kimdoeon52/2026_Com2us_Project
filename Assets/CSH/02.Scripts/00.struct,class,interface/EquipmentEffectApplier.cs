using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 시작 직전에 한 번 호출해서, 장착 파츠에 대응하는 장비 효과를 ActionExecutor에 등록하는 함수.
///
/// 호출 위치: BattleManager.InitializeBattle 이후, StartBattle 이전.
///  - PlayerRobotBootstrap.Awake에서 executor.Init이 끝나 State가 이미 존재하고,
///  - 장착 파츠 목록과 스냅샷(CombatDataHub)을 이 시점에 알고 있으며,
///  - CombatClock의 첫 틱은 첫 Update 이후라서 Start 안에서 등록하면 첫 입력보다 항상 먼저다.
/// 정식 RobotAssembler가 생기면 CombatantBuilder.Build 직후로 옮기면 된다.
/// </summary>
public static class EquipmentEffectApplier
{
    // executor: 효과를 받을 로봇. equippedParts: 장착 파츠 목록. table: partID -> 효과 매핑
    public static void Apply(ActionExecutor executor, IEnumerable<PartMasterData> equippedParts, EquipmentEffectTable table)
    {
        if (executor == null)
        {
            Debug.LogWarning("[EquipmentEffectApplier] executor가 null이라 장비 효과를 등록하지 못함");
            return;
        }

        var set = new EquipmentEffectSet();

        if (equippedParts != null && table != null)
        {
            foreach (var part in equippedParts)
            {
                if (part == null) continue;

                // 같은 partID의 효과는 이 부위의 장비에서 온 것으로 기록한다 (그 부위가 파손되면 효과가 꺼짐)
                var ctx = new EffectContext(executor, part.slotType);
                foreach (var effect in table.GetEffects(part.partID))
                {
                    if (effect == null) continue;
                    set.Add(effect.CreateInstance(ctx), part.slotType);
                }
            }
        }

        // SetEffects 안에서 이전 효과 해제(OnUnequip) -> 교체 -> 새 효과 OnEquip 순서로 처리된다
        executor.SetEffects(set);
        Debug.Log($"[EquipmentEffectApplier] {executor.FighterId}: 장비 효과 {set.Count}개 등록");
    }
}
