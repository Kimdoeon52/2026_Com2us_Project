using Cysharp.Threading.Tasks;

// [Stage2 랭커4] 끈질긴 노장 - 스킬 "연장전": 막판(15초 미만)에 밀리고 있으면 시간 +15초 + 레이즈. 1회
public class BossNpc9 : BossAiBase
{
    private const float ExtraSeconds = 15f;

    public override string SkillName => "연장전";
    public override string SkillLine => "포기란 없다! 시간 더 줘!";
    protected override string[] ThinkLines => new[] { "아직 안 끝났어.", "젊은 것들이 성급하군.", "버티는 놈이 이기는 법." };
    protected override string[] RaiseLines => new[] { "{0}G! 끝까지 간다!", "{0}G! 아직이다!", "{0}G! 한 번 더!" };
    protected override string GiveUpLine => "...이번엔 내가 졌군.";

    protected override void SetupSkill()
    {
        SetSkillChance(55, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m)
        => !IsLeading(m) && m.RemainingTime < 15f && CanAffordBid(m, m.CurrentCost + RaiseGold);

    public override UniTask UseSkillAsync(Auction m)
    {
        m.RemainingTime += ExtraSeconds;
        BidAt(m, m.CurrentCost + RaiseGold);
        m.ShowErrorMessage($"연장전! 시간 +{ExtraSeconds:0}초").Forget();
        return UniTask.CompletedTask;
    }
}
