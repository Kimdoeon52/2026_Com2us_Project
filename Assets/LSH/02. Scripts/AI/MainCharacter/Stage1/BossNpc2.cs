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
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    {
        return base.RaiseThink(actionPrise, actionRealPrise);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        return base.IsBigRaiseThink(currentPrice, baseCost);
    }
    public override string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "흐응~ 이것도 잡아보시지?";
    }
}
