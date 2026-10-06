using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;
/*

 입찰 프로세스 
물건의 기준 가격 제시
3~8명의 AI NPC입찰자(보스 포함)가 등장
각 AI 입찰가 순차적으로 1회씩 입찰
플레이어가 그에 맞춰 가격을 올릴수있음
남은시간이 10초 이하일때 입찰마다 카운트 10초 초기화 진행
최종 낙찰

AI 입찰 아키타입
후반 형
경매 종료 직전(10초전) 구간에만 개입
그전 까지는 개입X
초반형
경매 시작 직후 시세보다 높게 지르기 위해 입찰을 시작한다.
추격형
남이 입찰할때 마다 기준가+30 % 기준 아래로 계속 따라 붙는다.
신중형
중반까지 지켜보다가 경쟁자 수가 2명 이하일때 진입
집착형
선호 카테고리 부품이면 기준가 + 50%기준 질러버린다.

NPC 입찰자 스탯 구조
선호 부품 카테고리
NPC가 특정 종류의 부품을 더 적극적이게 입찰하게 설정
예산 규모
NPC가 입찰할수있는 최대 자금
관계도
경쟁(입찰)을 통해 누적되는 수치


 10/4 해야할꺼.
 판매 ui 대충 물건 올리는거 부분 그리기. 기능 필요없음 시각적으로 보이게만.

 AI 성격 5개 기획서 확인하고 만들기.
 보스 세명.
 조무레기 다섯명. 골드 상관x
 물건 바꾸기. 장비들로.

 보스 일정 조건에 달성하면서 큰 레이즈하면 컷씬 연출하기.

 보스 스킬 1: 크게 걸기
 보스 스킬 2: 블라인드(자신이 제시한 가격 이하로 입찰한 사람은 포기하게 만들기)


 경매 종류를 늘리기.
 일반 경매
 주먹 경매
 입찰 경매


 */

public class Auction : PersistentSingleton<Auction>
{
    [Header("경매 설정")]
    [SerializeField] public AuctionType currentAuctionType = AuctionType.Normal; //현재 경매 모드

    [Header("물건 정보 UI")]
    [SerializeField] public TextMeshProUGUI auctionName; //물건 이름
    [SerializeField] public TextMeshProUGUI auctionCost; //물건 가격
    [SerializeField] private TextMeshProUGUI auctionGrade;// 물건 등급

    [Header("대사 & 메시지 UI")]
    [SerializeField] public TextMeshProUGUI chat; //경매하는 사람 채팅 나올 곳
    [SerializeField] public TextMeshProUGUI errorMessage; //에러 메시지 나올 곳
    [SerializeField] public TextMeshProUGUI myGold;// 내 골드 보유 수 나올 곳

    [Header("일반 경매 조작 버튼")] //순서 상관없이 쭉쭉 돌아가며 레이즈 하며 진행
    [SerializeField] public Button raiseButton; //레이즈 버튼
    [SerializeField] public Button giveUpButton; //포기 버튼

    [Header("주먹 경매 입력 UI")] //값을 입력해서 모두가 동시에 땅!
    [SerializeField] public TMP_InputField fistBidInputField; // 값입력할 곳
    [SerializeField] public Button fistSubmitButton; // 확인 버튼

    [Header("입찰 경매 수락/거절 UI")] //살래? 말래? 결정 하는 곳
    [SerializeField] public Button biddingAcceptButton; //수락 버튼
    [SerializeField] public Button biddingRejectButton; //거절 버튼

    [Header("참여 AI 리스트")]
    [SerializeField] public List<NpcAiBase> npcAiList; //참여Ai목록
    [SerializeField] public List<BossAiBase> bossAiList; //참여Boss목록
    [SerializeField] private List<AllAiBase> auctionAiList; //지금 경매에 참여하고 있는 Ai들
    [SerializeField] private int dayOfEnterNpc; //오늘 경매에 참여할 NPC수

    [Header("경매 물품 리스트")]
    public List<PartsDefinition> auctionItem; // 경매에 나올 모든 물품들 모아놓은 곳

    [Header("컷씬 매니저")]
    [SerializeField] private AuctionCutsceneManager cutsceneManager; //컷씬 용

    [Header("제한 시간 UI")]
    [SerializeField] public Image timeBar; //제한 시간 이미지
    private float auctionTime = 60f; //총 시간
    public float RemainingTime { get; set; } = 0f; //현재 시간
    public bool IsTimeRunning { get; set; } = false; // 시간이 끝났는가? 체크

    [Header("임시 콘솔창")]
    public TextMeshProUGUI console; //임시로 쓰는 거

    // --- 프로퍼티 & 캡슐화 변수 ---
    public string PlayerName { get; private set; } // 플레이어 이름 받아올 용도의 변수
    public int CurrentCost { get; set; } = 0; //현재 물건 가격을 나타낼 변수
    public bool IsAuctioningFin { get; set; } = true; //경매가 끝났는지 체크하는 변수
    public bool IsCutscenePlaying { get; set; } = false; //컷씬이 실행되고 있는지 체크하는 변수
    public string WinnerName { get; set; } = ""; //승자 이름을 저장할 변수
    public bool IfPlayerWin { get; set; } = false; //플레이어가 이겼는지 체크하는 변수

    private PartsDefinition stuff; //현재 경매 물품을 저장할 용도
    public PartGrade stuffGrade; //경매 물품 등급을 저장할 용도
    private Vector2 errorMessagePos; //에러 메시지가 나올 초기 위치
    private bool isChatting = false; //채팅 중인지 체크하는 용도
    private bool isPlayerGiveUp = false; //플레이가 포기했는지 체크하는 용도

