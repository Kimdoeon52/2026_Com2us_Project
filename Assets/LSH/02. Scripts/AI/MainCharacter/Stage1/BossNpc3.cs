using UnityEngine;

public class BossNpc3 : BossAiBase
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
    public override string BigRaiseSkillName() //큰 레이즈가 생길때 보스 맨트
    {
        return "저기~ 재밌는거 보여줄까?";
    }
}
