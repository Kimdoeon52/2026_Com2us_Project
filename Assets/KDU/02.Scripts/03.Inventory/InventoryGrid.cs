using System.Collections.Generic;
using UnityEngine;

// 파츠 배치 그리드
public class InventoryGrid
{
    public struct Placement
    {
        public PartsInstance Instance;
        public PartsDefinition Definition;
    }

    private const int Empty = -1;

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _cells;
    private readonly List<Placement> _placements = new List<Placement>();

    public int Width => _width;
    public int Height => _height;
    public int Count => _placements.Count;
    public IReadOnlyList<Placement> Placements => _placements;

    public InventoryGrid(InventoryDefinition definition)
    {
        Vector2Int size = definition != null ? definition.GridSize : Vector2Int.one;
        _width = Mathf.Max(1, size.x);
        _height = Mathf.Max(1, size.y);
        _cells = new int[_width * _height];
        ClearCells();
    }

    // ignoreIndex는 자기 자신을 옮길 때 제외할 배치
    public bool CanPlace(Vector2Int size, Vector2Int origin, int ignoreIndex = Empty)
    {
        if (size.x <= 0 || size.y <= 0)
            return false;

        if (origin.x < 0 || origin.y < 0)
            return false;

        if (origin.x + size.x > _width || origin.y + size.y > _height)
            return false;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int occupant = _cells[CellIndex(origin.x + x, origin.y + y)];
                if (occupant != Empty && occupant != ignoreIndex)
                    return false;
            }
        }

        return true;
    }

    // 칸을 점유한 배치 인덱스. 비었으면 -1
    public int GetIndexAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
            return Empty;

        return _cells[CellIndex(cell.x, cell.y)];
    }

    public bool TryGetPlacement(int index, out Placement placement)
    {
        placement = default;

        if (index < 0 || index >= _placements.Count)
            return false;

        placement = _placements[index];
        return true;
    }

    public bool TryPlace(PartsDefinition definition, Vector2Int origin, out int index)
    {
        index = Empty;

        if (definition == null || !CanPlace(definition.Size, origin))
            return false;

        var placement = new Placement
        {
            Instance = new PartsInstance(definition.Id, origin),
            Definition = definition,
        };

        _placements.Add(placement);
        index = _placements.Count - 1;
        Fill(definition.Size, origin, index);
        return true;
    }

    // 좌하단부터 빈자리를 찾아 배치한다
    public bool TryAutoPlace(PartsDefinition definition, out int index)
    {
        index = Empty;

        if (definition == null)
            return false;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (TryPlace(definition, new Vector2Int(x, y), out index))
                    return true;
            }
        }

        return false;
    }

    public bool TryMove(int index, Vector2Int origin)
    {
        if (index < 0 || index >= _placements.Count)
            return false;

        var placement = _placements[index];
        if (!CanPlace(placement.Definition.Size, origin, index))
            return false;

        Fill(placement.Definition.Size, placement.Instance.Origin, Empty);
        placement.Instance.Origin = origin;
        Fill(placement.Definition.Size, origin, index);
        return true;
    }

    public bool Remove(int index)
    {
        if (index < 0 || index >= _placements.Count)
            return false;

        _placements.RemoveAt(index);
        RebuildCells();
        return true;
    }

    public void Clear()
    {
        _placements.Clear();
        ClearCells();
    }

    // 세이브에서 복원. 배치할 수 없는 항목은 버린다
    public void Load(List<PartsInstance> instances, PartsCatalog catalog)
    {
        Clear();

        if (instances == null || catalog == null)
            return;

        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            if (instance == null)
                continue;

            var definition = catalog.Find(instance.DefinitionId);
            if (definition == null || !CanPlace(definition.Size, instance.Origin))
                continue;

            var placement = new Placement { Instance = instance, Definition = definition };
            _placements.Add(placement);
            Fill(definition.Size, instance.Origin, _placements.Count - 1);
        }
    }

    public List<PartsInstance> ToSaveList()
    {
        var list = new List<PartsInstance>(_placements.Count);
        for (int i = 0; i < _placements.Count; i++)
        {
            list.Add(_placements[i].Instance);
        }

        return list;
    }

    private void Fill(Vector2Int size, Vector2Int origin, int value)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                _cells[CellIndex(origin.x + x, origin.y + y)] = value;
            }
        }
    }

    private void RebuildCells()
    {
        ClearCells();
        for (int i = 0; i < _placements.Count; i++)
        {
            var placement = _placements[i];
            Fill(placement.Definition.Size, placement.Instance.Origin, i);
        }
    }

    private void ClearCells()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = Empty;
        }
    }

    private int CellIndex(int x, int y)
    {
        return y * _width + x;
    }
}
