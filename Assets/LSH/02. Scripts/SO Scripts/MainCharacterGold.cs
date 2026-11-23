using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerGold", menuName = "LSH/MainCharacterGold")]
[System.Serializable]
public class MainCharacterGold : ScriptableObject
{
    public int mainCharacterID;
    public string mainCharacterName;
    public int mainCharacterGold;
    public int score;
    public int power;
    public int rank;
    public PartGrade[] parts = new PartGrade[4];

    [Tooltip("경매로 모은 부품과 개수. 같은 부품이 3개 모이면 그 등급으로 장비 한 칸이 강화되고 3개 소모됨")]
    public List<OwnedComponent> components = new List<OwnedComponent>();
}

// 랭커가 가진 부품 1종 (GlobalGold.SaveGame으로 mainGold.json에 함께 저장됨)
[System.Serializable]
public class OwnedComponent
{
    public string id;          // ComponentDefinition.Id
    public string displayName; // 저장 파일에서 알아보기 쉽게 (로직에는 안 씀)
    public PartGrade grade;    // 이 부품의 등급 (3개 모이면 이 등급으로 강화)
    public int count;          // 지금 가지고 있는 개수 (강화에 쓰면 3개씩 줄어듦)
}
