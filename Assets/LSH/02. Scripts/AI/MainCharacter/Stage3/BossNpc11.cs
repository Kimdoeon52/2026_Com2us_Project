using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커1] 광전사 (전부 느낌표, "크하하!", 흥분)
// 스킬 "광란의 연타": 밀리고 있으면 레이즈를 3번 연속으로 몰아침 (한 번에 RaiseGold x2). 2회
// 연출: 타격마다 붉은 번쩍 + 카메라 쾅 + 가격표 펀치 + 내 초상화 들썩 → 마지막에 "3 COMBO!" 도장
public class BossNpc11 : BossAiBase
{
    private const int ComboCount = 3;

    public override string SkillName => "광란의 연타";
    public override string SkillLine => "크하하하! 멈출 수 없다! 연속 레이즈다!!";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Shake;
    protected override Color SkillColor => new Color(1f, 0.2f, 0.1f);
    protected override string[] ThinkLines => new[] { "더! 더!!", "피가 끓는다!!", "크하하! 재밌어지는데!", "가만히 있으니 몸이 근질거린다!", "누가 덤빌 테냐!!" };
    protected override string[] RaiseLines => new[] { "{0}G!! 크하하!", "{0}G! 덤벼라!!", "{0}G!! 아직 부족해!", "{0}G!!! 더 와라!!" };
    protected override string[] OutbidLines => new[] { "좋아!! 그래야 재밌지!!", "크하하! 반격이다!!", "감히 나를 넘어?!" };
    protected override string[] WinLines => new[] { "크하하하!! 내가 이겼다!!", "최고의 싸움이었다!!" };
    protected override string GiveUpLine => "크윽... 숨차군... 여기까지다!";

    private int Step => RaiseGold * 2;

    protected override void SetupSkill()
    {
        SetSkillChance(35, 2);
        RaiseGold = 25;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && CanAffordBid(m, m.CurrentCost + Step * ComboCount);

    public override async UniTask UseSkillAsync(Auction m)
    {
        SpriteRenderer me = SeatPortrait;
        for (int i = 1; i <= ComboCount; i++)
        {
            BidAt(m, m.CurrentCost + Step);
            m.UpdateAuctionCostUI();
            ShowThinkChatWithText($"{i}연타! {m.CurrentCost}G!");
            m.PlaySound(m.clickSoundEffect);
            SkillFx.Flash(SkillColor, 0.2f, 0.3f);
            SkillFx.ShakeCamera(0.2f + i * 0.1f, 0.25f);
            SkillFx.PulseText(m.auctionCost, SkillColor, 0.3f + i * 0.15f);
            SkillFx.Punch(me != null ? me.transform : null, 0.15f, 0.25f);
            if (i < ComboCount) await UniTask.Delay(400);
        }
        SkillFx.Stamp($"{ComboCount} COMBO!", SkillColor, 0.7f, -14f).Forget();
    }
}
