using System.Collections.Generic;
using UnityEngine;

// 파츠와 부품이 같이 올라가는 배치 그리드. 드래그·UI는 다루지 않는다
public class InventoryGrid
{
    // 부품 칸은 1x1 고정, 파츠는 정의 모양
    public struct Entry
    {
        public bool IsComponent;
        public PartGrade Grade;
        public PartsInstance Parts;
        public PartsDefinition Definition;
        public PartsShape Shape;
        public Vector2Int Origin;
        public Vector2Int Size;
    }

    private const int Empty = -1;

    private static readonly PartsShape SingleCell = new PartsShape(Vector2Int.one);

    private readonly int _width;
    private readonly int _height;
    private readonly int[] _cells;
    private readonly List<Entry> _entries = new List<Entry>();
    private readonly ComponentStock _stock;

    public int Width => _width;
    public int Height => _height;
    public int Count => _entries.Count;
    public IReadOnlyList<Entry> Entries => _entries;

    public InventoryGrid(InventoryDefinition definition, ComponentStock stock)
    {
        Vector2Int size = definition != null ? definition.GridSize : Vector2Int.one;
        _width = Mathf.Max(1, size.x);
        _height = Mathf.Max(1, size.y);
        _cells = new int[_width * _height];
        _stock = stock;
        ClearCells();
    }

