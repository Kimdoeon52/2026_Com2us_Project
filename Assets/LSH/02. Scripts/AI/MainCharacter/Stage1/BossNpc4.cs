using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커4] 장난꾸러기 (반말, "히히", "~지롱", "메롱")
// 스킬 "연막탄": 8초 동안 물품 이름/가격/등급/받침대 물건을 가림. 1회
// 연출: 회색 연기가 화면을 덮었다 걷힘 + 받침대 물건 흔들 + "???" 도장
public class BossNpc4 : BossAiBase
{
    private const float HideSeconds = 8f;

    public override string SkillName => "연막탄";
    public override string SkillLine => "히히, 아무것도 안 보이게 해주지롱~";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Spin;
    protected override Color SkillColor => new Color(0.65f, 0.65f, 0.7f);
    protected override string[] ThinkLines => new[] { "히히, 뭘 숨겨볼까~", "까꿍!", "심심한데 장난이나 칠까?", "메롱~ 못 잡지롱~", "어른들은 너무 진지해~" };
    protected override string[] RaiseLines => new[] { "{0}G! 맞혀봐~", "{0}G! 히히!", "{0}G~ 놀랐지롱?", "{0}G! 장난 아니지롱!" };
    protected override string[] OutbidLines => new[] { "치사해! 다시 할래!", "에잇, 반칙이야!", "히잉, 내 거였는데!" };
    protected override string[] WinLines => new[] { "야호! 내 장난감이다!", "히히, 이겼지롱~" };
    protected override string GiveUpLine => "에이, 재미없어. 안 해!";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 1);
        RaiseGold = 10;
    }

    protected override bool WantsSkill(Auction m) => m.RemainingTime > 20f && !m.IsInfoHidden;

    public override async UniTask UseSkillAsync(Auction m)
    {
        SpriteRenderer lot = m.LotImage;
        SkillFx.Tremble(lot != null ? lot.transform : null, 0.1f, 0.6f);
        SkillFx.Veil(SkillColor, 0.75f, 0.3f, 0.6f, 1.2f).Forget(); // 연기가 확 퍼졌다 걷힘
        await UniTask.Delay(300);
        m.HideAuctionInfo(HideSeconds);
        SkillFx.Stamp("???", Color.white, 0.8f, 8f).Forget();
    }
}
