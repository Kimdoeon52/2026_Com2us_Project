using UnityEngine;

// 파츠 정의. 인벤토리 배치에 필요한 최소 필드만 둔다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 정의", fileName = "PartsDefinition")]
public class PartsDefinition : ScriptableObject
{
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("그리드 크기")]
    [SerializeField] private Vector2Int _size = Vector2Int.one;

    public string Id => _id;
    public string DisplayName => _displayName;
    public Vector2Int Size => _size;

    private void OnValidate()
    {
        _size.x = Mathf.Max(1, _size.x);
        _size.y = Mathf.Max(1, _size.y);
    }
}
