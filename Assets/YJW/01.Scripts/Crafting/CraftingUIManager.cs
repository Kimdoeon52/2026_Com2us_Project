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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            OpenCraftingWindow();
        if (Input.GetKeyDown(KeyCode.K))
            OpenShopWindow();
        if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.LeftShift))
            OpenFixWindow();
    }

    private void OpenCraftingWindow()
    {
        // 제작창만 열어도 재료를 끌어올 수 있게 그리드를 왼쪽 공간에 같이 켠다
        if (inventoryRoot != null)
        {
            inventoryRoot.anchoredPosition = inventoryPosition;
            inventoryRoot.gameObject.SetActive(true);
        }

        craftingWindow.SetActive(true);
        leftArmToggle.isOn = true;
    }

    public void CloseCraftingWindow()
    {
        craftingWindow.SetActive(false);
        CraftingManager.Instance.AllComponentReturn();

        if (inventoryRoot != null)
            inventoryRoot.gameObject.SetActive(false);
    }

    private void OpenShopWindow()
    {
        shopWindow.SetActive(true);
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
