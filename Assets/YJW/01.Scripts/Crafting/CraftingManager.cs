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

    public int SortParts(string partID)
    {
        string number = partID.Split('_')[^1];

        switch (number)
        {
            case "001" or "002" or "003":   // 왼쪽 팔
                return 1;
            case "004" or "005" or "006":   // 오른쪽 팔
                return 2;
            case "007" or "008" or "009":   // 왼쪽 다리
                return 3;
            case "010" or "011" or "012":   // 오른쪽 다리
                return 4;

        }
        return 0;
    }
}
