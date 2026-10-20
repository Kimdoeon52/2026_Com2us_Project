using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장착 부위 칸 1개. 인벤토리 드롭 대상이고, 드래그나 우클릭으로 해제한다
public class EquipmentSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("부위")]
    [SerializeField] private Slot _slot;

    [Tooltip("장착 파츠 이미지")]
    [SerializeField] private Image _icon;

    [Tooltip("해제 드래그를 처리할 인벤토리 드래그")]
    [SerializeField] private InventoryDragController _drag;

    [Tooltip("툴팁")]
    [SerializeField] private InventoryTooltip _tooltip;

    private Sprite _emptySprite;
    private Color _emptyColor;

    public Slot Slot => _slot;
    public Image Icon => _icon;

    // 컴포넌트를 붙일 때 씬에서 찾아 채운다
    private void Reset()
    {
        if (_drag == null)
            _drag = FindAnyObjectByType<InventoryDragController>();

        if (_tooltip == null)
            _tooltip = FindAnyObjectByType<InventoryTooltip>();
    }

    private void Awake()
    {
        // 이미 배치된 칸은 Reset이 안 돌아서 비어 있을 수 있다
        if (_tooltip == null)
            _tooltip = FindAnyObjectByType<InventoryTooltip>(FindObjectsInactive.Include);

        if (_icon == null)
            return;

        _emptySprite = _icon.sprite;
        _emptyColor = _icon.color;
        _icon.preserveAspect = true;
    }

    // 코어 칸은 코어를, 나머지는 장착 파츠를 그린다
    public void Show(InventoryHost host)
    {
        if (_icon == null)
            return;

        Sprite sprite;
        bool filled;

        if (_slot == Slot.Core)
        {
            CoreDefinition core = host != null ? host.Core : null;
            filled = core != null;
            sprite = filled ? core.Sprite : null;
        }
        else
        {
            PartsDefinition parts = host != null ? host.GetEquipped(_slot) : null;
            filled = parts != null;
            sprite = filled ? parts.Sprite : null;
        }

        // 이미지가 아직 없으면 빈 칸 이미지를 흰색으로 그려 찬 칸임을 보인다
        _icon.sprite = sprite != null ? sprite : _emptySprite;
        _icon.color = filled ? Color.white : _emptyColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || InventoryHost.Instance == null)
            return;

        InventoryHost.Instance.Unequip(_slot);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || eventData.dragging || InventoryHost.Instance == null)
            return;

        InventoryHost host = InventoryHost.Instance;
        if (_slot == Slot.Core)
        {
            if (host.Core != null)
                _tooltip.Show(host.Core.GetTooltip(host.CoreLevel));
            return;
        }

        PartsDefinition parts = host.GetEquipped(_slot);
        if (parts != null)
            _tooltip.Show(parts.GetTooltip());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.Hide();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.Hide();

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
