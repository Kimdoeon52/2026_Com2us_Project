using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TextDialog", menuName = "LSH/TextDialog")]
public class TextDialogue : ScriptableObject
{
    [SerializeField] private string[] dialouge;

    Dictionary<string, string[]> dialouges = new Dictionary<string, string[]> { };

    private void AddDailogue(string name, int start, int end)
    {
        dialouges.Add(name, dialouge[start..end]);
    }
}
