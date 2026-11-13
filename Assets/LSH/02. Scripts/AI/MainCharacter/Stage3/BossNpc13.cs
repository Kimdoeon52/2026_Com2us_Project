using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커3] 저격수 (말수 적음, "...", "표적 확인.")
// 스킬 "막판 저격": 종료 8초 전 밀리고 있으면 1G 더 얹어 선두 탈취 (시간은 안 늘려줌). 2회
// 막판을 노리기 때문에 15초 미만부터는 고민 시간이 짧아짐
// 연출: 화면이 어두워지며 강한 줌 인 → "◎" 조준 도장 → 탕! 흰 번쩍 + 카메라 반동
public class BossNpc13 : BossAiBase
{
    public override string SkillName => "막판 저격";
    public override string SkillLine => "...조준.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.SlowFade;
    protected override Color SkillColor => new Color(0.9f, 0.1f, 0.1f);
    protected override string[] ThinkLines => new[] { "...", "기다린다.", "아직이다.", "숨을 고르고...", "바람은... 없군." };
    protected override string[] RaiseLines => new[] { "{0}G.", "{0}G. 명중.", "{0}G. 표적 확인.", "{0}G. ...." };
    protected override string[] OutbidLines => new[] { "...움직였군.", "표적 이탈. 재조준.", "..." };
    protected override string[] WinLines => new[] { "임무 완료.", "...한 발이면 충분했다." };
    protected override string GiveUpLine => "...표적을 놓쳤군.";

    public override int ThinkDelay
        => Auction.Instance != null && Auction.Instance.RemainingTime < 15f ? Random.Range(1500, 3000) : base.ThinkDelay;

    public override bool SkillRefreshesTimer => false;

    protected override void SetupSkill()
    {
        SetSkillChance(70, 2);
        RaiseGold = 10;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && m.RemainingTime < 8f && CanAffordBid(m, m.CurrentCost + 1);

    public override async UniTask UseSkillAsync(Auction m)
    {
        SkillFx.Veil(Color.black, 0.5f, 0.2f, 0.4f, 0.2f).Forget(); // 숨죽인 조준
        SkillFx.PunchZoom(-14f, 1f);
        await SkillFx.Stamp("◎", SkillColor, 0.3f, 0f);

        BidAt(m, m.CurrentCost + 1);
        ShowThinkChatWithText($"{m.CurrentCost}G. 탕.");
        SkillFx.Flash(Color.white, 0.25f, 0.8f); // 총구 섬광
        SkillFx.ShakeCamera(0.5f, 0.3f);
        SkillFx.PulseText(m.auctionCost, SkillColor, 0.5f);
    }
}
