using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage2 랭커5] 사기꾼 마술사 (쇼맨십, "레이디스 앤 젠틀맨~", "짠!")
// 스킬 "바꿔치기": 밀리고 있을 때 경매 물품을 다른 물품으로 교체 (가격은 유지 또는 새 시작가). 1회
// 연출: 보라 번쩍 2번 + 받침대 물건이 카드처럼 뒤집히며 교체 + "짜잔!" 도장
public class BossNpc10 : BossAiBase
{
    public override string SkillName => "바꿔치기";
    public override string SkillLine => "레이디스 앤 젠틀맨~ 지금부터 마술을 보여드리죠!";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Spin;
    protected override Color SkillColor => new Color(0.65f, 0.35f, 1f);
    protected override string[] ThinkLines => new[] { "자, 잘 보세요~", "손은 눈보다 빠르죠.", "이 모자 안에 뭐가 있을까요?", "박수는 마지막에 부탁드려요~", "오늘의 관객은 열정적이군요!" };
    protected override string[] RaiseLines => new[] { "{0}G! 박수!", "{0}G! 마법처럼!", "{0}G~ 짠!", "{0}G! 이것이 마술입니다!" };
    protected override string[] OutbidLines => new[] { "오호, 관객이 무대에 올라오셨군요?", "제 마술을 방해하시면 곤란합니다~", "그렇다면 다음 트릭을!" };
    protected override string[] WinLines => new[] { "짜잔~ 사라졌다가 제 손에!", "감사합니다! 앙코르는 없어요~" };
    protected override string GiveUpLine => "오늘의 마술은 여기까지~";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m) => !IsLeading(m) && m.auctionItem != null && m.auctionItem.Count >= 2;

    public override async UniTask UseSkillAsync(Auction m)
    {
        SkillFx.Flash(SkillColor, 0.3f, 0.45f, 2);
        SpriteRenderer lot = m.LotImage;
        PartsDefinition next = null;
        await SkillFx.FlipX(lot != null ? lot.transform : null, () => next = m.SwapAuctionItem(), 0.5f); // 뒤집히는 순간 바뀜
        SkillFx.PulseText(m.auctionName, SkillColor, 0.4f);
        SkillFx.Stamp("짜잔!", SkillColor, 0.7f, 12f).Forget();
        if (next != null) m.ShowErrorMessage($"물품이 [{next.DisplayName}](으)로 바뀌었습니다!").Forget();
    }
}
