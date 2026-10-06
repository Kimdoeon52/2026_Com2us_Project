using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// partID -> 장비 효과 목록 매핑 테이블 (SO).
/// PartMasterData(KKH)를 수정하지 않고 NYH 쪽에서 장비 효과를 붙이기 위한 별도 테이블이다.
/// 나중에 KKH와 합의되면 PartMasterData에 효과 필드를 두는 방식으로 옮겨도 된다
/// (EquipmentEffectApplier가 이 테이블을 읽는 부분만 바꾸면 됨).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Equipment Effect Table", fileName = "EquipmentEffectTable")]
public class EquipmentEffectTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("PartMasterData.partID와 정확히 같은 문자열")]
        public string partID;

        [Tooltip("이 파츠를 장착했을 때 등록될 효과들 (ActionModifierData, ActionReplaceEffect 등 EquipmentEffect 하위 에셋)")]
        public EquipmentEffect[] effects;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private static readonly EquipmentEffect[] Empty = new EquipmentEffect[0];

    // 매번 리스트를 순회하지 않도록 첫 조회 때 딕셔너리로 만들어둔다. 직렬화하지 않는 캐시다
    private Dictionary<string, EquipmentEffect[]> lookup;
    private Dictionary<int, EquipmentEffect> skillLookup; // partID용 lookup과 분리

    private void OnEnable() { lookup = null; skillLookup = null; }
    private void OnValidate() { lookup = null; skillLookup = null; }

    /// <summary>partID에 연결된 효과 목록. 없으면 빈 배열</summary>
    public IReadOnlyList<EquipmentEffect> GetEffects(string partID)
    {
        if (string.IsNullOrEmpty(partID)) return Empty;

        if (lookup == null) BuildLookup();
        return lookup.TryGetValue(partID, out var effects) ? effects : Empty;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, EquipmentEffect[]>();
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.partID)) continue;

            if (lookup.ContainsKey(entry.partID))
                Debug.LogWarning($"[EquipmentEffectTable] partID '{entry.partID}'가 중복 등록됨 - 뒤의 항목이 앞을 덮어씀", this);

            lookup[entry.partID] = entry.effects ?? Empty;
        }
    }

    public EquipmentEffect Get(int skillId)
    {
        if (skillLookup == null)
        {
            skillLookup = new Dictionary<int, EquipmentEffect>();
            foreach (var entry in entries)
            {
                if (entry?.effects == null) continue;
                foreach (var e in entry.effects)
                {
                    if (e == null) continue;
                    if (!skillLookup.TryAdd(e.SkillId, e))
                        Debug.LogWarning($"[EquipmentEffectTable] skillId {e.SkillId} 중복", this);
                }
            }
        }
        return skillLookup.TryGetValue(skillId, out var r) ? r : null;
    }
}