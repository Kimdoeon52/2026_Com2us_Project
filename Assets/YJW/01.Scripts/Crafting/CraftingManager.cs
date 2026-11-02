using UnityEngine;

public class CraftingManager : Singleton<CraftingManager>
{
    public CombinationSlotDragController[] slots;
    public PartsCatalog catalog;
    public ComponentCatalog components;

    public void AllComponentReturn()
    {
        for(int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].ReturnComponent();
        }
    }

    public int SortParts(string partID)
    {
        PartsDefinition part = catalog != null ? catalog.Find(partID) : null;
        if (part == null)
            return 0;

        switch (part.Slot)
        {
            case Slot.LeftArm:      // 왼쪽 팔
                return 1;
            case Slot.RightArm:     // 오른쪽 팔
                return 2;
            case Slot.LeftLeg:      // 왼쪽 다리
                return 3;
            case Slot.RightLeg:     // 오른쪽 다리
                return 4;
        }
        return 0;
    }
}
