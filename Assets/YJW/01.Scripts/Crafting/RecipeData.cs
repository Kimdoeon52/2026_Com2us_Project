using UnityEngine;

public class RecipeData : ScriptableObject
{
    [Tooltip("레시피 ID")]
    [SerializeField] private string _recipeID;

    [Tooltip("결과물 SO")]
    [SerializeField] private PartsDefinition _partDefinition;

    public string RecipeID => _recipeID;
    public PartsDefinition PartDefinition => _partDefinition;
}
