using Cysharp.Threading.Tasks;

// [Stage3 랭커1] 광전사 - 스킬 "광란의 연타": 밀리고 있으면 레이즈를 3번 연속으로 몰아침 (한 번에 RaiseGold x2). 2회
public class BossNpc11 : BossAiBase
{
    private const int ComboCount = 3;

    public override string SkillName => "광란의 연타";
    public override string SkillLine => "멈출 수 없다! 연속 레이즈!";
    protected override string[] ThinkLines => new[] { "더! 더!", "피가 끓는군!", "크하하! 재밌어지는데!" };
    protected override string[] RaiseLines => new[] { "{0}G!! 하하하!", "{0}G! 덤벼라!", "{0}G!! 아직 부족해!" };
    protected override string GiveUpLine => "크윽... 숨차군.";

    private int Step => RaiseGold * 2;

    protected override void SetupSkill()
    {
        SetSkillChance(35, 2);
        RaiseGold = 25;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, m.CurrentCost + Step * ComboCount);

    public override async UniTask UseSkillAsync(Auction m)
    {
        for (int i = 1; i <= ComboCount; i++)
        {
            BidAt(m, m.CurrentCost + Step);
            m.UpdateAuctionCostUI();
            ShowThinkChatWithText($"{i}연타! {m.CurrentCost}G!");
            m.PlaySound(m.clickSoundEffect);
            if (i < ComboCount) await UniTask.Delay(400);
        }
    }
}
