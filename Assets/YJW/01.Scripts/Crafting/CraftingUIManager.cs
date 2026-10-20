using UnityEngine;
using UnityEngine.UI;

public class CraftingUIManager : PersistentSingleton<CraftingUIManager>
{
    [SerializeField] private GameObject craftingWindow;
    [SerializeField] private Toggle leftArmToggle;

    [SerializeField] private GameObject shopWindow;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            OpenCraftingWindow();
        if (Input.GetKeyDown(KeyCode.K))
            OpenShopWindow();
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

    private void OpenShopWindow()
    {
        shopWindow.SetActive(true);
    }

    public void CloseShopWindow()
    {
        shopWindow.SetActive(false);
    }
}
