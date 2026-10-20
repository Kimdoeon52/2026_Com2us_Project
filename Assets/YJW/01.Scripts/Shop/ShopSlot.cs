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

    private void Update()
    {
        CanSell();
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
    }

    bool hasPart(PartsDefinition part)
    {
        return InventoryHost.Instance.Grid.Entries.Any(e => !e.IsComponent && e.Definition == part);
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
