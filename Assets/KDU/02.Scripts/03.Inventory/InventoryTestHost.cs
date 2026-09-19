using System;
using UnityEngine;

// 인벤토리 단독 테스트용 데이터 보유. UI는 여기서 Grid/Save를 읽어간다
public class InventoryTestHost : MonoBehaviour
{
    [Tooltip("그리드 규격")]
    [SerializeField] private InventoryDefinition _inventoryDefinition;

    [Tooltip("파츠 목록")]
    [SerializeField] private PartsCatalog _catalog;

    [Tooltip("난수 시드")]
    [SerializeField] private int _seed = 1;

    [Tooltip("시작 시 자동 배치할 파츠 수")]
    [Min(0)]
    [SerializeField] private int _startingParts;

    private Rng _rng;
    private InventoryGrid _grid;
    private InventorySaveData _save;
    private string _snapshot;

    public InventoryGrid Grid => _grid;
    public InventorySaveData Save => _save;
    public PartsCatalog Catalog => _catalog;

    // 데이터가 바뀌면 UI가 전체 리빌드한다
    public event Action Changed;

    private void Awake()
    {
        _rng = new Rng(_seed);
        _save = new InventorySaveData();
        _grid = new InventoryGrid(_inventoryDefinition, _save.Components);

        for (int i = 0; i < _startingParts; i++)
        {
            GiveRandomPart();
        }
    }

    // 지정 파츠를 빈자리에 배치
    public bool GivePart(PartsDefinition definition)
    {
        if (_grid == null || definition == null)
            return false;

        bool placed = _grid.TryAutoPlaceParts(definition, out int _);
        if (placed)
            Changed?.Invoke();

        return placed;
    }

    [ContextMenu("파츠 지급")]
    public bool GiveRandomPart()
    {
        if (_catalog == null || _catalog.Parts == null || _catalog.Parts.Length == 0)
            return false;

        return GivePart(_catalog.Parts[_rng.Range(0, _catalog.Parts.Length)]);
    }

    // 드래그 드롭으로 엔트리를 옮긴다
    public bool MoveEntry(int index, Vector2Int origin)
    {
        if (_grid == null || !_grid.TryMove(index, origin))
            return false;

        Changed?.Invoke();
        return true;
    }

    public void GiveComponent(PartGrade grade, int amount)
    {
        _save.Components.Add(grade, amount);
        _grid.SyncComponents();
        Changed?.Invoke();
    }

    [ContextMenu("부품 지급")]
    public void GiveSampleComponents()
    {
        _save.Components.Add(PartGrade.Common, 10);
        _save.Components.Add(PartGrade.Rare, 3);
        _save.Components.Add(PartGrade.Epic, 1);
        _grid.SyncComponents();
        Changed?.Invoke();
    }

    // 수량이 0이 되면 칸도 사라진다
    public bool ConsumeComponent(PartGrade grade, int amount)
    {
        bool consumed = _save.Components.TryConsume(grade, amount);
        if (!consumed)
            return false;

        _grid.SyncComponents();
        Changed?.Invoke();
        return true;
    }

    [ContextMenu("에픽 부품 1개 소모")]
    public void ConsumeSampleComponent()
    {
        ConsumeComponent(PartGrade.Epic, 1);
    }

    [ContextMenu("전체 비우기")]
    public void ClearAll()
    {
        _save.Components.Clear();
        _grid.Clear();
        Changed?.Invoke();
    }

    // 직렬화 왕복 테스트. 저장 → 비우기 → 복원 후 배치가 같아야 한다
    [ContextMenu("세이브 캡처")]
    public void CaptureSnapshot()
    {
        _save.CaptureFrom(_grid);
        _snapshot = JsonUtility.ToJson(_save, true);
        Debug.Log(_snapshot);
    }

    [ContextMenu("세이브 복원")]
    public void RestoreSnapshot()
    {
        if (string.IsNullOrEmpty(_snapshot))
        {
            Debug.LogWarning("캡처된 세이브가 없다.");
            return;
        }

        _save = JsonUtility.FromJson<InventorySaveData>(_snapshot);
        _grid = new InventoryGrid(_inventoryDefinition, _save.Components);
        _save.RestoreTo(_grid, _catalog);
        Changed?.Invoke();
    }

    // 현재 점유 상태를 문자 그리드로 출력
    [ContextMenu("그리드 출력")]
    public void LogGrid()
    {
        var text = new System.Text.StringBuilder();
        for (int y = _grid.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                int index = _grid.GetIndexAt(new Vector2Int(x, y));
                text.Append(index < 0 ? ". " : $"{index % 10} ");
            }

            text.AppendLine();
        }

        Debug.Log(text.ToString());
    }
}
