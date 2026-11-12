using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커3] 저격수 - 스킬 "막판 저격": 종료 8초 전 밀리고 있으면 1G 더 얹어 선두 탈취 (시간은 안 늘려줌). 2회
// 막판을 노리기 때문에 15초 미만부터는 고민 시간이 짧아짐
public class BossNpc13 : BossAiBase
{
    public override string SkillName => "막판 저격";
    public override string SkillLine => "조준... 발사.";
    protected override string[] ThinkLines => new[] { "기다린다...", "아직이다.", "숨을 고르고..." };
    protected override string[] RaiseLines => new[] { "{0}G.", "{0}G. 명중.", "{0}G. 표적 확인." };
    protected override string GiveUpLine => "표적을 놓쳤군.";

    public override int ThinkDelay
        => Auction.Instance != null && Auction.Instance.RemainingTime < 15f ? Random.Range(1500, 3000) : base.ThinkDelay;

    public override bool SkillRefreshesTimer => false;

    protected override void SetupSkill()
    {
        SetSkillChance(70, 2);
        RaiseGold = 10;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && m.RemainingTime < 8f && CanAffordBid(m, m.CurrentCost + 1);

    public override UniTask UseSkillAsync(Auction m)
    {
        BidAt(m, m.CurrentCost + 1);
        ShowThinkChatWithText($"{m.CurrentCost}G. 끝이다.");
        return UniTask.CompletedTask;
    }
}
