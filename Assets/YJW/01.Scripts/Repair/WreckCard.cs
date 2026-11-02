using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 잔해 카드 1장. 일반 등급용 카드에는 복구 관련 항목이 없다
public class WreckCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _key;
    [SerializeField] private TextMeshProUGUI _slot;
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private RepairBadge _badge;

    [Header("복구 가능 카드만")]
    [SerializeField] private TextMeshProUGUI _cost;
    [SerializeField] private TextMeshProUGUI _material;
    [SerializeField] private Button _restoreButton;
    [SerializeField] private TextMeshProUGUI _restoreLabel;

    private PartsDefinition _part;

    public void SetData(PartsDefinition part)
    {
        _part = part;
        _key.text = RepairRules.SlotKey(part.Slot);
        _slot.text = $"{RepairRules.SlotName(part.Slot)} · 잔해";
        _name.text = part.DisplayName;
        _badge.SetGrade(part.Grade);

        if (_restoreButton != null) SetRestoreData();
    }

    private void SetRestoreData()
    {
        int cost = RepairRules.RestoreCost(_part);
        int material = RepairRules.RestoreMaterial(_part.Grade);
        int owned = InventoryHost.Instance.CountComponents(_part.Grade);
        bool hasMaterial = owned >= material;
        bool hasGold = FixWindowManager.Instance.PlayerGold >= cost;

        _cost.text = $"{cost:N0}G";
        _material.text = material > 0 ? MaterialText(material, owned, hasMaterial) : "없음";

        _restoreButton.interactable = hasMaterial && hasGold;
        _restoreLabel.color = hasMaterial && hasGold ? RepairColors.Cyan : RepairColors.Neutral;

        if (!hasMaterial) _restoreLabel.text = "재료 부족";
        else if (!hasGold) _restoreLabel.text = "골드 부족";
        else _restoreLabel.text = material > 0 ? $"복구 · {cost:N0}G + 부품 {material}" : $"복구 · {cost:N0}G";
    }

    private string MaterialText(int material, int owned, bool hasMaterial)
    {
        string color = ColorUtility.ToHtmlStringRGB(hasMaterial ? RepairColors.Good : RepairColors.Bad);
        return $"{RepairRules.GradeName(_part.Grade)} 부품 {material}개 <color=#{color}>보유 {owned}</color>";
    }

    public void OnRestoreButton()
    {
        FixWindowManager.Instance.RestoreWreck(_part);
    }

    public void OnDiscardButton()
    {
        InventoryHost.Instance.DiscardWreck(_part.Id);
    }
}
