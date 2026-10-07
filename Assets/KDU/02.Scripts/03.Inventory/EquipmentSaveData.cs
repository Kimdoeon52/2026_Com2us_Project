using System;
using UnityEngine;

// 부위별 장착 파츠 ID. 세이브 대상
[Serializable]
public class EquipmentSaveData
{
    public static readonly int SlotCount = Enum.GetValues(typeof(Slot)).Length;

    [SerializeField] private string[] _equipped = new string[SlotCount];

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
    }

    public void Clear()
    {
        _equipped = new string[SlotCount];
    }

    // 부위가 늘어난 뒤 옛 세이브를 읽어도 안전하게
    private void EnsureSize()
    {
        if (_equipped == null)
            _equipped = new string[SlotCount];
        else if (_equipped.Length < SlotCount)
            Array.Resize(ref _equipped, SlotCount);
    }
}
