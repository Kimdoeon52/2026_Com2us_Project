using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 주먹 경매 (서면 동시 입찰)
 1. 입찰 단계: 제한 시간 안에 모두 금액을 적어서 봉인 (0원 = 포기)
    - 플레이어 음수/보유 골드 초과 입력은 다시 입력
    - 제한 시간 안에 제출 안 하면 자동 0원
 2. 카운트다운: 셋! 둘! 하나!
 3. 동시 오픈: 모든 명패가 한 번에 뒤집히며 금액 공개
 4. 판정: 최고 입찰자 낙찰 (동점이면 먼저 제출한 사람)
 */
public class FistAuctionMode : IAuctionMode
{
    private const float RevealRollTime = 1.2f; // 숫자 굴리는 시간 (전원 동일)
    private const float WarningTime = 5f; // 남은 시간 경고 구간

    private class Bidder
    {
        public string Name;
        public int ID;
        public AllAiBase Ai; // 플레이어는 null
        public FistBidPlate Plate;
        public int Bid;
        public bool IsSubmitted;
        public int SubmitOrder; // 동점일 때 먼저 낸 사람 승리
    }

    private int submitCounter;

    public async UniTask DoingAuctionAsync(Auction manager, PartsDefinition stuff, List<AllAiBase> participants)
    {
        manager.ClearRoundUI();
        submitCounter = 0;
        float bidTime = manager.FistBidTime;

        // ===== 참가자 명패 세팅 =====
        Bidder player = new Bidder { Name = manager.PlayerName, ID = manager.PlayerID };
        player.Plate = CreatePlate(manager, manager.PlayerSlot);
        List<Bidder> bidders = new List<Bidder> { player };

        foreach (var ai in participants)
        {
            if (ai == null) continue;
            Bidder bidder = new Bidder { Name = ai.NPCName, ID = ai.NPCID, Ai = ai };
            bidder.Plate = CreatePlate(manager, ai.transform.parent); // AI는 좌석(랭커자리)의 자식으로 생성됨
            bidders.Add(bidder);
        }

        manager.AppendConsoleLog("--- 주먹 경매 진행 중 ---");
        manager.AnnounceBriefAsync($"주먹 경매를 시작합니다! {bidTime:0}초 안에 입찰가를 적어 주세요. (0G = 포기)", 2.5f).Forget(); // 안내 후 자동 퇴장
        manager.EnableFistAuctionUI(true);

        // ===== 1. 입찰 단계 =====
        List<UniTask> aiTasks = bidders.Where(b => b.Ai != null)
                                       .Select(b => AiWriteBidAsync(manager, b, stuff, bidTime))
                                       .ToList();
        await WaitPlayerBidAsync(manager, player, bidTime);

        if (bidders.Any(b => !b.IsSubmitted))
        {
            manager.AppendConsoleLog("다른 참가자들이 아직 입찰서를 쓰고 있습니다...");
            await UniTask.WhenAll(aiTasks);
        }

        // ===== 2. 카운트다운 (진행자 재등장) =====
        await manager.SayAsync("모든 입찰서가 등록되었습니다!");
        await UniTask.Delay(900);
        foreach (string count in new[] { "셋!", "둘!", "하나!" })
        {
            manager.SetChat(count);
            PunchChat(manager, 0.3f);
            bidders.ForEach(b => b.Plate?.Shake(6f));
            await UniTask.Delay(700);
        }
        manager.SetChat("오픈!!");
        PunchChat(manager, 0.5f);

        // ===== 3. 동시 오픈 =====
        await UniTask.WhenAll(bidders.Select(b => b.Plate != null ? b.Plate.RevealAsync(b.Bid, RevealRollTime) : UniTask.CompletedTask));
        await UniTask.Delay(600);

        // ===== 4. 판정 =====
        List<Bidder> ranking = bidders.OrderByDescending(b => b.Bid).ThenBy(b => b.SubmitOrder).ToList();
        manager.AppendConsoleLog("--- 주먹 경매 결과 ---");
        for (int i = 0; i < ranking.Count; i++)
        {
            manager.AppendConsoleLog($"{i + 1}위 {ranking[i].Name}: {(ranking[i].Bid > 0 ? $"{ranking[i].Bid:N0}G" : "포기")}");
        }

        Bidder winner = ranking[0];
        if (winner.Bid <= 0)
        {
            bidders.ForEach(b => b.Plate?.Lose());
            await manager.SayAsync("아무도 입찰하지 않았습니다. 경매가 유찰되었습니다."); // 진행자는 그대로 남아 마무리 대사로 이어짐
            manager.WinnerID = -1;
            manager.IfPlayerWin = false;
            await UniTask.Delay(2000);
            return;
        }

        foreach (var b in bidders)
        {
            if (b == winner) b.Plate?.Win(b.Bid);
            else b.Plate?.Lose();
        }

        manager.CurrentCost = winner.Bid;
        manager.UpdateAuctionCostUI();
        manager.WinnerID = winner.ID;
        manager.IfPlayerWin = winner == player;

        PunchChat(manager, 0.4f);
        ShowReactions(bidders, winner, player);
        await manager.SayAsync(BuildResultChat(winner, ranking)); // 진행자는 그대로 남아 낙찰 축하 대사로 이어짐

        await UniTask.Delay(2500);
    }

