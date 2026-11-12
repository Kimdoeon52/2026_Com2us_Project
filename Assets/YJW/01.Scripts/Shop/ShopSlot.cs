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
    [SerializeField] private Color canSellColor = Color.yellow;
    [SerializeField] private Color cannotSellColor = Color.white;
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

        // 판매가는 SO의 고철 수입가. 0인 파츠(기본·전설·프로토타입)는 0G로 팔리지 않게 판매 불가로 표시한다
        Gold = part != null ? part.ScrapPrice : 0;
        _cost.text = Gold > 0 ? Gold.ToString() : "판매 불가";

        _part = part;
        CanSell();   // 다른 파츠가 들어왔으니 판매 가능 여부도 다시 판정
    }

    private bool IsSellable => _part != null && Gold > 0;

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
        if (IsSellable && hasPart(_part)) buttonBG.color = canSellColor;
        else buttonBG.color = cannotSellColor;
    }

    public void OnSellButton()
    {
        if (!IsSellable || !hasPart(_part) || GlobalGold.Instance == null)
            return;

        // 파츠가 실제로 빠졌을 때만 골드를 준다
        if (!InventoryHost.Instance.RemovePart(_part))
            return;

        GlobalGold.Instance.GetGold(0, Gold);
        GlobalGold.Instance.SaveGame();
    }
}
