using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커4] 능글맞은 사기꾼 (능청스러운 말투, "에이~", "진짜예요~")
// 스킬 "가짜 호가": 7초 동안 가격표에 가짜 숫자가 마구 바뀌어 표시됨. 2회
// 연출: 빨강/초록/파랑 글리치 번쩍 연속 + 가격표 덜덜 + 화면 흔들 + "?!" 도장
public class BossNpc14 : BossAiBase
{
    private const float FakeSeconds = 7f;

    public override string SkillName => "가짜 호가";
    public override string SkillLine => "에이~ 그 가격표, 진짜라고 믿으세요?";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Glitch;
    protected override Color SkillColor => new Color(0.3f, 1f, 0.6f);
    protected override string[] ThinkLines => new[] { "믿으셔도 됩니다~", "제가 언제 거짓말 했나요?", "싸게 해드릴게요~ 진짜로.", "에이~ 다들 왜 그렇게 의심하세요?", "이거 진품 맞아요, 진짜예요~" };
    protected override string[] RaiseLines => new[] { "{0}G! 정말이에요!", "{0}G~ 진짜 마지막!", "{0}G! 믿어주세요~", "{0}G! 손해 보는 장사예요~" };
    protected override string[] OutbidLines => new[] { "에이~ 그거 진짜 돈 맞아요?", "어라라~ 이러시면 곤란한데~", "흐흐, 그럼 저도 진심으로~" };
    protected override string[] WinLines => new[] { "에이~ 좋은 거래였죠? 흐흐.", "감사합니다~ 환불은 안 돼요~" };
    protected override string GiveUpLine => "어이쿠, 들켰나? 튀자~";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 2);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => m.RemainingTime > 10f && !m.IsFakePriceActive && !m.IsInfoHidden;

    public override async UniTask UseSkillAsync(Auction m)
    {
        Color[] glitch = { new Color(1f, 0.2f, 0.2f), new Color(0.2f, 1f, 0.4f), new Color(0.3f, 0.5f, 1f) };
        foreach (Color c in glitch)
        {
            SkillFx.Flash(c, 0.12f, 0.35f);
            SkillFx.ShakeCamera(0.15f, 0.1f);
            await UniTask.Delay(110);
        }
        m.ShowFakePrice(FakeSeconds);
        SkillFx.PulseText(m.auctionCost, SkillColor, 0.5f);
        SkillFx.Stamp("?!", SkillColor, 0.6f, 15f).Forget();
    }
}
