using UnityEngine;

// 코어 정의 목록. 세이브의 ID를 정의로 되돌릴 때와 새 게임의 기본 코어를 정할 때 쓴다
[CreateAssetMenu(menuName = "KDU/인벤토리/코어 목록", fileName = "CoreCatalog")]
public class CoreCatalog : ScriptableObject
{
    [Tooltip("새 게임 시 장착된 코어")]
    [SerializeField] private CoreDefinition _defaultCore;

    [Tooltip("코어 정의")]
    [SerializeField] private CoreDefinition[] _cores;

    public CoreDefinition DefaultCore => _defaultCore;
    public CoreDefinition[] Cores => _cores;

    public CoreDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (_defaultCore != null && _defaultCore.Id == id)
            return _defaultCore;

        if (_cores == null)
            return null;

        for (int i = 0; i < _cores.Length; i++)
        {
            if (_cores[i] != null && _cores[i].Id == id)
                return _cores[i];
        }

        return null;
    }
}