    // 경매 전략 딕셔너리
    private Dictionary<AuctionType, IAuctionMode> nowAuctionMode;

    // 비동기 UI 입력 수신용 CS
    private UniTaskCompletionSource<int> fistBidTcs;
    private UniTaskCompletionSource<bool> biddingDecisionTcs;
    /*
    UniTaskCompletionSource: 비동기 작업을 수동으로 완료시킬 수 있는 신호기
    "버튼 클릭이나 입력 같은 이벤트가 일어날 때까지 코드를 멈춰두었다가, 이벤트가 발생하면 입력받은 데이터(값)를 가지고 다음으로 넘어가게 해주는 비동기 대기용 신호기"

    public async UniTask WaitClick() <- 해당 방식은 프레임마다 체크해야해서 불안함
    {
        while (!isClicked)
        {
            await UniTask.Yield(); // 클릭할 때까지 매 프레임 기다림
        }
    } 
    await tcs.Task: 완료 신호가 올 때까지 기다리는 곳

    tcs.TrySetResult(값): 신호를 보내면서 결과값을 전달하고 기다리던 코드를 다시 가동시키는 곳 
     */


    /*
     WaitUntil이 좋은 경우

    키보드/마우스의 단순 입력 (Input.GetKeyDown, Input.GetMouseButtonDown)

    단순히 "스페이스바 누르면 다음 대사 넘어가기" 같은 연출 로직

    UniTaskCompletionSource가 좋은 경우

    Unity UI 버튼 (Button.onClick)

    UI InputField로 숫자를 받아야 할 때 ("주먹 경매 금액 입력")

    버튼이 여러 개라서 "수락(true)을 눌렀는지 / 거절(false)을 눌렀는지" 결과를 받아야 할 때 ("입찰 경매 수락/거절")
     */
    private void OnEnable()
    {
        BossAiBase.OnBossBigRaiseCutscene += HandleBossBigRaiseCutscene; //컷씬 옵져버 등록
    }

    private void OnDisable()
    {
        BossAiBase.OnBossBigRaiseCutscene -= HandleBossBigRaiseCutscene; // 컷씬 옵져버 해제
    }

    private async UniTask HandleBossBigRaiseCutscene(string bossName, string skillName) //컷씬 대기용 함수
    {
        IsCutscenePlaying = true; // 컷씬 플레이 시작
        if (cutsceneManager != null)
        {
            await cutsceneManager.PlayBossCutsceneAsync(bossName, skillName); // awiat으로 컷씬 연출 대기
        }
        IsCutscenePlaying = false; // 컷씬 끝
    }

    protected override void Awake()
    {
        base.Awake(); // Instance용 base

        // 전략 등록
        nowAuctionMode = new Dictionary<AuctionType, IAuctionMode> // noewAuctionMode에 현재 모드 넣기
        {
            { AuctionType.Normal, new NormalAuctionMode() },
            { AuctionType.Fist, new FistAuctionMode() },
            { AuctionType.Bidding, new BiddingAuctionMode() }
        };

        if (errorMessage != null)
            errorMessagePos = errorMessage.rectTransform.anchoredPosition; //첫 위치 저장

        NormalAuctionButtonReady(false); // 레이즈/포기   버튼 비활
        FistAuctionButtonReady(false); 
        BiddingAuctionButtonReady(false);
        EnableFistAuctionUI(false); // 등록/포기 버튼 비활
        EnableBiddingAcceptUI(false); // 입찰/포기 버튼 비활
    }

    private void Start()
    {
        if (GlobalGold.Instance.mainCharacterdata != null && GlobalGold.Instance.mainCharacterdata.Count > 0)
        {
            PlayerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName; //플레이어 이름 삽입
        }
        else
        {
            Debug.LogError("GlobalGold에 mainCharacterdata가 읍다");
            PlayerName = "Player"; // 플레이어 이름 비우면 그냥 플레이어
        }

        if (auctionItem != null && auctionItem.Count > 0)
        {
            stuff = auctionItem[Random.Range(0, auctionItem.Count)]; // stuff에 경매에 있는 아이템 랜덤 등록
            CurrentCost = stuff.Cost / 2;
            stuffGrade = stuff.Grade; // 등급 넣기
            if (auctionGrade != null) auctionGrade.text = stuffGrade.ToString(); // Text에 등급 보여주기
        }
        else
        {
            Debug.LogError("경매 물품 리스트가 읍다");
            return;
        }

        UpdateGoldDisplay(); // 골드 업데이트
        DayOfAuctionEnter(); // 그날 하루 경매할 인원 정하기
        GetAuctionNpc(dayOfEnterNpc); // ai 정하기
    }
    private void Update()
    {
        if (IsAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
            StartAuction().Forget(); //딱 한번 실행.옥션 시작
        }
    }

    private void DayOfAuctionEnter()
    {
        dayOfEnterNpc = Random.Range(3, 5); // 경매 인원 3~4 (임시)
    }

