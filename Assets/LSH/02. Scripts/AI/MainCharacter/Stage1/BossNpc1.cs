using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커1] 거만한 부자 - 스킬 "큰손": 남이 앞서 있으면 현재가의 15%(최소 100G)를 한 번에 얹어 선두 탈환. 2회
public class BossNpc1 : BossAiBase
{
    public override string SkillName => "큰손";
    public override string SkillLine => "그렇게 조금 걸어서 되겠어?";
    protected override string[] ThinkLines => new[] { "돈이면 다 되지.", "흠, 푼돈 싸움이군.", "지갑이 심심하네." };
    protected override string[] RaiseLines => new[] { "{0}G. 껌값이지.", "{0}G! 잔돈은 됐어.", "{0}G. 이 정도야 뭐." };
    protected override string GiveUpLine => "흥, 이딴 고철에 더는 안 써.";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 2);
        RaiseGold = 15;
    }

    private int BigRaisePrice(Auction m) => m.CurrentCost + Mathf.Max(100, Mathf.RoundToInt(m.CurrentCost * 0.15f));

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, BigRaisePrice(m));

    public override UniTask UseSkillAsync(Auction m)
    {
        BidAt(m, BigRaisePrice(m));
        ShowThinkChatWithText($"{m.CurrentCost}G! 확 올려버리지!");
        return UniTask.CompletedTask;
    }
}
