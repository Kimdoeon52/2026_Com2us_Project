using System;
using UnityEngine;

// 인벤토리에 배치된 파츠 1개. 세이브 대상
[Serializable]
public class PartsInstance
{
    [SerializeField] private string _definitionId;
    [SerializeField] private Vector2Int _origin;
    [SerializeField] private bool _isDestroyed;
    [SerializeField] private int _lostDurability;   // 잃은 내구도. 0이면 최대치

    [NonSerialized] private PartsDefinition _definition;

    public PartsDefinition Definition => _definition;
    public Vector2Int Origin { get => _origin; set => _origin = value; }
    public bool IsDestroyed => _isDestroyed;

    public int Durability => _isDestroyed ? 0 : _definition.Durability;
    public int CurrentDurability => _isDestroyed ? 0 : Mathf.Max(0, _definition.Durability - _lostDurability);
    public int LostDurability { get => _lostDurability; set => _lostDurability = Mathf.Max(0, value); }
    public int Cost => _isDestroyed ? 0 : _definition.Cost;

    public PartsInstance() { }

    public PartsInstance(PartsDefinition definition, Vector2Int origin)
    {
        _definition = definition;
        _definitionId = definition.Id;
        _origin = origin;
    }

    public void DestroyPart() => _isDestroyed = true;
    public string GetTooltip() => _definition.GetTooltip(_isDestroyed);

    // 로드 직후 호출. Id로 SO를 다시 찾아 연결
    public void Resolve(PartsCatalog db) => _definition = db.Find(_definitionId);
}