    // ==================== 입찰 단계 ====================
    private async UniTask WaitPlayerBidAsync(Auction manager, Bidder player, float bidTime)
    {
        UniTask<int> bidTask = manager.WaitPlayerFistBidAsync();
        bool isPlayerDone = false;
        int playerBid = 0;
        bidTask.ContinueWith(bid => { playerBid = bid; isPlayerDone = true; }).Forget();

        float remain = bidTime;
        int lastWarnSecond = -1;
        manager.ShowTimer(true); // 입찰 단계에서만 타이머 표시
        if (manager.timeBar != null) manager.timeBar.fillAmount = 1f;

        while (!isPlayerDone && remain > 0f)
        {
            remain -= Time.deltaTime;
            if (manager.timeBar != null) manager.timeBar.fillAmount = Mathf.Clamp01(remain / bidTime);

            int second = Mathf.CeilToInt(remain);
            if (remain <= WarningTime && second != lastWarnSecond && second > 0) // 마지막 5초 경고
            {
                lastWarnSecond = second; // 진행자 대사 대신 타임바 펀치로만 재촉
                if (manager.timeBar != null)
                {
                    manager.timeBar.transform.DOKill(true);
                    manager.timeBar.transform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6, 0.5f);
                }
            }
            await UniTask.Yield();
        }

        if (!isPlayerDone) // 시간 초과 -> 자동 0원
        {
            manager.ForceFistBid(0);
            manager.ShowErrorMessage("시간 초과! 0G(포기)로 제출됩니다.").Forget();
            if (AuctionPlayer.Instance != null) AuctionPlayer.Instance.ShowThinkChatWithText("앗, 시간이...!");
            await UniTask.WaitUntil(() => isPlayerDone);
        }
        else
        {
            if (AuctionPlayer.Instance != null)
                AuctionPlayer.Instance.ShowThinkChatWithText(playerBid > 0 ? "이 정도면 되겠지." : "이번엔 패스.");
        }

        manager.ShowTimer(false);
        manager.EnableFistAuctionUI(false);

        player.Bid = playerBid;
        Submit(player);
        manager.AppendConsoleLog($"{player.Name}님이 입찰서를 제출했습니다.");
    }

    private async UniTask AiWriteBidAsync(Auction manager, Bidder bidder, PartsDefinition stuff, float bidTime)
    {
        AllAiBase ai = bidder.Ai;
        ai.ShowThinkChat();

        // 제한 시간 안에는 반드시 제출하도록 고민 시간 제한
        int maxDelay = Mathf.Max(1000, Mathf.Min(ai.ThinkDelay, (int)((bidTime - 1f) * 1000)));
        await UniTask.Delay(Random.Range(800, maxDelay));

        bidder.Bid = ai.CalculateFistBid(stuff.Cost, stuff.Grade); // 필요 없으면 0G, 필요하면 원가 x 2~4배
        Submit(bidder);
        ai.ShowThinkChatWithText("결정했다.");
        manager.AppendConsoleLog($"{ai.NPCName}님이 입찰서를 제출했습니다.");
    }

    private void Submit(Bidder bidder)
    {
        bidder.IsSubmitted = true;
        bidder.SubmitOrder = submitCounter++;
        bidder.Plate?.Seal();
    }

    // ==================== 연출 헬퍼 ====================
    private FistBidPlate CreatePlate(Auction manager, Transform slot)
    {
        if (slot == null) return null;
        FistBidPlate plate = FistBidPlate.Create(slot, manager.chat != null ? manager.chat.font : null); // 좌석 초상화 바로 아래에 자동 배치
        manager.RegisterRoundUI(plate.gameObject);
        return plate;
    }

    private void PunchChat(Auction manager, float strength)
    {
        if (manager.chat == null) return;
        Transform chatTransform = manager.chat.transform;
        chatTransform.DOKill(true);
        chatTransform.localScale = Vector3.one;
        chatTransform.DOPunchScale(Vector3.one * strength, 0.35f, 8, 0.6f);
    }

    private string BuildResultChat(Bidder winner, List<Bidder> ranking)
    {
        Bidder second = ranking.Count > 1 ? ranking[1] : null;
        if (second != null && second.Bid == winner.Bid)
            return $"동점! 먼저 입찰서를 낸 {winner.Name}님이 {winner.Bid:N0}G에 낙찰!";
        if (second != null && second.Bid > 0)
            return $"{winner.Name}님이 {winner.Bid:N0}G에 낙찰! (2위 {second.Name}님과 {winner.Bid - second.Bid:N0}G 차이)";
        return $"{winner.Name}님이 {winner.Bid:N0}G에 단독 낙찰!";
    }

    private void ShowReactions(List<Bidder> bidders, Bidder winner, Bidder player)
    {
        foreach (var b in bidders)
        {
            if (b.Ai == null) continue;
            if (b == winner) b.Ai.ShowThinkChatWithText("후후, 내 거다!");
            else if (b.Bid > 0) b.Ai.ShowThinkChatWithText(Random.value < 0.5f ? "이럴 수가...!" : "쳇, 아깝군.");
        }

        if (AuctionPlayer.Instance == null) return;
        if (winner == player) AuctionPlayer.Instance.ShowThinkChatWithText("좋았어! 내가 가져간다!");
        else if (player.Bid == winner.Bid) AuctionPlayer.Instance.ShowThinkChatWithText("같은 금액인데 한발 늦었다니...");
        else if (player.Bid > 0) AuctionPlayer.Instance.ShowThinkChatWithText($"{winner.Bid - player.Bid:N0}G 차이라니...");
    }
}