    private void GetAuctionNpc(int enterNpc) // 그날 하루 경매할 npc 수 매개변수
    {
        auctionAiList.Clear(); //옥션 리스트 비우고
        bool isBossEnter = Random.value < 1f; //지금은 1f로 실험용인데 나중에 확률 30%로 바꾸기
        if (isBossEnter && bossAiList.Count > 0)
        {
            int randomBossIndex = Random.Range(0, bossAiList.Count); // 어떤 보스 넣을지
            auctionAiList.Add(bossAiList[randomBossIndex]); // auctionAiList에 보스 하나 쑤셔넣기
        }

        HashSet<int> selectedIndices = new HashSet<int>(); // 중첩되면 안되니까 hash
        int targetCount = Mathf.Min(enterNpc, npcAiList.Count); // enterNpc값이 안들어가면 안되니까 그냥 유효성 검사용임.

        while (selectedIndices.Count < targetCount) //
        {
            selectedIndices.Add(Random.Range(0, npcAiList.Count)); //npc리스트에 그날 하루 인원 수 만큼 삽입
        }

        foreach (int index in selectedIndices) //중첩 안된 랜덤한 Npc를 auctionAiList에 넣기
        {
            auctionAiList.Add(npcAiList[index]);
        }
    } // 총원은 enterNpc + 1(보스) 명

    private async UniTask StartAuction()
    {
        IsAuctioningFin = false; // 아직 경매 안끝남
        isChatting = true; // 채팅 시작
        NormalAuctionButtonReady(false); // 레이즈/포기   버튼 비활
        FistAuctionButtonReady(false);
        BiddingAuctionButtonReady(false);
        foreach (var ai in auctionAiList) // 경매 참여하는 ai들 만큼
        {
            if (ai != null)
            {
                AppendConsoleLog($"{ai.NPCName}님이 참가 했습니다."); // Console에 참가 보여주기
                ai.ReadyForAction(); // ai들 준비완료
            }
        }

        await StartAuctionChatting(); //경매 아저씨 대사 출력
        await UniTask.Delay(1000); //1초 대기

        auctionName.text = stuff.DisplayName; // 경매 물품 이름 출력
        UpdateAuctionCostUI(); // 가격표 보여주기
        await WaitInput(); //스페이스 바 입력 대기

        await IntroAuction(); // 경매 아저씨 경매 물품 소개

        IsAuctioningFin = false; 
        isPlayerGiveUp = false;
        WinnerName = "";
        IfPlayerWin = false;
        auctionTime = 60f;

        isChatting = false;

        // 일반 경매일 때만 기본 버튼 활성화
        if (currentAuctionType == AuctionType.Normal)
        {
            NormalAuctionButtonReady(true); // 버튼 활성화
        }

        // 전략 패턴 실행
        if (nowAuctionMode.TryGetValue(currentAuctionType, out var mode))
        {
            await mode.DoingAuctionAsync(this, stuff, auctionAiList);
        }

        await EndAuction();
        IsAuctioningFin = true;
    }

    // --- 공통 헬퍼 및 UI 제어 메서드 ---
    public void UpdateAuctionCostUI() // 물건 가격 표시 업데이트
    {
        if (auctionCost != null)
        {
            auctionCost.text = CurrentCost.ToString();
            Debug.Log($"[UI 갱신 성공] 현재 텍스트에 적용된 값: {CurrentCost}");
        }
        else
        {
            Debug.LogError("🔴 [오류] auctionCost 변수가 인스펙터에 연결되어 있지 않습니다!");
        }
    }

    public void SetChat(string text) // 경매 아저씨 채팅
    {
        if (chat != null) chat.text = text;
    }

    public void AppendConsoleLog(string log) // 콘솔 로그 채팅
    {
        if (console != null) console.text += log + "\n";
    }

    public bool CheckAllGiveUp() // 전부 항복했는지 체크
    {
        int inGamePlayer = 0;
        if (!isPlayerGiveUp) inGamePlayer++;
        foreach (var ai in auctionAiList)
        {
            if (ai != null && !ai.IsReady) inGamePlayer++;
        }
        if (inGamePlayer == 0) return true;
        return inGamePlayer <= 1 && !string.IsNullOrEmpty(WinnerName);
    }

    public async UniTask StartAuctionTimer() // 제한 시간 시작
    {
        RemainingTime = auctionTime;
        IsTimeRunning = true;

        if (timeBar != null)
        {
            timeBar.gameObject.SetActive(true);
            timeBar.fillAmount = 1f;
        }

        while (RemainingTime > 0f && IsTimeRunning)
        {
            if (!IsCutscenePlaying)
            {
                RemainingTime -= Time.deltaTime;
                if (timeBar != null)
                {
                    timeBar.fillAmount = Mathf.Clamp01(RemainingTime / auctionTime);
                }
            }
            await UniTask.Yield();
        }

        IsTimeRunning = false;
        if (timeBar != null) timeBar.gameObject.SetActive(false);
    }

    // --- 주먹 경매 UI 입출력 ---
    public void EnableFistAuctionUI(bool active) // 주먹 경매용 버튼
    {
        if (fistBidInputField != null) fistBidInputField.gameObject.SetActive(active);
        if (fistSubmitButton != null) fistSubmitButton.gameObject.SetActive(active);
    }

    public async UniTask<int> WaitPlayerFistBidAsync() //플레이어 입력 대기
    {
        fistBidTcs = new UniTaskCompletionSource<int>();
        return await fistBidTcs.Task;
    }

    public void OnSubmitFistBid() // 주먹 경매용
    {
        if (fistBidInputField != null && int.TryParse(fistBidInputField.text, out int bid)) // 값 입력을 제대로 했다면
        {
            fistBidTcs?.TrySetResult(bid); // bid값 삽입
        }
        else
        {
            fistBidTcs?.TrySetResult(0); //입력 제대로 못할 시 0원 삽입
        }
    }

