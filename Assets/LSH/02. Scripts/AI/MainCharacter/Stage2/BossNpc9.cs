using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage2 랭커4] 끈질긴 노장 (느긋한 어르신 말투, "허허", "~하네, 젊은이")
// 스킬 "연장전": 막판(15초 미만)에 밀리고 있으면 시간 +15초 + 레이즈. 1회
// 연출: 내 초상화 기합 점프 2번 + 타임바 초록 펀치 + 흰 번쩍 + "+15초" 도장
public class BossNpc9 : BossAiBase
{
    private const float ExtraSeconds = 15f;

    public override string SkillName => "연장전";
    public override string SkillLine => "허허, 아직 끝나지 않았네! 시간 좀 더 주게!";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.SlideLeft;
    protected override Color SkillColor => new Color(0.5f, 1f, 0.5f);
    protected override string[] ThinkLines => new[] { "허허, 아직 안 끝났네.", "젊은이들이 성급하구먼.", "버티는 놈이 이기는 법이지.", "이 늙은이, 아직 쌩쌩하다네.", "허리가 좀 쑤시는구먼..." };
    protected override string[] RaiseLines => new[] { "{0}G! 끝까지 가세!", "{0}G! 아직이네!", "{0}G! 한 번 더!", "{0}G. 허허, 어떤가?" };
    protected override string[] OutbidLines => new[] { "허허, 제법이구먼, 젊은이.", "이 늙은이를 우습게 보면 곤란하네.", "좋아, 기운이 나는구먼!" };
    protected override string[] WinLines => new[] { "허허, 끈기가 이긴 거라네.", "젊은이들, 다음엔 더 버텨보게." };
    protected override string GiveUpLine => "허허... 이번엔 내가 졌구먼.";

    protected override void SetupSkill()
    {
        SetSkillChance(55, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m)
        => !IsLeading(m) && m.RemainingTime < 15f && CanAffordBid(m, m.CurrentCost + RaiseGold);

    public override UniTask UseSkillAsync(Auction m)
    {
        SpriteRenderer me = SeatPortrait;
        SkillFx.Hop(me != null ? me.transform : null, 0.35f, 2, 0.6f);
        SkillFx.Flash(Color.white, 0.4f, 0.4f);
        SkillFx.PulseGraphic(m.timeBar, SkillColor, 8f);
        SkillFx.Stamp($"+{ExtraSeconds:0}초", SkillColor, 0.7f, 6f).Forget();

        m.RemainingTime += ExtraSeconds;
        BidAt(m, m.CurrentCost + RaiseGold);
        m.ShowErrorMessage($"연장전! 시간 +{ExtraSeconds:0}초").Forget();
        return UniTask.CompletedTask;
    }
}
