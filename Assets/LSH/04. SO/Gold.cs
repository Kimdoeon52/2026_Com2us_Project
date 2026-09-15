using UnityEngine;

[CreateAssetMenu(fileName = "Gold", menuName = "Gold/GoldSys")]
[System.Serializable]
public class Gold : ScriptableObject
{
    public string npcName;
    public int gold;
}
