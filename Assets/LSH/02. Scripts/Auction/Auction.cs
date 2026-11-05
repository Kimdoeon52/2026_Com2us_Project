using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

public class Auction : Singleton<Auction>
{
    [Header("경매 설정")]
    [SerializeField] public AuctionType currentAuctionType = AuctionType.Normal; //현재 경매 모드

    [Header("연속 경매 설정")]
    [SerializeField] private int totalAuctionRounds = 3; //연속으로 진행할 경매 횟수
    [SerializeField] private string nextSceneName = ""; //경매 전부 끝난 뒤 이동할 씬 (비워두면 이동 안 함)
    private int currentRound = 0; //현재 진행 중인 회차
    [SerializeField] [Min(1f)] private float giveUpTimeScale = 3f; //플레이어가 포기하면 경매를 n배속으로 진행 (빠른 종료 유도)

    [Header("물건 정보 UI")]
    [SerializeField] public TextMeshProUGUI auctionName; //물건 이름
    [SerializeField] public TextMeshProUGUI auctionCost; //물건 가격
    [SerializeField] public TextMeshProUGUI auctionGrade;// 물건 등급

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
    [SerializeField] private float fistBidTime = 20f; // 주먹 경매 입력 제한 시간 (시간 초과 시 자동 0원)
    public float FistBidTime => fistBidTime;

    [Header("참여 AI 리스트")]
    [SerializeField] public List<NpcAiBase> npcAiList; //참여Ai목록
    [SerializeField] private AuctionStageData stageData; //스테이지별 참여Boss목록
    [SerializeField] public List<AllAiBase> auctionAiList; //지금 경매에 참여하고 있는 Ai들
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

    //===================================경매 탈출 버튼===================
    [SerializeField] private Button getOutAuction;

    //===================================상점=============================
    [Header("상점 UI")]
    [SerializeField] private GameObject shopPanel; //상점 창
    [SerializeField] private Button shopButton; //모드 선택 화면의 상점 열기 버튼
    [SerializeField] private Button shopCloseButton; //상점 닫기 버튼

    private UniTaskCompletionSource<AuctionType> modeSelectTcs;
    private bool isSelectingMode = false; //모드 선택 대기 중인지 체크 (상점은 이때만 열 수 있음)
    private bool IsShopOpen => shopPanel != null && shopPanel.activeSelf;
    //===============================ai 슬롯 위치====================
    [Header("AI 슬롯")]
    [SerializeField] private List<RectTransform> aiSlots;

    private List<AllAiBase> spawnedAiList = new List<AllAiBase>();
    private List<GameObject> roundUiList = new List<GameObject>(); // 회차마다 지울 연출용 UI (주먹 경매 명패 등)


    // --- 프로퍼티 & 캡슐화 변수 ---
    public string PlayerName { get; private set; } // 플레이어 이름 받아올 용도의 변수
    public int PlayerID { get; set; } // 승자 이름 받아올 용도의 변수
    public int CurrentCost { get; set; } = 0; //현재 물건 가격을 나타낼 변수
    public bool IsAuctioningFin { get; set; } = true; //경매가 끝났는지 체크하는 변수
    public bool IsCutscenePlaying { get; set; } = false; //컷씬이 실행되고 있는지 체크하는 변수
    public int WinnerID { get; set; } = -1; //승자 ID를 저장할 변수
    public bool IfPlayerWin { get; set; } = false; //플레이어가 이겼는지 체크하는 변수

    public PartsDefinition stuff; //현재 경매 물품을 저장할 용도
    public PartGrade stuffGrade; //경매 물품 등급을 저장할 용도
    private Vector2 errorMessagePos; //에러 메시지가 나올 초기 위치
    private bool isPlayerGiveUp = false; //플레이가 포기했는지 체크하는 용도

    // 경매 전략 딕셔너리
    private Dictionary<AuctionType, IAuctionMode> nowAuctionMode;

    // 비동기 UI 입력 수신용 CS
    private UniTaskCompletionSource<int> fistBidTcs;

    //======================슬롯 이미지=====================
    [Header("플레이어 및 NPC 슬롯")]
    [SerializeField] public Image[] npcs;
    [SerializeField] private Image playerImage;
    public RectTransform PlayerSlot => playerImage != null ? playerImage.rectTransform : null;

