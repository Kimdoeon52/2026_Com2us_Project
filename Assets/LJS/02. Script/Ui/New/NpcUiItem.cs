using UnityEngine;
using TMPro;
public class NpcUiItem : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI cpText;

    // 매니저의 NPC 데이터를 받아와 UI 텍스트에 적용하는 함수
    public void SetData(NPC npcData)
    {
        rankText.text = npcData.rank.ToString(); //
        nameText.text = npcData.name;            //
        scoreText.text = npcData.score.ToString(); //
        cpText.text = npcData.cp.ToString();       //[cite: 6]
    }
}
