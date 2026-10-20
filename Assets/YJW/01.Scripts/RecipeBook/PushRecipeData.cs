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

        if (correctRecipes.Count <= 0) return;
        
        for(int i = 0; i < correctRecipes.Count; i++)
        {
            slots[i].GetComponent<RecipeSlot>().PushData(correctRecipes[i], allParts.Find(correctRecipes[i].PartID).Sprite); 
        }
    }

}
