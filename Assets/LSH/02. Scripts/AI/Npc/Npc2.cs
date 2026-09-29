using UnityEngine;

public class Npc2: NpcAiBase
{
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 0.7f;

    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    {
        if(IsReady || data == null) return false;
        int NextPrice = actionPrise + 100;
        if(NextPrice > data.npcGold * actionRate)
        {
            IsReady = true;
            return false;
        }

        int maxGold = Mathf.FloorToInt(data.npcGold * actionRate); //자신이 이번에 배팅할 최대치 설정

        if(NextPrice > maxGold)//자신이 생각한 골드보다 높을 경우 포기
        {
            IsReady = true;
            Debug.Log($"쫄려서 퇴장");
            return false;
        }
        return true; //만약 허용범위면 레이즈 승인
    }
}
