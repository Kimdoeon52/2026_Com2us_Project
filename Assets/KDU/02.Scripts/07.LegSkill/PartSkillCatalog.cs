using System;
using System.Collections.Generic;
using UnityEngine;

// 스킬 ID → 행동·표시 정보 매핑
[CreateAssetMenu(menuName = "KDU/LegSkill/Part Skill Catalog", fileName = "PartSkillCatalog")]
public class PartSkillCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("스킬 ID")]
        public string Id;

        [Tooltip("표시명")]
        public string DisplayName;

        [Tooltip("스킬 종류 태그")]
        public string TypeLabel;

        [Tooltip("행동. 비우면 표시만")]
        public ActionData Action;

        [Tooltip("스킬 레벨업 가능")]
        public bool CanLevelUp;
    }

    [Tooltip("매핑 목록")]
    [SerializeField] private Entry[] _entries;

    public bool TryFind(string id, out Entry entry)
    {
        entry = default;
        if (_entries == null || string.IsNullOrEmpty(id))
            return false;

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].Id != id)
                continue;

            entry = _entries[i];
            return true;
        }

        return false;
    }

    public ActionData Find(string id)
    {
        return TryFind(id, out Entry entry) ? entry.Action : null;
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
        }
    }
}
