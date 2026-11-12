using System.Collections.Generic;
using UnityEngine;

public class PushRecipeData : MonoBehaviour
{
    [SerializeField] private GameObject[] slots;
    [SerializeField] private RecipeData[] recipes;
    private List<RecipeData> correctRecipes = new List<RecipeData>();
    [SerializeField] private PartsCatalog allParts;

    public void CategoryBT(int n)
    {
        PushData(n);
    }

    private void PushData(int _n = 0)
    {
        correctRecipes.Clear();

        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeData recipe = recipes[i];
            int categoryNum = CraftingManager.Instance.SortParts(recipe.PartID);
            if (categoryNum == _n)
                correctRecipes.Add(recipe);
        }

        if (correctRecipes.Count > slots.Length)
            Debug.LogWarning($"레시피 {correctRecipes.Count}개가 슬롯 {slots.Length}칸보다 많아 일부가 안 보임");

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            RecipeSlot slot = slots[i].GetComponent<RecipeSlot>();

            // 이전 탭에서 채운 칸이 남지 않도록 레시피가 없는 칸은 비운다
            if (i >= correctRecipes.Count)
            {
                slot.PushData(null, null);
                continue;
            }

            PartsDefinition part = allParts.Find(correctRecipes[i].PartID);
            slot.PushData(correctRecipes[i], part != null ? part.Sprite : null);
        }
    }

}
