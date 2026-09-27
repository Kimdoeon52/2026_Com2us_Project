using UnityEngine;

public class CraftingManager : PersistentSingleton<CraftingManager>
{
    [SerializeField] private CombinationSlotDragController[] slots;

    public void AllComponentReturn()
    {
        for(int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].ReturnComponent();
        }
    }
}
