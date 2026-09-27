using UnityEngine;

public class CraftingUIManager : PersistentSingleton<CraftingUIManager>
{
    [SerializeField] private GameObject craftingWindow;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            OpenCraftingWindow();
    }

    private void OpenCraftingWindow()
    {
        craftingWindow.SetActive(true);
    }

    public void CloseCraftingWindow()
    {
        craftingWindow.SetActive(false);
        CraftingManager.Instance.AllComponentReturn();
    }
}
