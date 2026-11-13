using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage2 랭커2] 무뚝뚝한 경비대장 (군대식 단답, "~다.", "이상.")
// 스킬 "입찰 봉쇄": 플레이어가 선두일 때 5초간 플레이어 레이즈 잠금 + 바로 레이즈. 1회
// 연출: 강철색 번쩍 + 쿵 카메라 흔들 + 레이즈 버튼 회색 흔들 + 플레이어 초상화 덜덜 + "LOCK" 도장
public class BossNpc7 : BossAiBase
{
    private const float LockSeconds = 5f;

    public override string SkillName => "입찰 봉쇄";
    public override string SkillLine => "거기. 손 내려라. 명령이다.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.DropDown;
    protected override Color SkillColor => new Color(0.45f, 0.55f, 0.7f);
    protected override string[] ThinkLines => new[] { "...", "움직이지 마라.", "수상한 놈이 있군.", "경계 태세 유지.", "이상 없음." };
    protected override string[] RaiseLines => new[] { "{0}G.", "{0}G. 이상.", "{0}G. 확인.", "{0}G. 진입한다." };
    protected override string[] OutbidLines => new[] { "...침입자 발견.", "선을 넘었군.", "경고는 한 번뿐이다." };
    protected override string[] WinLines => new[] { "확보 완료. 이상.", "임무 완수." };
    protected override string GiveUpLine => "...철수한다. 이상.";

    protected override void SetupSkill()
    {
        SetSkillChance(45, 1);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsPlayerLeading(m) && m.RemainingTime < 30f;

    public override UniTask UseSkillAsync(Auction m)
    {
        SkillFx.Flash(SkillColor, 0.3f, 0.5f);
        SkillFx.ShakeCamera(0.4f, 0.4f);
        SkillFx.PulseGraphic(m.raiseButton != null ? m.raiseButton.targetGraphic : null, Color.gray, 20f);
        SpriteRenderer player = m.PlayerPortrait;
        SkillFx.Tremble(player != null ? player.transform : null, 0.05f, 0.6f);
        SkillFx.Stamp("LOCK", SkillColor, 0.8f, 0f).Forget();

        m.LockPlayerRaise(LockSeconds);
        int price = m.CurrentCost + RaiseGold;
        if (CanAffordBid(m, price)) BidAt(m, price); // 묶어두고 앞질러 가기
        return UniTask.CompletedTask;
    }
}
