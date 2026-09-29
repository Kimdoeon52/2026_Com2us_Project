using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/NpcGoldSys")]
[System.Serializable]
public class NpcGold : ScriptableObject 
{
    public int npcID;
    public string npcName;
    public int npcGold;
}
