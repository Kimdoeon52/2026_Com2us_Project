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

    //=================================모드 선택=========================
    [Header("모드 선택 UI")]
    [SerializeField] public GameObject modeSelectPanel;
    [SerializeField] public Button normalModeButton;
    [SerializeField] public Button fistModeButton;

    private UniTaskCompletionSource<AuctionType> modeSelectTcs;
    //===============================AI 위치 시키는 용도====================
    [Header("AI 슬롯 위치 (8칸)")]
    [SerializeField] private List<RectTransform> aiSlots;
    private List<AllAiBase> spawnedAiList = new List<AllAiBase>();
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
            { AuctionType.Fist, new FistAuctionMode() }
        };

        if (errorMessage != null)
            errorMessagePos = errorMessage.rectTransform.anchoredPosition; //첫 위치 저장

        // UI 버튼 이벤트 바인딩
        if (normalModeButton != null)
            normalModeButton.onClick.AddListener(OnSelectNormalMode);
        if (fistModeButton != null)
            fistModeButton.onClick.AddListener(OnSelectFistMode);
        if (fistSubmitButton != null)
            fistSubmitButton.onClick.AddListener(OnSubmitFistBid);
        if (raiseButton != null)
            raiseButton.onClick.AddListener(OnRaiseButton);
        if (giveUpButton != null)
            giveUpButton.onClick.AddListener(GiveUpButton);

        NormalAuctionButtonReady(false); // 레이즈/포기   버튼 비활
        EnableFistAuctionUI(false); 
        EnableModeSelectUI(false); // 버튼 비활
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

        StartAuction().Forget(); //옥션 시작
    }
    //private void Update()
    //{
    //    if (IsAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
    //    {
    //        StartAuction().Forget(); //딱 한번 실행.옥션 시작
    //    }
    //}

    private void DayOfAuctionEnter()
    {
        dayOfEnterNpc = Random.Range(3, 5); // 경매 인원 3~4 (임시)
    }

    private void GetAuctionNpc(int enterNpc) // 그날 하루 경매할 npc 수 매개변수
    {
        ClearSpawnedAi(); // 이전 회차 AI UI오브 젝트 제거

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

        SpawnAndPositionAi(); //Canvas 위치에 AI UI 푸리팹 생성 및 배치하는거임
    } // 총원은 enterNpc + 1(보스) 명

    private async UniTask StartAuction()
    {
        IsAuctioningFin = false; // 아직 경매 안끝남
        // 시작하자마자 유저 모드 선택부터 대기(스페이스바 입력 영향 안 받음)
        currentAuctionType = await WaitPlayerSelectModeAsync();
        AppendConsoleLog($"선택된 경매 모드: {currentAuctionType}");
        isChatting = true; // 채팅 시작

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

        // 선택된 모드에 맞는 조작 UI 전용 활성화
        if (currentAuctionType == AuctionType.Normal)
        {
            NormalAuctionButtonReady(true);
            EnableFistAuctionUI(false);
        }
        else if (currentAuctionType == AuctionType.Fist)
        {
            NormalAuctionButtonReady(false);
            EnableFistAuctionUI(true);
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
    //=========================모드 선택 관련 함수들===================
    public void EnableModeSelectUI(bool active)
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(active);
    }

    public void OnSelectNormalMode() => modeSelectTcs?.TrySetResult(AuctionType.Normal);
    public void OnSelectFistMode() => modeSelectTcs?.TrySetResult(AuctionType.Fist);

    public async UniTask<AuctionType> WaitPlayerSelectModeAsync()
    {
        // 이전 대사 넘기기용 스페이스바/마우스 클릭 신호가 씹히도록 1프레임 대기
        await UniTask.Yield();

        // 모드 선택 패널 활성화
        EnableModeSelectUI(true);

        modeSelectTcs = new UniTaskCompletionSource<AuctionType>();

        // 유저가 모드 선택 버튼을 누를 때까지 대기
        AuctionType selectedType = await modeSelectTcs.Task;

        // 선택 완료 후 패널 비활성화
        EnableModeSelectUI(false);

        return selectedType;
    }

    // --- 경매 마감 및 차감 로직 ---
    private async UniTask EndAuction() //경매 종료시
    {
        IsAuctioningFin = true; //끝났음 true
        isChatting = true; // 채팅 true
        IsTimeRunning = false; // 시간끝
        //버튼 전체 false
        NormalAuctionButtonReady(false);
        EnableFistAuctionUI(false);

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
    //================================Canvas에 AI 설치 =======================
    private void SpawnAndPositionAi()
    {
        for (int i = 0; i < auctionAiList.Count; i++)
        {
            if (i >= aiSlots.Count) break; // 슬롯 개수 초과 방지

            AllAiBase aiPrefab = auctionAiList[i];
            RectTransform slotTransform = aiSlots[i];

            if (aiPrefab != null && slotTransform != null)
            {
                // Canvas 슬롯의 자식으로 프리팹 생성
                AllAiBase spawnedAi = Instantiate(aiPrefab, slotTransform);

                // UI RectTransform 좌표 및 스케일 초기화 (슬롯 중앙 고정)
                RectTransform aiRect = spawnedAi.GetComponent<RectTransform>();
                if (aiRect != null)
                {
                    aiRect.anchoredPosition = Vector2.zero; // 슬롯 정중앙 위치
                    aiRect.localScale = Vector3.one;       // 크기 보정
                }
                else
                {
                    spawnedAi.transform.localPosition = Vector3.zero;
                    spawnedAi.transform.localScale = Vector3.one;
                }

                // 생성된 AI 스폰 리스트에 등록
                spawnedAiList.Add(spawnedAi);
            }
        }

        // 실제 씬에 생성되어 가동 중인 AI 리스트로 교체
        auctionAiList = new List<AllAiBase>(spawnedAiList);
    }

    private void ClearSpawnedAi()
    {
        foreach (var ai in spawnedAiList)
        {
            if (ai != null)
            {
                Destroy(ai.gameObject);
            }
        }
        spawnedAiList.Clear();
    }
    // ==============================에러 메시지==========================
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