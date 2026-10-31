using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _name;
    [SerializeField] private TextMeshProUGUI _grade;
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _cost;
    [SerializeField] private Image buttonBG;
    private PartsDefinition _part;
    private int Gold;

    // InventoryHost.Instance는 InventoryHost.Awake에서 채워지므로 Awake가 아니라 Start에서 구독
    private void Start()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed += CanSell;
        CanSell();
    }

    private void OnDestroy()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed -= CanSell;
    }

    public void SetData(string name, PartGrade grade, Sprite icon, int cost, PartsDefinition part)
    {
        _name.text = name;
        _grade.text = grade.ToString();
        _icon.sprite = icon;
        int disGold = 0;
        switch(grade)
        {
            case PartGrade.Common:
                disGold = (cost * 35 + 50) / 100;
                _cost.text = disGold.ToString();
                break;
            case PartGrade.Rare:
                disGold = (cost * 30 + 50) / 100;
                _cost.text = disGold.ToString();
                break;
            case PartGrade.Epic:
                disGold = (cost * 20 + 50) / 100;
                _cost.text = disGold.ToString();
                break;
        }
        _part = part;
        Gold = disGold;
        CanSell();   // 다른 파츠가 들어왔으니 판매 가능 여부도 다시 판정
    }

    bool hasPart(PartsDefinition part)
    {
        if (part == null || InventoryHost.Instance == null) return false;

        return InventoryHost.Instance.Grid.Entries.Any(e =>
            !e.IsComponent &&
            e.Definition == part &&
            e.Parts != null &&
            !e.Parts.IsDestroyed);
    }

    private void CanSell()
    {
        if (hasPart(_part) == true) buttonBG.color = Color.yellow;
        else buttonBG.color = Color.white;
    }

    public void OnSellButton()
    {
        if (hasPart(_part) == true)
        {
            InventoryHost.Instance.RemovePart(_part);
            GlobalGold.Instance.GetGold("윤지우", Gold);
            GlobalGold.Instance.SaveGame();
        }
        else return;
    }
}
