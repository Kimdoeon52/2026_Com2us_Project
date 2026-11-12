using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

// [Stage2 랭커3] 뒷골목 두목 - 스킬 "공포 분위기": 다른 AI들이 겁먹고 포기 (일반 NPC 70%, 보스 25%, 최소 1명). 1회
public class BossNpc8 : BossAiBase
{
    public override string SkillName => "공포 분위기";
    public override string SkillLine => "여기서 손 드는 놈, 얼굴 기억해 둔다.";
    protected override string[] ThinkLines => new[] { "여기 내 구역인 거 몰라?", "누가 감히...", "손가락 까딱하지 마라." };
    protected override string[] RaiseLines => new[] { "{0}G. 불만 있나?", "{0}G. 따라올 놈?", "{0}G. 조용히 해." };
    protected override string GiveUpLine => "오늘은 봐준다.";

    protected override void SetupSkill()
    {
        SetSkillChance(35, 1);
        RaiseGold = 15;
    }

    protected override bool WantsSkill(Auction m) => ActiveRivals(m).Count >= 2;

    public override UniTask UseSkillAsync(Auction m)
    {
        List<AllAiBase> rivals = ActiveRivals(m);
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
        return UniTask.CompletedTask;
    }
}
