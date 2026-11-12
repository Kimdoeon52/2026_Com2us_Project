using Cysharp.Threading.Tasks;

// [Stage3 랭커4] 능글맞은 사기꾼 - 스킬 "가짜 호가": 7초 동안 가격표에 가짜 숫자가 마구 바뀌어 표시됨. 2회
public class BossNpc14 : BossAiBase
{
    private const float FakeSeconds = 7f;

    public override string SkillName => "가짜 호가";
    public override string SkillLine => "가격표? 그거 진짜 맞아요?";
    protected override string[] ThinkLines => new[] { "믿으셔도 됩니다~", "제가 언제 거짓말 했나요?", "싸게 해드릴게요~ 진짜로." };
    protected override string[] RaiseLines => new[] { "{0}G! 정말이에요!", "{0}G~ 진짜 마지막!", "{0}G! 믿어주세요!" };
    protected override string GiveUpLine => "들켰나? 튀자.";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 2);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => m.RemainingTime > 10f && !m.IsFakePriceActive && !m.IsInfoHidden;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.ShowFakePrice(FakeSeconds);
        return UniTask.CompletedTask;
    }
}
