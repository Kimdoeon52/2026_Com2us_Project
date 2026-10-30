using UnityEngine;

public class BossNpc4 : BossAiBase
{
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 0.7f;

    public override void Start()
    {
        base.Start();
        RaiseGold = 400;
    }
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    {
        return base.RaiseThink(actionPrise, actionRealPrise, stuffGrade);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        return base.IsBigRaiseThink(currentPrice, baseCost);
    }// Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void UseBigSkill(Auction manager)
    {
        //경매 아이템이 안보이는 능력.
        manager.auctionName.text = "메롱 안보이지?";
        manager.auctionCost.text = "얼마일까?";
        manager.auctionGrade.text = "등급은 비밀이야~";
    }
    public override string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "아무것도 안보이게 만들어줄게?";
    }
}
