using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커3] 성급한 시계상 - 스킬 "초읽기": 자기가 선두일 때 남은 시간을 절반으로 (최소 5초). 1회
public class BossNpc3 : BossAiBase
{
    public override string SkillName => "초읽기";
    public override string SkillLine => "경기 시간이 너무 길지 않아?";
    protected override string[] ThinkLines => new[] { "시간은 금이라고.", "빨리빨리 좀 합시다.", "째깍, 째깍..." };
    protected override string[] RaiseLines => new[] { "{0}G! 자, 끝냅시다!", "{0}G. 시간 없어요.", "{0}G! 다음!" };
    protected override string GiveUpLine => "시간 낭비였군.";

    protected override void SetupSkill()
    {
        SetSkillChance(45, 1);
        RaiseGold = 5;
    }

    protected override bool WantsSkill(Auction m) => IsLeading(m) && m.RemainingTime > 12f;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.RemainingTime = Mathf.Max(5f, m.RemainingTime / 2f);
        m.ShowErrorMessage("남은 시간이 절반으로 줄었습니다!").Forget();
        return UniTask.CompletedTask;
    }
}
