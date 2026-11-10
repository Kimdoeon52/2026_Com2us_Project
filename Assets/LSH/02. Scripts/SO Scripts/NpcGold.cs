using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/NpcGoldSys")]
[System.Serializable]
public class NpcGold : ScriptableObject 
{
    public int npcID;
    public string npcName;
    public int npcGold;
    [Tooltip("장착 장비 등급. 경매 물품이 이 중 하나라도 높으면 필요한 물건으로 판단")]
    public PartGrade[] parts = new PartGrade[4];
}
