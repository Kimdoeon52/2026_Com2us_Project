using Cysharp.Threading.Tasks;

// [Stage1 랭커4] 장난꾸러기 - 스킬 "연막탄": 8초 동안 물품 이름/가격/등급을 가림. 1회
public class BossNpc4 : BossAiBase
{
    private const float HideSeconds = 8f;

    public override string SkillName => "연막탄";
    public override string SkillLine => "아무것도 안 보이게 만들어 줄게?";
    protected override string[] ThinkLines => new[] { "히히, 뭘 숨겨볼까~", "까꿍!", "심심한데 장난이나 칠까?" };
    protected override string[] RaiseLines => new[] { "{0}G! 맞혀봐~", "{0}G! 히히!", "{0}G~ 놀랐지?" };
    protected override string GiveUpLine => "에이, 재미없어.";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 1);
        RaiseGold = 10;
    }

    protected override bool WantsSkill(Auction m) => m.RemainingTime > 20f && !m.IsInfoHidden;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.HideAuctionInfo(HideSeconds);
        return UniTask.CompletedTask;
    }
}