    //==========================사운드=============================
    [Header("효과음 설정")]
    [SerializeField] private AudioSource sfxAudioSource; // 사운드 재생용 컴포넌트
    [SerializeField] public AudioClip giveUpEffect;      // GiveUpEffect
    [SerializeField] public AudioClip dialogueEffect;    // DialogueEffect
    [SerializeField] public AudioClip useSkillEffect;    // UseSkillEffect
    [SerializeField] public AudioClip clickSoundEffect;
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
        BossAiBase.OnBossBigRaiseCutscene += HandleBossBigRaiseCutscene; //컷씬 옵져버 등록 OnBossBigRaiseCutscene 실행시 HandleBossBigRaiseCutscene발동
    }

    private void OnDisable()
    {
        BossAiBase.OnBossBigRaiseCutscene -= HandleBossBigRaiseCutscene; // 컷씬 옵져버 해제
        ResetGameSpeed(); // 배속 상태로 씬을 떠나도 다음 씬에 배속이 남지 않게 복구
    }

    private async UniTask HandleBossBigRaiseCutscene(string bossName, string skillName, Sprite bannerSprite, Sprite characterSprite, Color newColor) //컷씬 대기용 함수
    {
        IsCutscenePlaying = true; // 컷씬 플레이 시작
        if (cutsceneManager != null)
        {
            await cutsceneManager.PlayBossCutsceneAsync(bossName, skillName, bannerSprite, characterSprite, newColor); // awiat으로 컷씬 연출 대기
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

        // UI 버튼 이벤트 바인딩 (인스펙터 중복 등록 방지를 위해 기존 리스너 제거 후 등록)
        BindButton(normalModeButton, OnSelectNormalMode);
        BindButton(fistModeButton, OnSelectFistMode);
        BindButton(fistSubmitButton, OnSubmitFistBid);
        BindButton(raiseButton, OnRaiseButton);
        BindButton(giveUpButton, GiveUpButton);
        BindButton(getOutAuction, OnGGAuction);
        BindButton(shopButton, OnOpenShop);
        BindButton(shopCloseButton, OnCloseShop);
        if (fistBidInputField != null)
        {
            fistBidInputField.onSubmit.RemoveAllListeners();
            fistBidInputField.onSubmit.AddListener(_ => OnSubmitFistBid()); // 엔터로도 제출
        }

        NormalAuctionButtonReady(false); // 레이즈/포기   버튼 비활
        EnableFistAuctionUI(false);
        EnableModeSelectUI(false); // 버튼 비활
        if (shopPanel != null) shopPanel.SetActive(false); // 상점은 닫힌 채로 시작
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        // RemoveAllListeners는 코드로 추가한 리스너만 지우고 인스펙터(Persistent) 리스너는 못 지움
        // 인스펙터에도 같은 함수가 등록돼 있으면 클릭 한 번에 두 번 호출되므로 Persistent 리스너를 꺼버림
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
    

    private void Start()
    {
        if (GlobalGold.Instance.mainCharacterdata != null && GlobalGold.Instance.mainCharacterdata.Count > 0)
        {
            PlayerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName; //플레이어 이름 삽입
            PlayerID = GlobalGold.Instance.mainCharacterdata[0].mainCharacterID;
        }
        else
        {
            Debug.LogError("GlobalGold에 mainCharacterdata가 읍다");
            PlayerName = "Player"; // 플레이어 이름 비우면 그냥 플레이어
            PlayerID = 0;
        }

        RunAuctionSessionAsync().Forget(); // 경매 연속 진행 시작
    }

    // 경매를 totalAuctionRounds번 연속 진행하고, 끝나면 다음 씬으로 이동
    private async UniTask RunAuctionSessionAsync()
    {
        SettleConsignments(); // 플레이어 위탁품 판매 정산

        for (currentRound = 1; currentRound <= totalAuctionRounds; currentRound++)
        {
            if (!PrepareRound()) return; // 물품이 없으면 중단

            AppendConsoleLog($"===== {currentRound}/{totalAuctionRounds} 번째 경매 =====");
            await StartAuction();
        }

        GlobalGold.Instance.AdvanceStage(stageData.StageCount); // 세션이 끝나면 다음 스테이지로
        MoveToNextScene(1000).Forget();
    }

    // 위탁 창에서 맡긴 파츠를 고정 낙찰가로 팔고 골드 지급
    private void SettleConsignments()
    {
        foreach (ConsignedPart part in ConsignmentSettlement.Settle())
        {
            AppendConsoleLog($"[위탁 판매] {ConsignmentSettlement.NameOf(part)} 낙찰 +{part.Price}G");
        }
    }

    // 현재 스테이지의 보스 명단 가져오기
    private List<BossAiBase> GetStageBosses()
    {
        int stage = GlobalGold.Instance.CurrentStage;
        List<BossAiBase> bosses = stageData != null ? stageData.GetBosses(stage) : null;
        if (bosses == null)
        {
            Debug.LogError($"Stage{stage} 보스 명단이 읍다. stageData를 확인해라");
        }
        return bosses;
    }

    // 회차 시작 전 물품/참가자/상태 초기화
    private bool PrepareRound()
    {
        if (auctionItem == null || auctionItem.Count == 0)
        {
            Debug.LogError("경매 물품 리스트가 읍다");
            return false;
        }

        List<BossAiBase> stageBosses = GetStageBosses();
        if (stageBosses == null) return false; // 보스 명단이 없으면 중단

        stuff = auctionItem[Random.Range(0, auctionItem.Count)]; // stuff에 경매에 있는 아이템 랜덤 등록
        CurrentCost = stuff.Cost / 2;
        stuffGrade = stuff.Grade; // 등급 넣기
        if (auctionGrade != null) auctionGrade.text = stuffGrade.ToString(); // Text에 등급 보여주기
        if (auctionName != null) auctionName.text = "";

        WinnerID = -1;
        IfPlayerWin = false;
        isPlayerGiveUp = false;
        IsTimeRunning = false;
        ResetGameSpeed(); // 이전 회차에서 포기로 걸린 배속 해제

        ClearRoundUI(); // 이전 회차 명패 제거
        NPCImageOff();
        UpdateGoldDisplay(); // 골드 업데이트
        DayOfAuctionEnter(); // 그 회차 경매할 인원 정하기
        GetAuctionNpc(dayOfEnterNpc, stageBosses); // ai 정하기
        return true;
    }

    // 모든 경매가 끝난 뒤 씬 이동. nextSceneName이 비어 있으면 이동하지 않음
    private async UniTask MoveToNextScene(int waitDelay)
    {
        await UniTask.Delay(waitDelay);
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.Log("[Auction] 모든 경매 종료. 이동할 씬이 지정되지 않아 이동하지 않음.");
            return;
        }

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(nextSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
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
        dayOfEnterNpc = Random.Range(5, 7); // 경매 인원 5~7 (모든 랭커5(필수) + 쩌리(N명))
    }

    private void GetAuctionNpc(int enterNpc, List<BossAiBase> stageBosses) // 그날 하루 경매할 npc 수, 현재 스테이지 보스 명단 매개변수
    {
        ClearSpawnedAi(); // 이전 회차 AI UI오브 젝트 제거

        auctionAiList.Clear(); //옥션 리스트 비우고
        //bool isBossEnter = Random.value < 1f; //지금은 1f로 실험용인데 나중에 확률 30%로 바꾸기
        foreach(BossAiBase isBossEnter in stageBosses)
        {
            auctionAiList.Add(isBossEnter);
            Debug.Log($"{isBossEnter.name} 참여");
        }

        HashSet<int> selectedIndices = new HashSet<int>(); // 중첩되면 안되니까 hash
        int targetCount = Mathf.Min(enterNpc - auctionAiList.Count, npcAiList.Count); // 보스가 들어갔으면 그만큼 빼서 총원이 enterNpc가 되게 함

        while (selectedIndices.Count < targetCount) //
        {
            selectedIndices.Add(Random.Range(0, npcAiList.Count)); //npc리스트에 그날 하루 인원 수 만큼 삽입
        }

        foreach (int index in selectedIndices) //중첩 안된 랜덤한 Npc를 auctionAiList에 넣기
        {
            auctionAiList.Add(npcAiList[index]);
        }

        //Canvas 위치에 AI UI 푸리팹 생성 및 배치하는거임
    } // 총원은 보스 포함 enterNpc 명

    private async UniTask StartAuction()
    {
        IsAuctioningFin = false; // 아직 경매 안끝남
        // 시작하자마자 유저 모드 선택부터 대기(스페이스바 입력 영향 안 받음)
        currentAuctionType = await WaitPlayerSelectModeAsync();
        AppendConsoleLog($"선택된 경매 모드: {currentAuctionType}");
        //==========Npc UI 활성화 ==========
        NPCImageOn();
        SpawnAndPositionAi();
        //===========================
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
        WinnerID  = -1;
        IfPlayerWin = false;
        auctionTime = 60f;


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
            if (ai != null && !ai.IsGiveUp) inGamePlayer++;
        }
        if (inGamePlayer == 0) return true;
        return inGamePlayer <= 1 && WinnerID != -1;
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
        if (fistBidInputField != null)
        {
            fistBidInputField.gameObject.SetActive(active);
            fistBidInputField.text = "";
            fistBidInputField.interactable = active;
        }
        if (fistSubmitButton != null)
        {
            fistSubmitButton.gameObject.SetActive(active);
            fistSubmitButton.interactable = active;
        }
    }

    public async UniTask<int> WaitPlayerFistBidAsync() //플레이어 입력 대기
    {
        fistBidTcs = new UniTaskCompletionSource<int>();
        if (fistBidInputField != null) fistBidInputField.ActivateInputField(); // 바로 타이핑 가능하게
        return await fistBidTcs.Task;
    }

    public void OnSubmitFistBid() // 주먹 경매용
    {
        if (fistBidTcs == null || fistBidInputField == null) return; // 입력 대기 중이 아님

        string text = fistBidInputField.text.Trim();
        if (!int.TryParse(text, out int bid)) // 빈칸/문자/너무 큰 수
        {
            RetryFistBid("금액을 입력해주세요. (포기는 0)");
            return;
        }
        if (bid < 0) // 음수는 다시 입력
        {
            RetryFistBid("음수는 입력할 수 없습니다. 다시 입력해주세요.");
            return;
        }
        int playerGold = GlobalGold.Instance != null ? GlobalGold.Instance.PlayerGold : 0;
        if (bid > playerGold) // 낙찰돼도 지불 못하는 금액은 다시 입력
        {
            RetryFistBid($"보유 골드({playerGold:N0}G)를 초과했습니다.");
            return;
        }

        ForceFistBid(bid);
    }

    public void ForceFistBid(int bid) // 입찰 확정 (시간 초과 시 0원 강제 제출에도 사용)
    {
        PlaySound(clickSoundEffect);
        if (fistBidTcs == null) return;
        UniTaskCompletionSource<int> tcs = fistBidTcs;
        fistBidTcs = null; // 중복 제출 방지

        if (fistBidInputField != null) fistBidInputField.interactable = false;
        if (fistSubmitButton != null) fistSubmitButton.interactable = false;
        tcs.TrySetResult(bid);
    }

    private void RetryFistBid(string message)
    {
        ShowErrorMessage(message).Forget();
        fistBidInputField.text = "";
        fistBidInputField.ActivateInputField();
        RectTransform fieldRect = (RectTransform)fistBidInputField.transform;
        fieldRect.DOKill(true);
        fieldRect.DOShakeAnchorPos(0.3f, 10f, 20); // 잘못 입력했다는 흔들림
    }

    //=========================회차 연출 UI===================
    public void RegisterRoundUI(GameObject go)
    {
        if (go != null) roundUiList.Add(go);
    }

    public void ClearRoundUI()
    {
        foreach (var go in roundUiList)
        {
            if (go != null) Destroy(go);
        }
        roundUiList.Clear();
    }
    //=========================모드 선택 관련 함수들===================
    public void EnableModeSelectUI(bool active)
    {
        if (modeSelectPanel != null) modeSelectPanel.SetActive(active);
    }

    public void OnSelectNormalMode()
    {
        PlaySound(clickSoundEffect);
        if (IsShopOpen) return; // 상점이 열려 있으면 경매 시작 안 함
        modeSelectTcs?.TrySetResult(AuctionType.Normal);
    }

    public void OnSelectFistMode()
    {
        PlaySound(clickSoundEffect);
        if (IsShopOpen) return;
        modeSelectTcs?.TrySetResult(AuctionType.Fist);
    }

    public async UniTask<AuctionType> WaitPlayerSelectModeAsync()
    {
        // 이전 대사 넘기기용 스페이스바/마우스 클릭 신호가 씹히도록 1프레임 대기
        await UniTask.Yield();

        // 버튼이 보이기 전에 신호기부터 준비 (패널이 켜진 프레임에 눌러도 씹히지 않게)
        modeSelectTcs = new UniTaskCompletionSource<AuctionType>();
        isSelectingMode = true;

        // 모드 선택 패널 활성화
        EnableModeSelectUI(true);

        // 유저가 모드 선택 버튼을 누를 때까지 대기
        AuctionType selectedType = await modeSelectTcs.Task;

        // 선택 완료 후 패널 비활성화
        isSelectingMode = false;
        EnableModeSelectUI(false);
        if (shopPanel != null) shopPanel.SetActive(false); // 혹시 열려 있으면 같이 닫기

        return selectedType;
    }

    //=========================상점 열기/닫기===================
    public void OnOpenShop()
    {
        if (!isSelectingMode || IsShopOpen || shopPanel == null) return; // 모드 선택 중에만 열 수 있음

        EnableModeSelectUI(false); // 상점 뒤에서 경매 버튼이 눌리지 않게 숨김
        shopPanel.SetActive(true);
    }

    public void OnCloseShop()
    {
        if (!IsShopOpen) return;

        shopPanel.SetActive(false);
        UpdateGoldDisplay(); // 상점에서 판매한 골드 반영
        if (isSelectingMode) EnableModeSelectUI(true); // 다시 경매 모드를 고를 수 있게
    }

    // --- 경매 마감 및 차감 로직 ---
    private async UniTask EndAuction() //경매 종료시
    {
        IsAuctioningFin = true; //끝났음 true
        IsTimeRunning = false; // 시간끝
        ResetGameSpeed(); // 낙찰 대사는 정상 속도로
        //버튼 전체 false
        NormalAuctionButtonReady(false);
        EnableFistAuctionUI(false);

        await ActionFinish(); // 경매 아저씨 대사 출력

        if (IfPlayerWin) //플레이어가 이겼을 경우
        {
            // 인벤토리에 먼저 넣어보고 성공했을 때만 골드 차감 (칸이 없으면 골드만 빠지는 거 방지)
            if (InventoryHost.Instance != null && InventoryHost.Instance.GivePart(stuff))
            {
                GlobalGold.Instance.UseGold(GlobalGold.Instance.mainCharacterdata[0].mainCharacterID, CurrentCost); // 플레이어 골드 소모
                UpdateGoldDisplay(); // 골드 시각적으로 보이기
                GlobalGold.Instance.SaveGame(); // 저장
            }
            else
            {
                Debug.LogWarning($"[Auction] 인벤토리에 {stuff.DisplayName}을(를) 넣지 못해 낙찰을 취소함 (인벤토리 가득 참 또는 InventoryHost 없음)");
            }
        }
        else
        {
            foreach (var ai in auctionAiList) // ai싹다 돌아보기
            {
                if (ai != null && ai.NPCID == WinnerID) // ai이름과 이긴 ai의 이름을 체크
                {
                    GlobalGold.Instance.UseGold(ai.NPCID, CurrentCost); //이긴 ai 골드차감
                    GlobalGold.Instance.ChangePartsGrade(ai.NPCID, stuffGrade);
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
        if (GlobalGold.Instance == null || GlobalGold.Instance.mainCharacterdata == null || GlobalGold.Instance.mainCharacterdata.Count == 0)
        {
            Debug.LogError("[ActionFinish] GlobalGold 데이터가 없습니다.");
            return;
        }
        int finalWinner = IfPlayerWin ? GlobalGold.Instance.mainCharacterdata[0].mainCharacterID : WinnerID;

        if (finalWinner == -1)
        {
            SetChat("낙찰자가 없습니다.");
            await WaitInput();
            return;
        }
        string winnerName = IfPlayerWin ? PlayerName : auctionAiList.Find(ai => ai.NPCID == finalWinner)?.NPCName ?? "알 수 없음";
        List<string> chatList = ChatList.Instance.GetChat("경매완료", winnerName);
        if (chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            {
                SetChat(chatMessage);
                await WaitInput();
            }
        }
    }

    private async UniTask WaitInput() // 입력대기 (스페이스바 또는 마우스 왼쪽 클릭)
    {
        await UniTask.Yield();
        await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !IsPointerOverButton()));
    }

    private bool IsPointerOverButton() // 버튼을 누른 클릭은 대사 넘김으로 치지 않음 (경매 나가기 등 오작동 방지)
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.GetComponentInParent<Selectable>() != null) return true;
        }
        return false;
    }

    private void ResetGameSpeed()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = 1f;
    }

    private void NormalAuctionButtonReady(bool active) // 일반 경매 버튼 활성화
    {
        PlaySound(clickSoundEffect);
        if (raiseButton != null) raiseButton.gameObject.SetActive(active);
        if (giveUpButton != null) giveUpButton.gameObject.SetActive(active);
    }
    private void UpdateGoldDisplay() // 보유 골드 시각적 업데이트 
    {
        if (myGold != null
            && GlobalGold.Instance != null
            && GlobalGold.Instance.mainCharacterdata != null
            && GlobalGold.Instance.mainCharacterdata.Count > 0)
        {
            myGold.text = "보유골드: " + GlobalGold.Instance.mainCharacterdata[0].mainCharacterGold.ToString();
        }
    }

    public void GiveUpButton() // 포기 버튼
    {
        if (!IsTimeRunning || isPlayerGiveUp) return;
        isPlayerGiveUp = true;
        NormalAuctionButtonReady(false);
        PlaySound(clickSoundEffect);
        PlaySound(giveUpEffect);
        if (AuctionPlayer.Instance != null)
            AuctionPlayer.Instance.ShowThinkChatWithText("못해먹겠네. 안해.");
        ShowErrorMessage($"입찰을 포기하셨습니다. ({giveUpTimeScale:0.#}배속)").Forget();
        Time.timeScale = giveUpTimeScale; // 포기했으니 남은 경매는 빠르게 진행 (AI 고민/타이머/연출 모두 배속)
        if (CheckAllGiveUp()) IsTimeRunning = false;
    }

    public void OnRaiseButton() // 레이즈 버튼
    {
        PlaySound(clickSoundEffect);
        if (!IsTimeRunning || isPlayerGiveUp || IsCutscenePlaying) return;
        if (GlobalGold.Instance == null || GlobalGold.Instance.mainCharacterdata == null || GlobalGold.Instance.mainCharacterdata.Count == 0)
        {
            Debug.LogError("[OnRaiseButton] GlobalGold 데이터가 없습니다.");
            return;
        }
        if (WinnerID != -1 && WinnerID == GlobalGold.Instance.mainCharacterdata[0].mainCharacterID)
        {
            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
            return;
        }
        Debug.Log($"[비교 확인] WinnerID: '{WinnerID}' / PlayerID: '{GlobalGold.Instance.mainCharacterdata[0].mainCharacterID}'");
        int needGold = CurrentCost + 15;
        if (GlobalGold.Instance.CanUseGold(GlobalGold.Instance.mainCharacterdata[0].mainCharacterID, needGold))
        {
            CurrentCost = needGold;
            UpdateAuctionCostUI();
            WinnerID = GlobalGold.Instance.mainCharacterdata[0].mainCharacterID;
            IfPlayerWin = true;
            SetChat($"{GlobalGold.Instance.mainCharacterdata[0].mainCharacterName} 님이 {CurrentCost}G로 레이즈!");
            if (RemainingTime < 10f) RemainingTime = 10f;
            if (AuctionPlayer.Instance != null)
                AuctionPlayer.Instance.ShowThinkChatWithText($"{needGold}G 레이즈!");
        }
        else
        {
            ShowErrorMessage("골드가 부족합니다.").Forget();
        }
    }
    public void OnGGAuction() //경매 자체 포기
    {
        ShowErrorMessage("경매를 나갑니다.").Forget();
        MoveToNextScene(2000).Forget();
    }
    //================================Canvas에 AI 설치 =======================
    private void SpawnAndPositionAi()
    {
        if (npcs == null || npcs.Length == 0)
        {
            Debug.LogError("[SpawnAndPositionAi] npcs 배열이 비어있거나 null입니다. 인스펙터를 확인하세요.");
            return;
        }
        if (aiSlots == null || aiSlots.Count == 0)
        {
            Debug.LogError("[SpawnAndPositionAi] aiSlots 리스트가 비어있거나 null입니다. 인스펙터를 확인하세요.");
            return;
        }

        // npcs 배열의 인덱스가 중복되지 않게 무작위로 뽑기 위한 HashSet
        HashSet<int> selectIndex = new HashSet<int>();

        // 배치할 개수 결정 (npcs 배열 크기와 auctionAiList 크기 중 작은 쪽 기준)
        int selectCount = Mathf.Min(npcs.Length, auctionAiList.Count);
        // npcs 배열 안에서 중복되지 않는 랜덤한 위치(인덱스)를 뽑음
        while (selectIndex.Count < selectCount)
        {
            int ranIndex = Random.Range(0, npcs.Length);
            selectIndex.Add(ranIndex); // HashSet이므로 중복 자동 방지
        }

        int count = 0;

        foreach (int index in selectIndex)
        {
            if (count >= auctionAiList.Count)
            {
                Debug.LogWarning($"[SpawnAndPositionAi] count({count})가 auctionAiList 범위를 벗어났습니다.");
                break;
            }
            if (index >= aiSlots.Count)
            {
                Debug.LogWarning($"[SpawnAndPositionAi] index({index})가 aiSlots 범위를 벗어났습니다.");
                count++;
                continue;
            }
            AllAiBase aiPrefab = auctionAiList[count];
            RectTransform slotTransform = aiSlots[index];

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
                spawnedAi.OnGiveUpTrue += () => SetImageAlpha(npcs[index], 0.1f); // 포기 시 NPC 이미지 반투명 처리
                spawnedAiList.Add(spawnedAi);
            }

            InsertNPCData(index, count); // NPC 이미지 삽입
            count++;
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

        _ = errorMessage.rectTransform.DOAnchorPosY(errorMessagePos.y + floatHight, floatTime).SetEase(Ease.OutQuad);
        await errorMessage.DOFade(0f, floatTime).SetEase(Ease.OutQuad).AsyncWaitForCompletion();

        errorMessage.gameObject.SetActive(false);
    }

    //=============================NPC 이미지 관련========================== 
    private void InsertNPCData(int index, int cnt)
    {
        if (index < 0 || index >= npcs.Length)
        {
            Debug.LogWarning($"[InsertNPCData] npcs 인덱스({index})가 범위를 벗어났습니다.");
            return;
        }
        if (cnt < 0 || cnt >= auctionAiList.Count)
        {
            Debug.LogWarning($"[InsertNPCData] auctionAiList 인덱스({cnt})가 범위를 벗어났습니다.");
            return;
        }
        // 뽑힌 랜덤한 npcs 위치들에 순서대로 auctionAiList의 데이터를 집어넣음
        npcs[index].sprite = auctionAiList[cnt].npcImage;
        SetImageAlpha(npcs[index], 1f); // 참여하는 칸은 불투명
        if (AuctionPlayer.Instance != null)
            playerImage.sprite = AuctionPlayer.Instance.playerImage;
    }
    
    private void NPCImageOn()
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            playerImage.gameObject.SetActive(true);
            npcs[i].gameObject.SetActive(true);
            SetImageAlpha(npcs[i], 0f); // 일단 투명, 참여하는 칸만 InsertNPCData에서 불투명 처리
        }
    }

    public void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
    private void NPCImageOff()
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            playerImage.gameObject.SetActive(false);
            npcs[i].gameObject.SetActive(false);
        }
    }
    //=========================사운드 재생======================
    // 사운드 재생 통합 함수
    public void PlaySound(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null)
        {
            sfxAudioSource.PlayOneShot(clip); // 메모리 추가 할당 없이 재생
        }
    }

}