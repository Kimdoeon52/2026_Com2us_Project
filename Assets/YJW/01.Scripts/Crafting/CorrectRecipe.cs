using UnityEngine;

public class CorrectRecipe : MonoBehaviour
{
    [SerializeField] private RecipeData[] recipeDatas;
    private CombinationSlot[] slots;

    [SerializeField] private ResultSlot resultSlot;
    public PartsDefinition resultPart;

    private void Awake()
    {
        GetSlotDatas();
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

    private void PushPartData()
    {
        string partID = FindRecipe();
        resultPart = partID != null ? resultSlot.FindPart(partID) : null;
        resultSlot.SetPart(resultPart);   // null이면 결과 슬롯을 비움
    }
}
