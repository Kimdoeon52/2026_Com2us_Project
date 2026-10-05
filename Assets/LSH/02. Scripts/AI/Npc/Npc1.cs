using UnityEngine;

public class Npc1 : NpcAiBase //후반형
{
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 1f; // 후반형은 올인까지 배팅 시도

    public override void Start()
    {
        base.Start();
        RaiseGold = 200; // 초기 레이즈 금액 설정
    }
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    {
        if(Auction.Instance.remainingTime > 10f) //10초 남았을 때만 시작.
        {
           return false;
        }
        return base.RaiseThink(actionPrise, actionRealPrise);
    }
}
