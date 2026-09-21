using System;
using UnityEngine;

/// <summary>
/// 전투 진행 상태 열거형임
/// </summary>
public enum BattleState
{
    Idle,           // 대기 상태임
    Ready,          // 준비 (스탯 주입 및 카운트다운 전) 단계임
    Fighting,       // 전투 진행 중 (60fps 액션 실행) 상태임
    BattleEnd       // 전투 종료 (KO 또는 타임아웃 판정 완료) 상태임
}

/// <summary>
/// [배틀 매니저 (BattleManager)]
/// 전투 씬의 전체 라이프사이클(시작, 스탯 세팅, 승패 판정, 종료)을 총괄 관리하는 매니저임.
/// 남윤호(NYH) 파트가 필요로 하는 스탯과 전투 상태를 중앙에서 관리하고 통보해줌.
/// </summary>
public class BattleManager : MonoBehaviour
{
    private static BattleManager instance;
    public static BattleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<BattleManager>();
            }
            return instance;
        }
    }

    [Header("전투 씬 오브젝트 바인딩 (선택 사항 - 비워둘 시 자동 탐색함)")]
    [Tooltip("플레이어 로봇 게임오브젝트임")]
    [SerializeField] private GameObject playerRobotObject;

    [Tooltip("상대/적 로봇 게임오브젝트임")]
    [SerializeField] private GameObject enemyRobotObject;

    [Header("전투 상태 및 설정")]
    [SerializeField] private BattleState currentState = BattleState.Idle;
    public BattleState CurrentState => currentState;

    [Tooltip("판돈 설정임 (승리 시 2배 지급, 패배 시 몰수됨)")]
    [SerializeField] private int betGold = 100;

    [Tooltip("전투 제한 시간(초)임 - 0이면 무제한임")]
    [SerializeField] private float timeLimit = 99f;
    private float currentTimer;

    // ========================================================================
    // 남윤호(NYH) 및 UI 파트 구독용 이벤트
    // ========================================================================
    /// <summary>전투 상태 변경 이벤트임</summary>
    public event Action<BattleState> OnBattleStateChanged;

    /// <summary>전투 시작 이벤트임</summary>
    public event Action OnBattleStarted;

    /// <summary>전투 종료 이벤트임: (bool playerWon, string winnerId, BattleSettlementResult result)</summary>
    public event Action<bool, string, BattleSettlementResult> OnBattleFinished;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // 전투 진행 중 제한 시간 체크함
        if (currentState == BattleState.Fighting && timeLimit > 0f)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
            {
                currentTimer = 0f;
                HandleTimeOver();
            }
        }
    }

    /// <summary>
    /// [1. 전투 초기화 및 스탯 세팅]
    /// 플레이어와 적의 CombatantSnapshot을 받아 DataHub에 등록하고 상태를 Ready로 전환함.
    /// </summary>
    public void InitializeBattle(
        CombatantSnapshot playerSnapshot,
        CombatantSnapshot enemySnapshot,
        int battleBetGold = 100,
        GameObject playerObj = null,
        GameObject enemyObj = null)
    {
        if (playerObj != null) playerRobotObject = playerObj;
        if (enemyObj != null) enemyRobotObject = enemyObj;

        betGold = battleBetGold;
        currentTimer = timeLimit;

        // 1. DataHub에 스냅샷 등록함
        if (CombatDataHub.Instance != null)
        {
            CombatDataHub.Instance.RegisterCombatants(playerSnapshot, enemySnapshot);
        }

        SetState(BattleState.Ready);
        Debug.Log("[BattleManager] 전투 초기화 및 스탯 세팅 완료됨. Ready 상태 진입함.");
    }

    /// <summary>
    /// [2. 전투 시작]
    /// 카운트다운 후 실제 조작 및 틱 실행 개시함
    /// </summary>
    public void StartBattle()
    {
        SetState(BattleState.Fighting);
        OnBattleStarted?.Invoke();
        Debug.Log("[BattleManager] 전투 개시함! (FIGHT!)");
    }

    /// <summary>
    /// [3. 타격 등으로 코어 HP가 0이 되어 사망했을 때 호출됨]
    /// </summary>
    public void OnFighterKilled(string defeatedFighterId)
    {
        if (currentState == BattleState.BattleEnd) return;

        bool isPlayerDefeated = defeatedFighterId.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
        bool playerWon = !isPlayerDefeated;
        string winnerId = playerWon ? "Player" : "Enemy";

        Debug.Log($"[BattleManager] KO 발생함! 패배자: {defeatedFighterId}, 승리자: {winnerId}");

        // 남윤호의 사망한 로봇 조작 비활성화 처리함
        GameObject defeatedObj = isPlayerDefeated ? playerRobotObject : enemyRobotObject;
        if (defeatedObj != null)
        {
            var executor = defeatedObj.GetComponent<ActionExecutor>();
            if (executor != null)
            {
                // 동작 정지를 위해 실행기 비활성화함
                executor.enabled = false;
            }
        }

        EndBattle(playerWon, winnerId);
    }

    /// <summary>
    /// 타임오버 시 판정함 (체력 비율이 더 높은 쪽 승리함)
    /// </summary>
    private void HandleTimeOver()
    {
        if (currentState == BattleState.BattleEnd) return;

        int playerHp = CombatDataHub.Instance != null ? CombatDataHub.Instance.GetCurrentHp("Player") : 0;
        int enemyHp = CombatDataHub.Instance != null ? CombatDataHub.Instance.GetCurrentHp("Enemy") : 0;

        bool playerWon = playerHp >= enemyHp;
        string winnerId = playerWon ? "Player" : "Enemy";

        Debug.Log($"[BattleManager] 타임아웃 판정함! Player HP: {playerHp} vs Enemy HP: {enemyHp} -> 승자: {winnerId}");
        EndBattle(playerWon, winnerId);
    }

    /// <summary>
    /// [4. 전투 종료 및 최종 정산 처리함]
    /// </summary>
    public void EndBattle(bool playerWon, string winnerId)
    {
        if (currentState == BattleState.BattleEnd) return;

        SetState(BattleState.BattleEnd);

        // 하드코어 승패 정산기 실행함 (내구도 복구 또는 영구 파괴 롤링 및 외부 통보함)
        CombatantSnapshot player = CombatDataHub.Instance?.PlayerSnapshot;
        CombatantSnapshot enemy = CombatDataHub.Instance?.EnemySnapshot;

        var settlement = BattleSettlementProcessor.ProcessSettlement(playerWon, player, enemy, betGold);

        OnBattleFinished?.Invoke(playerWon, winnerId, settlement);
        Debug.Log($"[BattleManager] 전투 종료 완료됨: 승자={winnerId}, {settlement.SummaryMessage}");
    }

    private void SetState(BattleState newState)
    {
        currentState = newState;
        OnBattleStateChanged?.Invoke(newState);
    }

    // ========================================================================
    // 남윤호(NYH) 파트 편의 조회 헬퍼임
    // ========================================================================
    /// <summary>남윤호 파트에서 참조할 수 있는 파이터 이동속도 반환함</summary>
    public float GetMoveSpeed(string fighterId)
    {
        return CombatDataHub.Instance != null ? CombatDataHub.Instance.GetFinalMoveSpeed(fighterId) : 3f;
    }

    public float GetRemainingTime() => currentTimer;
    public bool IsBattleActive() => currentState == BattleState.Fighting;
}
