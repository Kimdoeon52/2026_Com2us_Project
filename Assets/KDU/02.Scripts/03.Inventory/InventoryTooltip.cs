using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리·장착 칸 툴팁. 인벤토리 패널 루트에 두고 포인터 아래 칸의 엔트리를 보여준다
public class InventoryTooltip : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    // 씬 전환 유지 싱글톤이라 인스펙터로 연결하지 않는다
    private static InventoryHost Host => InventoryHost.Instance;

    [Tooltip("좌표 변환")]
    [SerializeField] private InventoryView _view;

    [Tooltip("툴팁 박스")]
    [SerializeField] private GameObject _box;

    [Tooltip("툴팁 텍스트")]
    [SerializeField] private TextMeshProUGUI _text;

    [Tooltip("툴팁 정렬 순서. 다른 캔버스 팝업보다 높게")]
    [SerializeField] private int _sortingOrder = 100;

    // 격자에서 띄운 툴팁인지. 장착 칸이 띄운 건 격자 밖 이동으로 끄지 않는다
    private bool _fromGrid;

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_view == null)
            _view = FindAnyObjectByType<InventoryView>();
    }

    private void OnDisable()
    {
        Hide();
    }

    public void Show(string text)
    {
        if (_box == null || string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        if (_text != null)
            _text.text = text;

        _box.SetActive(true);
        _fromGrid = false;

        // 장착 팝업 위에 그린다. 켠 뒤에 건다
        Canvas canvas = _box.GetOrAddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = _sortingOrder;
    }

    public void Hide()
    {
        if (_text != null)
            _text.text = string.Empty;

        if (_box != null)
            _box.SetActive(false);

        _fromGrid = false;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData.dragging)
        {
            Hide();
            return;
        }

        if (!TryGetCell(eventData, out Vector2Int cell))
        {
            if (_fromGrid)
                Hide();
            return;
        }

        Show(GetEntryText(Host.Grid.GetIndexAt(cell)));
        _fromGrid = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_fromGrid)
            Hide();
    }

    private string GetEntryText(int index)
    {
        if (!Host.Grid.TryGetEntry(index, out InventoryGrid.Entry entry))
            return null;

        if (entry.IsComponent)
            return entry.Component != null ? entry.Component.GetTooltip() : null;

        return entry.Definition != null ? entry.Definition.GetTooltip() : null;
    }

    // 격자 밖이면 false
    private bool TryGetCell(PointerEventData eventData, out Vector2Int cell)
    {
        cell = default;

        if (Host == null || Host.Grid == null || _view == null || _view.PartsLayer == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.PartsLayer, eventData.position, eventData.enterEventCamera, out Vector2 local))
            return false;

        float size = Mathf.Max(1f, _view.CellSize);
        cell = new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.y / size));
        return cell.x >= 0 && cell.y >= 0 && cell.x < Host.Grid.Width && cell.y < Host.Grid.Height;
    }
}
