using Cysharp.Threading.Tasks;

// [Stage2 랭커5] 사기꾼 마술사 - 스킬 "바꿔치기": 밀리고 있을 때 경매 물품을 다른 물품으로 교체 (가격은 유지 또는 새 시작가). 1회
public class BossNpc10 : BossAiBase
{
    public override string SkillName => "바꿔치기";
    public override string SkillLine => "짜잔~ 물건이 바뀌었습니다!";
    protected override string[] ThinkLines => new[] { "자, 잘 보세요~", "손은 눈보다 빠르죠.", "이 모자 안에 뭐가 있을까요?" };
    protected override string[] RaiseLines => new[] { "{0}G! 박수!", "{0}G! 마법처럼!", "{0}G~ 짠!" };
    protected override string GiveUpLine => "마술은 여기까지~";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && m.auctionItem != null && m.auctionItem.Count >= 2;

    public override UniTask UseSkillAsync(Auction m)
    {
        PartsDefinition next = m.SwapAuctionItem();
        if (next != null) m.ShowErrorMessage($"물품이 [{next.DisplayName}](으)로 바뀌었습니다!").Forget();
        return UniTask.CompletedTask;
    }
}
