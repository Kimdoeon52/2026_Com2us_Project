using Cysharp.Threading.Tasks;

// [Stage2 랭커2] 무뚝뚝한 경비대장 - 스킬 "입찰 봉쇄": 플레이어가 선두일 때 5초간 플레이어 레이즈 잠금 + 바로 레이즈. 1회
public class BossNpc7 : BossAiBase
{
    private const float LockSeconds = 5f;

    public override string SkillName => "입찰 봉쇄";
    public override string SkillLine => "손 내려. 잠깐 쉬어라.";
    protected override string[] ThinkLines => new[] { "...", "움직이지 마라.", "수상한 놈이 있군." };
    protected override string[] RaiseLines => new[] { "{0}G.", "{0}G. 이상.", "{0}G. 확인." };
    protected override string GiveUpLine => "...철수한다.";

    protected override void SetupSkill()
    {
        SetSkillChance(45, 1);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsPlayerLeading(m) && m.RemainingTime < 30f;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.LockPlayerRaise(LockSeconds);
        int price = m.CurrentCost + RaiseGold;
        if (CanAffordBid(m, price)) BidAt(m, price); // 묶어두고 앞질러 가기
        return UniTask.CompletedTask;
    }
}