    // --- 입찰 경매 UI 입출력 ---
    public void EnableBiddingAcceptUI(bool active) // 입찰 경매 버튼 활성화
    {
        if (biddingAcceptButton != null) biddingAcceptButton.gameObject.SetActive(active); //입찰 경매 확인 버튼 활성화
        if (biddingRejectButton != null) biddingRejectButton.gameObject.SetActive(active); //입찰 경매 포기 버튼 활성화
    }

    public async UniTask<bool> WaitPlayerBiddingDecisionAsync()  //플레이어 선택 대기
    {
        biddingDecisionTcs = new UniTaskCompletionSource<bool>();
        return await biddingDecisionTcs.Task;
    }

    public void OnBiddingAccept() => biddingDecisionTcs?.TrySetResult(true); // 입찰 대기
    public void OnBiddingReject() => biddingDecisionTcs?.TrySetResult(false); // 거절 대기

    // --- 경매 마감 및 차감 로직 ---
    private async UniTask EndAuction() //경매 종료시
    {
        IsAuctioningFin = true; //끝났음 true
        isChatting = true; // 채팅 true
        IsTimeRunning = false; // 시간끝
        //버튼 전체 false
        NormalAuctionButtonReady(false);
        EnableFistAuctionUI(false);
        EnableBiddingAcceptUI(false);

        await ActionFinish(); // 경매 아저씨 대사 출력

        if (IfPlayerWin) //플레이어가 이겼을 경우
        {
            GlobalGold.Instance.UseGold(PlayerName, CurrentCost); // 플레이어 골드 소모
            UpdateGoldDisplay(); // 골드 시각적으로 보이기
            GlobalGold.Instance.SaveGame(); // 저장
        }
        else
        {
            foreach (var ai in auctionAiList) // ai싹다 돌아보기
            {
                if (ai != null && ai.NPCName == WinnerName) // ai이름과 이긴 ai의 이름을 체크
                {
                    GlobalGold.Instance.UseGold(ai.NPCName, CurrentCost); //이긴 ai 골드차감
                    GlobalGold.Instance.SaveGame(); // 저장
                    break;
                }
            }
        }
    }

    private async UniTask StartAuctionChatting()
    {
        List<string> chatList = ChatList.Instance.GetChat("경매시작", stuff.DisplayName); // 경매 시작에 잇는 대사들 출력
        if (chatList != null && chatList.Count > 0) // 
        {
            foreach (var chatMessage in chatList)
            {
                SetChat(chatMessage); //대사 출력
                await WaitInput();
            }
        }
    }

    private async UniTask IntroAuction()
    {
        List<string> chatList = ChatList.Instance.GetChat("물건소개", stuff.DisplayName);
        if (chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            {
                SetChat(chatMessage);
                await WaitInput();
            }
        }
    }

    private async UniTask ActionFinish()
    {
        string finalWinner = IfPlayerWin ? GlobalGold.Instance.mainCharacterdata[0].mainCharacterName : WinnerName;

        if (string.IsNullOrEmpty(finalWinner))
        {
            SetChat("낙찰자가 없습니다.");
            await WaitInput();
            return;
        }

        List<string> chatList = ChatList.Instance.GetChat("경매완료", finalWinner);
        if (chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            {
                SetChat(chatMessage);
                await WaitInput();
            }
        }
    }

    private async UniTask WaitInput() // 입력대기
    {
        await UniTask.Yield();
        await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Space));
    }

    private void NormalAuctionButtonReady(bool active) // 일반 경매 버튼 활성화
    {
        if (raiseButton != null) raiseButton.gameObject.SetActive(active);
        if (giveUpButton != null) giveUpButton.gameObject.SetActive(active);
    }
    private void FistAuctionButtonReady(bool active)
    {
        if (fistBidInputField != null) fistBidInputField.gameObject.SetActive(active);
        if (fistSubmitButton != null) fistSubmitButton.gameObject.SetActive(active);
    }
    private void BiddingAuctionButtonReady(bool active)
    {
        if(biddingAcceptButton != null) biddingAcceptButton.gameObject.SetActive(active);
        if (biddingRejectButton != null) biddingRejectButton.gameObject.SetActive(active);
    }
    private void UpdateGoldDisplay() // 보유 골드 시각적 업데이트 
    {
        if (myGold != null)
            myGold.text = "보유골드: " + GlobalGold.Instance.mainCharacterdata[0].mainCharacterGold.ToString();
    }

    public void GiveUpButton() // 포기 버튼
    {
        if (!IsTimeRunning || isPlayerGiveUp) return;
        isPlayerGiveUp = true;
        NormalAuctionButtonReady(false);
        ShowErrorMessage("입찰을 포기하셨습니다.").Forget();
        if (CheckAllGiveUp()) IsTimeRunning = false;
    }

    public void OnRaiseButton() // 레이즈 버튼
    {
        if (!IsTimeRunning || isPlayerGiveUp) return;
        if (WinnerName == GlobalGold.Instance.mainCharacterdata[0].mainCharacterName)
        {
            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
            return;
        }

        int needGold = CurrentCost + 100;
        if (GlobalGold.Instance.CanUseGold(PlayerName, needGold))
        {
            CurrentCost = needGold;
            UpdateAuctionCostUI();

            WinnerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName;
            IfPlayerWin = true;
            SetChat($"{GlobalGold.Instance.mainCharacterdata[0].mainCharacterName} 님이 {CurrentCost}G로 레이즈!");
            if (RemainingTime < 10f) RemainingTime = 10f;
        }
        else
        {
            ShowErrorMessage("골드가 부족합니다.").Forget();
        }
    }

    public async UniTask ShowErrorMessage(string message) //에러 메시지 출력 (중앙부 부터 쭉 올라가면서 메시지 출력)
    {
        if (errorMessage == null) return;

        errorMessage.DOKill();
        errorMessage.rectTransform.DOKill();
        errorMessage.gameObject.SetActive(true);
        errorMessage.text = message;

        Color color = errorMessage.color;
        color.a = 1f;
        errorMessage.color = color;
        errorMessage.rectTransform.anchoredPosition = errorMessagePos;

        await UniTask.Delay(500);

        float floatTime = 1f;
        float floatHight = 50f;

        errorMessage.rectTransform.DOAnchorPosY(errorMessagePos.y + floatHight, floatTime).SetEase(Ease.OutQuad);
        await errorMessage.DOFade(0f, floatTime).SetEase(Ease.OutQuad).AsyncWaitForCompletion();

        errorMessage.gameObject.SetActive(false);
    }
}




