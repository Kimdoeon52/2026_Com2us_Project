using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 창이 켜지면 창을 페이드하고, 목록 줄과 세부 항목을 차례로 보여준다
public class WindowOpenEffect : MonoBehaviour
{
    private const float WindowFadeTime = 0.2f;
    private const float ItemFadeTime = 0.25f;
    private const float RowStartDelay = 0.15f;
    private const float RowInterval = 0.07f;
    private const float LateStartDelay = 0.6f;
    private const float LateInterval = 0.2f;
    private const float SlideDistance = 24f;
    private const int MaxStaggerRows = 12;
    private const float LineTime = 0.3f;
    private const float TypeInterval = 0.02f;
    private const int MaxVisibleAll = 99999;

    [Tooltip("자식들이 한 줄씩 나타나는 부모 (목록의 Content 등)")]
    [SerializeField] private RectTransform[] _rowGroups;

    [Tooltip("목록 다음에 차례로 나타나는 항목")]
    [SerializeField] private RectTransform[] _lateItems;

    [Tooltip("왼쪽부터 그려지는 선")]
    [SerializeField] private RectTransform[] _lines;

    [Tooltip("한 글자씩 나타나는 글자")]
    [SerializeField] private TMP_Text[] _typeTexts;

    private class EffectItem
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Position;
        public float Alpha;
    }

    private List<EffectItem> _items = new List<EffectItem>();
    private Dictionary<RectTransform, System.Action> _lineRestores = new Dictionary<RectTransform, System.Action>();
    private CanvasGroup _window;

    private void Awake()
    {
        _window = GetOrAddCanvasGroup(gameObject);
    }

    private void OnEnable()
    {
        _window.alpha = 0f;
        StartCoroutine(PlayRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreAll();
    }

    // 목록은 Start에서 채워지는 창이 있어서 한 프레임 뒤에 시작한다
    private IEnumerator PlayRoutine()
    {
        yield return null;

        _window.DOFade(1f, WindowFadeTime).SetUpdate(true).SetLink(gameObject);

        foreach (var group in _rowGroups)
        {
            if (group.gameObject.activeInHierarchy) PlayRows(group);
        }

        for (int i = 0; i < _lateItems.Length; i++)
        {
            if (_lateItems[i].gameObject.activeInHierarchy)
                PlayItem(_lateItems[i], LateStartDelay + LateInterval * i);
        }

        foreach (var line in _lines)
        {
            if (line.gameObject.activeInHierarchy) PlayLine(line);
        }

        foreach (var text in _typeTexts)
        {
            if (text.gameObject.activeInHierarchy) PlayType(text);
        }
    }

    private void PlayLine(RectTransform line)
    {
        // 기준점을 왼쪽으로 옮겨 왼쪽부터 늘어나게 한다
        Vector2 pivot = line.pivot;
        Vector2 position = line.anchoredPosition;
        line.pivot = new Vector2(0f, pivot.y);
        line.anchoredPosition = position - new Vector2(pivot.x * line.rect.width, 0f);
        line.localScale = new Vector3(0f, 1f, 1f);

        _lineRestores[line] = () =>
        {
            line.localScale = Vector3.one;
            line.pivot = pivot;
            line.anchoredPosition = position;
        };

        line.DOScaleX(1f, LineTime).SetDelay(RowStartDelay).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(line.gameObject)
            .OnComplete(() => RestoreLine(line));
    }

    private void RestoreLine(RectTransform line)
    {
        if (_lineRestores.TryGetValue(line, out var restore))
        {
            restore();
            _lineRestores.Remove(line);
        }
    }

    private void PlayType(TMP_Text text)
    {
        text.ForceMeshUpdate();
        int count = text.textInfo.characterCount;

        text.maxVisibleCharacters = 0;
        DOTween.To(() => text.maxVisibleCharacters, x => text.maxVisibleCharacters = x, count, count * TypeInterval)
            .SetDelay(RowStartDelay)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetTarget(text)
            .SetLink(text.gameObject)
            .OnComplete(() => text.maxVisibleCharacters = MaxVisibleAll);
    }

    private void PlayRows(RectTransform group)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(group);

        int index = 0;
        foreach (RectTransform row in group)
        {
            if (!row.gameObject.activeSelf) continue;

            PlayItem(row, RowStartDelay + RowInterval * Mathf.Min(index, MaxStaggerRows));
            index++;
        }
    }

    private void PlayItem(RectTransform rect, float delay)
    {
        var item = new EffectItem
        {
            Rect = rect,
            Group = GetOrAddCanvasGroup(rect.gameObject),
            Position = rect.anchoredPosition
        };
        item.Alpha = item.Group.alpha;
        _items.Add(item);

        item.Group.alpha = 0f;
        rect.anchoredPosition = item.Position + Vector2.right * SlideDistance;

        item.Group.DOFade(item.Alpha, ItemFadeTime).SetDelay(delay).SetUpdate(true).SetLink(rect.gameObject);
        rect.DOAnchorPos(item.Position, ItemFadeTime).SetDelay(delay).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(rect.gameObject);
    }

    // 도중에 꺼져도 다음에 켤 때 위치와 투명도가 어긋나지 않게 되돌린다
    private void RestoreAll()
    {
        _window.DOKill();
        _window.alpha = 1f;

        foreach (var item in _items)
        {
            if (item.Rect == null) continue;

            item.Group.DOKill();
            item.Rect.DOKill();
            item.Group.alpha = item.Alpha;
            item.Rect.anchoredPosition = item.Position;
        }
        _items.Clear();

        foreach (var line in _lines)
        {
            if (line == null) continue;

            line.DOKill();
            RestoreLine(line);
        }

        foreach (var text in _typeTexts)
        {
            if (text == null) continue;

            DOTween.Kill(text);
            text.maxVisibleCharacters = MaxVisibleAll;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }
}
