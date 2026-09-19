using UnityEngine;
using UnityEngine.UI;

// 파츠를 그린다. 이미지가 있으면 외곽에 한 장, 없으면 점유 칸마다 블록
public class ShapeRenderer : MonoBehaviour
{
    [Tooltip("칸 1개 프리팹")]
    [SerializeField] private GameObject _blockPrefab;

    private Image _image;
    private RectTransform _layer;
    private int _active;

    private void Awake()
    {
        TryGetComponent(out _image);
    }

    public void Draw(PartsShape shape, Sprite sprite, float cellSize, Color color)
    {
        if (sprite != null)
        {
            DrawSprite(sprite, color);
            return;
        }

        DrawBlocks(shape, cellSize, color);
    }

    public void Tint(Color color)
    {
        if (_image != null && _image.enabled)
            _image.color = color;

        if (_layer == null)
            return;

        for (int i = 0; i < _active; i++)
        {
            if (_layer.GetChild(i).TryGetComponent(out Image block))
                block.color = color;
        }
    }

    // 외곽 렉트는 뷰가 이미 모양 크기로 맞춰 놓는다
    private void DrawSprite(Sprite sprite, Color color)
    {
        HideBlocks(0);

        if (_image == null)
            return;

        _image.enabled = true;
        _image.sprite = sprite;
        _image.color = color;
    }

    private void DrawBlocks(PartsShape shape, float cellSize, Color color)
    {
        // 외곽 사각형은 꺾인 모양과 다르므로 끈다
        if (_image != null && _blockPrefab != null)
            _image.enabled = false;

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

        HideBlocks(_active);
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

        if (rect.TryGetComponent(out Image block))
            block.color = color;
    }

    private void HideBlocks(int startIndex)
    {
        if (_layer == null)
            return;

        _active = startIndex;

        for (int i = startIndex; i < _layer.childCount; i++)
        {
            _layer.GetChild(i).gameObject.SetActive(false);
        }
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
