using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class NormalAuctionMode : IAuctionMode
{
    public async UniTask ExecuteAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants)
    {
        manager.currentCost = stuff.Cost / 2; // 반값 시작
        manager.UpdateAuctionCostUI();

        manager.isTimeRunning = true;

        // AI 독립 입찰 루프 가동
        RunAllAiAsync(manager, stuff, participants).Forget();

        // 타이머 카운트다운
        await manager.StartAuctionTimer();
    }
}
