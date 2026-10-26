using UnityEngine;

public class CorrectRecipe : MonoBehaviour
{
    [SerializeField] private RecipeData[] recipeDatas;
    private CombinationSlot[] slots;

    [SerializeField] private ResultSlot resultSlot;
    public PartsDefinition resultPart;

    // RefreshMissingComponents 안의 ConsumeComponent가 다시 Changed를 부르므로 재진입 방지용
    private bool _refreshing;

    private void Awake()
    {
        GetSlotDatas();
    }

    // InventoryHost.Instance는 InventoryHost.Awake에서 채워지므로 Awake가 아니라 Start에서 구독
    private void Start()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed += RefreshMissingComponents;
    }

    private void OnDestroy()
    {
        if (InventoryHost.Instance != null)
            InventoryHost.Instance.Changed -= RefreshMissingComponents;
    }

    // 인벤토리가 바뀔 때마다 빨간 칸(재료 없음)이 이제 재료를 가져올 수 있는지 다시 확인한다
    private void RefreshMissingComponents()
    {
        if (_refreshing || slots == null)
            return;

        _refreshing = true;
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            CombinationSlot slot = slots[i];
            if (slot.Component == null || slot.HasComponent)
                continue;

            // 재고가 생겼으면 실제로 1개 빼오고 흰색으로 바꾼다
            if (InventoryHost.Instance.ConsumeComponent(slot.Component.Id, 1))
            {
                slot.SetComponent(slot.Component, true);
                changed = true;
            }
        }

        _refreshing = false;

        if (changed)
            PushPartData();   // 빨간 칸이 줄었으니 결과 슬롯 다시 판정
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            PushPartData();
    }

    public void GetSlotDatas()
    {
        slots = GetComponentsInChildren<CombinationSlot>();
        Debug.Log($"슬롯갯수{slots.Length}");
    }

    public string FindRecipe()
    {
        foreach (var recipe in recipeDatas)
        {
            bool allMatch = true;
            for (int i = 0; i < 9; i++)
            {
                if (!IsSameID(slots[i].componentID, recipe.Combination[i]))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) return recipe.PartID;
        }
        return null;
    }

    public bool isCorrect() => FindRecipe() != null;

    private bool IsSameID(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) a = null;
        if (string.IsNullOrEmpty(b)) b = null;
        return a == b;
    }

    public void PushPartData()
    {
        string partID = FindRecipe();
        if (isCorrect() == true && HasAllComponents())   // 빨간 칸이 하나라도 있으면 제작 불가
        { 
            partID = FindRecipe();
            resultPart = partID != null ? resultSlot.FindPart(partID) : null;
            resultSlot.SetPart(resultPart);   // null이면 결과 슬롯을 비움
        }
        else
        {
            partID = "";
            resultPart = null;
            resultSlot.SetPart(null);
        }
    }

    // 슬롯 9칸 모두 실제로 인벤토리에서 빠진 부품인지 (빨간 칸이 없는지)
    private bool HasAllComponents()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Component != null && !slots[i].HasComponent)   // 빈 칸은 검사 대상 아님
                return false;
        }
        return true;
    }

    public void ConfirmCraft()
    {
        if (resultPart != null)
            ConsumeSlots();
    }

    private void ConsumeSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].SetComponent(null);
        }
    }
}
