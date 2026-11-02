using System;
using System.Collections.Generic;
using UnityEngine;

// 스킬 ID → 행동 매핑
[CreateAssetMenu(menuName = "KDU/LegSkill/Part Skill Catalog", fileName = "PartSkillCatalog")]
public class PartSkillCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("스킬 ID")]
        public string Id;

        [Tooltip("행동")]
        public ActionData Action;
    }

    [Tooltip("매핑 목록")]
    [SerializeField] private Entry[] _entries;

    public ActionData Find(string id)
    {
        if (_entries == null || string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].Id == id)
                return _entries[i].Action;
        }

        return null;
    }

    private void OnValidate()
    {
        if (_entries == null)
            return;

        var seen = new HashSet<string>();
        for (int i = 0; i < _entries.Length; i++)
        {
            string id = _entries[i].Id;
            if (string.IsNullOrEmpty(id))
                continue;

            if (!seen.Add(id))
                Debug.LogWarning($"{name}: 스킬 ID {id} 중복", this);

            if (_entries[i].Action == null)
                Debug.LogWarning($"{name}: 스킬 ID {id} 행동 비어 있음", this);
        }
    }
}
