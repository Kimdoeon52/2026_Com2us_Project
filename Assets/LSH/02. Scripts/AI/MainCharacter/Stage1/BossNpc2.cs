using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

// [Stage1 랭커2] 새침한 아가씨 (얄미운 존댓말, "호호", "~거든요?")
// 스킬 "강제 퇴장": 선두가 아닌 다른 AI 한 명을 강제로 포기시킴. 1회
// 연출: 분홍 번쩍 + 대상 초상화 덜덜 → 위로 튕겨 빙글 + "OUT!" 도장
public class BossNpc2 : BossAiBase
{
    public override string SkillName => "강제 퇴장";
    public override string SkillLine => "호호, 거기 당신. 나가주시겠어요~?";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.SlideRight;
    protected override Color SkillColor => new Color(1f, 0.45f, 0.75f);
    protected override string[] ThinkLines => new[] { "흐응~ 다들 진지하시네요?", "어머, 저거 예쁘다~", "누구부터 울려볼까요~?", "제 눈에 들면 제 거거든요?", "호호, 땀 흘리는 거 귀엽네요." };
    protected override string[] RaiseLines => new[] { "{0}G~ 이것도 못 따라오세요?", "{0}G! 제 거 할래요~", "{0}G~ 호호호.", "{0}G. 어머, 너무 싸다~" };
    protected override string[] OutbidLines => new[] { "어머? 감히 저를요?", "흥, 기분 상했거든요?", "그거 제가 먼저 찜했는데요?" };
    protected override string[] WinLines => new[] { "호호, 당연히 제 거죠~", "다들 수고하셨어요~ 호호." };
    protected override string GiveUpLine => "흥! 이제 관심 없거든요!";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 10;
    }

    // 선두는 빼고 고름 (선두를 쫓아내면 포기한 사람이 낙찰되는 문제 방지)
    protected override bool WantsSkill(Auction m) => ActiveRivals(m, includeWinner: false).Count > 0;

    public override async UniTask UseSkillAsync(Auction m)
    {
        List<AllAiBase> targets = ActiveRivals(m, includeWinner: false);
        if (targets.Count == 0) return;
        AllAiBase target = targets[Random.Range(0, targets.Count)];

        SpriteRenderer portrait = PortraitOf(target);
        Transform pt = portrait != null ? portrait.transform : null;
        SkillFx.Flash(SkillColor, 0.4f, 0.4f);
        SkillFx.Tremble(pt, 0.06f, 0.5f);
        await UniTask.Delay(500);
        SkillFx.Stamp("OUT!", new Color(1f, 0.2f, 0.4f), 0.6f, 10f).Forget();
        await SkillFx.KickOut(pt);

        target.IsGiveUp = true;
        m.AppendConsoleLog($"{target.NPCName}님이 강제로 퇴장당했습니다!");
    }
}
