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

    public string Id => _id;
    public string DisplayName => _displayName;
    public PartsShape Shape => _shape;

    // 외곽 크기. 렌더 크기 계산용
    public Vector2Int Size => _shape.Size;

    private void OnValidate()
    {
        _shape.Validate();
    }
}
