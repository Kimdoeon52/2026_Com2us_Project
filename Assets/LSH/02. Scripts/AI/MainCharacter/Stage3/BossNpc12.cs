using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage3 랭커2] 얼음 여왕 (차갑고 우아한 존댓말, "~군요", "~하세요")
// 스킬 "시간 동결": 자기가 선두일 때, 이번 경매 동안 입찰해도 남은 시간이 10초로 돌아가지 않음. 1회
// 연출: 하늘색 서리가 천천히 화면을 덮었다 걷힘 + 타임바 하늘색 + 모든 경쟁자 초상화 얼어붙듯 파랗게 + "FREEZE" 도장
public class BossNpc12 : BossAiBase
{
    public override string SkillName => "시간 동결";
    public override string SkillLine => "이제 아무리 외쳐도, 시간은 늘어나지 않아요.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.SlowFade;
    protected override Color SkillColor => new Color(0.55f, 0.85f, 1f);
    protected override string[] ThinkLines => new[] { "차갑게 끝내죠.", "서두를 필요 없어요.", "모든 건 얼어붙기 마련이군요.", "소란스럽군요.", "우아하게 가져가겠어요." };
    protected override string[] RaiseLines => new[] { "{0}G. 이걸로 충분하군요.", "{0}G. 우아하게.", "{0}G. 얼어붙으세요.", "{0}G. 조용히 하세요." };
    protected override string[] OutbidLines => new[] { "...무례하군요.", "제 앞을 막으시겠다고요?", "그 열기, 곧 식혀드리죠." };
    protected override string[] WinLines => new[] { "예상대로군요.", "다들, 몸조심하세요." };
    protected override string GiveUpLine => "...녹아버렸네요.";

    protected override void SetupSkill()
    {
        SetSkillChance(50, 1);
        RaiseGold = 20;
    }

    protected override bool WantsSkill(Auction m) => IsLeading(m) && !m.NoTimerRefresh && m.RemainingTime < 40f;

    public override UniTask UseSkillAsync(Auction m)
    {
        m.NoTimerRefresh = true;

        SkillFx.Veil(SkillColor, 0.5f, 0.8f, 0.5f, 1.2f).Forget();
        if (m.timeBar != null) m.timeBar.color = SkillColor; // 이번 경매 동안 타임바가 얼어붙은 색
        foreach (AllAiBase ai in ActiveRivals(m)) SkillFx.Tint(PortraitOf(ai), SkillColor, 1.5f);
        SkillFx.Tint(m.PlayerPortrait, SkillColor, 1.5f);
        SkillFx.Stamp("FREEZE", SkillColor, 0.9f, 0f).Forget();
        m.ShowErrorMessage("시간 동결! 입찰해도 시간이 늘어나지 않습니다.").Forget();
        return UniTask.CompletedTask;
    }
}
