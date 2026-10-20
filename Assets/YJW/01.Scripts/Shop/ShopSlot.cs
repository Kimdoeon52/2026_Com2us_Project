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
        switch(grade)
        {
            case PartGrade.Common:
                _cost.text = ((cost * 35 + 50)/100).ToString();
                break;
            case PartGrade.Rare:
                _cost.text = ((cost * 30 + 50) / 100).ToString();
                break;
            case PartGrade.Epic:
                _cost.text = ((cost * 20 + 50) / 100).ToString();
                break;
        }
        _part = part;
        Gold = cost;
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
