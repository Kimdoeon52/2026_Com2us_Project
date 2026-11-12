using Cysharp.Threading.Tasks;

// [Stage3 랭커2] 얼음 여왕 - 스킬 "시간 동결": 자기가 선두일 때, 이번 경매 동안 입찰해도 남은 시간이 10초로 돌아가지 않음. 1회
public class BossNpc12 : BossAiBase
{
    public override string SkillName => "시간 동결";
    public override string SkillLine => "이제 아무리 불러도 시간은 늘어나지 않아요.";
    protected override string[] ThinkLines => new[] { "차갑게 끝내죠.", "서두를 필요 없어요.", "모든 건 얼어붙기 마련." };
    protected override string[] RaiseLines => new[] { "{0}G. 이걸로 충분해요.", "{0}G. 우아하게.", "{0}G. 얼어붙으세요." };
    protected override string GiveUpLine => "...녹아버렸네.";

    protected override void SetupSkill()
    {
        SetSkillChance(50, 1);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsLeading(m) && !m.NoTimerRefresh && m.RemainingTime < 40f;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.NoTimerRefresh = true;
        m.ShowErrorMessage("시간 동결! 입찰해도 시간이 늘어나지 않습니다.").Forget();
        return UniTask.CompletedTask;
    }
}
