using UnityEngine;

// 파츠 정의. 인벤토리 배치에 필요한 최소 필드만 둔다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 정의", fileName = "PartsDefinition")]
public class PartsDefinition : ScriptableObject
{
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("점유 모양")]
    [SerializeField] private PartsShape _shape = new PartsShape();

    [Tooltip("파츠 이미지. 외곽 비율에 맞춰 그린다")]
    [SerializeField] private Sprite _sprite;

    public string Id => _id;
    public string DisplayName => _displayName;
    public PartsShape Shape => _shape;
    public Sprite Sprite => _sprite;

    // 외곽 크기. 렌더 크기 계산용
    public Vector2Int Size => _shape.Size;

    private void OnValidate()
    {
        _shape.Validate();
        WarnAspect();
    }

    // 비율이 어긋나면 늘어나 보인다
    private void WarnAspect()
    {
        if (_sprite == null)
            return;

        float sprite = _sprite.rect.width / _sprite.rect.height;
        float shape = (float)_shape.Size.x / _shape.Size.y;

        if (Mathf.Abs(sprite - shape) > 0.01f)
            Debug.LogWarning($"{name}: 이미지 비율이 {_shape.Size.x}x{_shape.Size.y}와 다르다.", this);
    }
}
