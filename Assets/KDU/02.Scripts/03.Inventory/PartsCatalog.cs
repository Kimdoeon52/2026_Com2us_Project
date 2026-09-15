using UnityEngine;

// 파츠 정의 목록. 세이브의 ID를 정의로 되돌릴 때 쓴다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 목록", fileName = "PartsCatalog")]
public class PartsCatalog : ScriptableObject
{
    [Tooltip("파츠 정의")]
    [SerializeField] private PartsDefinition[] _parts;

    public PartsDefinition[] Parts => _parts;

    public PartsDefinition Find(string id)
    {
        if (_parts == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < _parts.Length; i++)
        {
            if (_parts[i] != null && _parts[i].Id == id)
                return _parts[i];
        }

        return null;
    }
}
