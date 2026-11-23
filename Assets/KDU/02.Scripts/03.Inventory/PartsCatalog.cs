using UnityEngine;

// 파츠 정의 목록. 세이브의 ID를 정의로 되돌릴 때 쓴다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 목록", fileName = "PartsCatalog")]
public class PartsCatalog : ScriptableObject
{
    [Tooltip("파츠 정의")]
    [SerializeField] private PartsDefinition[] _parts;

    [Tooltip("새 게임 시 장착된 파츠")]
    [SerializeField] private PartsDefinition[] _startingEquipment;

    public PartsDefinition[] Parts => _parts;
    public PartsDefinition[] StartingEquipment => _startingEquipment;

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

    // 해당 부위의 시작 파츠. 빈 부위 기본값으로 쓴다
    public PartsDefinition FindStarting(Slot slot)
    {
        if (_startingEquipment == null)
            return null;

        for (int i = 0; i < _startingEquipment.Length; i++)
        {
            if (_startingEquipment[i] != null && _startingEquipment[i].Slot == slot)
                return _startingEquipment[i];
        }

        return null;
    }
}
