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
        [Tooltip("구분용 이름 (조회에는 쓰이지 않는다). 효과는 각 에셋의 skillId로 찾는다 — PartMasterData.activeSkillId / passiveSkillIds와 맞출 것")]
        public string partID;

        [Tooltip("이 파츠를 장착했을 때 등록될 효과들 (ActionModifierData, ActionReplaceEffect 등 EquipmentEffect 하위 에셋)")]
        public EquipmentEffect[] effects;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // 매번 리스트를 순회하지 않도록 첫 조회 때 딕셔너리로 만들어둔다. 직렬화하지 않는 캐시다
    private Dictionary<int, EquipmentEffect> skillLookup;

    private void OnEnable() => skillLookup = null;
    private void OnValidate() => skillLookup = null;

    /// <summary>skillId에 해당하는 효과. 없으면 null (EquipmentEffectApplier가 장착 파츠의 스킬 ID로 부른다)</summary>
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