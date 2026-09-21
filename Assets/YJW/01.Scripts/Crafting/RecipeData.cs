using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "YJW/크래프팅/조합법", fileName = "Recipe")]
public class RecipeData : ScriptableObject
{
    [Tooltip("레시피 ID")]
    [SerializeField] private string _recipeID;

    [Tooltip("결과물 SO")]
    [SerializeField] private string _partID;

    [Tooltip("조합법")]
    [SerializeField] string[] _combination = new string[9];

    public string RecipeID => _recipeID;
    public string PartID => _partID;
    public string[] Combination => _combination;

    private void OnValidate()
    {
        if (_combination == null || _combination.Length != 9)
            System.Array.Resize(ref _combination, 9);
    }

}
