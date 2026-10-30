using System.Collections.Generic;
using UnityEngine;

public class BossNpc2 : BossAiBase
{
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 0.4f;

    public override void Start()
    {
        base.Start();
        RaiseGold = 300;
    }
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    {
        return base.RaiseThink(actionPrise, actionRealPrise, stuffGrade);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        return base.IsBigRaiseThink(currentPrice, baseCost);
    }
    public override string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "흐응~ 넌 강퇴야~";
    }
    public override void UseBigSkill(Auction manager)
    {
        AllAiBase target;
        int randomIndex = Random.Range(0, manager.auctionAiList.Count);
        target = manager.auctionAiList[randomIndex];
        if(target.NPCName == data.mainCharacterName)
        {
            return;
        }
        target.IsGiveUp = true; // 타겟을 강제로 포기하게 만들어버림
    }
    protected override void HandleGiveUp()
    {
        ShowThinkChatWithText("흥! 나 안할래!");
    }
}
