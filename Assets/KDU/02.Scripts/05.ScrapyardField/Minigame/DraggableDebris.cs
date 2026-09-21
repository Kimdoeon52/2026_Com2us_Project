using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 고물 조각 하나. 경계 밖에서 놓으면 치워진다
public class DraggableDebris : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _rect;
    private RectTransform _bounds;
    private Canvas _canvas;
    private Action<DraggableDebris> _onRemoved;
    private Vector2 _grabOrigin;
    private bool _removed;

    public bool Removed => _removed;

    private void Awake() => _rect = (RectTransform)transform;

    public void Setup(RectTransform bounds, Canvas canvas, Action<DraggableDebris> onRemoved)
    {
        if (_rect == null)
            _rect = (RectTransform)transform;

        _bounds = bounds;
        _canvas = canvas;
        _onRemoved = onRemoved;
        _removed = false;
        gameObject.SetActive(true);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _grabOrigin = _rect.anchoredPosition;

        // 끌고 있는 조각이 다른 조각에 가리지 않게 맨 앞으로
        _rect.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_removed)
            return;

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        _rect.anchoredPosition += eventData.delta / Mathf.Max(0.0001f, scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_removed)
            return;

        // 경계 안에서 놓으면 그 자리에 둔다
        if (_bounds == null || RectTransformUtility.RectangleContainsScreenPoint(_bounds, eventData.position, eventData.pressEventCamera))
        {
            _rect.anchoredPosition = _grabOrigin;
            return;
        }

        _removed = true;
        gameObject.SetActive(false);
        _onRemoved?.Invoke(this);
    }
}
