using UnityEngine;

//보스 스킬: 50% 확률로 1000골드 올리기. 2회
public class BossNpc1 : BossAiBase
{ //보스 성격 만들기
    [Range(0.1f, 1f)]
    [Tooltip("자신이 골드 %까지 베팅에 사용할지.")]
    public float actionRate = 0.7f;
    [SerializeField] private int bigRaiseGold = 100; //큰 레이즈가 생기면 얼마를 올릴 것인지.
    public override void Start()
    {
        base.Start();
        SetSkillChance(30, 3); //스킬 확률과 횟수 설정
        RaiseGold = 15; 
    }
    public override bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade)
    {
        return base.RaiseThink(actionPrise, actionRealPrise, stuffGrade);
    }
    public override bool IsBigRaiseThink(int currentPrice, int baseCost)
    {
        if(!GlobalGold.Instance.CanUseGold(NPCID, currentPrice + bigRaiseGold))
        {
            return false;
        }
        return base.IsBigRaiseThink(currentPrice, baseCost);
    }
    public override void UseBigSkill(Auction manager)
    {
        manager.CurrentCost += bigRaiseGold;
        manager.WinnerID = NPCID; //큰 레이즈를 사용하면 승자
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