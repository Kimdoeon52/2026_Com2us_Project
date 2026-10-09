using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FistAuctionMode : IAuctionMode
{
    public async UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants)
    {
        manager.SetChat("주먹 경매가 시작되었습니다! 원하는 입찰가를 입력해주세요.");
        manager.EnableFistAuctionUI(true);
        manager.AppendConsoleLog("--- 주먹 경매 진행 중 ---");

        int playerBid = await manager.WaitPlayerFistBidAsync();
        manager.EnableFistAuctionUI(false); //주먹 경매용 버튼 Ui비활성화

        // 2. 제출된 제시가 모음
        Dictionary<string, int> allBids = new Dictionary<string, int>(); // 제시가를 담을 딕션너뤼

        if (playerBid > 0)
        {
            allBids.Add(manager.PlayerName, playerBid); //플레이어가 값을 정한 값을 넣음
        }

        // 3. AI 입찰 계산
        foreach (var ai in participants) // 참가한 Ai들 루틴 
        {
            if (ai == null || ai.IsReady) continue; // ai 포기 한 상태는 무시

            // AI 성격/보유 자금 기반 단 1회 가치 산정
            int aiBid = CalculateAiFistBid(ai, stuff.Cost); // 각 ai가 얼마를 걸엇는지 aiBid에 넣기
            
            allBids.Add(ai.NPCName, aiBid); // 누가 얼마 걸었는지 딕션어리에 저장
            manager.AppendConsoleLog($"{ai.NPCName}님이 입찰서를 제출했습니다."); // 
        }

        await UniTask.Delay(1000);
        manager.SetChat("모든 참가자의 입찰서 제출이 완료되었습니다! 결과를 공개합니다.");
        await UniTask.Delay(2000);

        string highestBidder = "";
        int maxBid = -1;
        foreach (var kvp in allBids)
        {
            manager.AppendConsoleLog($"{kvp.Key}님의 입찰가: {kvp.Value}G");
            if (kvp.Value > maxBid)
            {
                maxBid = kvp.Value;
                highestBidder = kvp.Key;
            }
        }

        // 4. 결과 적용
        manager.CurrentCost = maxBid;
        manager.UpdateAuctionCostUI();
        manager.WinnerName = highestBidder;
        manager.IfPlayerWin = (highestBidder == manager.PlayerName);

        manager.SetChat($"최고 입찰자: {highestBidder}님 ({maxBid}G)! 축하합니다.");
    }

    private int CalculateAiFistBid(AllAiBase ai, int baseCost)
    {
        // AI 예산 내에서 물건 원가의 0.8배 ~ 1.5배 사이로 비밀 입찰
        int maxPossible = (int)(baseCost * Random.Range(0.8f, 1.5f));

        // 보유 골드 검증
        if (!GlobalGold.Instance.CanUseGold(ai.NPCName, maxPossible))
        {
            maxPossible = ai.Gold; // 올인 또는 남은 잔고 전체
        }

        return maxPossible;
    }
}
