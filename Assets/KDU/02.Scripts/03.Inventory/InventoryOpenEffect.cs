using DG.Tweening;
using UnityEngine;

// 인벤토리가 켜지면 창을 늦게 페이드하고, 들어 있는 아이템을 차례로 페이드한다. 위치는 건드리지 않는다
public class InventoryOpenEffect : MonoBehaviour
{
    [Tooltip("창 등장 지연 (초)")]
    [Min(0f)][SerializeField] private float _delay = 0.25f;

    [Tooltip("창 페이드 시간 (초)")]
    [Min(0f)][SerializeField] private float _fadeTime = 0.2f;

    [Tooltip("아이템이 들어 있는 레이어")]
    [SerializeField] private RectTransform[] _itemLayers;

    [Tooltip("아이템 사이 간격 (초)")]
    [Min(0f)][SerializeField] private float _itemInterval = 0.03f;

    [Tooltip("아이템 등장 시간 (초)")]
    [Min(0f)][SerializeField] private float _itemTime = 0.25f;

    [Tooltip("간격이 더 늘어나지 않는 아이템 수")]
    [Min(1)][SerializeField] private int _maxStagger = 20;

    private CanvasGroup _window;

    private void Awake()
    {
        _window = gameObject.GetOrAddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        _window.DOKill();
        _window.alpha = 0f;
        _window.DOFade(1f, _fadeTime).SetDelay(_delay).SetUpdate(true).SetLink(gameObject);

        PlayItems();
    }

    private void OnDisable()
    {
        _window.DOKill();
        _window.alpha = 1f;
        ResetItems();
    }

    private void PlayItems()
    {
        if (_itemLayers == null)
            return;

        int index = 0;
        foreach (RectTransform layer in _itemLayers)
        {
            if (layer == null)
                continue;

            foreach (Transform item in layer)
            {
                if (!item.gameObject.activeSelf)
                    continue;

                float delay = _delay + _itemInterval * Mathf.Min(index, _maxStagger);
                CanvasGroup group = item.GetOrAddComponent<CanvasGroup>();
                group.DOKill();
                group.alpha = 0f;
                group.DOFade(1f, _itemTime).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(item.gameObject);
                index++;
            }
        }
    }

    // 도중에 꺼져도 다음에 켤 때 투명도가 어긋나지 않게
    private void ResetItems()
    {
        if (_itemLayers == null)
            return;

        foreach (RectTransform layer in _itemLayers)
        {
            if (layer == null)
                continue;

            foreach (Transform item in layer)
            {
                CanvasGroup group = item.GetComponent<CanvasGroup>();
                if (group == null)
                    continue;

                group.DOKill();
                group.alpha = 1f;
            }
        }
    }
}
