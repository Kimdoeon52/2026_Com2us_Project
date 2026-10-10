using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 일반 경기의 베팅 로직 및 UI를 담당하는 싱글톤 매니저.
/// </summary>

public class ArenaBettingManager : MonoBehaviour
{
    public static ArenaBettingManager Instance { get; private set; }

    [Header("UI 패널")]
    public GameObject bettingUiPanel; // 가장 큰 베팅 UI
    public GameObject inputGroup; // 판돈 작성용 그룹
    public GameObject confirmGroup; // 버튼용 그룹

    [Header("UI 객체들")]
    public TextMeshProUGUI npcInfoText; // 상대의 정보 텍스트
    public TextMeshProUGUI systemMessageText; // 시스템 텍스트
    public TMP_InputField betInputField; // 플레이어가 작성하는 베팅값
    public Button submitButton; // 베팅 버튼
    public Button acceptButton; // 베팅 수락 버튼
    public Button rejectButton; // 베팅 거절 버튼

    [Header("경기장 알림 UI")]
    public CanvasGroup ArenaInfoGroup; // 알림창 투명도 조절용
    public TextMeshProUGUI ArenaInfoText; // 알림창 내용 텍스트
    private Tween InfoTween; // DOTween 덮어쓰기 방지용

    private NpcData currentNpc;
    private int currentBasePrice; // 지역 기초금액
    private int minBetLimit; // 최소금
    private int maxBetLimit; // 최대감
    private int npcCounterBetAmount; // Npc 역제안 금액

    // 플레이어 제안 결과 저장용
    private int currentProposedBet;
    private bool isConfirmingPlayerBet;

    private enum BetState { First, Second }
    private BetState currentState;

    private void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 버튼 클릭 시 실행될 함수들을 동적으로 연결
        submitButton.onClick.AddListener(OnSubmitButtonClicked);
        acceptButton.onClick.AddListener(OnAcceptButtonClicked);
        rejectButton.onClick.AddListener(OnRejectButtonClicked);

        bettingUiPanel.SetActive(false);

