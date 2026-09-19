using System;
using UnityEngine;

// 파츠가 점유하는 칸 모양. 외곽 크기 + 칸 마스크로 ㄱ·ㄴ 같은 꺾인 모양을 표현한다
[Serializable]
public class PartsShape
{
    [Tooltip("외곽 크기")]
    [SerializeField] private Vector2Int _size = Vector2Int.one;

    [Tooltip("점유 칸")]
    [SerializeField] private bool[] _cells = { true };

    // 외곽 사각형. 배치 판정은 Contains로 한다
    public Vector2Int Size => _size;

    public PartsShape() { }

    public PartsShape(Vector2Int size)
    {
        _size = size;
        _cells = null;
        EnsureSize();
    }

    // 모양 기준 로컬 좌표가 점유 칸인지
    public bool Contains(int x, int y)
    {
        if (x < 0 || y < 0 || x >= _size.x || y >= _size.y)
            return false;

        EnsureSize();
        return _cells[y * _size.x + x];
    }

    public int CellCount()
    {
        EnsureSize();

        int count = 0;
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i])
                count++;
        }

        return count;
    }

    // 전부 비면 배치할 수 없으므로 한 칸은 남긴다
    public void Validate()
    {
        EnsureSize();

        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i])
                return;
        }

        _cells[0] = true;
    }

    // 길이가 어긋나면 꽉 찬 사각형으로 되돌린다
    private void EnsureSize()
    {
        _size.x = Mathf.Max(1, _size.x);
        _size.y = Mathf.Max(1, _size.y);

        int count = _size.x * _size.y;
        if (_cells != null && _cells.Length == count)
            return;

        _cells = new bool[count];
        for (int i = 0; i < count; i++)
        {
            _cells[i] = true;
        }
    }
}
