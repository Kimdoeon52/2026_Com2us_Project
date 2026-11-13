using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage2 랭커1] 탐욕스런 세무관 (딱딱한 공문서체, "규정상", "~하십시오")
// 스킬 "경매세 징수": 플레이어가 선두일 때, 이번 경매 동안 플레이어 레이즈 비용 +20G. 2회(중첩)
// 연출: 진녹색 번쩍 + 플레이어 초상화 빨갛게 + 레이즈 버튼 흔들 + "TAX +20G" 도장
public class BossNpc6 : BossAiBase
{
    private const int TaxGold = 20;

    public override string SkillName => "경매세 징수";
    public override string SkillLine => "규정상, 지금부터 레이즈마다 세금을 부과하겠습니다.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.DropDown;
    protected override Color SkillColor => new Color(0.2f, 0.65f, 0.35f);
    protected override string[] ThinkLines => new[] { "모든 거래는 과세 대상입니다.", "장부를 확인해 보겠습니다.", "탈세는 용납하지 않습니다.", "영수증은 챙기셨습니까?", "세율 계산 중입니다." };
    protected override string[] RaiseLines => new[] { "{0}G. 영수증 끊어드리죠.", "{0}G. 세금 포함입니다.", "{0}G, 정산 완료.", "{0}G. 규정에 따른 입찰입니다." };
    protected override string[] OutbidLines => new[] { "추가 납부를 확인했습니다. 대응하겠습니다.", "그 금액, 출처가 의심스럽군요.", "세무 조사 대상으로 등록합니다." };
    protected override string[] WinLines => new[] { "압류... 아니, 낙찰 처리하겠습니다.", "정산 완료. 수고하셨습니다." };
    protected override string GiveUpLine => "...장부엔 손해로 기록해 두겠습니다.";

    protected override void SetupSkill()
    {
        SetSkillChance(40, 2);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsPlayerLeading(m);

    public override UniTask UseSkillAsync(Auction m)
    {
        m.PlayerRaiseTax += TaxGold;

        SkillFx.Flash(SkillColor, 0.4f, 0.4f);
        SkillFx.Tint(m.PlayerPortrait, new Color(1f, 0.3f, 0.3f), 1f);
        SkillFx.PulseGraphic(m.raiseButton != null ? m.raiseButton.targetGraphic : null, SkillColor, 12f);
        SkillFx.Stamp($"TAX +{TaxGold}G", SkillColor, 0.8f, -12f).Forget();
        m.ShowErrorMessage($"경매세! 레이즈 비용이 {m.PlayerRaiseStep}G가 되었습니다.").Forget();
        return UniTask.CompletedTask;
    }
}
