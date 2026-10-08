using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리·장착 칸 툴팁. 인벤토리 패널 루트에 두고 포인터 아래 칸의 엔트리를 보여준다
public class InventoryTooltip : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    [Tooltip("데이터 보유")]
    [SerializeField] private InventoryHost _host;

    [Tooltip("좌표 변환")]
    [SerializeField] private InventoryView _view;

    [Tooltip("툴팁 박스")]
    [SerializeField] private GameObject _box;

    [Tooltip("툴팁 텍스트")]
    [SerializeField] private TextMeshProUGUI _text;

    // 격자에서 띄운 툴팁인지. 장착 칸이 띄운 건 격자 밖 이동으로 끄지 않는다
    private bool _fromGrid;

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_host == null)
            _host = FindAnyObjectByType<InventoryHost>();

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

        Show(GetEntryText(_host.Grid.GetIndexAt(cell)));
        _fromGrid = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_fromGrid)
            Hide();
    }

    private string GetEntryText(int index)
    {
        if (!_host.Grid.TryGetEntry(index, out InventoryGrid.Entry entry))
            return null;

        if (entry.IsComponent)
            return entry.Component != null ? entry.Component.GetTooltip() : null;

        return entry.Definition != null ? entry.Definition.GetTooltip() : null;
    }

    // 격자 밖이면 false
    private bool TryGetCell(PointerEventData eventData, out Vector2Int cell)
    {
        cell = default;

        if (_host == null || _host.Grid == null || _view == null || _view.PartsLayer == null)
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.PartsLayer, eventData.position, eventData.enterEventCamera, out Vector2 local))
            return false;

        float size = Mathf.Max(1f, _view.CellSize);
        cell = new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.y / size));
        return cell.x >= 0 && cell.y >= 0 && cell.x < _host.Grid.Width && cell.y < _host.Grid.Height;
    }
}
