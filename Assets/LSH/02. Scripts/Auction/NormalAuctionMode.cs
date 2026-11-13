using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class NormalAuctionMode : IAuctionMode //일반 경매 방식
{
    public async UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants) //participants에는 지금 경매중인 ai들이 전달될 것이다.
    {
        manager.CurrentCost = stuff.Cost / 2; // 반값 시작
        manager.UpdateAuctionCostUI(); // 코스트 가격 시각적 업데이트

        manager.IsTimeRunning = true; // 시간 가는거 true로

        foreach (var ai in participants) // 이번 물품에 원가의 몇 배(2~4배)까지 낼지 AI마다 결정
        {
            if (ai != null) ai.RollBidLimit();
        }

        // AI 독립 입찰 루프 가동
        RunAllAiAsync(manager, stuff, participants).Forget();

        // 타이머 카운트다운
        await manager.StartAuctionTimer(); //제한 시간 작동 On
    }

    private async UniTask RunAllAiAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants) 
    {
        List<UniTask> aiTasks = new List<UniTask>(); //각 AI들의 비동기 작업(Task)들을 하나로 담아두기 위한 리스트
        foreach (var ai in participants) //AI경매 참여 인원들
        {
            if (ai != null) 
            {
                aiTasks.Add(AiRoutine(manager, ai, stuff, participants)); // 각 ai루틴하는 애들을 aiTask에 넣기
            }
        }
        await UniTask.WhenAll(aiTasks); //모든 AI의 루틴 작업이 전부 끝날 때까지 이 자리에서 대기
    }

    private async UniTask AiRoutine(Auction manager, AllAiBase ai, PartsDefinition stuff, List<AllAiBase> participants)
    {
        // 각 AI가 다른 시점에 첫 고민을 시작하도록 랜덤 초기 딜레이 (딱 한 번만 실행)
        await UniTask.Delay(Random.Range(500, ai.ThinkDelay));
        bool wasLeading = false; // 직전 고민 때 선두였는지 (역전당하면 보스가 반응 대사)

        while (manager.IsTimeRunning && !manager.IsAuctioningFin)
        {
            if (ai.IsGiveUp)
            {
                await UniTask.Yield(); // 포기 상태에서도 최소 1프레임 대기로 스핀 방지
                continue;
            }
            BossAiBase boss = ai as BossAiBase;
            bool isLeading = manager.WinnerID == ai.NPCID;

            // 이미 최고 입찰자면 레이즈 고민은 안 함. 단, 스킬이 남은 보스는 선두일 때 쓰는 스킬(초읽기 등)을 위해 계속 고민
            if (isLeading && (boss == null || !boss.HasSkillLeft))
            {
                wasLeading = true;
                await UniTask.Yield(); // 무한 루프 방지용 최소 대기
                continue;
            }

            int thinkDelay = ai.ThinkDelay; //각 ai 생각 시간 삽입
            if (!isLeading)
            {
                manager.AppendConsoleLog($"{ai.NPCName}님이 {thinkDelay / 1000}초 동안 고민중.\n"); //콘솔 출력
                if (wasLeading && boss != null) ai.ShowThinkChatWithText(boss.GetOutbidLine()); // 역전당함 → 보스 개성 반응
                else ai.ShowThinkChat(); // ai 고민 말풍선 띄우기
            }
            wasLeading = isLeading;
            await UniTask.Delay(thinkDelay); // thinkDelay 만큼
            await UniTask.WaitWhile(() => manager.IsCutscenePlaying);

            if (!manager.IsTimeRunning || manager.IsAuctioningFin) break;
            if (ai.IsGiveUp) continue; // 고민하는 사이 다른 보스 스킬로 쫓겨남

            // ===== 보스 스킬: 레이즈와 별개로 매 고민마다 조건/확률 체크 =====
            if (boss != null && boss.TryRollSkill(manager))
            {
                await UseBossSkillAsync(manager, boss);
                if (CheckEndCountdown(manager)) break;
                continue;
            }

            if (manager.WinnerID == ai.NPCID) continue; // 선두라면 스킬만 노림

            PartsDefinition item = manager.stuff != null ? manager.stuff : stuff; // 바꿔치기 스킬로 물품이 바뀔 수 있음

            // 공통 판단: 필요 없는 물건이거나 원가 x 2~3배 한도를 넘으면 포기
            if (!ai.PassCommonRaiseRule(manager.CurrentCost, item.Cost, item.Grade))
            {
                ai.IsGiveUp = true;
            }

            if (!ai.IsGiveUp && ai.RaiseThink(manager.CurrentCost, item.Cost, item.Grade))
            {
                manager.CurrentCost += ai.RaiseGold;
                manager.UpdateAuctionCostUI();

                // 레이즈 성공 말풍선 연출 (보스는 개성 대사)
                ai.ShowThinkChatWithText(ai.GetRaiseLine(manager.CurrentCost));
                ApplyAiBid(manager, ai);
            }
            else if (ai.IsGiveUp) // 진짜 포기한 경우만 (후반형처럼 관망하며 false를 돌려준 건 포기 아님)
            {
                manager.AppendConsoleLog($"{ai.NPCName}판단 끝! 결과 포기.\n");
                manager.PlaySound(manager.giveUpEffect); // 진행자 대사 없이 효과음 + 반투명 처리로만 표현
            }
            else
            {
                manager.AppendConsoleLog($"{ai.NPCName}님은 이번엔 지켜봅니다.\n");
            }

            if (CheckEndCountdown(manager)) break; // 이 AI 루틴은 종료, 타이머가 자연스럽게 5초 후 경매 마감
        }
    }

    private bool CheckEndCountdown(Auction manager) //모두가 포기하면 낙찰 카운트다운 돌입
    {
        if (!manager.CheckAllGiveUp()) return false;
        manager.AppendConsoleLog("더 이상 입찰자가 없습니다! 낙찰까지 5초!");
        if (manager.RemainingTime > 5f)
            manager.RemainingTime = 5f; // 타이머를 5초로 고정 (즉시 종료 X)
        return true;
    }

    // 보스 스킬: 대사 → 컷씬(타이머 정지) → 효과 → 가격/선두가 바뀌었으면 입찰로 인정
    private async UniTask UseBossSkillAsync(Auction manager, BossAiBase boss)
    {
        int beforeCost = manager.CurrentCost;
        int beforeWinner = manager.WinnerID;

        boss.ShowThinkChatWithText(boss.SkillLine);
        manager.PlaySound(manager.useSkillEffect);
        manager.AppendConsoleLog($"[스킬] {boss.NPCName}의 <{boss.SkillName}>!");
        await boss.TriggerCutscene(boss.NPCName, boss.SkillName);
        if (!manager.IsTimeRunning || manager.IsAuctioningFin) return;

        boss.PlaySkillIntroFx(); // 내 초상화 기합 연출
        await boss.UseSkillAsync(manager); // 효과 + 보스별 전용 연출
        manager.UpdateAuctionCostUI();

        bool tookLead = manager.WinnerID == boss.NPCID && (beforeWinner != boss.NPCID || manager.CurrentCost != beforeCost);
        if (tookLead) ApplyAiBid(manager, boss, boss.SkillRefreshesTimer);
    }

    private void ApplyAiBid(Auction manager, AllAiBase ai, bool refreshTimer = true) // AI를 최고 입찰자로 등록
    {
        manager.WinnerID = ai.NPCID;
        manager.IfPlayerWin = false;
        manager.AppendConsoleLog($"{ai.NPCName}님이 {manager.CurrentCost}G로 레이즈!"); // 레이즈는 말풍선/가격표로만 보여줌
        if (refreshTimer) manager.RefreshTimerOnBid();
    }
}
