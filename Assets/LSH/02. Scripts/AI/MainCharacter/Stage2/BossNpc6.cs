using Cysharp.Threading.Tasks;

// [Stage2 랭커1] 탐욕스런 세무관 - 스킬 "경매세 징수": 플레이어가 선두일 때, 이번 경매 동안 플레이어 레이즈 비용 +20G. 2회(중첩)
public class BossNpc6 : BossAiBase
{
    private const int TaxGold = 20;

    public override string SkillName => "경매세 징수";
    public override string SkillLine => "레이즈하실 때마다 세금 좀 걷겠습니다.";
    protected override string[] ThinkLines => new[] { "모든 거래는 과세 대상이죠.", "장부를 확인해볼까...", "탈세는 용납 못 합니다." };
    protected override string[] RaiseLines => new[] { "{0}G. 영수증 끊어드리죠.", "{0}G. 세금 포함입니다.", "{0}G, 정산 완료." };
    protected override string GiveUpLine => "...장부엔 손해로 적어두지.";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 2);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsPlayerLeading(m);

    public override UniTask UseSkillAsync(Auction m)
    {
        m.PlayerRaiseTax += TaxGold;
        m.ShowErrorMessage($"경매세! 레이즈 비용이 {m.PlayerRaiseStep}G가 되었습니다.").Forget();
        return UniTask.CompletedTask;
    }
}
