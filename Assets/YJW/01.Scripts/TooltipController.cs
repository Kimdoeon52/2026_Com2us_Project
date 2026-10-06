using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject toolTipBox;
    [SerializeField] private TextMeshProUGUI tmp;
    private PartsDefinition? part;
    private ComponentDefinition? component;

    private ResultSlot? r_slot;
    private CombinationSlot? c_slot;
    private RecipeSlot? re_slot;


    private void Start()
    {
        r_slot = GetComponent<ResultSlot>();
        c_slot = GetComponent<CombinationSlot>();
        re_slot = GetComponent<RecipeSlot>();
    }

    private string GetTooltipText()
    {
        if (r_slot != null)
        {
            if (r_slot._part == null) return null;
            return r_slot._part?.GetTooltip();
        }

        if (c_slot != null)
        {
            if (c_slot.componentID == null) return null;
            return CraftingManager.Instance.components.Find(c_slot.componentID)?.GetTooltip();
        }

        if (re_slot != null)
        {
            if (re_slot.data == null) return null;
            return CraftingManager.Instance.catalog.Find(re_slot.data.PartID)?.GetTooltip();
        }

        return null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string text = GetTooltipText();
        if (string.IsNullOrEmpty(text)) return;

        toolTipBox.SetActive(true);
        tmp.text = text;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tmp.text = "";
        toolTipBox.SetActive(false);
    }
}