//public class Auction : PersistentSingleton<Auction>
//{
//    [Header("물건 이름 나올 곳")]
//    [SerializeField] public TextMeshProUGUI auctionName; // 이름 나오는 Text공간
//    [Header("물건 가격 나올 곳")]
//    [SerializeField] public TextMeshProUGUI auctionCost; // 가격 나오는 Text공간
//    [Header("물건 등급 나올 곳")]
//    [SerializeField] private TextMeshProUGUI auctionGrade; // 등급 나오는 Text공간
//    [Header("경매 대사 나올 곳")]
//    [SerializeField] public TextMeshProUGUI chat; // 채팅 나오는 Text공간
//    [Header("오류 메시지 출력")]
//    [SerializeField] public TextMeshProUGUI errorMessage; // 오류 메시지 출력 공간
//    [Header("내 골드 나올 곳")]
//    [SerializeField] public TextMeshProUGUI myGold; // 채팅 나오는 Text공간


//    [Header("조작 버튼")]
//    [SerializeField] public Button raiseButton;
//    [SerializeField] public Button giveUpButton;

//    [Header("참여 Ai 리스트")]
//    [SerializeField] public List<NpcAiBase> npcAiList; //참여 Ai 리스트

//    [Header("참여 Boss 리스트")]
//    [SerializeField] public List<BossAiBase> bossAiList; //참여 Boss 리스트

//    [Header("참여 Ai + Boss 리스트")]
//    [SerializeField] private List<AllAiBase> auctionAiList; //참여 Ai + Boss 리스트
//    [SerializeField] private int dayOfEnterNpc; //그날 하루 Npc 참여 수

//    [Header("경매 물품 리스트")]
//    public List<PartsDefinition> auctionItem; //경매 물품리스트 나중에 TestStuff를 바꿀것

//    [Header("플레이어 이름")]
//    private string playerName; //플레이어 이름


//    //=====================================컷씬 용도======================================================
//    [Header("컷씬 매니저")]
//    [SerializeField] private AuctionCutsceneManager cutsceneManager; //컷씬 매니저

//    private bool isCutscenePlaying = false; //컷씬 진행중인지 확인

//    //=================================경매진행변수==============================================
//    public int currentCost = 0; // 현재 가격
//    private PartsDefinition stuff;
//    private bool isAuctioningFin = true; // 경매가 끝낫는지 확인
//    public PartGrade stuffGrade;
//    //=======================시간 제한=================================
//    [Header("제한 시간 UI")]
//    [SerializeField] public Image timeBar; // 제한 시간 UI

//    private float auctionTime = 60f; // 경매 제한 시간
//    public float remainingTime = 0f; // 남은 시간
//    public bool isTimeRunning = false; // 시간 진행중인지 확인
//    //=======================대사 & 턴 제어================================
//    bool isChatting = false; // 대사 진행중인지 확인
//    private Vector2 errorMessagePos;

//    private bool isPlayerGiveUp = false;
//    private string winnerName = "";
//    private bool ifPlayerWin = false;

//    //===========================임시 콘솔창================================
//    [Header("임시 콘솔창")]
//    public TextMeshProUGUI console;

//    //=============================컷씬용 이벤트 ================================
//    private void OnEnable()
//    {
//        BossAiBase.OnBossBigRaiseCutscene += HandleBossBigRaiseCutscene;
//    }
//    private void OnDisable()
//    {
//        BossAiBase.OnBossBigRaiseCutscene -= HandleBossBigRaiseCutscene;
//    }

//    private async UniTask HandleBossBigRaiseCutscene(string bossName, string skillName)
//    {
//        isCutscenePlaying = true;

//        if (cutsceneManager != null)
//        {
//            await cutsceneManager.PlayBossCutsceneAsync(bossName, skillName); //컷씬 재생 동안 대기
//        }

//        isCutscenePlaying = false;
//    }
//    //===================================================================================
//    protected override void Awake()
//    {
//        base.Awake();
//        if (errorMessage != null)
//            errorMessagePos = errorMessage.rectTransform.anchoredPosition;

//        ButtonReady(false);//내 턴에 나타나는 버튼 비활성화
//    }

//    private void Start()
//    {
//        // 1. 플레이어 데이터 리스트 체크
//        if (GlobalGold.Instance.mainCharacterdata != null && GlobalGold.Instance.mainCharacterdata.Count > 0)
//        {
//            playerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName;
//        }
//        else
//        {
//            Debug.LogError("GlobalGold에 mainCharacterdata가 읍다");
//            playerName = "Player";
//        }
//        if (auctionItem != null && auctionItem.Count > 0)
//        {
//            stuff = auctionItem[Random.Range(0, auctionItem.Count)];
//            currentCost = stuff.Cost / 2;//반값 부터 시작
//            stuffGrade = stuff.Grade;
//            auctionGrade.text = stuffGrade.ToString();
//        }
//        else
//        {
//            Debug.LogError("경매 물품 리스트가 읍다");
//            return;
//        }

