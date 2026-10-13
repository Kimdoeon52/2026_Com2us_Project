using UnityEngine;

public class PushPartsDataInShop : MonoBehaviour
{
    [SerializeField] private ShopSlot[] slots;

    private bool SortPartGrade(PartsDefinition part, PartGrade grade)
    {
        if (part.Grade == grade) return true;
        return false;
    }

    public void ClickGradeButton(int n)
    {
        PushPartDatas(n);
    }

    private void PushPartDatas(int n)
    {
        PartGrade _grade;
        if (n == 1) _grade = PartGrade.Common;
        else if (n == 2) _grade = PartGrade.Rare;
        else _grade = PartGrade.Epic;

        PartsCatalog catalog = CraftingManager.Instance.catalog;
        int count = 0;
        foreach(var part in catalog.Parts)
        {
            if (SortPartGrade(part, _grade) == true)
            {
                slots[count].SetData(part.DisplayName, part.Grade, part.Sprite, part.Cost, part);
                count++;
            }
            else continue;
        }
    }
}
