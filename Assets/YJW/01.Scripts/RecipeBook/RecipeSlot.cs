using UnityEngine;
using UnityEngine.UI;

public class RecipeSlot : MonoBehaviour
{
    public RecipeData data;
    [SerializeField] private Image image;

    public void PushData(RecipeData recipe, Sprite icon)
    {
        data = recipe;
        image.sprite = icon;
    }
}
