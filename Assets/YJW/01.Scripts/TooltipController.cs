using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject toolTipBox;
    [SerializeField] private TextMeshProUGUI tmp;

    private ResultSlot r_slot;
    private CombinationSlot c_slot;
    private RecipeSlot re_slot;

    // 툴팁 상자가 이름·아이콘·설명 구조(TooltipView)면 그걸 채우고, 아니면 예전처럼 글자만 넣는다
    private TooltipView view;

    private void Start()
    {
        r_slot = GetComponent<ResultSlot>();
        c_slot = GetComponent<CombinationSlot>();
        re_slot = GetComponent<RecipeSlot>();

        if (toolTipBox != null)
            view = toolTipBox.GetComponent<TooltipView>();
    }

    // 이 칸에 올라간 파츠 또는 부품. 비었으면 둘 다 null
    private void GetTarget(out PartsDefinition part, out ComponentDefinition component)
    {
        part = null;
        component = null;

        CraftingManager manager = CraftingManager.Instance;

        if (r_slot != null)
            part = r_slot._part;
        else if (c_slot != null && c_slot.componentID != null && manager != null)
            component = manager.components.Find(c_slot.componentID);
        else if (re_slot != null && re_slot.data != null && manager != null)
            part = manager.catalog.Find(re_slot.data.PartID);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GetTarget(out PartsDefinition part, out ComponentDefinition component);
        if (part == null && component == null) return;

        toolTipBox.SetActive(true);

        if (view == null)
        {
            tmp.text = part != null ? part.GetTooltip() : component.GetTooltip();
            return;
        }

        if (part != null)
            view.Show(part.DisplayName, part.Sprite, part.Grade, $"{PartGrades.ToLabel(part.Grade)} {RepairRules.SlotName(part.Slot)} 파츠", part.Description,
                part.Slot == Slot.LeftLeg || part.Slot == Slot.RightLeg);
        else
            view.Show(component.DisplayName, component.Sprite, component.Grade, $"{PartGrades.ToLabel(component.Grade)} 부품", component.Description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (view == null) tmp.text = "";
        toolTipBox.SetActive(false);
    }
}
