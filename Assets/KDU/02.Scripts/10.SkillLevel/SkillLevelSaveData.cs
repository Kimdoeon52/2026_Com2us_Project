using System;
using UnityEngine;

// 부위별 스킬 ID와 레벨. 2레벨 이상만 기록한다. 세이브 대상
[Serializable]
public class SkillLevelSaveData
{
    private static readonly int SlotCount = Enum.GetValues(typeof(Slot)).Length;

    [SerializeField] private string[] _skillIds = new string[SlotCount];
    [SerializeField] private int[] _levels = new int[SlotCount];

    // 비었으면 null
    public string GetSkillId(Slot slot)
    {
        EnsureSize();
        string id = _skillIds[(int)slot];
        return string.IsNullOrEmpty(id) ? null : id;
    }

    public int GetLevel(Slot slot)
    {
        EnsureSize();
        return Mathf.Max(1, _levels[(int)slot]);
    }

    public void Set(Slot slot, string skillId, int level)
    {
        EnsureSize();
        _skillIds[(int)slot] = skillId;
        _levels[(int)slot] = level;
    }

    public void Clear(Slot slot)
    {
        Set(slot, null, 0);
    }

    // 부위가 늘어난 뒤 옛 세이브를 읽어도 안전하게
    private void EnsureSize()
    {
        if (_skillIds == null)
            _skillIds = new string[SlotCount];
        else if (_skillIds.Length < SlotCount)
            Array.Resize(ref _skillIds, SlotCount);

        if (_levels == null)
            _levels = new int[SlotCount];
        else if (_levels.Length < SlotCount)
            Array.Resize(ref _levels, SlotCount);
    }
}