        if(ArenaInfoGroup != null)
        {
            ArenaInfoGroup.alpha = 0f;
            ArenaInfoGroup.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (bettingUiPanel.activeSelf)
        {
            // 엔터 키(메인 키보드 및 숫자 패드) 입력 감지
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (inputGroup.activeSelf)
                {
                    OnSubmitButtonClicked(); // 입력창이 켜져있으면 제시하기 실행
                }
                else if (confirmGroup.activeSelf)
                {
                    OnAcceptButtonClicked(); // 확인창이 켜져있으면 수락 실행
                }
            }
        }
    }

    public void OnMatchButtonClicked(NpcData targetNpc, int regionBasePrice) // Ui초기화
    {
        currentNpc = targetNpc;
        currentBasePrice = regionBasePrice;

        if (!currentNpc.CanPlayMatch())
        {
            ShowArenaInfo("NPC의 소지금이 부족하여\n경기를 진행할 수 없습니다."); // 노란 알림
            return;
        }

        minBetLimit = Mathf.RoundToInt(currentBasePrice * 0.7f);
        maxBetLimit = currentNpc.currentWealth;

        // UI 텍스트 갱신
        npcInfoText.text = $"[상대 정보]\n소지금: {currentNpc.currentWealth}G | 지역 기준가: {currentBasePrice}G";

        // 상태를 첫 번째 베팅으로 초기화
        currentState = BetState.First;
        betInputField.text = "";

        // 인풋필드 자동 선택
        betInputField.Select();
        betInputField.ActivateInputField();

        ShowInputUI($"베팅값을 설정해주세요.\n(최소: {minBetLimit}G ~ 최대: {maxBetLimit}G)");
        bettingUiPanel.SetActive(true);
    }

    private void OnSubmitButtonClicked()
    {
        // 1. 입력 필드의 문자열을 숫자로 안전하게 변환
        if (!int.TryParse(betInputField.text, out int playerBetAmount))
        {
            ShowArenaInfo("<color=red>올바른 금액을 입력해 주세요.</color>");
            return;
        }

        // 2. 금액 유효성 검증
        if (!IsValidBetAmount(playerBetAmount)) return;

        // 3. 상태에 따라 분기
        if (currentState == BetState.First) ProcessFirstBet(playerBetAmount);
        else if (currentState == BetState.Second) ProcessSecondBet(playerBetAmount);
    }

    private void ProcessFirstBet(int playerBetAmount)
    {
        currentProposedBet = playerBetAmount;
        if (currentNpc.DecideAccept(playerBetAmount, currentBasePrice))
        {
            isConfirmingPlayerBet = true;
            ShowCounterOfferUI($"상대가 제안을 수락했습니다.\n<color=green>{playerBetAmount}G</color>로 경기를 시작하시겠습니까?");
        }
        else
        {
            isConfirmingPlayerBet = false;
            npcCounterBetAmount = currentNpc.GetCounterOffer(playerBetAmount, currentBasePrice);
            ShowCounterOfferUI($"NPC가 제시조건이 마음에 들지 않아\n새로<color=yellow>{npcCounterBetAmount}G</color>로 제시했습니다.\n수락하시겠습니까?");
        }
    }

    private void ProcessSecondBet(int playerBetAmount)
    {
        if (currentNpc.DecideAccept(playerBetAmount, currentBasePrice))
        {
            StartMatch(playerBetAmount);
        }
        else
        {
            ShowArenaInfo("NPC가 재제시를 거절했습니다.\n베팅 값이 기준가로 고정됩니다."); // 노란 알림
            StartMatch(currentBasePrice); // 거절 두번 하면 기준값으로 베팅 고정
        }
    }

    private void ShowArenaInfo(string message)
    {
        if (ArenaInfoGroup == null || ArenaInfoText == null)
        {
            Debug.Log($"[Arena Notification] {message}");
            return;
        }

        ArenaInfoText.text = message;
        ArenaInfoGroup.gameObject.SetActive(true);
        ArenaInfoGroup.alpha = 1f;

        if (InfoTween != null && InfoTween.IsActive())
        {
            InfoTween.Kill();
        }

        // 1.5초 대기 후 0.5초 동안 서서히 투명해지는 연출
        InfoTween = ArenaInfoGroup.DOFade(0f, 0.5f)
            .SetDelay(1.5f)
            .OnComplete(() => ArenaInfoGroup.gameObject.SetActive(false));
    }

    // --- UI 조작 헬퍼 함수 ---

    private void ShowInputUI(string message)
    {
        systemMessageText.text = message;
        inputGroup.SetActive(true);
        confirmGroup.SetActive(false);
    }

    private void ShowCounterOfferUI(string message)
    {
        systemMessageText.text = message;
        inputGroup.SetActive(false);
        confirmGroup.SetActive(true);
    }

    // --- 역제안 버튼 이벤트 => 수락 및 거절 통합 ---

    private void OnAcceptButtonClicked()
    {
        // 플레이어의 제안확정 인지, NPC 역제안 수락인지 플래그로 분기
        if (isConfirmingPlayerBet)
            StartMatch(currentProposedBet);
        else
            StartMatch(npcCounterBetAmount);
    }

    private void OnRejectButtonClicked()
    {
        // 플레이어가 본인의 제안을 스스로 철회하든, NPC의 역제안을 거절하든 두 번째 베팅으로 넘어감
        currentState = BetState.Second;
        betInputField.text = "";

        // 인풋필드 자동 선택 유지
        betInputField.Select();
        betInputField.ActivateInputField();

        ShowInputUI($"새로운 금액을 다시 제시해 주세요.\n(최소: {minBetLimit}G ~ 최대: {maxBetLimit}G)");
    }

    /*private void AcceptNpcCounterOffer() => StartMatch(npcCounterBetAmount);

    private void RejectNpcCounterOffer()
    {
        currentState = BetState.Second; // 상태를 재제시(두 번째 베팅)로 변경
        betInputField.text = "";
        ShowInputUI($"새로운 금액을 다시 제시해 주세요.\n(최소: {minBetLimit}G ~ 최대: {maxBetLimit}G)");
    }*/

    // --- 검증 및 최종 처리 ---
    private void StartMatch(int finalBetAmount)
    {
        bettingUiPanel.SetActive(false);
        ShowArenaInfo($"베팅 종료! {finalBetAmount}G로 경기가 시작됩니다!"); // 노란 알림
    }

    private bool IsValidBetAmount(int amount)
    {
        if (amount < minBetLimit)
        {
            ShowArenaInfo($"<color=red>최소 금액은 {minBetLimit}G 입니다.</color>");
            return false;
        }
        if (amount > maxBetLimit)
        {
            ShowArenaInfo($"<color=red>최대 금액은 {maxBetLimit}G 입니다.</color>");
            return false;
        }
        return true;
    }

}
