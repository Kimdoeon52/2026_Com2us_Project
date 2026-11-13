using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커1] 거만한 부자 (반말, 돈 자랑, "훗")
// 스킬 "큰손": 남이 앞서 있으면 현재가의 15%(최소 100G)를 한 번에 얹어 선두 탈환. 2회
// 연출: 금빛 번쩍 + 줌 인 + 가격표 금색 펀치 + "+n G" 금색 도장
public class BossNpc1 : BossAiBase
{
    public override string SkillName => "큰손";
    public override string SkillLine => "훗, 그렇게 쪼잔하게 걸어서 되겠어?";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.ZoomPunch;
    protected override Color SkillColor => new Color(1f, 0.82f, 0.15f);
    protected override string[] ThinkLines => new[] { "돈이면 다 되지.", "훗, 푼돈 싸움이군.", "지갑이 심심하네.", "이 정도 고철, 용돈으로도 사지.", "다들 가난해 보여서 안쓰럽군." };
    protected override string[] RaiseLines => new[] { "{0}G. 껌값이지.", "{0}G! 잔돈은 됐어.", "{0}G. 훗, 이 정도야 뭐.", "{0}G. 영수증은 필요 없어." };
    protected override string[] OutbidLines => new[] { "호오, 나한테 덤빈다고?", "푼돈 좀 있나 보지?", "건방지군. 지갑 꺼낸다." };
    protected override string[] WinLines => new[] { "역시 돈이 최고지. 훗.", "포장해서 집으로 보내." };
    protected override string GiveUpLine => "흥, 이딴 고철에 더는 안 써.";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 2);
        RaiseGold = 15;
    }

    private int BigRaisePrice(Auction m) => m.CurrentCost + Mathf.Max(100, Mathf.RoundToInt(m.CurrentCost * 0.15f));

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, BigRaisePrice(m));

    public override UniTask UseSkillAsync(Auction m)
    {
        int before = m.CurrentCost;
        BidAt(m, BigRaisePrice(m));
        ShowThinkChatWithText($"{m.CurrentCost}G! 확 올려버리지!");

        SkillFx.Flash(SkillColor, 0.5f, 0.45f);
        SkillFx.PunchZoom(-8f, 0.6f);
        SkillFx.PulseText(m.auctionCost, SkillColor, 0.6f);
        SkillFx.Stamp($"+{m.CurrentCost - before}G", SkillColor, 0.7f, -6f).Forget();
        return UniTask.CompletedTask;
    }
}