//        UpdateGoldDisplay();
//        DayOfAuctionEnter();
//        GetAuctionNpc(dayOfEnterNpc);
//    }
//    private void Update()
//    {
//        if (isAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
//        {
//            StartAuction().Forget();
//        }
//    }

//    //========================================경매 시작시 참여 npc목록 ========================================
//    private void DayOfAuctionEnter()
//    {
//        dayOfEnterNpc = Random.Range(3, 5); //그날 하루 참여 Npc 수 랜덤 7명까지 현재 임시로 4명까지
//    }
//    private void GetAuctionNpc(int enterNpc)//그날 하루 참여 Npc 목록
//    {
//        // 경매 참가 인원수가 전체 NPC 수보다 많으면 전체 수로 보정
//        auctionAiList.Clear();
//        bool isBossEnter = Random.value < 1f; //35%확률로 보스 참여 임시로 100%로 설정
//        if (isBossEnter && bossAiList.Count > 0)
//        {
//            int randomBossIndex = Random.Range(0, bossAiList.Count);
//            auctionAiList.Add(bossAiList[randomBossIndex]); //보스 참여
//        }
//        HashSet<int> selectedIndices = new HashSet<int>();
//        int targetCount = Mathf.Min(enterNpc, npcAiList.Count); // npcAiList 기준으로 제한

//        while (selectedIndices.Count < targetCount)
//        {
//            int randomIndex = Random.Range(0, npcAiList.Count);
//            selectedIndices.Add(randomIndex);
//        }

//        foreach (int index in selectedIndices)
//        {
//            auctionAiList.Add(npcAiList[index]); //auctionAiList에서 랜덤으로 선택된 애들 추가.
//        }
//    }

//    //============================================경매시작===========================================================

//    private async UniTask StartAuction() //경매 시작 부분.
//    {
//        isAuctioningFin = false;
//        isChatting = true;
//        ButtonReady(false); //플레이어 선택 버튼 비활성화
//        foreach (var ai in auctionAiList)
//        {
//            if (ai != null)
//            {
//                console.text += $"{ai.NPCName}님이 참가 했습니다.\n";
//                ai.ReadyForAction(); //AI 준비
//            }
//        }
//        // 첫 대사 하고
//        await StartAuctionChatting();
//        await UniTask.Delay(1000);
//        // 물품 보여주고
//        auctionName.text = stuff.DisplayName;
//        auctionCost.text = currentCost.ToString();
//        await WaitInput(); //입력대기
//        // 물품 소개 하고
//        await IntroAuction();

//        isAuctioningFin = false;
//        isPlayerGiveUp = false; //포기 초기화
//        winnerName = "";
//        ifPlayerWin = false;
//        auctionTime = 60f; // 첫 경매 시작 제한시간 재설정


//        isChatting = false;
//        ButtonReady(true); //플레이어 선택 버튼 활성화

//        isTimeRunning = true; //이거 해놔야 정상적으로 ai가 돌아감;; 진짜 조건부 힘들다
//        RunAllAiAsync().Forget();  // AI들의 독립 입찰 루프 가동
//        await StartAuctionTimer(); // 타이머 카운트다운 (시간 다 되면 자동으로 루프 통과)

//        //타이머 종료 후 낙찰 처리
//        await EndAuction();
//        isAuctioningFin = true;
//    }
//    //================================AI들의 실시간 루프========================================
//    private async UniTask RunAllAiAsync()
//    {
//        // 각 AI마다 독립적인 루프를 비동기로 동시 실행
//        List<UniTask> aiTasks = new List<UniTask>();
//        foreach (var ai in auctionAiList)
//        {
//            if (ai != null)
//            {
//                aiTasks.Add(AiRoutine(ai));
//            }
//        }

//        await UniTask.WhenAll(aiTasks);
//    }
//    // 개별 AI가 각자의 생각 주기(딜레이)를 갖고 독자적으로 입찰하는 루틴
//    private async UniTask AiRoutine(AllAiBase ai)
//    {
//        while (isTimeRunning && !isAuctioningFin)
//        {
//            // 각 AI마다 고민하는 시간을 다르게 부여 (5초~ 20초 사이)
//            int thinkDelay = ai.ThinkDelay;

//            if (!ai.IsReady)
//            {
//                console.text += $"{ai.NPCName}님이 {thinkDelay / 1000}초 동안 고민중.\n";
//            }

//            await UniTask.Delay(thinkDelay);

//            await UniTask.WaitWhile(() => isCutscenePlaying); //보스 컷씬 진행중이면 대기

//            if (!isTimeRunning || isAuctioningFin) break;

//            // 이미 자기가 최고 입찰자면 굳이 자기 돈을 또 올릴 필요 없음
//            if (winnerName == ai.NPCName)
//            {
//                console.text += $"{ai.NPCName}은 현재 최고 입찰자라 더이상 레이즈하지 않습니다.\n";
//                continue;
//            }


//            // AI가 포기 상태면 제외 (IsReady 혹은 IsGiveUp 플래그 확인)
//            if (ai.IsReady) continue;

