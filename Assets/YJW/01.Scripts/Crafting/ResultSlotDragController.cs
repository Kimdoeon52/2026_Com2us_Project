using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ResultSlotDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CorrectRecipe _recipe;
    [SerializeField] private ResultSlot _resultSlot;
    [SerializeField] private InventoryTestHost _host;
    [SerializeField] private InventoryView _view;

    [SerializeField] private GameObject _ghostPrefab;
    [SerializeField] private RectTransform _ghostLayer;

    private RectTransform _ghost;
    private Image _ghostImage;
    private CanvasGroup _source;
    private PartsDefinition _dragged;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragged = _recipe.resultPart;

        if (_dragged == null)
            return;

        _source = GetComponent<CanvasGroup>();
        if (_source == null)
            _source = gameObject.AddComponent<CanvasGroup>();
        _source.alpha = 0.35f;

        EnsureGhost();
        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragged == null)
            return;

        MoveGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragged != null && IsOverInventory(eventData) && _host.GivePart(_dragged))
        {
            _recipe.ConfirmCraft();
            _resultSlot.SetPart(null);
        }

        if (_source != null)
        {
            _source.alpha = 1f;
            _source = null;
        }

        HideGhost();
        _dragged = null;
    }

    // 인벤토리 패널(파츠/부품 레이어의 부모) 위에 놓였는지
    private bool IsOverInventory(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit != null && hit.transform.IsChildOf(_view.PartsLayer.parent);
    }

    private void EnsureGhost()
    {
        if (_ghost != null || _ghostPrefab == null)
            return;

        _ghost = (RectTransform)Instantiate(_ghostPrefab, _ghostLayer).transform;
        _ghostImage = _ghost.GetComponent<Image>();

        if (_ghostImage != null)
        {
            _ghostImage.raycastTarget = false;
            _ghostImage.sprite = _dragged.Sprite;
            _ghostImage.enabled = true;
        }

        _ghost.gameObject.SetActive(true);
        _ghost.SetAsLastSibling();
    }

    private void MoveGhost(PointerEventData eventData)
    {
        if (_ghost == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostLayer, eventData.position, eventData.pressEventCamera, out Vector2 local);

        Vector2 pivotOffset = new Vector2(
            _ghostLayer.rect.width * _ghostLayer.pivot.x,
            _ghostLayer.rect.height * _ghostLayer.pivot.y);

        _ghost.anchoredPosition = local + pivotOffset - _ghost.rect.size;
    }

    private void HideGhost()
    {
        if (_ghost != null)
            _ghost.gameObject.SetActive(false);
    }
}