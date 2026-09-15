using UnityEngine;

// 인벤토리 그리드 규격
[CreateAssetMenu(menuName = "KDU/인벤토리/인벤토리 정의", fileName = "InventoryDefinition")]
public class InventoryDefinition : ScriptableObject
{
    [Tooltip("그리드 크기")]
    [SerializeField] private Vector2Int _gridSize = new Vector2Int(6, 12);

    public Vector2Int GridSize => _gridSize;

    private void OnValidate()
    {
        _gridSize.x = Mathf.Max(1, _gridSize.x);
        _gridSize.y = Mathf.Max(1, _gridSize.y);
    }
}
