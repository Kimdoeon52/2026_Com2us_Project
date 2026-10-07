using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장착 부위 칸 1개. 인벤토리 드롭 대상이고, 드래그나 우클릭으로 해제한다
public class EquipmentSlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("부위")]
    [SerializeField] private Slot _slot;

    [Tooltip("장착 파츠 이미지")]
    [SerializeField] private Image _icon;

    [Tooltip("해제 드래그를 처리할 인벤토리 드래그")]
    [SerializeField] private InventoryDragController _drag;

    private Sprite _emptySprite;
    private Color _emptyColor;

    public Slot Slot => _slot;
    public Image Icon => _icon;

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_drag == null)
            _drag = FindAnyObjectByType<InventoryDragController>();
    }

    private void Awake()
    {
        if (_icon == null)
            return;

        _emptySprite = _icon.sprite;
        _emptyColor = _icon.color;
        _icon.preserveAspect = true;
    }

    // null이면 빈 칸 모양으로
    public void Show(PartsDefinition parts)
    {
        if (_icon == null)
            return;

        _icon.sprite = parts != null ? parts.Sprite : _emptySprite;
        _icon.color = parts != null ? Color.white : _emptyColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || InventoryHost.Instance == null)
            return;

        InventoryHost.Instance.Unequip(_slot);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_drag != null)
            _drag.BeginFromEquipment(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_drag != null)
            _drag.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_drag != null)
            _drag.OnEndDrag(eventData);
    }
}
