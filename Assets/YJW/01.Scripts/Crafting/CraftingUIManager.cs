using UnityEngine;
using UnityEngine.UI;

public class CraftingUIManager : Singleton<CraftingUIManager>
{
    [SerializeField] private GameObject craftingWindow;
    [SerializeField] private Toggle leftArmToggle;
    [SerializeField] private RectTransform inventoryRoot;
    [SerializeField] private Vector2 inventoryPosition = new Vector2(-581f, 0f);

    [SerializeField] private GameObject shopWindow;
    [SerializeField] private GameObject fixWindow;

    // 씬에 제작창을 켜 둔 채로 시작하면 창의 OnEnable이 이 싱글톤보다 먼저 돌 수 있어 한 번 더 맞춘다
    private void Start()
    {
        if (craftingWindow != null && craftingWindow.activeInHierarchy)
            ShowInventory(true);
    }

    // 제작창이 켜지고 꺼질 때 CraftingWindowInventory가 부른다
    // 제작창만 열어도 재료를 끌어올 수 있게 그리드를 왼쪽 공간에 같이 켠다
    public void ShowInventory(bool show)
    {
        if (inventoryRoot == null) return;

        if (show)
            inventoryRoot.anchoredPosition = inventoryPosition;
        inventoryRoot.gameObject.SetActive(show);
    }

    private void OpenCraftingWindow()
    {
        craftingWindow.SetActive(true);
        leftArmToggle.isOn = true;
    }

    public void CloseCraftingWindow()
    {
        craftingWindow.SetActive(false);
        CraftingManager.Instance.AllComponentReturn();
    }

    public void CloseShopWindow()
    {
        shopWindow.SetActive(false);
    }

    private void OpenFixWindow()
    {
        fixWindow.SetActive(true);
    }

    public void CloseFixWindow()
    {
        fixWindow.SetActive(false);
    }
}
