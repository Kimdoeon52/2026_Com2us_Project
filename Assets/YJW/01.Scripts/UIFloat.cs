using DG.Tweening;
using UnityEngine;

// 조작 안내처럼 눈에 띄어야 하는 UI를 위아래로 살짝 띄운다
public class UIFloat : MonoBehaviour
{
    [SerializeField] private float _distance = 3f;
    [SerializeField] private float _time = 1f;

    private RectTransform _rect;
    private Vector2 _position;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _position = _rect.anchoredPosition;
    }

    private void OnEnable()
    {
        _rect.anchoredPosition = _position;
        _rect.DOAnchorPosY(_position.y + _distance, _time)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        _rect.DOKill();
        _rect.anchoredPosition = _position;
    }
}
