using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/TestStuffSO")]
[System.Serializable]
public class TestStuff : ScriptableObject
{
    public string stuffName;
    public int cost;
}
