using TMPro;
using UnityEngine;

// 선택한 부위의 상세 정보
public class RepairDetailPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private TextMeshProUGUI _partName;
    [SerializeField] private RepairBadge _gradeBadge;
    [SerializeField] private RepairBadge _stateBadge;
    [SerializeField] private TextMeshProUGUI _durability;
    [SerializeField] private RepairDurabilityBar _durabilityBar;

    [Header("수리비 계산")]
    [SerializeField] private TextMeshProUGUI _baseCostLabel;
    [SerializeField] private TextMeshProUGUI _baseCost;
    [SerializeField] private TextMeshProUGUI _gradeRateLabel;
    [SerializeField] private TextMeshProUGUI _gradeRate;
    [SerializeField] private TextMeshProUGUI _lostDurability;
    [SerializeField] private TextMeshProUGUI _totalCost;

    [Header("기타")]
    [SerializeField] private TextMeshProUGUI _time;
    [SerializeField] private TextMeshProUGUI _zeroDurability;
    [SerializeField] private TextMeshProUGUI _breakChance;

    public void Refresh()
    {
        Slot slot = FixWindowManager.Instance.SelectedSlot;
        PartsDefinition part = InventoryHost.Instance.GetEquipped(slot);
        int current = InventoryHost.Instance.GetDurability(slot);
        int max = part != null ? part.Durability : 0;
        RepairPartState state = RepairRules.GetState(part, current);
        string slotName = RepairRules.SlotName(slot);

        _description.text = $"{slotName} · {RepairRules.SlotKey(slot)}";
        _partName.text = part != null ? part.DisplayName : "빈 슬롯";
        _durability.text = part != null ? $"{current} / {max}" : "-";
        _durabilityBar.SetValue(current, max, state);

        _gradeBadge.gameObject.SetActive(part != null);
        _stateBadge.gameObject.SetActive(part != null);
        if (part != null)
        {
            _gradeBadge.SetGrade(part.Grade);
            _stateBadge.SetState(state);
        }

        _baseCostLabel.text = $"부위 기본 수리비 ({slotName})";
        _baseCost.text = $"{RepairRules.BaseRepairCost(slot)}G";
        _gradeRateLabel.text = part != null ? $"× 등급 배율 ({RepairRules.GradeName(part.Grade)})" : "× 등급 배율";
        _gradeRate.text = part != null ? RepairRules.GradeRate(part.Grade).ToString("0.#") : "-";
        _lostDurability.text = max > 0 ? $"{Mathf.RoundToInt((max - current) * 100f / max)}%" : "-";
        _totalCost.text = part != null ? $"{RepairRules.RepairCost(part, current)}G" : "-";

        _time.text = FixWindowManager.Instance.NeedRepair(slot) ? $"{RepairRules.RepairHours}시간" : "-";
        _zeroDurability.text = slot == Slot.Head ? "패시브 스킬 해제" : "액티브 스킬 봉인";
        _breakChance.text = part != null ? $"{BattleSettlementProcessor.GetDestructionProbability(part.Grade):P0} (등급 기준)" : "-";
    }
}
