using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 장착 파츠 목록의 한 줄
public class RepairPartRow : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Slot _slot;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _selectedBar;
    [SerializeField] private Toggle _checkBox;
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private RepairBadge _badge;
    [SerializeField] private RepairDurabilityBar _durabilityBar;
    [SerializeField] private TextMeshProUGUI _durability;
    [SerializeField] private Image _stateMark;
    [SerializeField] private TextMeshProUGUI _state;
    [SerializeField] private TextMeshProUGUI _cost;
    [SerializeField] private TextMeshProUGUI _time;

    private void Start()
    {
        _checkBox.onValueChanged.AddListener(OnCheckChanged);
    }

    private void OnCheckChanged(bool isOn)
    {
        FixWindowManager.Instance.SetChecked(_slot, isOn);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        FixWindowManager.Instance.Select(_slot);
    }

    public void Refresh()
    {
        FixWindowManager manager = FixWindowManager.Instance;
        PartsDefinition part = InventoryHost.Instance.GetEquipped(_slot);
        int current = InventoryHost.Instance.GetDurability(_slot);
        RepairPartState state = RepairRules.GetState(part, current);
        bool selected = manager.SelectedSlot == _slot;
        bool needRepair = manager.NeedRepair(_slot);

        _background.sprite = selected ? RepairSprites.Instance.rowSelected : RepairSprites.Instance.rowNormal;
        _selectedBar.SetActive(selected);

        _name.text = part != null ? part.DisplayName : "빈 슬롯";
        _durability.text = part != null ? $"{current}/{part.Durability}" : "-";
        _durabilityBar.SetValue(current, part != null ? part.Durability : 0, state);

        _state.text = RepairRules.StateName(state);
        _state.color = RepairColors.State(state);
        _stateMark.gameObject.SetActive(part != null);
        _stateMark.sprite = RepairSprites.Instance.StatusIcon(state);

        _badge.gameObject.SetActive(part != null);
        if (part != null) _badge.SetGrade(part.Grade);

        _cost.text = part != null ? $"{RepairRules.RepairCost(part, current)}G" : "-";
        _time.text = TimeText(part, needRepair);

        _checkBox.SetIsOnWithoutNotify(manager.IsChecked(_slot));
        _checkBox.interactable = needRepair;
    }

    private string TimeText(PartsDefinition part, bool needRepair)
    {
        if (part == null) return FixWindowManager.Instance.HasWreck(_slot) ? "잔해 보관 중" : "-";

        return needRepair ? $"{RepairRules.RepairHours}시간" : "수리 불필요";
    }
}
