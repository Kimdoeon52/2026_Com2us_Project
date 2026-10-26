using Cysharp.Threading.Tasks.Triggers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RecipeSlot : MonoBehaviour, IPointerClickHandler
{
    public RecipeData data;
    [SerializeField] private Image image;

    private Image slotImage;

    private void Awake()
    {
        slotImage = GetComponent<Image>();
    }

    private void Update()
    {
        if (HaveAllComponents() == true)
            slotImage.color = Color.green;
        else slotImage.color = Color.white;
    }

    public void PushData(RecipeData recipe, Sprite icon)
    {
        data = recipe;
        image.sprite = icon;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Pushcomponents();
    }

    private void _HasComponent()
    {
        var Slots = CraftingManager.Instance.slots;
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i].GetComponent<CombinationSlot>();
            
        }
    }

    private void Pushcomponents()
    {
        if (data == null) return;   // 카테고리 버튼을 안 눌러 데이터가 없는 슬롯

        var manager = CraftingManager.Instance;

        // 기존에 올려둔 부품이 있으면 먼저 인벤토리로 돌려놓는다 (안 하면 덮어써져서 사라짐)
        manager.AllComponentReturn();

        for (int i = 0; i < manager.slots.Length; i++)
        {
            var slot = manager.slots[i].GetComponent<CombinationSlot>();
            var component = manager.components.Find(data.Combination[i]);   // PartID가 아니라 i번째 칸의 부품 ID

            // 빈 칸은 검사할 게 없으니 true(흰색)
            // 부품 칸은 인벤토리에서 1개 빼보고, 성공하면 true(흰색) / 재고 없으면 false(빨강)
            bool has = component == null || InventoryHost.Instance.ConsumeComponent(component.Id, 1);

            slot.SetComponent(component, has);   // componentID는 SetComponent 안에서 같이 세팅되므로 따로 넣을 필요 없음
        }

        // 결과 슬롯 갱신 (슬롯들의 부모에 CorrectRecipe가 있음)
        manager.slots[0].GetComponentInParent<CorrectRecipe>().PushPartData();
    }

    private bool HaveAllComponents()
    {
        var need = new Dictionary<ComponentDefinition, int>();

        if (data == null) return false;
        // 레시피 9칸에서 부품별 필요 개수를 센다 (빈 칸은 건너뜀)
        foreach (string id in data.Combination)
        {
            var component = CraftingManager.Instance.components.Find(id);
            if (component == null) continue;

            need.TryGetValue(component, out int count);
            need[component] = count + 1;
        }

        // 하나라도 부족하면 false, 전부 통과해야 true
        foreach (var pair in need)
        {
            if (InventoryHost.Instance.Save.Components.Get(pair.Key.Id) < pair.Value)
                return false;
        }
        return true;
    }
}
