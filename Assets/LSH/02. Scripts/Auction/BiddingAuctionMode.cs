using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class BiddingAuctionMode : IAuctionMode
{
    public async UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants)
    {
        int targetPrice = stuff.Cost * 2; // 제시 정가
        manager.CurrentCost = targetPrice; // 현재 가격 업데이트
        manager.UpdateAuctionCostUI(); // 가격 시각적 업데이트

        manager.SetChat($"제시 가격은 {targetPrice}G입니다! 순서대로 구매 여부를 결정합니다."); // 경매 아저씨 대사
        await UniTask.Delay(1500); // 1.5초 대기

        // 순서 리스트 생성
        List<string> turnList = new List<string>(); // 순서 결정
        foreach (var ai in participants)
        {
            if (ai != null && !ai.IsReady) turnList.Add(ai.NPCName); // 순서에 Npc이름 삽입
        }
        turnList.Add(manager.PlayerName); //마지막에 플레이어 까지

        // 순서 무작위 셔플
        for (int i = 0; i < turnList.Count; i++) //리스트에 있는 만큼
        {
            int randIndex = Random.Range(i, turnList.Count); //리스트에 있는 만큼 랜덤 수 구함
            var temp = turnList[i]; // temp에 이 순간 리스트에
            turnList[i] = turnList[randIndex]; // 순서 교체
            turnList[randIndex] = temp; // 순서 교체
        }

        bool isBought = false;

        foreach (var bidder in turnList)
        {
            manager.SetChat($"{bidder}님의 순서입니다."); //순서대로 입찰 진행
            await UniTask.Delay(1000); // 1초 대기

            if (bidder == manager.PlayerName) // 플레이어 차례 라면 
            {
                manager.EnableBiddingAcceptUI(true); //버튼 Ui 키고
                bool accepted = await manager.WaitPlayerBiddingDecisionAsync(); //누를 때까지 대기
                manager.EnableBiddingAcceptUI(false);// 버튼 Ui 제거

                if (accepted) // 매입을 눌렀다면
                {
                    if (GlobalGold.Instance.CanUseGold(manager.PlayerName, targetPrice)) //골드 잇는지 체크
                    {
                        manager.WinnerName = manager.PlayerName; // 승자 이름을 플레이어로
                        manager.IfPlayerWin = true; // 플레이어 이김
                        isBought = true; // 샀다
                        manager.SetChat($"{manager.PlayerName}님이 제시가에 즉시 구매하셨습니다!"); // 경매 아저씨 대사
                        break;
                    }
                    else
                    {
                        manager.ShowErrorMessage("골드가 부족합니다! 자동으로 통과됩니다.").Forget(); //돈없음ㅋㅋ
                    }
                }
            }
            else
            {
                AllAiBase ai = participants.Find(x => x.NPCName == bidder); //AI 차례
                if (ai != null && CanAiBuyAtPrice(ai, targetPrice, stuff)) // ai판단
                {
                    manager.WinnerName = ai.NPCName; // ai 승리
                    manager.IfPlayerWin = false; // 플레이어는 못이김
                    isBought = true; //삿어요
                    manager.SetChat($"{ai.NPCName}님이 {targetPrice}G에 즉시 구매하셨습니다!"); // 경매 아저씨 대사
                    break;
                }
                else
                {
                    manager.SetChat($"{bidder}님은 이번 제시가를 거절하셨습니다."); //안사용
                }
            }
        }

        if (!isBought)
        {
            manager.WinnerName = "";
            manager.IfPlayerWin = false;
        }

        await UniTask.Delay(1500);
    }

    private bool CanAiBuyAtPrice(AllAiBase ai, int price, PartsDefinition stuff)
    {
        if (!GlobalGold.Instance.CanUseGold(ai.NPCName, price)) return false; //일단 가지고 있는 돈으로 살 수 있는지 체크
        return price <= stuff.Cost * 1.1f; //원가의 1.1배라면? 너무 비싸면 안삼. 지금은 2배로 임시로 해놔서 안살듯
    }
}
