using System;
using UnityEngine;

// 등급별 부품 스택. 개별 ID 없이 등급당 1칸, 수량 무제한
[Serializable]
public class ComponentStock : IComponentSink
{
    [SerializeField] private int[] _counts = new int[PartGrades.Count];
    [SerializeField] private Vector2Int[] _origins = CreateOrigins();

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

    // 배치된 칸 좌표. x가 음수면 미배치
    public bool TryGetOrigin(PartGrade grade, out Vector2Int origin)
    {
        origin = Vector2Int.zero;

        if (!PartGrades.IsValid(grade))
            return false;

        EnsureSize();
        origin = _origins[(int)grade];
        return origin.x >= 0 && origin.y >= 0;
    }

    public void SetOrigin(PartGrade grade, Vector2Int origin)
    {
        if (!PartGrades.IsValid(grade))
            return;

        EnsureSize();
        _origins[(int)grade] = origin;
    }

    public void ClearOrigin(PartGrade grade)
    {
        SetOrigin(grade, NoOrigin);
    }

    public void Clear()
    {
        EnsureSize();
        Array.Clear(_counts, 0, _counts.Length);
        for (int i = 0; i < _origins.Length; i++)
        {
            _origins[i] = NoOrigin;
        }
    }

    private static Vector2Int NoOrigin => new Vector2Int(-1, -1);

    private static Vector2Int[] CreateOrigins()
    {
        var origins = new Vector2Int[PartGrades.Count];
        for (int i = 0; i < origins.Length; i++)
        {
            origins[i] = NoOrigin;
        }

        return origins;
    }

    // 세이브가 예전 등급 개수로 저장돼 있어도 맞춘다
    private void EnsureSize()
    {
        if (_counts == null)
            _counts = new int[PartGrades.Count];
        else if (_counts.Length != PartGrades.Count)
            Array.Resize(ref _counts, PartGrades.Count);

        if (_origins == null)
        {
            _origins = CreateOrigins();
            return;
        }

        if (_origins.Length == PartGrades.Count)
            return;

        int previous = _origins.Length;
        Array.Resize(ref _origins, PartGrades.Count);
        for (int i = previous; i < _origins.Length; i++)
        {
            _origins[i] = NoOrigin;
        }
    }
}
