using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FistAuctionMode : IAuctionMode
{
    public async UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants)
    {
        manager.SetChat("주먹 경매가 시작되었습니다! 원하는 입찰가를 입력해주세요.");
        manager.AppendConsoleLog("--- 주먹 경매 진행 중 ---");

        // 1. 플레이어 입찰가 입력 대기
        manager.EnableFistAuctionUI(true); //주먹 경매용 버튼 Ui활성화
        int playerBid = await manager.WaitPlayerFistBidAsync();
        manager.EnableFistAuctionUI(false); //주먹 경매용 버튼 Ui비활성화

        // 2. 제출된 제시가 모음
        Dictionary<string, int> bids = new Dictionary<string, int>(); // 제시가를 담을 딕션너뤼

        if (playerBid > 0)
        {
            bids.Add(manager.PlayerName, playerBid); //플레이어가 값을 정한 값을 넣음
        }

        // 3. AI 입찰 계산
        foreach (var ai in participants) // 참가한 Ai들 루틴 
        {
            if (ai == null || ai.IsReady) continue; // ai 포기 한 상태는 무시

            // AI 성격/보유 자금 기반 단 1회 가치 산정
            int aiBid = CalculateAiFistBid(ai, stuff); // 각 ai가 얼마를 걸엇는지 aiBid에 넣기
            if (aiBid > 0 && GlobalGold.Instance.CanUseGold(ai.NPCName, aiBid)) //aiBid가 가능한지 판단
            {
                bids.Add(ai.NPCName, aiBid); // 누가 얼마 걸었는지 딕션어리에 저장
                manager.AppendConsoleLog($"{ai.NPCName}님이 입찰서를 제출했습니다."); // 
            }
        }

        manager.SetChat("모든 참가자의 입찰서 제출이 완료되었습니다! 결과를 공개합니다.");
        await UniTask.Delay(2000);

        // 4. 승자 결정
        if (bids.Count == 0) //유효성 검사 및 초기화
        {
            manager.WinnerName = "";
            manager.IfPlayerWin = false;
            return;
        }

        var highestBid = bids.OrderByDescending(x => x.Value).First(); //정렬 후 가장 먼저 있는걸 highestBid에 삽입

        manager.CurrentCost = highestBid.Value; // 가장 높은 가격의 벨류를 현 가격에 대입
        manager.UpdateAuctionCostUI(); //가격 시각적 업데이트
        manager.WinnerName = highestBid.Key; // 누가 이겼는지 전달
        manager.IfPlayerWin = (highestBid.Key == manager.PlayerName); // 플레이어가 이겼다면 플레이어 승리판정

        manager.SetChat($"최종 낙찰자: {highestBid.Key} ({highestBid.Value}G)");
        await UniTask.Delay(1500); // 잠시 대기
    }

    private int CalculateAiFistBid(AllAiBase ai, PartsDefinition stuff)
    {
        // 원가 기준 0.8배 ~ 1.4배 범위 내 무작위 제출
        float multiplier = Random.Range(0.8f, 1.4f); 
        return Mathf.RoundToInt(stuff.Cost * multiplier); //일단 임시로 쓰는 수치
    }
}
