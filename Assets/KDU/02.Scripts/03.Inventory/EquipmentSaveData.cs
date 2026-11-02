using System;
using UnityEngine;

// 부위별 장착 파츠 ID. 세이브 대상
[Serializable]
public class EquipmentSaveData
{
    public static readonly int SlotCount = Enum.GetValues(typeof(Slot)).Length;

    [SerializeField] private string[] _equipped = new string[SlotCount];
    [SerializeField] private int[] _lostDurability = new int[SlotCount];   // 부위별 잃은 내구도. 0이면 최대치

    // 비었으면 null
    public string Get(Slot slot)
    {
        EnsureSize();
        string id = _equipped[(int)slot];
        return string.IsNullOrEmpty(id) ? null : id;
    }

    public void Set(Slot slot, string id)
    {
        EnsureSize();
        _equipped[(int)slot] = id;
        _lostDurability[(int)slot] = 0;
    }

    public int GetLostDurability(Slot slot)
    {
        EnsureSize();
        return _lostDurability[(int)slot];
    }

    public void SetLostDurability(Slot slot, int lost)
    {
        EnsureSize();
        _lostDurability[(int)slot] = Mathf.Max(0, lost);
    }

    public void Clear()
    {
        _equipped = new string[SlotCount];
        _lostDurability = new int[SlotCount];
    }

    // 부위가 늘어난 뒤 옛 세이브를 읽어도 안전하게
    private void EnsureSize()
    {
        if (_equipped == null)
            _equipped = new string[SlotCount];
        else if (_equipped.Length < SlotCount)
            Array.Resize(ref _equipped, SlotCount);

        if (_lostDurability == null)
            _lostDurability = new int[SlotCount];
        else if (_lostDurability.Length < SlotCount)
            Array.Resize(ref _lostDurability, SlotCount);
    }
}