//            // AI의 고유 판단 실행
//            if (ai.RaiseThink(currentCost, stuff.Cost))
//            {
//                int raiseStep = ai.RaiseGold; //각 Ai별 기본 입찰 단위
//                // 보스인지 확인 후 큰 레이즈 조건 체크
//                if (ai is BossAiBase boss)
//                {
//                    // 필요 시 특정 조건에서 금액을 대폭 증액
//                    if (boss.IsBigRaiseThink(currentCost, stuff.Cost))
//                    {//임시임 현재 물건이 400원이고 원래 가격이 200원이라면 1.5배 이상이므로 컷씬 연출
//                        raiseStep = boss.BigRaiseGold();
//                        currentCost += raiseStep; //조건 성립시 컷씬과 함꼐 500원증가

//                        // 옵저버 이벤트 발동 -> 모든 보스 공통 컷씬 실행 및 대기
//                        await boss.TriggerCutscene(boss.NPCName, boss.BigRaiseSkillName());
//                    }
//                    else
//                    {
//                        currentCost += raiseStep; //조건 미성립시 100원증가
//                    }
//                }
//                else
//                {
//                    currentCost += raiseStep; //일반 잡몹은 100원증가
//                }

//                auctionCost.text = currentCost.ToString();
//                winnerName = ai.NPCName;
//                ifPlayerWin = false;

//                chat.text = $"{ai.NPCName} 님이 {currentCost}G로 레이즈!";

//                if (remainingTime < 10f) //10초 미만이면 10초로 초기화
//                {
//                    remainingTime = 10f;
//                }
//            }
//            else
//            {
//                console.text += $"{ai.NPCName}판단 끝! 결과 포기.\n";
//                // 예산 초과 등으로 포기
//                chat.text = $"{ai.NPCName} 님이 입찰을 포기했습니다.";
//            }

//            // 모든 참가자가 포기했는지 수시로 검사
//            if (CheckAllGiveUp())
//            {
//                isTimeRunning = false; // 타이머를 즉시 종료시켜 경매 마감
//                break;
//            }
//        }
//    }

//    private bool CheckAllGiveUp() //모두 포기했는지 확인
//    {
//        int inGamePlayer = 0;
//        if (!isPlayerGiveUp) inGamePlayer++; //플레이어가 포기 안했으면 1더해주고
//        foreach (var ai in auctionAiList)
//        {
//            if (ai != null && !ai.IsReady) //AI가 포기 안했으면 또 더해주고
//            {
//                inGamePlayer++;
//            }
//        }
//        if (inGamePlayer == 0) return true;
//        return inGamePlayer <= 1 && !string.IsNullOrEmpty(winnerName); //1명 이하이면 경매 종료
//    }
//    //==============================경매 종료========================================

//    private async UniTask EndAuction()
//    {
//        isAuctioningFin = true;
//        isChatting = true;
//        isTimeRunning = false;
//        ButtonReady(false);

//        await ActionFinish();

//        if (ifPlayerWin)
//        {
//            GlobalGold.Instance.UseGold(playerName, currentCost); //플레이어 골드 차감
//            UpdateGoldDisplay(); //내 골드 표시 및 업데이트
//            GlobalGold.Instance.SaveGame(); //할지는 일단 대기
//        }
//        else
//        {
//            foreach (var ai in auctionAiList)
//            {
//                if (ai != null && ai.NPCName == winnerName)
//                {
//                    GlobalGold.Instance.UseGold(ai.NPCName, currentCost); //AI 골드 차감
//                    GlobalGold.Instance.SaveGame(); //AI 골드 저장
//                    break;
//                }
//            }
//        }

//    }
//    //======================채팅====================================
//    private async UniTask StartAuctionChatting() //경매 시작 부분 대사 함수
//    {
//        List<string> chatList = ChatList.Instance.GetChat("경매시작", stuff.DisplayName);
//        if (chatList != null && chatList.Count > 0)
//        {
//            foreach (var chatMessage in chatList)
//            {
//                chat.text = chatMessage;
//                await WaitInput(); //입력대기
//            }
//        }
//    }
//    private async UniTask IntroAuction() //물품 소개
//    {
//        List<string> chatList = ChatList.Instance.GetChat("물건소개", stuff.DisplayName);
//        if (chatList != null && chatList.Count > 0)
//        {
//            foreach (var chatMessage in chatList)
//            {
//                chat.text = chatMessage;
//                await WaitInput(); //입력대기
//            }
//        }
//    }
//    private async UniTask ActionFinish() //경매완료
//    {
//        string winerName = ifPlayerWin ? GlobalGold.Instance.mainCharacterdata[0].mainCharacterName : this.winnerName; //낙찰자 이름 결정

//        if (string.IsNullOrEmpty(winerName)) //낙찰자가 없으면
//        {
//            chat.text = "낙찰자가 없습니다.";
//            await WaitInput();
//            return;
//        }
//        List<string> chatList = ChatList.Instance.GetChat("경매완료", winerName);
//        if (chatList != null && chatList.Count > 0)
//        {
//            foreach (var chatMessage in chatList)
//            {
//                chat.text = chatMessage;
//                await WaitInput(); //입력대기
//            }
//        }
//    }
//    //============================================================================

//    private async UniTask WaitInput() //입력 대기
//    {
//        // 다음 대사까지 즉시 스킵되는 현상 방지
//        await UniTask.Yield();

//        // Space 키 눌릴 때까지 대기
//        await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Space));
//    }
//    //=============================== 제한 시간 ============================================================

