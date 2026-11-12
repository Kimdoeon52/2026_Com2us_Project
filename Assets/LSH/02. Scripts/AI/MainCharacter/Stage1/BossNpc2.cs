using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

// [Stage1 랭커2] 새침한 아가씨 - 스킬 "강제 퇴장": 선두가 아닌 다른 AI 한 명을 강제로 포기시킴. 1회
public class BossNpc2 : BossAiBase
{
    public override string SkillName => "강제 퇴장";
    public override string SkillLine => "흐응~ 넌 강퇴야~";
    protected override string[] ThinkLines => new[] { "흐응~ 다들 진지하네?", "어머, 저거 예쁘다~", "누구부터 울려볼까~?" };
    protected override string[] RaiseLines => new[] { "{0}G~ 이것도 못 따라와?", "{0}G! 내 거 할래~", "{0}G~ 호호." };
    protected override string GiveUpLine => "흥! 나 안 할래!";

    protected override void SetupSkill()
    {
        SetSkillChance(30, 1);
        RaiseGold = 10;
    }

    // 선두는 빼고 고름 (선두를 쫓아내면 포기한 사람이 낙찰되는 문제 방지)
    protected override bool WantsSkill(Auction m) => ActiveRivals(m, includeWinner: false).Count > 0;

    public override UniTask UseSkillAsync(Auction m)
    {
        List<AllAiBase> targets = ActiveRivals(m, includeWinner: false);
        if (targets.Count == 0) return UniTask.CompletedTask;
        AllAiBase target = targets[Random.Range(0, targets.Count)];
        target.IsGiveUp = true;
        m.AppendConsoleLog($"{target.NPCName}님이 강제로 퇴장당했습니다!");
        return UniTask.CompletedTask;
    }
}
