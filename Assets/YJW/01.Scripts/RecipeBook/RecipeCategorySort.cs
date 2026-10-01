using System.Runtime.CompilerServices;
using UnityEngine;

public class RecipeCategorySort : MonoBehaviour
{
    private int SortParts(string partID)
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
