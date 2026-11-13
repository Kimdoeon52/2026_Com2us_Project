using Cysharp.Threading.Tasks;
using UnityEngine;

// [Stage1 랭커3] 성급한 시계상 (빠르고 조급한 말투, "빨리빨리!", "째깍")
// 스킬 "초읽기": 자기가 선두일 때 남은 시간을 절반으로 (최소 5초). 1회
// 연출: 째깍째깍 효과음 3번 + 타임바 빨갛게 흔들림 + 주황 번쩍 + "×½" 도장
public class BossNpc3 : BossAiBase
{
    public override string SkillName => "초읽기";
    public override string SkillLine => "빨리빨리! 시간 너무 길잖아!";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Shake;
    protected override Color SkillColor => new Color(1f, 0.55f, 0.1f);
    protected override string[] ThinkLines => new[] { "시간은 금이라고!", "빨리빨리 좀 합시다!", "째깍, 째깍, 째깍...", "아직이야? 아직이야?", "1초가 아깝다니까!" };
    protected override string[] RaiseLines => new[] { "{0}G! 자, 끝냅시다!", "{0}G! 빨리!", "{0}G! 다음! 다음!", "{0}G! 고민할 시간 없어!" };
    protected override string[] OutbidLines => new[] { "으악, 시간 끄는 놈이 있어!", "빨리 끝내려 했는데!", "아 진짜, 째깍째깍!" };
    protected override string[] WinLines => new[] { "끝! 칼퇴근이다!", "역시 빠른 게 최고야!" };
    protected override string GiveUpLine => "시간 낭비였어! 난 간다!";

    protected override void SetupSkill()
    {
        SetSkillChance(45, 1);
        RaiseGold = 5;
    }

    protected override bool WantsSkill(Auction m) => IsLeading(m) && m.RemainingTime > 12f;

    public override async UniTask UseSkillAsync(Auction m)
    {
        for (int i = 0; i < 3; i++) // 째깍 째깍 째깍
        {
            m.PlaySound(m.clickSoundEffect);
            SkillFx.PulseGraphic(m.timeBar, Color.red, 10f);
            await UniTask.Delay(220);
        }
        m.RemainingTime = Mathf.Max(5f, m.RemainingTime / 2f);
        SkillFx.Flash(SkillColor, 0.4f, 0.4f);
        SkillFx.Stamp("×½", SkillColor, 0.6f, -10f).Forget();
        m.ShowErrorMessage("남은 시간이 절반으로 줄었습니다!").Forget();
    }
}
