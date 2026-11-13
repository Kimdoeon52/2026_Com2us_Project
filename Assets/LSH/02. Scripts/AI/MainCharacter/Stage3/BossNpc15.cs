using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

// [Stage3 랭커5] 경매장의 지배자 (고압적인 위엄, "감히", "물러나라")
// 스킬 "절대 권력": 현재가의 25%(최소 RaiseGold x3)를 얹어 선두가 되고 다른 AI 전원 퇴장. 1회
// 연출: 어둠이 깔림 → 경쟁자 초상화 전원 덜덜 → 보라 번쩍 + 최강 카메라 흔들 + 줌 인 + "절대 권력" 도장
public class BossNpc15 : BossAiBase
{
    public override string SkillName => "절대 권력";
    public override string SkillLine => "모두 물러나라. 이 경매장의 주인이 말한다.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.ZoomPunch;
    protected override Color SkillColor => new Color(0.6f, 0.2f, 0.9f);
    protected override string[] ThinkLines => new[] { "이 경매장의 주인은 나다.", "하찮군.", "감히 나와 겨루겠다고?", "지루하군. 빨리 끝내라.", "모든 것은 결국 내 손에 들어온다." };
    protected override string[] RaiseLines => new[] { "{0}G. 감히.", "{0}G. 무릎 꿇어라.", "{0}G. 당연한 결과다.", "{0}G. 이것이 격의 차이다." };
    protected override string[] OutbidLines => new[] { "...감히 내 위에 서려 하는가.", "흥미롭군. 짓밟아주지.", "분수를 모르는군." };
    protected override string[] WinLines => new[] { "처음부터 내 것이었다.", "기억해라. 이곳의 주인을." };
    protected override string GiveUpLine => "...흥미를 잃었다. 가져가라.";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 25;
    }

    private int PowerPrice(Auction m) => m.CurrentCost + Mathf.Max(RaiseGold * 3, Mathf.RoundToInt(m.CurrentCost * 0.25f));

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, PowerPrice(m));

    public override async UniTask UseSkillAsync(Auction m)
    {
        List<AllAiBase> rivals = ActiveRivals(m);
        SkillFx.Veil(Color.black, 0.7f, 0.4f, 0.5f, 0.6f).Forget();
        foreach (AllAiBase ai in rivals)
        {
            SpriteRenderer p = PortraitOf(ai);
            SkillFx.Tremble(p != null ? p.transform : null, 0.08f, 1f);
        }
        await UniTask.Delay(600);

        BidAt(m, PowerPrice(m));
        foreach (AllAiBase ai in rivals) ai.IsGiveUp = true; // 플레이어만 남김

        SkillFx.Flash(SkillColor, 0.6f, 0.6f);
        SkillFx.ShakeCamera(0.8f, 0.7f);
        SkillFx.PunchZoom(-10f, 0.8f);
        SkillFx.PulseText(m.auctionCost, SkillColor, 0.7f);
        SkillFx.Stamp("절대 권력", SkillColor, 1f, 0f).Forget();
        m.AppendConsoleLog("다른 AI들이 모두 물러났습니다!");
    }
}
