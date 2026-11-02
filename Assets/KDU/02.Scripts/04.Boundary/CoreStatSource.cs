using System.Collections.Generic;
using UnityEngine;

// 코어 레벨과 장착 파츠로 표시용 스탯을 계산한다. 공식은 전투 쪽 CombatCalculator를 그대로 쓴다
public class CoreStatSource : IPlayerStatSource
{
    private readonly List<StatLine> _lines = new List<StatLine>();
    private readonly PartsDefinition[] _bySlot = new PartsDefinition[EquipmentSaveData.SlotCount];

    public IReadOnlyList<StatLine> GetStats(IReadOnlyList<string> equippedPartIds)
    {
        _lines.Clear();

        InventoryHost host = InventoryHost.Instance;
        CoreDefinition core = host != null ? host.Core : null;
        if (core == null)
        {
            _lines.Add(new StatLine("코어", "-"));
            return _lines;
        }

        CollectParts(host, equippedPartIds);

        int level = host.CoreLevel;
        CoreLevel stats = core.GetLevel(level);
        float reduction = CombatCalculator.CalculateDamageReductionRate(stats.Defense);
        int attack = CombatCalculator.CalculateTotalAttackPower(core.BaseAttack, Attack(Slot.LeftArm), Attack(Slot.RightArm));
        float speed = CombatCalculator.CalculateMoveSpeed(Speed(Slot.LeftLeg), Speed(Slot.RightLeg));

        float cooldown = Mathf.Min(Sum(p => p.CooldownReduction), core.CooldownReductionCap);
        float defenseBonus = Sum(p => p.DefenseBonus);
        float range = Sum(p => p.RangeIncrease);

        _lines.Add(new StatLine("코어 레벨", $"LV.{level}"));
        _lines.Add(new StatLine("체력", stats.Hp.ToString("N0")));
        _lines.Add(new StatLine("방어력", defenseBonus > 0f ? $"{stats.Defense} (+{defenseBonus:0.0})" : stats.Defense.ToString()));
        _lines.Add(new StatLine("피해 감소", $"{reduction * 100f:0.0}%"));
        _lines.Add(new StatLine("공격력", attack.ToString()));
        _lines.Add(new StatLine("이동속도", speed.ToString("0.#")));
        _lines.Add(new StatLine("쿨타임 감소", $"{cooldown:0.0}%"));
        _lines.Add(new StatLine("사거리 증가", $"{range:0.0}%"));
        return _lines;
    }

    // 장착(빈 부위는 기본 파츠) 파츠 합
    private float Sum(System.Func<PartsDefinition, float> stat)
    {
        float total = 0f;
        for (int i = 0; i < _bySlot.Length; i++)
        {
            if (_bySlot[i] != null)
                total += stat(_bySlot[i]);
        }

        return total;
    }

    // 빈 부위는 전투 입장 시 기본 파츠가 끼워지므로 시작 파츠로 계산한다
    private void CollectParts(InventoryHost host, IReadOnlyList<string> equippedPartIds)
    {
        System.Array.Clear(_bySlot, 0, _bySlot.Length);

        PartsCatalog catalog = host.Catalog;
        if (catalog == null)
            return;

        if (catalog.StartingEquipment != null)
        {
            foreach (PartsDefinition part in catalog.StartingEquipment)
            {
                if (part != null)
                    _bySlot[(int)part.Slot] = part;
            }
        }

        if (equippedPartIds == null)
            return;

        for (int i = 0; i < equippedPartIds.Count; i++)
        {
            PartsDefinition part = catalog.Find(equippedPartIds[i]);
            if (part != null)
                _bySlot[(int)part.Slot] = part;
        }
    }

    private int Attack(Slot slot)
    {
        PartsDefinition part = _bySlot[(int)slot];
        return part != null ? part.AttackPower : 0;
    }

    private float Speed(Slot slot)
    {
        PartsDefinition part = _bySlot[(int)slot];
        return part != null ? part.MoveSpeedIndex : 0f;
    }
}
