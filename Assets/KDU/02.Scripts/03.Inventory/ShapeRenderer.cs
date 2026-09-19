using UnityEngine;
using UnityEngine.UI;

// 모양대로 칸 블록을 깐다. 파츠 아이템과 드래그 고스트가 같이 쓴다
public class ShapeRenderer : MonoBehaviour
{
    [Tooltip("칸 1개 프리팹")]
    [SerializeField] private GameObject _blockPrefab;

    private RectTransform _layer;
    private int _active;

    private void Awake()
    {
        if (_blockPrefab == null)
            return;

        // 외곽 사각형은 꺾인 모양과 다르므로 끈다
        if (TryGetComponent(out Image image))
            image.enabled = false;
    }

    public void Draw(PartsShape shape, float cellSize, Color color)
    {
        if (_blockPrefab == null || shape == null)
            return;

        EnsureLayer();
        _active = 0;

        for (int y = 0; y < shape.Size.y; y++)
        {
            for (int x = 0; x < shape.Size.x; x++)
            {
                if (!shape.Contains(x, y))
                    continue;

                DrawBlock(new Vector2Int(x, y), cellSize, color);
                _active++;
            }
        }

        for (int i = _active; i < _layer.childCount; i++)
        {
            _layer.GetChild(i).gameObject.SetActive(false);
        }
    }

    public void Tint(Color color)
    {
        if (_layer == null)
            return;

        for (int i = 0; i < _active; i++)
        {
            if (_layer.GetChild(i).TryGetComponent(out Image image))
                image.color = color;
        }
    }

    private void DrawBlock(Vector2Int cell, float cellSize, Color color)
    {
        var rect = (RectTransform)_layer.GetOrCreateChild(_blockPrefab, _active);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = (Vector2)cell * cellSize;
        rect.sizeDelta = new Vector2(cellSize, cellSize);
        rect.gameObject.SetActive(true);

        if (rect.TryGetComponent(out Image image))
            image.color = color;
    }

    // 라벨 같은 기존 자식과 섞이지 않게 전용 컨테이너를 쓴다
    private void EnsureLayer()
    {
        if (_layer != null)
            return;

        var go = new GameObject("Blocks", typeof(RectTransform));
        _layer = (RectTransform)go.transform;
        _layer.SetParent(transform, false);
        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.zero;
        _layer.pivot = Vector2.zero;
        _layer.anchoredPosition = Vector2.zero;
        _layer.sizeDelta = Vector2.zero;
        _layer.SetAsFirstSibling();
    }
}
