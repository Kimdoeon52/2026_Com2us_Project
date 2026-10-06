using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public interface IAuctionMode
{
    // 경매 모드별 독립 실행 루틴
    UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants);
}