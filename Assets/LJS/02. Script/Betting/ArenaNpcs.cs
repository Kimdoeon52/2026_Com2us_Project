using UnityEngine;

/// <summary>
/// NPC 객체에 넣는 코드. 플레이어 상호작용과 매니저에게 데이터 전달함
/// </summary>

public enum NpcTendency { Aggressive, Normal, Cautious }

public class NpcData // 매니저 데이터 전달용 클래스
{
    public int currentWealth; // 보유 금액
    public INpcBettingLogic bettingLogic; // 인터페이스

    public bool CanPlayMatch() => currentWealth > 0;
    public bool DecideAccept(int playerBet, int basePrice) => bettingLogic.ThinkPlayerBet(playerBet, currentWealth, basePrice);
    public int GetCounterOffer(int playerBet, int basePrice) => bettingLogic.NpcCounterBet(playerBet, currentWealth, basePrice);
}

public class ArenaNpcs : MonoBehaviour
{
    public string npcName; // NPC 이름
    public int currentWealth; // 보유 금액
    public NpcTendency currentTendency; // 이넘 변수

    private INpcBettingLogic bettingLogic; // 인터페이스 변수
    private int currentRegionBasePrice; // 지역별 베팅 평균가

    [Header("SO 연결")]
    public ArenaNpcInteractionSO npcInteractionSO;

    // NPC 초기화
    public void InitializeNpc(string name, int wealth, NpcTendency tendency, int regionBasePrice)
    {
        npcName = name;
        currentWealth = wealth;
        currentTendency = tendency;
        currentRegionBasePrice = regionBasePrice;

        switch (tendency)
        {
            case NpcTendency.Aggressive: bettingLogic = new AggressiveNpcLogic(); break;
            case NpcTendency.Normal: bettingLogic = new NormalNpcLogic(); break;
            case NpcTendency.Cautious: bettingLogic = new CautiousNpcLogic(); break;
        }
        gameObject.SetActive(true);
    }

    public void BettingStart()
    {
        NpcData myData = new NpcData
        {
            currentWealth = this.currentWealth,
            bettingLogic = this.bettingLogic
        };

        // 변경점: FindObjectOfType 대신 싱글톤 Instance 호출로 성능 및 구조 개선
        ArenaBettingManager.Instance.OnMatchButtonClicked(myData, currentRegionBasePrice);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && npcInteractionSO != null)
        {
            // 채널에 내 정보(this)를 담아 방송 on
            npcInteractionSO.RaiseInteractableEntered(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && npcInteractionSO != null)
        {
            npcInteractionSO.RaiseInteractableExited();
        }
    }
}
