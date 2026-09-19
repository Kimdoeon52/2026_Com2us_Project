using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 드래그 입력. 패널 루트에 두고 자식에서 올라온 드래그를 받는다
public class InventoryDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const int None = -1;

    [Tooltip("데이터 보유")]
    [SerializeField] private InventoryTestHost _host;

    [Tooltip("좌표 변환·렌더")]
    [SerializeField] private InventoryView _view;

    [Header("고스트")]
    [SerializeField] private GameObject _ghostPrefab;

    [Tooltip("고스트를 담을 루트. 비면 파츠 레이어의 부모")]
    [SerializeField] private RectTransform _ghostLayer;

    [Header("표시")]
    [Tooltip("배치 가능 색")]
    [SerializeField] private Color _validColor = new Color(0.3f, 1f, 0.4f, 0.5f);

    [Tooltip("배치 불가 색")]
    [SerializeField] private Color _invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    [Tooltip("드래그 중 원본 투명도")]
    [Range(0f, 1f)]
    [SerializeField] private float _sourceAlpha = 0.35f;

    private RectTransform _ghost;
    private Image _ghostImage;
    private ShapeRenderer _ghostShape;
    private CanvasGroup _source;
    private int _index = None;
    private PartsShape _shape;
    private Vector2Int _grabOffset;

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_host == null)
            _host = FindAnyObjectByType<InventoryTestHost>();

        if (_view == null)
            _view = FindAnyObjectByType<InventoryView>();
    }

    private void OnEnable()
    {
        if (GetComponent<RectTransform>() == null)
            Debug.LogWarning("UI 오브젝트에 붙여야 드래그가 들어온다.");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _index = None;

        if (!IsReady() || !TryGetLocal(eventData, out Vector2 local))
            return;

        Vector2Int cell = ToCell(local);
        int index = _host.Grid.GetIndexAt(cell);

        if (index < 0 || !_host.Grid.TryGetEntry(index, out InventoryGrid.Entry entry))
            return;

        _index = index;
        _shape = entry.Shape;
        _grabOffset = cell - entry.Origin;

        FadeSource(index);
        ShowGhost(local);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_index == None || !TryGetLocal(eventData, out Vector2 local))
            return;

        MoveGhost(local);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_index == None)
            return;

        if (TryGetLocal(eventData, out Vector2 local))
            _host.MoveEntry(_index, ToOrigin(local));

        RestoreSource();
        HideGhost();
        _index = None;
    }

    private bool IsReady()
    {
        return _host != null && _host.Grid != null && _view != null && _view.PartsLayer != null;
    }

    // 파츠 레이어 기준 좌표. 레이어 피벗은 좌하단
    private bool TryGetLocal(PointerEventData eventData, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_view.PartsLayer, eventData.position, eventData.pressEventCamera, out local);
    }

    private Vector2Int ToCell(Vector2 local)
    {
        float size = Mathf.Max(1f, _view.CellSize);
        return new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.y / size));
    }

    // 잡은 지점 보정을 뺀 배치 원점
    private Vector2Int ToOrigin(Vector2 local)
    {
        return ToCell(local) - _grabOffset;
    }

    private void ShowGhost(Vector2 local)
    {
        EnsureGhost();

        if (_ghost == null)
            return;

        _ghost.sizeDelta = (Vector2)_shape.Size * _view.CellSize;
        _ghost.gameObject.SetActive(true);
        _ghost.SetAsLastSibling();

        if (_ghostShape != null)
            _ghostShape.Draw(_shape, null, _view.CellSize, _validColor);

        MoveGhost(local);
    }

    // 포인터를 따라가지 않고 배치될 칸에 스냅한다
    private void MoveGhost(Vector2 local)
    {
        if (_ghost == null)
            return;

        Vector2Int origin = ToOrigin(local);
        _ghost.anchoredPosition = _view.CellToAnchored(origin);

        Color color = _host.Grid.CanPlace(_shape, origin, _index) ? _validColor : _invalidColor;

        if (_ghostShape != null)
            _ghostShape.Tint(color);
        else if (_ghostImage != null)
            _ghostImage.color = color;
    }

    private void HideGhost()
    {
        if (_ghost != null)
            _ghost.gameObject.SetActive(false);
    }

    private void EnsureGhost()
    {
        if (_ghost != null || _ghostPrefab == null)
            return;

        RectTransform layer = _ghostLayer != null ? _ghostLayer : (RectTransform)_view.PartsLayer.parent;
        _ghost = (RectTransform)Instantiate(_ghostPrefab, layer).transform;
        _ghostImage = _ghost.GetComponent<Image>();
        _ghostShape = _ghost.GetComponent<ShapeRenderer>();

        if (_ghostImage != null)
            _ghostImage.raycastTarget = false;
    }

    // 드래그 중 원본은 반투명으로 남는다
    private void FadeSource(int index)
    {
        RectTransform rect = _view.GetEntryRect(index);

        if (rect == null)
            return;

        _source = rect.GetOrAddComponent<CanvasGroup>();
        _source.alpha = _sourceAlpha;
    }

    private void RestoreSource()
    {
        if (_source == null)
            return;

        _source.alpha = 1f;
        _source = null;
    }
}
