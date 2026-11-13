using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

// [Stage2 랭커3] 뒷골목 두목 (거친 반말, "어이", "~냐?")
// 스킬 "공포 분위기": 다른 AI들이 겁먹고 포기 (일반 NPC 70%, 보스 25%, 최소 1명). 1회
// 연출: 어두운 핏빛 장막 + 강한 카메라 흔들 + 모든 경쟁자 초상화 덜덜 + "공포" 도장
public class BossNpc8 : BossAiBase
{
    public override string SkillName => "공포 분위기";
    public override string SkillLine => "어이, 거기서 손 드는 놈. 얼굴 기억해 둔다.";
    public override CutsceneStyle CutsceneStyle => CutsceneStyle.Shake;
    protected override Color SkillColor => new Color(0.55f, 0.05f, 0.05f);
    protected override string[] ThinkLines => new[] { "어이, 여기 내 구역인 거 몰라?", "누가 감히...", "손가락 까딱하지 마라.", "뭘 쳐다보냐?", "오늘 기분 별로다." };
    protected override string[] RaiseLines => new[] { "{0}G. 불만 있냐?", "{0}G. 따라올 놈?", "{0}G. 조용히 해라.", "{0}G. 이걸로 끝이다." };
    protected override string[] OutbidLines => new[] { "어이, 지금 나한테 하는 거냐?", "간이 부었구나?", "...이따 뒷골목에서 보자." };
    protected override string[] WinLines => new[] { "봤냐? 이게 내 구역 규칙이다.", "다들 수고했다. 조심히 가라." };
    protected override string GiveUpLine => "...오늘은 봐준다.";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m) => ActiveRivals(m).Count >= 2;

    public override async UniTask UseSkillAsync(Auction m)
    {
        List<AllAiBase> rivals = ActiveRivals(m);
        SkillFx.Veil(SkillColor, 0.6f, 0.2f, 0.6f, 0.8f).Forget();
        SkillFx.ShakeCamera(0.6f, 0.8f);
        foreach (AllAiBase ai in rivals)
        {
            SpriteRenderer p = PortraitOf(ai);
            SkillFx.Tremble(p != null ? p.transform : null, 0.07f, 1f); // 다들 벌벌
        }
        SkillFx.Stamp("공포", new Color(1f, 0.2f, 0.2f), 0.8f, -5f).Forget();
        await UniTask.Delay(700);

        int scared = 0;
        foreach (AllAiBase ai in rivals)
        {
            float chance = ai is BossAiBase ? 0.25f : 0.7f; // 랭커들은 잘 안 쫄음
            if (Random.value < chance) { ai.IsGiveUp = true; scared++; }
        }
        if (scared == 0 && rivals.Count > 0) // 아무도 안 쫄면 한 명은 반드시
        {
            rivals[Random.Range(0, rivals.Count)].IsGiveUp = true;
            scared = 1;
        }
        m.AppendConsoleLog($"겁먹은 {scared}명이 경매를 포기했습니다!");
    }
}
