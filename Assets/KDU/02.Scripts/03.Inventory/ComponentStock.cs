using System;
using UnityEngine;

// 등급별 부품 스택. 개별 ID 없이 int 배열 하나로 충분하다
[Serializable]
public class ComponentStock : IComponentSink
{
    [SerializeField] private int[] _counts = new int[PartGrades.Count];

    public int Get(PartGrade grade)
    {
        if (!PartGrades.IsValid(grade))
            return 0;

        EnsureSize();
        return _counts[(int)grade];
    }

    public void Add(PartGrade grade, int amount)
    {
        if (!PartGrades.IsValid(grade) || amount <= 0)
            return;

        EnsureSize();
        _counts[(int)grade] += amount;
    }

    public bool TryConsume(PartGrade grade, int amount)
    {
        if (!PartGrades.IsValid(grade) || amount <= 0)
            return false;

        EnsureSize();
        int index = (int)grade;
        if (_counts[index] < amount)
            return false;

        _counts[index] -= amount;
        return true;
    }

    public void Clear()
    {
        EnsureSize();
        Array.Clear(_counts, 0, _counts.Length);
    }

    // 세이브가 예전 등급 개수로 저장돼 있어도 맞춘다
    private void EnsureSize()
    {
        if (_counts == null)
        {
            _counts = new int[PartGrades.Count];
            return;
        }

        if (_counts.Length != PartGrades.Count)
            Array.Resize(ref _counts, PartGrades.Count);
    }
}
