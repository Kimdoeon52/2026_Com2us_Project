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
    public override bool RaiseThink(int actionPrise, int actionRealPrise)
    {
       return base.RaiseThink(actionPrise, actionRealPrise);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        return currentPrice >= (int)(baseCost * 5f); //원가의 5배면 아무도 못잡게 크게 걸어버린다.
    }
}
