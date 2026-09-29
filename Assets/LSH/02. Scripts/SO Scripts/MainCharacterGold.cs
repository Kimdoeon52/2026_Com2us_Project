using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/GoldSys")]
[System.Serializable]
public class MainCharacterGold : ScriptableObject
{
    public int mainCharacterID;
    public string mainCharacterName;
    public int mainCharacterGold;
}
