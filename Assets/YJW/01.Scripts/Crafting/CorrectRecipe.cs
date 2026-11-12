using UnityEngine;

public class CorrectRecipe : MonoBehaviour
{
    [SerializeField] private RecipeData[] recipeDatas;
    private CombinationSlot[] slots;

    [SerializeField] private ResultSlot resultSlot;
    public PartsDefinition resultPart;

    // 지금 결과 슬롯에 뜬 파츠의 레시피. 제작 확정 시 소요 시간을 읽는다
    private RecipeData _resultRecipe;

    private const int GridSize = 9;

    // RefreshMissingComponents 안의 ConsumeComponent가 다시 Changed를 부르므로 재진입 방지용
    private bool _refreshing;

    // 같은 연결 오류를 매번 찍지 않도록
    private bool _warnedSlots;
    private readonly System.Collections.Generic.HashSet<string> _warnedRecipes = new System.Collections.Generic.HashSet<string>();

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
        if (_refreshing || !HasValidSlots())
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

    // 조합 칸이 9개 모두 연결돼 있어야 판정한다. 잘못된 연결은 한 번만 알린다
    private bool HasValidSlots()
    {
        bool valid = slots != null && slots.Length == GridSize && System.Array.TrueForAll(slots, s => s != null);
        if (!valid && !_warnedSlots)
        {
            _warnedSlots = true;
            Debug.LogError($"[CorrectRecipe] 조합 칸이 {(slots != null ? slots.Length : 0)}개 연결돼 있습니다. {GridSize}칸이 모두 있어야 제작할 수 있어 제작을 중단합니다.", this);
        }
        return valid;
    }

    // 비었거나 9칸이 아닌 레시피는 판정에서 빼고 한 번만 알린다
    private bool IsValidRecipe(RecipeData recipe)
    {
        if (recipe != null && recipe.Combination != null && recipe.Combination.Length == GridSize)
            return true;

        if (_warnedRecipes.Add(recipe != null ? recipe.name : "(비어 있음)"))
            Debug.LogWarning($"[CorrectRecipe] 레시피 '{(recipe != null ? recipe.name : "비어 있음")}'의 조합법이 {GridSize}칸이 아니어서 건너뜁니다.", this);
        return false;
    }

    public string FindRecipe()
    {
        RecipeData recipe = FindMatchedRecipe();
        return recipe != null ? recipe.PartID : null;
    }

    private RecipeData FindMatchedRecipe()
    {
        if (!HasValidSlots() || recipeDatas == null)
            return null;

        foreach (var recipe in recipeDatas)
        {
            if (!IsValidRecipe(recipe))
                continue;

            bool allMatch = true;
            for (int i = 0; i < GridSize; i++)
            {
                if (!IsSameID(slots[i].componentID, recipe.Combination[i]))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch) return recipe;
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
        if (resultSlot == null)
        {
            Debug.LogError("[CorrectRecipe] 결과 슬롯이 연결되지 않아 제작을 중단합니다.", this);
            resultPart = null;
            return;
        }

        RecipeData recipe = FindMatchedRecipe();
        if (recipe != null && HasAllComponents())   // 빨간 칸이 하나라도 있으면 제작 불가
            resultPart = resultSlot.FindPart(recipe.PartID);
        else
            resultPart = null;

        _resultRecipe = resultPart != null ? recipe : null;

        resultSlot.SetPart(resultPart);   // null이면 결과 슬롯을 비움
    }

    // 슬롯 9칸 모두 실제로 인벤토리에서 빠진 부품인지 (빨간 칸이 없는지)
    private bool HasAllComponents()
    {
        if (!HasValidSlots())
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Component != null && !slots[i].HasComponent)   // 빈 칸은 검사 대상 아님
                return false;
        }
        return true;
    }

    public void ConfirmCraft()
    {
        if (resultPart == null)
            return;

        int hours = _resultRecipe != null ? _resultRecipe.RequiredTime : 0;
        ConsumeSlots();

        // 결과를 비워야 같은 파츠를 재료 없이 다시 꺼낼 수 없다
        resultPart = null;
        _resultRecipe = null;

        // 레시피의 제작 소요 시간만큼 게임 시간이 흐른다
        if (hours > 0 && TimeSystemManager.Instance != null)
            TimeSystemManager.Instance.PassTime(hours);
    }

    private void ConsumeSlots()
    {
        if (!HasValidSlots())
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].SetComponent(null);
        }
    }
}
