using System;
using System.Collections.Generic;


/// <summary>
/// 한 로봇이 장착 장비로부터 받은 효과 인스턴스 묶음 (순수 C# 클래스, 전투 중에만 존재).
/// 어느 부위 장비에서 온 효과인지 함께 기억해서, 그 부위가 파손되면 효과가 꺼지게 한다.
/// </summary>
public class EquipmentEffectSet
{
    private struct Entry
    {
        public EffectInstance Instance;
        public BodyPart SourcePart;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private bool equipped;

    public int Count => entries.Count;

    /// <summary>효과 인스턴스를 등록한다. sourcePart는 이 효과를 준 장착 부위 (파손 시 효과 제외용)</summary>
    public void Add(EffectInstance instance, BodyPart sourcePart)
    {
        if (instance == null) return;
        entries.Add(new Entry { Instance = instance, SourcePart = sourcePart });
    }

    /// <summary>모든 인스턴스의 OnEquip 호출. 두 번 불려도 한 번만 실행된다</summary>
    public void Equip()
    {
        if (equipped) return;
        equipped = true;
        foreach (var e in entries) e.Instance.OnEquip();
    }

    /// <summary>모든 인스턴스의 OnUnequip 호출. Equip 전이거나 이미 해제했으면 아무것도 안 한다</summary>
    public void Unequip()
    {
        if (!equipped) return;
        equipped = false;
        foreach (var e in entries) e.Instance.OnUnequip();
    }

    /// <summary>
    /// action에 해당하는 보정만 합산한다. isPartBroken이 true를 반환하는 부위에서 온 효과는 제외한다 (null이면 검사 안 함).
    /// </summary>
    public ResolvedModifiers Resolve(ActionData action, Func<BodyPart, bool> isPartBroken)
    {
        var r = ResolvedModifiers.Identity;
        if (action == null) return r;

        foreach (var e in entries)
        {
            if (isPartBroken != null && isPartBroken(e.SourcePart)) continue;
            e.Instance.Contribute(action, ref r);
        }
        return r;
    }

    /// <summary>
    /// 요청된 행동을 대체할 효과가 있으면 대체할 행동을, 없으면 요청된 행동 그대로 반환한다.
    /// 여러 효과가 같은 행동을 대체하려 하면 ReplacePriority가 높은 쪽이 이기고, 같으면 나중에 등록된 쪽이 이긴다.
    /// </summary>
    public ActionData ResolveReplacement(ActionData requested, Func<BodyPart, bool> isPartBroken)
    {
        if (requested == null) return null;

        ActionData best = null;
        int bestPriority = int.MinValue;

        foreach (var e in entries)
        {
            if (isPartBroken != null && isPartBroken(e.SourcePart)) continue;

            var candidate = e.Instance.Replace(requested);
            if (candidate == null) continue;

            int priority = e.Instance.ReplacePriority;
            if (best == null || priority >= bestPriority)
            {
                best = candidate;
                bestPriority = priority;
            }
        }

        return best != null ? best : requested;
    }
}
