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
        while (manager.IsTimeRunning && !manager.IsAuctioningFin)
        {
            int thinkDelay = ai.ThinkDelay; //각 ai 생각 시간 삽입

            // 이미 최고 입찰자라면 고민(ThinkDelay)도 안 하고 연출도 스킵!
            if (manager.WinnerName == ai.NPCName)
            {
                await UniTask.Yield(); // 무한 루프 방지용 최소 대기
                continue;
            }
            if (!ai.IsReady)
            {
                manager.AppendConsoleLog($"{ai.NPCName}님이 {thinkDelay / 1000}초 동안 고민중.\n"); //콘솔 출력
            }

            // ai 고민 말풍선 띄우기
            ai.ShowThinkChat();

            await UniTask.Delay(thinkDelay); // thinkDelay 만큼 
            await UniTask.WaitWhile(() => manager.IsCutscenePlaying);

            if (!manager.IsTimeRunning || manager.IsAuctioningFin) break;

            if (ai.IsReady) continue;

            Debug.Log($"[AI 루틴 체크] {ai.NPCName} | 현재가: {manager.CurrentCost} | AI 보유 RaiseGold: {ai.RaiseGold}");

            if (ai.RaiseThink(manager.CurrentCost, stuff.Cost))
            {
                int raiseStep = ai.RaiseGold;

                if (ai is BossAiBase boss && boss.IsBigRaiseThink(manager.CurrentCost, stuff.Cost))
                {
                    raiseStep = boss.BigRaiseGold();
                    manager.CurrentCost += raiseStep;
                    manager.UpdateAuctionCostUI();

                    // 빅 레이즈 성공 말풍선 연출
                    ai.ShowThinkChatWithText($"{manager.CurrentCost}G! 확 올려버리지!");
                    await boss.TriggerCutscene(boss.NPCName, boss.BigRaiseSkillName());
                }
                else
                {
                    manager.CurrentCost += raiseStep;
                    manager.UpdateAuctionCostUI();

                    // 일반 레이즈 성공 말풍선 연출 (금액 외치기)
                    ai.ShowThinkChatWithText($"{manager.CurrentCost}G 가본다!");
                }

                manager.WinnerName = ai.NPCName;
                manager.IfPlayerWin = false;
                manager.SetChat($"{ai.NPCName} 님이 {manager.CurrentCost}G로 레이즈!");

                if (manager.RemainingTime < 10f)
                {
                    manager.RemainingTime = 10f;
                }
            }
            else
            {
                Debug.Log($"[AI 레이즈 실패/포기] {ai.NPCName} | RaiseThink 결과: false");
                manager.AppendConsoleLog($"{ai.NPCName}판단 끝! 결과 포기.\n");
                manager.SetChat($"{ai.NPCName} 님이 입찰을 포기했습니다.");
            }

            if (manager.CheckAllGiveUp()) //모두가 포기하면 플레이어 제한시간 종료
            {
                manager.IsTimeRunning = false;
                break;
            }
        }
    }
}