//    private async UniTask StartAuctionTimer()
//    {
//        remainingTime = auctionTime; //남은시간초기화
//        isTimeRunning = true; //시간 진행중

//        if (timeBar != null) //시간바가 있으면(있겠지.)
//        {
//            timeBar.gameObject.SetActive(true); //시간바 활성화
//            timeBar.fillAmount = 1f; //시간바 초기화
//        }
//        while (remainingTime > 0f && isTimeRunning)
//        {
//            if (!isCutscenePlaying)
//            {
//                remainingTime -= Time.deltaTime; //남은시간 감소
//                if (timeBar != null)
//                {
//                    timeBar.fillAmount = Mathf.Clamp01(remainingTime / auctionTime); //시간바 업데이트
//                }
//            }
//            await UniTask.Yield(); //다음 프레임까지 대기
//        }
//        isTimeRunning = false; //시간 진행중 아님
//        //if(timeBar != null)
//        //{
//        //    timeBar.gameObject.SetActive(false); //시간바 비활성화
//        //}
//    }
//    //============================== 버튼 활성화/비활성화 ============================================================
//    private void ButtonReady(bool active)
//    {
//        if (raiseButton != null)
//            raiseButton.gameObject.SetActive(active);
//        if (giveUpButton != null)
//            giveUpButton.gameObject.SetActive(active);
//    }
//    //=============================== 내 보유 골드 표시 및 업데이트 ===========================================
//    private void UpdateGoldDisplay()
//    {
//        if (myGold != null)
//            myGold.text = "보유골드: " + GlobalGold.Instance.mainCharacterdata[0].mainCharacterGold.ToString(); //내 골드 가져오기
//        //여기서 내 골드 표시 UI 업데이트 코드 추가 가능
//    }
//    //=============================== 포기 or 레이즈 버튼클릭==================================================
//    public void GiveUpButton() //포기 버튼 클릭시
//    {
//        if (!isTimeRunning || isPlayerGiveUp) return;
//        isPlayerGiveUp = true;
//        ButtonReady(false); //버튼 비활성화
//        ShowErrorMessage("입찰을 포기하셨습니다.").Forget();
//        //isAuctioningFin = true;
//        if (CheckAllGiveUp())
//        {
//            isTimeRunning = false;
//        }
//    }
//    public void OnBigRaiseButton() //큰 레이즈 버튼 클릭
//    {
//        if (!isTimeRunning || isPlayerGiveUp) return;
//        if (winnerName == GlobalGold.Instance.mainCharacterdata[0].mainCharacterName) //이미 내가 최고 입찰자면 레이즈 불가
//        {
//            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
//            return;
//        }
//        int needGold = currentCost + 1000; //다음 입찰 가격
//        if (GlobalGold.Instance.CanUseGold(playerName, needGold)) //가격비교
//        {
//            currentCost = needGold;
//            auctionCost.text = currentCost.ToString();

//            winnerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName; //입찰자 이름 업데이트
//            ifPlayerWin = true;
//            chat.text = $"{GlobalGold.Instance.mainCharacterdata[0].mainCharacterName} 님이 {currentCost}G로 레이즈!";
//            if (remainingTime < 10f) //10초 미만이면 10초로 초기화
//            {
//                remainingTime = 10f;
//            }
//        }
//        else
//        {
//            ShowErrorMessage("골드가 부족합니다.").Forget();
//        }
//    }
//    public void OnRaiseButton() //일반 레이즈 버튼 클릭
//    {
//        if (!isTimeRunning || isPlayerGiveUp) return;
//        if (winnerName == GlobalGold.Instance.mainCharacterdata[0].mainCharacterName) //이미 내가 최고 입찰자면 레이즈 불가
//        {
//            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
//            return;
//        }
//        int needGold = currentCost + 100; //다음 입찰 가격
//        if (GlobalGold.Instance.CanUseGold(playerName, needGold)) //가격비교
//        {
//            currentCost = needGold;
//            auctionCost.text = currentCost.ToString();

//            winnerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName; //입찰자 이름 업데이트
//            ifPlayerWin = true;
//            chat.text = $"{GlobalGold.Instance.mainCharacterdata[0].mainCharacterName} 님이 {currentCost}G로 레이즈!";
//            if (remainingTime < 10f) //10초 미만이면 10초로 초기화
//            {
//                remainingTime = 10f;
//            }
//        }
//        else
//        {
//            ShowErrorMessage("골드가 부족합니다.").Forget();
//        }
//    }
//    //========================================오류 메시지 출력==================================================
//    private async UniTask ShowErrorMessage(string message) //오류 메시지 출력
//    {
//        errorMessage.DOKill(); // 이전 애니메이션 중지
//        errorMessage.rectTransform.DOKill(); // 중지
//        errorMessage.gameObject.SetActive(true);
//        errorMessage.text = message;

//        //원래 위치로
//        Color color = errorMessage.color;
//        color.a = 1f; // 불투명하게 설정
//        errorMessage.color = color;
//        errorMessage.rectTransform.anchoredPosition = errorMessagePos; // 원래 위치로 초기화

//        await UniTask.Delay(500);

//        float floatTime = 1f;
//        float floatHight = 50f;

//        errorMessage.rectTransform.DOAnchorPosY(errorMessagePos.y + floatHight, floatTime).SetEase(Ease.OutQuad); //Y축으로 올라가유

//        await errorMessage.DOFade(0f, floatTime).SetEase(Ease.OutQuad).AsyncWaitForCompletion();//페이드 아웃 트윈

//        errorMessage.gameObject.SetActive(false);
//    }
//}
