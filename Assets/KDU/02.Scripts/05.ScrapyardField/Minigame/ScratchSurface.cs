using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 긁기 표면. 저해상도 텍스처의 알파를 지우고 긁힌 비율만 계산한다.
// 픽셀아트라 해상도가 낮아 CPU 조작으로 충분하고, 비율을 정확히 셀 수 있다
[RequireComponent(typeof(RawImage))]
public class ScratchSurface : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Tooltip("긁기 텍스처 해상도. 픽셀아트라 낮게 잡는다")]
    [Min(8)]
    [SerializeField] private int _resolution = 96;

    [Tooltip("브러시 반지름. 텍스처 픽셀 단위. 해상도 대비 비율이 체감 난이도를 정한다")]
    [Min(1)]
    [SerializeField] private int _brushRadius = 9;

    [Tooltip("덮개 색")]
    [SerializeField] private Color32 _coverColor = new Color32(120, 118, 110, 255);

    private RawImage _image;
    private RectTransform _rect;
    private Texture2D _texture;
    private Color32[] _pixels;
    private int _clearedCount;
    private bool _dirty;
    private bool _inputEnabled;

    // 0~1. 성공 판정 기준값
    public float Cleared01 => _pixels == null || _pixels.Length == 0 ? 0f : (float)_clearedCount / _pixels.Length;

    private void Awake()
    {
        _image = GetComponent<RawImage>();
        _rect = (RectTransform)transform;
    }

    public void Rebuild()
    {
        if (_texture == null || _texture.width != _resolution)
        {
            _texture = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _pixels = new Color32[_resolution * _resolution];
            _image.texture = _texture;
        }

        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = _coverColor;

        _clearedCount = 0;
        _dirty = false;
        _texture.SetPixels32(_pixels);
        _texture.Apply();
    }

    public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

    public void OnPointerDown(PointerEventData eventData) => ScratchAt(eventData);

    public void OnDrag(PointerEventData eventData) => ScratchAt(eventData);

    private void ScratchAt(PointerEventData eventData)
    {
        if (!_inputEnabled || _pixels == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Rect rect = _rect.rect;
        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);

        Scratch(u, v);
    }

    private void Scratch(float u, float v)
    {
        int cx = Mathf.RoundToInt(u * (_resolution - 1));
        int cy = Mathf.RoundToInt(v * (_resolution - 1));
        int radiusSqr = _brushRadius * _brushRadius;

        for (int y = -_brushRadius; y <= _brushRadius; y++)
        {
            int py = cy + y;
            if (py < 0 || py >= _resolution)
                continue;

            for (int x = -_brushRadius; x <= _brushRadius; x++)
            {
                int px = cx + x;
                if (px < 0 || px >= _resolution)
                    continue;

                if ((x * x) + (y * y) > radiusSqr)
                    continue;

                int index = (py * _resolution) + px;
                if (_pixels[index].a == 0)
                    continue;

                _pixels[index].a = 0;
                _clearedCount++;
                _dirty = true;
            }
        }
    }

    // 여러 번 긁혀도 GPU 업로드는 프레임당 한 번
    private void LateUpdate()
    {
        if (!_dirty)
            return;

        _dirty = false;
        _texture.SetPixels32(_pixels);
        _texture.Apply();
    }
}
