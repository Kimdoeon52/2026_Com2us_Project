using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/MainCharacterGold")]
[System.Serializable]
public class MainCharacterGold : ScriptableObject
{
    public string mainCharacterName;
    public int mainCharacterGold;
    public int score;
    public int power;
    public int rank;
    public PartGrade[] parts = new PartGrade[4];
}
