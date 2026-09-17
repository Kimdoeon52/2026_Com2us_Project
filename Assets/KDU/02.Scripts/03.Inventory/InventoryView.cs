using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인벤토리 렌더. 입력은 다루지 않는다
public class InventoryView : MonoBehaviour
{
    [Tooltip("데이터 보유")]
    [SerializeField] private InventoryTestHost _host;

    [Header("루트")]
    [SerializeField] private RectTransform _gridRoot;
    [SerializeField] private RectTransform _partsLayer;
    [SerializeField] private RectTransform _componentLayer;

    [Header("프리팹")]
    [SerializeField] private GameObject _cellPrefab;
    [SerializeField] private GameObject _partsPrefab;
    [SerializeField] private GameObject _componentPrefab;

    [Header("표시")]
    [Tooltip("셀 크기. GridLayoutGroup과 같아야 한다")]
    [SerializeField] private float _cellSize = 56f;

    [Tooltip("등급 색")]
    [SerializeField] private Color[] _gradeColors = new Color[PartGrades.Count];

    public float CellSize => _cellSize;
    public RectTransform PartsLayer => _partsLayer;

    private void Start()
    {
        if (_host == null || _host.Grid == null)
        {
            Debug.LogWarning("Host가 비었다.");
            return;
        }

        BuildGrid();
        _host.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_host != null)
            _host.Changed -= Refresh;
    }

    public void Refresh()
    {
        var entries = _host.Grid.Entries;
        int partsCount = 0;
        int componentCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsComponent)
            {
                DrawComponent(entries[i], componentCount);
                componentCount++;
            }
            else
            {
                DrawParts(entries[i], partsCount);
                partsCount++;
            }
        }

        DeactivateFrom(_partsLayer, partsCount);
        DeactivateFrom(_componentLayer, componentCount);
    }

    // 배경 칸. 한 번만 생성한다
    private void BuildGrid()
    {
        int total = _host.Grid.Width * _host.Grid.Height;
        for (int i = 0; i < total; i++)
        {
            _gridRoot.GetOrCreateChild(_cellPrefab, i);
        }
    }

    private void DrawParts(InventoryGrid.Entry entry, int poolIndex)
    {
        RectTransform rect = Prepare(_partsLayer, _partsPrefab, poolIndex, entry);

        var label = rect.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = entry.Definition != null ? entry.Definition.DisplayName : string.Empty;
    }

    private void DrawComponent(InventoryGrid.Entry entry, int poolIndex)
    {
        RectTransform rect = Prepare(_componentLayer, _componentPrefab, poolIndex, entry);

        var image = rect.GetComponent<Image>();
        if (image != null)
            image.color = GetGradeColor(entry.Grade);

        var label = rect.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = _host.Save.Components.Get(entry.Grade).ToString();
    }

    // 파괴하지 않고 재사용한다. 드래그 중 파괴되면 포인터 이벤트가 끊긴다
    private RectTransform Prepare(RectTransform layer, GameObject prefab, int poolIndex, InventoryGrid.Entry entry)
    {
        var rect = (RectTransform)layer.GetOrCreateChild(prefab, poolIndex);
        rect.anchoredPosition = CellToAnchored(entry.Origin);
        rect.sizeDelta = (Vector2)entry.Size * _cellSize;
        rect.gameObject.SetActive(true);
        return rect;
    }

    private void DeactivateFrom(RectTransform layer, int startIndex)
    {
        for (int i = startIndex; i < layer.childCount; i++)
        {
            layer.GetChild(i).gameObject.SetActive(false);
        }
    }

    public Vector2 CellToAnchored(Vector2Int origin)
    {
        return new Vector2(origin.x * _cellSize, origin.y * _cellSize);
    }

    private Color GetGradeColor(PartGrade grade)
    {
        int index = (int)grade;
        if (_gradeColors == null || index < 0 || index >= _gradeColors.Length)
            return Color.white;

        return _gradeColors[index];
    }
}
