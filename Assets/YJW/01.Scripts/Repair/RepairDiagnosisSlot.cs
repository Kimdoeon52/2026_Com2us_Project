using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로봇 진단 그림의 부위 버튼
public class RepairDiagnosisSlot : MonoBehaviour
{
    [SerializeField] private Slot _slot;
    [SerializeField] private Image _background;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Image _status;

    public void OnClickSlot()
    {
        FixWindowManager.Instance.Select(_slot);
    }

    public void Refresh()
    {
        PartsDefinition part = InventoryHost.Instance.GetEquipped(_slot);
        int current = InventoryHost.Instance.GetDurability(_slot);
        RepairPartState state = RepairRules.GetState(part, current);
        bool selected = FixWindowManager.Instance.SelectedSlot == _slot;

        _background.sprite = selected ? RepairSprites.Instance.slotSelected : RepairSprites.Instance.slotNormal;

        string slotName = part != null ? RepairRules.SlotName(_slot) : "빈 슬롯";
        _label.text = $"{RepairRules.SlotKey(_slot)}\n{slotName}";

        if (selected) _label.color = RepairColors.Ink;
        else _label.color = part != null ? RepairColors.Bone : RepairColors.Neutral;

        _status.gameObject.SetActive(part != null);
        _status.sprite = RepairSprites.Instance.StatusIcon(state);
    }
}