    // ignoreIndex는 자기 자신을 옮길 때 제외할 엔트리
    public bool CanPlace(PartsShape shape, Vector2Int origin, int ignoreIndex = Empty)
    {
        if (shape == null)
            return false;

        Vector2Int size = shape.Size;

        // 빈 구석은 그리드 밖이어도 되므로 점유 칸만 본다
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (!shape.Contains(x, y))
                    continue;

                int cellX = origin.x + x;
                int cellY = origin.y + y;

                if (cellX < 0 || cellY < 0 || cellX >= _width || cellY >= _height)
                    return false;

                int occupant = _cells[CellIndex(cellX, cellY)];
                if (occupant != Empty && occupant != ignoreIndex)
                    return false;
            }
        }

        return true;
    }

    // 칸을 점유한 엔트리 인덱스. 비었으면 -1
    public int GetIndexAt(Vector2Int cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
            return Empty;

        return _cells[CellIndex(cell.x, cell.y)];
    }

    public bool TryGetEntry(int index, out Entry entry)
    {
        entry = default;

        if (index < 0 || index >= _entries.Count)
            return false;

        entry = _entries[index];
        return true;
    }

    public bool TryPlaceParts(PartsDefinition definition, Vector2Int origin, out int index)
    {
        index = Empty;

        if (definition == null || !CanPlace(definition.Shape, origin))
            return false;

        var entry = new Entry
        {
            IsComponent = false,
            Parts = new PartsInstance(definition.Id, origin),
            Definition = definition,
            Shape = definition.Shape,
            Origin = origin,
            Size = definition.Size,
        };

        index = Add(entry);
        return true;
    }

    // 좌하단부터 빈자리를 찾아 배치한다
    public bool TryAutoPlaceParts(PartsDefinition definition, out int index)
    {
        index = Empty;

        if (definition == null)
            return false;

        if (!TryFindFreeOrigin(definition.Shape, out Vector2Int origin))
            return false;

        return TryPlaceParts(definition, origin, out index);
    }

    public bool TryMove(int index, Vector2Int origin)
    {
        if (index < 0 || index >= _entries.Count)
            return false;

        Entry entry = _entries[index];
        if (!CanPlace(entry.Shape, origin, index))
            return false;

        Fill(entry.Shape, entry.Origin, Empty);
        entry.Origin = origin;

        if (entry.IsComponent && _stock != null)
            _stock.SetOrigin(entry.Grade, origin);
        else if (!entry.IsComponent && entry.Parts != null)
            entry.Parts.Origin = origin;

        _entries[index] = entry;
        Fill(entry.Shape, origin, index);
        return true;
    }

    // 부품 칸은 수량이 0이 될 때만 사라진다
    public bool Remove(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return false;

        if (_entries[index].IsComponent)
            return false;

        _entries.RemoveAt(index);
        RebuildCells();
        return true;
    }

    // 스택 수량과 칸을 맞춘다. 수량이 바뀐 뒤 호출한다
    public void SyncComponents()
    {
        if (_stock == null)
            return;

        RemoveEmptyComponents();

        for (int i = 0; i < PartGrades.Count; i++)
        {
            var grade = (PartGrade)i;
            if (_stock.Get(grade) <= 0)
                continue;

            if (IndexOfComponent(grade) >= 0)
                continue;

            PlaceComponent(grade);
        }
    }

    public void Clear()
    {
        _entries.Clear();
        ClearCells();
    }

    // 세이브에서 복원. 배치할 수 없는 항목은 버린다
    public void Load(List<PartsInstance> instances, PartsCatalog catalog)
    {
        Clear();

        if (instances != null && catalog != null)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                PartsInstance instance = instances[i];
                if (instance == null)
                    continue;

                PartsDefinition definition = catalog.Find(instance.DefinitionId);
                if (definition == null || !CanPlace(definition.Shape, instance.Origin))
                    continue;

                Add(new Entry
                {
                    IsComponent = false,
                    Parts = instance,
                    Definition = definition,
                    Shape = definition.Shape,
                    Origin = instance.Origin,
                    Size = definition.Size,
                });
            }
        }

        SyncComponents();
    }

    public List<PartsInstance> ToSaveList()
    {
        var list = new List<PartsInstance>(_entries.Count);
        for (int i = 0; i < _entries.Count; i++)
        {
            if (!_entries[i].IsComponent && _entries[i].Parts != null)
                list.Add(_entries[i].Parts);
        }

        return list;
    }

    // 저장된 좌표를 먼저 쓰고, 막혀 있으면 빈자리를 찾는다
    private void PlaceComponent(PartGrade grade)
    {
        Vector2Int origin;
        bool hasSaved = _stock.TryGetOrigin(grade, out origin) && CanPlace(SingleCell, origin);

        if (!hasSaved && !TryFindFreeOrigin(SingleCell, out origin))
            return;

        Add(new Entry
        {
            IsComponent = true,
            Grade = grade,
            Shape = SingleCell,
            Origin = origin,
            Size = Vector2Int.one,
        });

        _stock.SetOrigin(grade, origin);
    }

    private void RemoveEmptyComponents()
    {
        bool removed = false;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            if (!entry.IsComponent || _stock.Get(entry.Grade) > 0)
                continue;

            _stock.ClearOrigin(entry.Grade);
            _entries.RemoveAt(i);
            removed = true;
        }

        if (removed)
            RebuildCells();
    }

    private int IndexOfComponent(PartGrade grade)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].IsComponent && _entries[i].Grade == grade)
                return i;
        }

        return Empty;
    }

    private bool TryFindFreeOrigin(PartsShape shape, out Vector2Int origin)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                origin = new Vector2Int(x, y);
                if (CanPlace(shape, origin))
                    return true;
            }
        }

        origin = Vector2Int.zero;
        return false;
    }

    private int Add(Entry entry)
    {
        _entries.Add(entry);
        int index = _entries.Count - 1;
        Fill(entry.Shape, entry.Origin, index);
        return index;
    }

    private void Fill(PartsShape shape, Vector2Int origin, int value)
    {
        if (shape == null)
            return;

        for (int y = 0; y < shape.Size.y; y++)
        {
            for (int x = 0; x < shape.Size.x; x++)
            {
                if (!shape.Contains(x, y))
                    continue;

                _cells[CellIndex(origin.x + x, origin.y + y)] = value;
            }
        }
    }

    private void RebuildCells()
    {
        ClearCells();
        for (int i = 0; i < _entries.Count; i++)
        {
            Fill(_entries[i].Shape, _entries[i].Origin, i);
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
