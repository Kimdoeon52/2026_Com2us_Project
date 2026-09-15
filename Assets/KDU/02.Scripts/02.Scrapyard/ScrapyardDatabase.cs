using UnityEngine;

// 지역 정의 목록. 지역 추가는 배열 원소 추가로 끝난다
[CreateAssetMenu(menuName = "KDU/고물상/지역 목록", fileName = "ScrapyardDatabase")]
public class ScrapyardDatabase : ScriptableObject
{
    [Tooltip("지역 정의")]
    [SerializeField] private ScrapyardDefinition[] _regions;

    public ScrapyardDefinition[] Regions => _regions;

    public int Count => _regions != null ? _regions.Length : 0;

    public ScrapyardDefinition Find(int regionIndex)
    {
        if (_regions == null)
            return null;

        for (int i = 0; i < _regions.Length; i++)
        {
            if (_regions[i] != null && _regions[i].RegionIndex == regionIndex)
                return _regions[i];
        }

        return null;
    }
}
