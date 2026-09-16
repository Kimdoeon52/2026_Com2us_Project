using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/GoldSys")]
[System.Serializable]
public class Gold : ScriptableObject
{
    public int ID;
    public string npcName;
    public int gold;
}
