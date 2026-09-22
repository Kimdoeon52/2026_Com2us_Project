using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CombinationSlotDragController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private CombinationSlot _slot;
    [SerializeField] private GameObject _ghostPrefab;
    [SerializeField] private RectTransform _ghostLayer;
    [SerializeField] private CorrectRecipe _recipe;

    private RectTransform _ghost;
    private Image _ghostImage;
    private CanvasGroup _source;
    private ComponentDefinition _dragged;

    private void Reset()
    {
        if (_slot == null)
            _slot = GetComponent<CombinationSlot>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragged = _slot.Component;   // 비어있으면 null

        if (_dragged == null)
            return;

        _source = _slot.GetOrAddComponent<CanvasGroup>();
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
        if (_dragged != null)
        {
            CombinationSlot target = FindTargetSlot(eventData);

            if (target != null && target != _slot)
            {
                // 이미 차 있으면 서로 자리를 바꾼다
                ComponentDefinition other = target.Component;
                target.SetComponent(_dragged);
                _slot.SetComponent(other);
                _recipe.PushPartData();
            }
            // target == null 이거나 자기 자신이면 그대로 둔다 (아무것도 안 함)
        }

        if (_source != null)
        {
            _source.alpha = 1f;
            _source = null;
        }

        HideGhost();
        _dragged = null;
    }

    private CombinationSlot FindTargetSlot(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit != null ? hit.GetComponentInParent<CombinationSlot>() : null;
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
        if (_ghost == null || _ghostLayer == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_ghostLayer, eventData.position, eventData.pressEventCamera, out Vector2 local);

        Vector2 pivotOffset = new Vector2(
            _ghostLayer.rect.width * _ghostLayer.pivot.x,
            _ghostLayer.rect.height * _ghostLayer.pivot.y);

        // 고스트 크기만큼 왼쪽·아래로 밀어서, 마우스가 고스트의 오른쪽 위 모서리에 오게 함
        _ghost.anchoredPosition = local + pivotOffset - _ghost.rect.size;
    }

    private void HideGhost()
    {
        if (_ghost != null)
            _ghost.gameObject.SetActive(false);
    }
}