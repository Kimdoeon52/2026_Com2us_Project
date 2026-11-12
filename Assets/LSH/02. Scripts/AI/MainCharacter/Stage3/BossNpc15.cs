using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커5] 경매장의 지배자 - 스킬 "절대 권력": 현재가의 25%(최소 RaiseGold x3)를 얹어 선두가 되고 다른 AI 전원 퇴장. 1회
public class BossNpc15 : BossAiBase
{
    public override string SkillName => "절대 권력";
    public override string SkillLine => "모두 물러나라. 이건 내 것이다.";
    protected override string[] ThinkLines => new[] { "이 경매장의 주인은 나다.", "하찮군.", "감히 나와 겨루겠다고?" };
    protected override string[] RaiseLines => new[] { "{0}G. 감히.", "{0}G. 무릎 꿇어라.", "{0}G. 당연한 결과다." };
    protected override string GiveUpLine => "...흥미를 잃었다.";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 25;
    }

    private int PowerPrice(Auction m) => m.CurrentCost + Mathf.Max(RaiseGold * 3, Mathf.RoundToInt(m.CurrentCost * 0.25f));

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, PowerPrice(m));

    public override UniTask UseSkillAsync(Auction m)
    {
        BidAt(m, PowerPrice(m));
        foreach (AllAiBase ai in ActiveRivals(m)) ai.IsGiveUp = true; // 플레이어만 남김
        m.AppendConsoleLog("다른 AI들이 모두 물러났습니다!");
        return UniTask.CompletedTask;
    }
}
