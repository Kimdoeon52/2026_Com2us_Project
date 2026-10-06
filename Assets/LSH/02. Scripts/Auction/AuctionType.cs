using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public enum AuctionType
{
    Normal, // 일반 경매 (실시간 레이즈)
    Fist,   // 주먹 경매 (서면 동시 입찰)
    Bidding // 입찰 경매 (가격 제시 후 순차 응찰)
}
