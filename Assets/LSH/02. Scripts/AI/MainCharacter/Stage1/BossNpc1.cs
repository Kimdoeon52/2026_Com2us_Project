using UnityEngine;

//보스 스킬: 현재 물건 가격보다 5배 비싼거라면 한번에 크게 거는 보스.
public class BossNpc1 : BossAiBase
{ //보스 성격 만들기
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 0.7f;

    public override void Start()
    {
        base.Start();
        RaiseGold = 200; 
    }
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    {
        return base.RaiseThink(actionPrise, actionRealPrise, stuffGrade);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        return base.IsBigRaiseThink(currentPrice, baseCost);
    }
    public override void UseBigSkill(Auction manager)
    {
        manager.CurrentCost += 1000;
    }
    public override string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "그렇게 조금 걸어서 되겠어?";
    }
    protected override void HandleGiveUp()
    {
        ShowThinkChatWithText("쳇 난 포기 하겠어.");
    }
}