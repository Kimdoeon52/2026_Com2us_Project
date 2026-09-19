using System.Collections.Generic;
using UnityEngine;

// 부품 정의 목록. 부품 추가는 배열 원소 추가로 끝난다
[CreateAssetMenu(menuName = "KDU/인벤토리/부품 목록", fileName = "ComponentCatalog")]
public class ComponentCatalog : ScriptableObject
{
    [Tooltip("부품 정의")]
    [SerializeField] private ComponentDefinition[] _components;

    private readonly List<ComponentDefinition> _pickBuffer = new List<ComponentDefinition>();

    public ComponentDefinition[] Components => _components;

    public ComponentDefinition Find(string id)
    {
        if (_components == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < _components.Length; i++)
        {
            if (_components[i] != null && _components[i].Id == id)
                return _components[i];
        }

        return null;
    }

    // 해당 등급 부품 중 하나를 균등하게 뽑는다. 없으면 false
    public bool TryPick(PartGrade grade, Rng rng, out ComponentDefinition component)
    {
        component = null;

        if (_components == null || rng == null)
            return false;

        _pickBuffer.Clear();
        for (int i = 0; i < _components.Length; i++)
        {
            if (_components[i] != null && _components[i].Grade == grade)
                _pickBuffer.Add(_components[i]);
        }

        if (_pickBuffer.Count == 0)
            return false;

        component = _pickBuffer[rng.Range(0, _pickBuffer.Count)];
        return true;
    }

    private void OnValidate()
    {
        WarnDuplicateIds();
    }

    // ID가 겹치면 세이브 복원이 엉킨다
    private void WarnDuplicateIds()
    {
        if (_components == null)
            return;

        var seen = new HashSet<string>();
        for (int i = 0; i < _components.Length; i++)
        {
            string id = _components[i]?.Id;
            if (string.IsNullOrEmpty(id))
                continue;

            if (!seen.Add(id))
                Debug.LogWarning($"{name}: 부품 ID '{id}'가 중복된다.", this);
        }
    }
}
