using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PushRecipeData : MonoBehaviour
{
    [SerializeField] private GameObject[] slots;
    [SerializeField] private RecipeData[] recipes;
    private List<string> Parts;

    public void CategoryBT(int n)
    {
        PushData(n);
    }

    private void PushData(int _n)
    {
        for(int i = 0; i < recipes.Length; i++)
        {
            string partID = recipes[i].PartID;
            int categoryNum = CraftingManager.Instance.SortParts(partID);
            if (categoryNum == _n)
                Parts.Add(partID);
        }

        if (Parts.Count <= 0) return;

        for(int i = 0; i < Parts.Count; i++)
        {
            //slots[i].GetComponent<RecipeSlot>().data 
        }
    }

}
