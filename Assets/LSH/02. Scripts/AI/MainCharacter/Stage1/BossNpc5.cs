using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커5] 짠돌이 상인 - 스킬 "눈물의 흥정": 가격이 원가의 1.2배를 넘으면 20% 깎으면서 자기가 선두가 됨. 1회
public class BossNpc5 : BossAiBase
{
    public override string SkillName => "눈물의 흥정";
    public override string SkillLine => "너무 비싸요... 네고 가능한가요...?";
    protected override string[] ThinkLines => new[] { "비싸... 너무 비싸...", "계산기가 어디 갔지...", "한 푼이라도 아껴야..." };
    protected override string[] RaiseLines => new[] { "{0}G... 이게 최선이에요.", "{0}G... 눈물이 나네요.", "{0}G... 큰맘 먹었어요." };
    protected override string GiveUpLine => "이 가격엔 못 사요...";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 1);
        RaiseGold = 10;
    }

    private int NegoPrice(Auction m) => Mathf.Max(m.stuff != null ? m.stuff.Cost / 2 : 1, Mathf.RoundToInt(m.CurrentCost * 0.8f));

    protected override bool WantsSkill(Auction m)
        => !IsLeading(m) && m.stuff != null && m.CurrentCost > m.stuff.Cost * 1.2f && CanAffordBid(m, NegoPrice(m));

    public override UniTask UseSkillAsync(Auction m)
    {
        BidAt(m, NegoPrice(m));
        m.ShowErrorMessage($"가격이 {m.CurrentCost}G로 깎였습니다!").Forget();
        return UniTask.CompletedTask;
    }
}
