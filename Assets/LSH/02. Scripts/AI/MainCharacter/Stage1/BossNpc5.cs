using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

// [Stage1 랭커5] 짠돌이 상인 (울먹이는 존댓말, "흑흑", "...요")
// 스킬 "눈물의 흥정": 가격이 원가의 1.2배를 넘으면 20% 깎으면서 자기가 선두가 됨. 1회
// 연출: 내 초상화 꾸벅(납작) + 파란 눈물 번쩍 + 가격표 초록 펀치 + "-20%" 도장
public class BossNpc5 : BossAiBase
{
    public override string SkillName => "눈물의 흥정";
    public override string SkillLine => "흑흑... 너무 비싸요... 조금만 깎아주세요...";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.SlowFade;
    protected override Color SkillColor => new Color(0.4f, 0.7f, 1f);
    protected override string[] ThinkLines => new[] { "비싸... 너무 비싸요...", "계산기가 어디 갔지...", "한 푼이라도 아껴야 해요...", "흑, 통장이 울고 있어요...", "할인 쿠폰 없나요...?" };
    protected override string[] RaiseLines => new[] { "{0}G... 이게 최선이에요.", "{0}G... 눈물이 나요...", "{0}G... 큰맘 먹었어요.", "{0}G... 흑흑." };
    protected override string[] OutbidLines => new[] { "흑... 그렇게까지 하셔야 해요?", "제 저금통이...", "너무해요... 흑흑." };
    protected override string[] WinLines => new[] { "흑흑... 싸게 샀어요...!", "아껴 쓸게요... 소중히..." };
    protected override string GiveUpLine => "이 가격엔 도저히 못 사요...";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 1);
        RaiseGold = 10;
    }

    private int NegoPrice(Auction m) => Mathf.Max(m.stuff != null ? m.stuff.Cost / 2 : 1, Mathf.RoundToInt(m.CurrentCost * 0.8f));

    protected override bool WantsSkill(Auction m)
        => !IsLeading(m) && m.stuff != null && m.CurrentCost > m.stuff.Cost * 1.2f && CanAffordBid(m, NegoPrice(m));

    public override async UniTask UseSkillAsync(Auction m)
    {
        SpriteRenderer me = SeatPortrait;
        if (me != null) // 꾸벅 (납작하게 숙였다가 원래대로)
        {
            Transform t = me.transform;
            float baseY = t.localScale.y;
            await t.DOScaleY(baseY * 0.75f, 0.25f).SetLink(me.gameObject).AsyncWaitForCompletion();
            t.DOScaleY(baseY, 0.3f).SetEase(Ease.OutBack).SetLink(me.gameObject);
        }
        SkillFx.Flash(SkillColor, 0.5f, 0.35f);
        BidAt(m, NegoPrice(m));
        Color green = new Color(0.3f, 1f, 0.4f);
        SkillFx.PulseText(m.auctionCost, green, 0.5f);
        SkillFx.Stamp("-20%", green, 0.6f, -8f).Forget();
        m.ShowErrorMessage($"가격이 {m.CurrentCost}G로 깎였습니다!").Forget();
    }
}
