using System;
using System.Collections.Generic;
using UnityEngine;

// 부품 스택 목록. 스택 1개가 1칸이고, 한 칸 최대 개수를 넘으면 같은 부품이라도 스택을 나눈다
[Serializable]
public class ComponentStock : IComponentSink
{
    [Serializable]
    public class Slot
    {
        public string Id;
        public int Count;
        public Vector2Int Origin = NoOrigin;

        // 배치된 칸이 있는지
        public bool HasOrigin => Origin.x >= 0 && Origin.y >= 0;
    }

    [SerializeField] private List<Slot> _slots = new List<Slot>();

    public IReadOnlyList<Slot> Slots => _slots;

    public static Vector2Int NoOrigin => new Vector2Int(-1, -1);

    // 모든 스택 합계
    public int Get(string id)
    {
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].Id == id)
                total += _slots[i].Count;
        }

        return total;
    }

    // 기존 스택을 먼저 채우고 남으면 새 스택을 만든다. 더해진 양을 반환
    public int Add(ComponentDefinition component, int amount)
    {
        if (component == null || string.IsNullOrEmpty(component.Id) || amount <= 0)
            return 0;

        int left = amount;
        for (int i = 0; i < _slots.Count && left > 0; i++)
        {
            Slot slot = _slots[i];
            if (slot == null || slot.Id != component.Id)
                continue;

            int take = Mathf.Min(left, component.RemainingCapacity(slot.Count));
            slot.Count += take;
            left -= take;
        }

        while (left > 0)
        {
            int take = Mathf.Min(left, component.RemainingCapacity(0));
            _slots.Add(new Slot { Id = component.Id, Count = take });
            left -= take;
        }

        return amount;
    }

    // 기존 스택에 더 들어갈 수 있는 양
    public int RoomInStacks(ComponentDefinition component)
    {
        int room = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null || _slots[i].Id != component.Id)
                continue;

            int remaining = component.RemainingCapacity(_slots[i].Count);
            if (remaining == int.MaxValue)
                return int.MaxValue;

            room += remaining;
        }

        return room;
    }

    // 적게 남은 스택부터 소모해 빈 칸을 먼저 비운다
    public bool TryConsume(string id, int amount)
    {
        if (amount <= 0 || Get(id) < amount)
            return false;

        int left = amount;
        while (left > 0)
        {
            Slot smallest = FindSmallest(id);
            int take = Mathf.Min(left, smallest.Count);
            smallest.Count -= take;
            left -= take;

            if (smallest.Count <= 0)
                _slots.Remove(smallest);
        }

        return true;
    }

    // 지정한 스택에서만 소모
    public bool TryConsume(Slot slot, int amount)
    {
        if (slot == null || amount <= 0 || slot.Count < amount || !_slots.Contains(slot))
            return false;

        slot.Count -= amount;
        if (slot.Count <= 0)
            _slots.Remove(slot);

        return true;
    }

    // 한 칸 최대 개수를 넘는 스택을 나눈다. 최대 개수를 줄였거나 예전 세이브일 때
    public void SplitOversized(ComponentCatalog catalog)
    {
        if (catalog == null)
            return;

        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            Slot slot = _slots[i];
            ComponentDefinition component = slot != null ? catalog.Find(slot.Id) : null;
            if (component == null || component.MaxCount <= 0 || slot.Count <= component.MaxCount)
                continue;

            int overflow = slot.Count - component.MaxCount;
            slot.Count = component.MaxCount;
            Add(component, overflow);
        }
    }

    public void Clear()
    {
        _slots.Clear();
    }

    private Slot FindSmallest(string id)
    {
        Slot smallest = null;
        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot == null || slot.Id != id || slot.Count <= 0)
                continue;

            if (smallest == null || slot.Count < smallest.Count)
                smallest = slot;
        }

        return smallest;
    }
}
