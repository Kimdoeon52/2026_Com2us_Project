using System;
using System.Collections.Generic;
using UnityEngine;

// 부품별 스택. 부품 1종당 1칸, 수량은 정의의 최대 소지 개수까지
[Serializable]
public class ComponentStock : IComponentSink
{
    [Serializable]
    public class Slot
    {
        public string Id;
        public int Count;
        public Vector2Int Origin = NoOrigin;
    }

    [SerializeField] private List<Slot> _slots = new List<Slot>();

    public IReadOnlyList<Slot> Slots => _slots;

    public int Get(string id)
    {
        Slot slot = FindSlot(id);
        return slot != null ? slot.Count : 0;
    }

    // 최대 소지 개수를 넘는 양은 버린다. 실제로 더해진 양을 반환
    public int Add(ComponentDefinition component, int amount)
    {
        if (component == null || string.IsNullOrEmpty(component.Id) || amount <= 0)
            return 0;

        Slot slot = GetOrCreateSlot(component.Id);
        int added = Mathf.Min(amount, component.RemainingCapacity(slot.Count));
        slot.Count += added;
        return added;
    }

    public bool TryConsume(string id, int amount)
    {
        if (amount <= 0)
            return false;

        Slot slot = FindSlot(id);
        if (slot == null || slot.Count < amount)
            return false;

        slot.Count -= amount;
        return true;
    }

    // 배치된 칸 좌표. x가 음수면 미배치
    public bool TryGetOrigin(string id, out Vector2Int origin)
    {
        origin = Vector2Int.zero;

        Slot slot = FindSlot(id);
        if (slot == null)
            return false;

        origin = slot.Origin;
        return origin.x >= 0 && origin.y >= 0;
    }

    public void SetOrigin(string id, Vector2Int origin)
    {
        Slot slot = FindSlot(id);
        if (slot != null)
            slot.Origin = origin;
    }

    public void ClearOrigin(string id)
    {
        SetOrigin(id, NoOrigin);
    }

    public void Clear()
    {
        _slots.Clear();
    }

    private static Vector2Int NoOrigin => new Vector2Int(-1, -1);

    private Slot FindSlot(string id)
    {
        if (_slots == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].Id == id)
                return _slots[i];
        }

        return null;
    }

    private Slot GetOrCreateSlot(string id)
    {
        Slot slot = FindSlot(id);
        if (slot != null)
            return slot;

        if (_slots == null)
            _slots = new List<Slot>();

        slot = new Slot { Id = id };
        _slots.Add(slot);
        return slot;
    }
}
