using UnityEngine;

// 제작창 루트에 붙인다. 창이 켜지고 꺼질 때 인벤토리도 같이 켜고 끈다
// 열기 함수를 거치지 않고 창을 직접 켜도(인스펙터 체크 등) 인벤토리가 따라온다
public class CraftingWindowInventory : MonoBehaviour
{
    private void OnEnable()
    {
        if (CraftingUIManager.Instance != null)
            CraftingUIManager.Instance.ShowInventory(true);
    }

    private void OnDisable()
    {
        if (CraftingUIManager.Instance != null)
            CraftingUIManager.Instance.ShowInventory(false);
    }
}
