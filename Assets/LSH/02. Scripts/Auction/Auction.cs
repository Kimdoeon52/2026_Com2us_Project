using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Auction : Singleton<Auction>
{
    [Header("경매 설정")]
    [SerializeField] public AuctionType currentAuctionType = AuctionType.Normal; //현재 경매 모드

    [Header("연속 경매 설정")]
    [SerializeField] private int totalAuctionRounds = 3; //연속으로 진행할 경매 횟수
    [Tooltip("경매가 모두 끝나거나 '나가기'를 누르면 이동할 씬 (경로 또는 이름). Build Settings에 등록돼 있어야 함")]
    [SerializeField] private string exitScenePath = "Assets/Scenes/MainScene/Main_Junk_Shop.unity";
    private AuctionType? sessionAuctionType; // 첫 회차에 고른 경매 방식 (이후 회차는 이걸로 자동 진행)
    private int currentRound = 0; //현재 진행 중인 회차
    [SerializeField] [Min(1f)] private float giveUpTimeScale = 3f; //플레이어가 포기하면 경매를 n배속으로 진행 (빠른 종료 유도)

    [Header("물건 정보 UI")]
    [SerializeField] public TextMeshPro auctionName; //물건 이름
    [SerializeField] public TextMeshPro auctionCost; //물건 가격
    [SerializeField] public TextMeshPro auctionGrade;// 물건 등급
    [SerializeField] private Color gradeColorRare; //레어등급 색상
    [SerializeField] private Color gradeColorEpic; //에픽등급 색상
    [SerializeField] private Color gradeColorLegendary = new Color(1f, 0.8f, 0.2f); //전설등급 색상
    [Header("대사 & 메시지 UI")]
    [SerializeField] public TextMeshProUGUI chat; //경매하는 사람 채팅 나올 곳
    [SerializeField] public TextMeshProUGUI errorMessage; //에러 메시지 나올 곳
    [SerializeField] public TextMeshProUGUI myGold;// 내 골드 보유 수 나올 곳
    [SerializeField] private AuctionAnnouncer announcer; // 경매 진행자(AuctionGirl). 비워두면 chat의 부모에 자동 생성
    [SerializeField] private float curtainRevealDelay = 1.2f; // 커튼 열리고 물건 감상할 시간

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
    [Tooltip("경매에 나올 부품 카탈로그. 비우면 InventoryHost의 카탈로그를 사용")]
    [UnityEngine.Serialization.FormerlySerializedAs("partsIngredient")]
    [SerializeField] private ComponentCatalog componentCatalog;
    [SerializeField] private PartGrade minAuctionGrade = PartGrade.Rare;      // 이 등급부터
    [SerializeField] private PartGrade maxAuctionGrade = PartGrade.Legendary; // 이 등급까지 경매에 나옴
    [System.NonSerialized] public List<ComponentDefinition> auctionItem; // 경매에 나올 모든 물품들 (카탈로그에서 등급으로 걸러서 생성)

    [Header("컷씬 매니저")]
    [SerializeField] private AuctionCutsceneManager cutsceneManager; //컷씬 용

    [Header("제한 시간 UI")]
    [SerializeField] public Image timeBar; //제한 시간 이미지
    [Tooltip("타이머 전체 묶음 (막대+테두리). 비우면 timeBar의 부모 'Timer'를 사용")]
    [SerializeField] private GameObject timerRoot;
    private float auctionTime = 60f; //총 시간
    public float RemainingTime { get; set; } = 0f; //현재 시간
    public bool IsTimeRunning { get; set; } = false; // 시간이 끝났는가? 체크

    //[Header("임시 콘솔창")]
    //public TextMeshProUGUI console; //임시로 쓰는 거

    //=================================모드 선택=========================
    [Header("모드 선택 UI")]
    [SerializeField] public GameObject modeSelectPanel;
    [SerializeField] public Button normalModeButton;
    [SerializeField] public Button fistModeButton;

    //===================================경매 시작 버튼 / 착석===================
    [Header("경매 시작 (좌측 버튼 → 모드 선택 패널)")]
    [Tooltip("화면 좌측 '경매 시작' 버튼 표시 여부. 끄면 NPC(등록직원) 메뉴로만 경매 시작")]
    [SerializeField] private bool showStartAuctionButton = false;
    [SerializeField] private Button startAuctionButton; // 비우면 일반 경매 버튼을 복제해서 화면 좌측에 자동 생성
    [SerializeField] private string startAuctionLabel = "경매 시작";
    [SerializeField] private Vector2 startButtonOffset = new Vector2(40f, 0f); // 좌측 가운데 기준 위치
    [SerializeField] private AuctionSeatLock seatLock; // 비우면 자동으로 붙음 (착석/이동 잠금/카메라 고정)
    private UniTaskCompletionSource startAuctionTcs;

    //===================================경매 탈출 버튼===================
    [SerializeField] private Button getOutAuction;

    //===================================상점=============================
    [Header("상점 UI")]
    [SerializeField] private GameObject shopPanel; //상점 창
    [SerializeField] private Button shopButton; //모드 선택 화면의 상점 열기 버튼
    [SerializeField] private Button shopCloseButton; //상점 닫기 버튼

    //===================================수리 (상점 안 수리 버튼)=============================
    [Header("수리 UI (상점의 수리 버튼 → 수리 창)")]
    [SerializeField] private Button repairButton; //상점 안의 수리 버튼 (RepairButton)
    [SerializeField] private GameObject repairWindow; //수리 창 (FixWindow 프리팹)
    [SerializeField] private Button repairCloseButton; //수리 창 닫기 버튼 (비워도 Esc로 닫힘)

    //===================================플레이어 (좌석 모습/말풍선)=============================
    [Header("플레이어 (씬에 AuctionPlayer가 없으면 자동 생성)")]
    [Tooltip("좌석에 보일 플레이어 모습 (무대를 보는 뒷모습)")]
    [SerializeField] private Sprite playerSeatSprite;
    [Tooltip("플레이어 말풍선 이미지 (AI 말풍선도 이걸 따라감)")]
    [SerializeField] private Sprite playerBubbleSprite;
    [Tooltip("플레이어 말풍선 글꼴")]
    [SerializeField] private TMP_FontAsset playerBubbleFont;

    private UniTaskCompletionSource<AuctionType> modeSelectTcs;
    private bool isSelectingMode = false; //모드 선택 대기 중인지 체크 (상점은 이때만 열 수 있음)
    private bool IsShopOpen => shopPanel != null && shopPanel.activeSelf;
    private bool IsRepairOpen => repairWindow != null && repairWindow.activeSelf;
    //===============================ai 슬롯 위치 (구 방식, 사용 안 함 → rankerSeatPoints로 대체)====================
    // [Header("AI 슬롯 (구 UI 슬롯, 사용 안 함 - 좌석은 rankerSeatPoints 사용)")]
    // [SerializeField] private List<Transform> aiSlots;

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

    public ComponentDefinition stuff; //현재 경매 물품(부품)을 저장할 용도
    public PartGrade stuffGrade; //경매 물품 등급을 저장할 용도
    private Vector2 errorMessagePos; //에러 메시지가 나올 초기 위치
    private bool isPlayerGiveUp = false; //플레이가 포기했는지 체크하는 용도

    // 경매 전략 딕셔너리
    private Dictionary<AuctionType, IAuctionMode> nowAuctionMode;

    // 비동기 UI 입력 수신용 CS
    private UniTaskCompletionSource<int> fistBidTcs;

    //======================슬롯 이미지 및 위치=====================
    [Header("플레이어 및 NPC 슬롯")]
    // [SerializeField] public SpriteRenderer[] npcs; //여기가 npc이미지들 (구 방식: 인스펙터 연결 → rankerSeatPoints로 대체)
    [System.NonSerialized] public SpriteRenderer[] npcs; // 랭커 좌석 초상화들 (BuildRankerSeats에서 생성)
    [SerializeField] private SpriteRenderer playerImage; //플레이어 들어갈 곳
    // [SerializeField] public Transform[] npcsPos; //여기는 npc위치들 (구 방식: 좌석 = 생성한 초상화 Transform으로 대체)
    [SerializeField] private Transform playerImagePos;
    public Transform PlayerSlot => playerImage != null ? playerImage.transform : null; // 플레이어 초상화 위치 (명패/말풍선 기준)

    [Header("랭커 좌석 (맵 프리팹 안의 오브젝트 위치만 사용)")]
    [Tooltip("이 위치들에 랭커 초상화를 코드로 생성함. 맵 오브젝트는 건드리지 않음")]
    [SerializeField] private Transform[] rankerSeatPoints;
    [SerializeField] private Vector3 rankerSeatOffset = Vector3.zero; // 위치 미세 조정용
    [SerializeField] private Vector3 rankerSeatScale = Vector3.one;
    [SerializeField] private string rankerSortingLayerName = "Default";
    [SerializeField] private int rankerSortingOrder = 20;
    private Transform rankerSeatRoot; // 생성된 랭커 좌석들의 부모 (씬 루트에 생성)

    [Header("무대 바라보기")]
    [Tooltip("관객(랭커/구경꾼)이 바라볼 무대 중앙. 비우면 경매 물건 위치(lotItemPoint)를 사용")]
    [SerializeField] private Transform stagePoint;
    public Transform StagePoint => stagePoint != null ? stagePoint : lotItemPoint;

    // from 위치에서 무대를 수평으로 바라보는 회전 (Y축만 회전, 뒷모습 스프라이트 기준: +Z가 무대를 향함)
    public static Quaternion FaceStage(Vector3 from, Transform stage)
    {
        if (stage == null) return Quaternion.identity;
        Vector3 dir = stage.position - from;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
    }
    [Tooltip("좌석 초상화의 월드 높이. 0이면 플레이어 초상화 높이에 맞춤 (보스/조무레기 크기 통일)")]
    [SerializeField] private float portraitWorldHeight = 0f;
    [Tooltip("좌석 초상화 크기 배율 (플레이어 포함 모두 같이 적용)")]
    [SerializeField] private float portraitSizeMultiplier = 0.9f;

    [Header("경매 물건 이미지 (맵 위치만 사용)")]
    [Tooltip("채우면 이 위치에 물건 이미지를 코드로 생성해서 띄움 (맵 오브젝트는 건드리지 않음)")]
    [SerializeField] private Transform lotItemPoint;
    [SerializeField] private Vector3 lotItemOffset = Vector3.zero; // 위치 미세 조정용
    [Tooltip("물건 이미지 긴 변의 월드 크기. 어떤 부품이든 이 크기 안에 맞춤")]
    [SerializeField] private float lotItemSize = 1f;
    [SerializeField] private string lotSortingLayerName = "Default";
    [SerializeField] private int lotSortingOrder = 30;
    [Tooltip("둥둥 떠다니는 높이 (0이면 안 움직임)")]
    [SerializeField] private float lotFloatHeight = 0.15f;
    [Tooltip("한 번 올라가는 데 걸리는 시간")]
    [SerializeField] private float lotFloatDuration = 1.2f;

    // ===== 구 방식 (맵의 SpriteRenderer 직접 사용) → lotItemPoint로 대체 =====
    // [Header("경매 물건 이미지 (무대 받침대 위)")]
    // [Tooltip("비우면 '임시_Lot_경매물건/파츠'의 SpriteRenderer를 자동으로 찾음")]
    // [SerializeField] private SpriteRenderer lotItemImage;
    // [SerializeField] private string lotObjectName = "임시_Lot_경매물건";
    private SpriteRenderer createdLotItemImage; // lotItemPoint 위치에 코드로 만든 물건 이미지
    private float lotItemBoxSize = -1f; // 원래 들어있던 임시 이미지 크기 (새 물품도 이 크기 안에 맞춤)
    private Vector3 lotItemBaseScale = Vector3.one;
    private readonly Dictionary<Transform, Vector3> seatBaseScales = new Dictionary<Transform, Vector3>();

    //==========================사운드=============================
    [Header("효과음 설정")]
    [SerializeField] private AudioSource sfxAudioSource; // 사운드 재생용 컴포넌트
    [SerializeField] public AudioClip giveUpEffect;      // GiveUpEffect
    [SerializeField] public AudioClip dialogueEffect;    // DialogueEffect
    [SerializeField] public AudioClip useSkillEffect;    // UseSkillEffect
    [SerializeField] public AudioClip clickSoundEffect;
    [Header("배경음 설정")]
    [SerializeField] private AudioClip normalBGM;
    [SerializeField] private AudioClip fistBGM;
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
    [SerializeField] private CurtainMove curtainMove;
    private void OnEnable()
    {
        BossAiBase.OnBossBigRaiseCutscene += HandleBossBigRaiseCutscene; //컷씬 옵져버 등록 OnBossBigRaiseCutscene 실행시 HandleBossBigRaiseCutscene발동
    }

    private void OnDisable()
    {
        BossAiBase.OnBossBigRaiseCutscene -= HandleBossBigRaiseCutscene; // 컷씬 옵져버 해제
        ResetGameSpeed(); // 배속 상태로 씬을 떠나도 다음 씬에 배속이 남지 않게 복구
    }

    private async UniTask HandleBossBigRaiseCutscene(string bossName, string skillName, Sprite bannerSprite, Sprite characterSprite, Color newColor, CutsceneStyle style) //컷씬 대기용 함수
    {
        IsCutscenePlaying = true; // 컷씬 플레이 시작
        if (cutsceneManager != null)
        {
            await cutsceneManager.PlayBossCutsceneAsync(bossName, skillName, bannerSprite, characterSprite, newColor, style); // awiat으로 컷씬 연출 대기
        }
        IsCutscenePlaying = false; // 컷씬 끝
    }

    protected override void Awake()
    {
        base.Awake(); // Instance용 base

        if (Instance == this)
        {
            BuildRankerSeats(); // 맵 위치를 받아 랭커 좌석 초상화 생성 (npcs/npcsPos 대체)
            BuildPlayerSeat(); // 플레이어 좌석 초상화도 같은 방식으로 생성 (playerImage가 비어 있을 때)
        }

        // 전략 등록
        nowAuctionMode = new Dictionary<AuctionType, IAuctionMode> // noewAuctionMode에 현재 모드 넣기
        {
            { AuctionType.Normal, new NormalAuctionMode() },
            { AuctionType.Fist, new FistAuctionMode() }
        };

        if (errorMessage != null)
            errorMessagePos = errorMessage.rectTransform.anchoredPosition; //첫 위치 저장

        SetupAnnouncer(); // 진행자는 말할 때만 등장하도록 숨긴 채 시작

        // UI 버튼 이벤트 바인딩 (인스펙터 중복 등록 방지를 위해 기존 리스너 제거 후 등록)
        BindButton(normalModeButton, OnSelectNormalMode);
        BindButton(fistModeButton, OnSelectFistMode);
        BindButton(fistSubmitButton, OnSubmitFistBid);
        BindButton(raiseButton, OnRaiseButton);
        BindButton(giveUpButton, GiveUpButton);
        BindButton(getOutAuction, OnGGAuction);
        BindButton(shopButton, OnOpenShop);
        BindButton(shopCloseButton, OnCloseShop);
        FindRepairUI();
        BindButton(repairButton, OnOpenRepair);
        BindButton(repairCloseButton, OnCloseRepair);
        if (repairWindow != null) repairWindow.SetActive(false); // 수리 창은 닫힌 채로 시작
        EnsureAuctionPlayer(); // 플레이어 좌석 모습 + 말풍선 (없으면 생성)
        if (startAuctionButton == null && showStartAuctionButton) startAuctionButton = CreateStartAuctionButton();
        BindButton(startAuctionButton, OnStartAuctionButton);
        if (startAuctionButton != null) startAuctionButton.gameObject.SetActive(false);
        if (seatLock == null && !TryGetComponent(out seatLock)) seatLock = gameObject.AddComponent<AuctionSeatLock>();
        if (fistBidInputField != null)
        {
            fistBidInputField.onSubmit.RemoveAllListeners();
            fistBidInputField.onSubmit.AddListener(_ => OnSubmitFistBid()); // 엔터로도 제출
        }

        if (timerRoot == null && timeBar != null)
        {
            Transform parent = timeBar.transform.parent;
            timerRoot = parent != null && parent.name.IndexOf("Timer", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? parent.gameObject
                : timeBar.gameObject;
        }
        ShowTimer(false); // 타이머는 경매(입찰)가 시작돼야 나타남
        ShowGold(false); // 보유 골드는 경매를 시작해야 나타남

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
        // 2026-11-16: 경매장에 들어올 때마다 지금 지역을 다시 읽는다 — 다른 곳에서 지역이 바뀌었으면 그 지역 보스 명단으로 나온다
        GlobalGold.Instance.ReloadStage();
        Debug.Log($"[경매] 현재 지역(스테이지) {GlobalGold.Instance.CurrentStage} 명단으로 진행");
        RunAuctionSessionAsync().Forget(); // 경매 연속 진행 시작
    }

    // 경매를 totalAuctionRounds번 연속 진행하고, 끝나면 다음 씬으로 이동
    private async UniTask RunAuctionSessionAsync()
    {
        SettleConsignments(); // 플레이어 위탁품 판매 정산

        // 좌측 '경매 시작' 버튼을 누를 때까지는 자유롭게 돌아다님
        await WaitStartAuctionButtonAsync();
        SeatPlayer(); // 플레이어 좌석 착석 + 이동 잠금 + 카메라 무대 정면 고정
        UpdateGoldDisplay();
        ShowGold(true); // 경매 시작 → 보유 골드 표시

        for (currentRound = 1; currentRound <= totalAuctionRounds; currentRound++)
        {
            if (!PrepareRound()) { ReleasePlayer(); return; } // 물품이 없으면 중단

            //AppendConsoleLog($"===== {currentRound}/{totalAuctionRounds} 번째 경매 =====");
            await StartAuction();
        }

        // 경매가 모두 끝나도 씬 이동 전까지는 착석(이동 잠금) 유지
        await CloseSessionChatting(); // 진행자 마무리 인사
        // 2026-11-16: 세션이 끝나도 지역은 안 바꾼다 (지역 이동은 다른 곳 담당 — GlobalGold.SetStage)
        MoveToNextScene(1000).Forget();
    }

    //=========================경매 시작 버튼 / 착석===================
    private async UniTask WaitStartAuctionButtonAsync()
    {
        // 버튼이 없어도 NPC 메뉴(AuctionBridge → OnStartAuctionButton)로 시작할 때까지 대기
        startAuctionTcs = new UniTaskCompletionSource();
        bool showButton = showStartAuctionButton && startAuctionButton != null;
        if (showButton) startAuctionButton.gameObject.SetActive(true);
        await startAuctionTcs.Task;
        startAuctionTcs = null;
        if (startAuctionButton != null) startAuctionButton.gameObject.SetActive(false);
    }

    public void OnStartAuctionButton()
    {
        PlaySound(clickSoundEffect);
        startAuctionTcs?.TrySetResult();
    }

    private void SeatPlayer()
    {
        if (seatLock == null) return;
        Transform seat = playerImagePos != null ? (playerImagePos.parent != null ? playerImagePos.parent : playerImagePos) : null; // 좌석 바닥 = 초상화의 부모 슬롯
        Transform bubble = AuctionPlayer.Instance != null ? AuctionPlayer.Instance.BubbleTransform : null;
        bool hasPortrait = ShowPlayerPortrait(); // 좌석 초상화(뒷모습)를 바로 표시
        seatLock.Seat(seat, bubble, hasPortrait); // 초상화가 없으면 걸어다니던 캐릭터를 그대로 보여줌
    }

    // 좌석에 플레이어 초상화 표시. 보여줄 이미지가 있으면 true
    private bool ShowPlayerPortrait()
    {
        BuildPlayerSeat(); // 혹시 아직 없으면 생성
        if (playerImage == null) return false;
        if (PlayerSeatSprite != null) playerImage.sprite = PlayerSeatSprite;
        if (playerImage.sprite == null) return false;

        playerImage.gameObject.SetActive(true);
        SetImageAlpha(playerImage, 1f);
        NormalizePortrait(playerImage);
        return true;
    }

    // 씬에 AuctionPlayer가 없으면 (플레이어 캐릭터를 프리팹으로 교체하며 빠진 경우) 말풍선 + 대사 + 좌석 모습을 가진 AuctionPlayer를 생성
    private void EnsureAuctionPlayer()
    {
#if UNITY_EDITOR
        FillPlayerDefaults();
#endif
        if (AuctionPlayer.Instance != null || FindFirstObjectByType<AuctionPlayer>(FindObjectsInactive.Include) != null) return;
        if (playerBubbleSprite == null) Debug.LogWarning("[Auction] 플레이어 말풍선 이미지(playerBubbleSprite)가 비어 있어 말풍선이 안 보일 수 있습니다.", this);

        GameObject root = new GameObject("AuctionPlayer (자동 생성)");
        root.transform.SetParent(transform, false);

        // 말풍선 (예전 씬의 DiaBOX와 같은 구성)
        GameObject bubbleGo = new GameObject("DiaBOX");
        bubbleGo.transform.SetParent(root.transform, false);
        bubbleGo.transform.localPosition = new Vector3(0f, 0.708f, 0f);
        SpriteRenderer bubble = bubbleGo.AddComponent<SpriteRenderer>();
        bubble.sprite = playerBubbleSprite;

        // 대사 글자
        GameObject chatGo = new GameObject("Chat", typeof(RectTransform));
        chatGo.transform.SetParent(bubbleGo.transform, false);
        ((RectTransform)chatGo.transform).sizeDelta = new Vector2(1.5f, 1f);
        TextMeshPro text = chatGo.AddComponent<TextMeshPro>();
        if (playerBubbleFont != null) text.font = playerBubbleFont;
        text.fontSize = 2f;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.text = "";

        AuctionPlayer player = root.AddComponent<AuctionPlayer>();
        player.Setup(bubble, text, playerSeatSprite, Vector3.one * 0.3f);
    }

#if UNITY_EDITOR
    // 비어 있으면 예전 경매장에서 쓰던 에셋으로 자동 채움 (인스펙터에서 바꾸면 그 값을 사용)
    private void OnValidate() => FillPlayerDefaults();

    private void FillPlayerDefaults()
    {
        // 좌석 모습 = Player_North (플레이어 뒷모습). 예전 기본값(Boxing_Backwalking_0)이 들어가 있으면 교체
        if (playerSeatSprite == null || playerSeatSprite.name == "Boxing_Backwalking_0")
        {
            Sprite north = LoadEditorSprite("e46f3ab3320e8aa4eaa9bf6dfd2c8269", null);
            if (north != null) playerSeatSprite = north;
            else if (playerSeatSprite == null) Debug.LogWarning("[Auction] Player_North를 스프라이트로 불러오지 못했습니다. 임포트 설정의 Texture Type을 Sprite로 바꿔주세요.", this);
        }
        if (playerBubbleSprite == null) playerBubbleSprite = LoadEditorSprite("daaeeec1f63854b4b953e7fd345ff7bb", null);
        if (playerBubbleFont == null)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath("25a5fb9e590d9954a85baefdeef7c150");
            if (!string.IsNullOrEmpty(path)) playerBubbleFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }
    }

    private static Sprite LoadEditorSprite(string guid, string spriteName)
    {
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite && (spriteName == null || sprite.name == spriteName)) return sprite;
        }
        return null;
    }
#endif

    private void ReleasePlayer()
    {
        if (seatLock != null) seatLock.Release();
        ShowGold(false); // 경매가 끝나면 보유 골드도 다시 숨김
    }

    // 보유 골드 표시/숨김. 글자의 부모가 골드 표시용 묶음(이름에 Gold 포함)이면 배경까지 함께
    private void ShowGold(bool show)
    {
        if (myGold == null) return;
        Transform parent = myGold.transform.parent;
        bool parentIsGoldGroup = parent != null && parent.GetComponent<Canvas>() == null
                                 && parent.name.IndexOf("gold", System.StringComparison.OrdinalIgnoreCase) >= 0;
        (parentIsGoldGroup ? parent.gameObject : myGold.gameObject).SetActive(show);
        if (show) myGold.gameObject.SetActive(true);
    }

    // 일반 경매 버튼을 복제해서 화면 좌측 가운데에 '경매 시작' 버튼 생성 (같은 디자인 유지)
    private Button CreateStartAuctionButton()
    {
        if (normalModeButton == null) return null;
        Canvas canvas = normalModeButton.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;

        Vector2 size = ((RectTransform)normalModeButton.transform).rect.size; // 원본이 늘어나는(stretch) 앵커여도 크기 유지
        if (size.x < 1f || size.y < 1f) size = new Vector2(260f, 90f);

        Button button = Instantiate(normalModeButton, canvas.rootCanvas.transform, false);
        button.name = "StartAuctionButton";
        RectTransform rt = (RectTransform)button.transform;
        rt.sizeDelta = size;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = startButtonOffset;
        rt.localScale = Vector3.one;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = startAuctionLabel;
        else
        {
            Text legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = startAuctionLabel;
        }
        return button;
    }

    // 위탁 창에서 맡긴 파츠를 고정 낙찰가로 팔고 골드 지급
    private void SettleConsignments()
    {
        foreach (ConsignedPart part in ConsignmentSettlement.Settle())
        {
            //AppendConsoleLog($"[위탁 판매] {ConsignmentSettlement.NameOf(part)} 낙찰 +{part.Price}G");
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
        BuildAuctionItems();
        if (auctionItem == null || auctionItem.Count == 0)
        {
            Debug.LogError($"경매 물품 리스트가 읍다. ComponentCatalog에 {minAuctionGrade}~{maxAuctionGrade} 등급 부품이 있는지 확인해라");
            return false;
        }

        List<BossAiBase> stageBosses = GetStageBosses();
        if (stageBosses == null) return false; // 보스 명단이 없으면 중단

        stuff = auctionItem[Random.Range(0, auctionItem.Count)]; // stuff에 경매에 있는 아이템 랜덤 등록
        CurrentCost = stuff.Price / 2; //원가의 절반
        stuffGrade = stuff.Grade; // 등급 넣기
        if (auctionGrade != null)
        {
            auctionGrade.text = stuffGrade.ToString(); // Text에 등급 보여주기
            switch(stuffGrade)
            {
                case PartGrade.Rare:
                    auctionGrade.color = gradeColorRare;
                    break;
                case PartGrade.Epic:
                    auctionGrade.color = gradeColorEpic;
                    break;
                case PartGrade.Legendary:
                    auctionGrade.color = gradeColorLegendary;
                    break;
                default:
                    auctionGrade.color = Color.white;
                    break;
            }
        }
        if (auctionName != null) auctionName.text = "";
        UpdateLotItemImage(); // 받침대 위 물건 이미지 교체 (커튼이 닫혀 있는 동안)

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

    // 모든 경매가 끝난 뒤(또는 나가기) 씬 이동. exitScenePath가 비어 있으면 이동하지 않음
    private async UniTask MoveToNextScene(int waitDelay)
    {
        await UniTask.Delay(waitDelay, ignoreTimeScale: true);
        ResetGameSpeed();
        if (string.IsNullOrWhiteSpace(exitScenePath))
        {
            Debug.Log("[Auction] 모든 경매 종료. 이동할 씬이 지정되지 않아 이동하지 않음.");
            ReleasePlayer();
            return;
        }

        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(exitScenePath);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(exitScenePath);
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
        // 경매 인원 5~7 (랭커5(필수) + 쩌리 N명). 좌석 수를 넘으면 못 앉으니 좌석 수로 제한
        int seatCount = npcs != null && npcs.Length > 0 ? npcs.Length : 7;
        dayOfEnterNpc = Mathf.Min(Random.Range(5, 8), seatCount);
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
        // 첫 회차에서만 경매 방식을 고르고, 이후 회차는 같은 방식으로 자동 진행
        if (sessionAuctionType == null) sessionAuctionType = await WaitPlayerSelectModeAsync();
        currentAuctionType = sessionAuctionType.Value;
        //AppendConsoleLog($"선택된 경매 모드: {currentAuctionType}");
        //==========Npc UI 활성화 ==========
        NPCImageOn();
        SpawnAndPositionAi();
        //===========================
        foreach (var ai in auctionAiList) // 경매 참여하는 ai들 만큼
        {
            if (ai != null)
            {
                //AppendConsoleLog($"{ai.NPCName}님이 참가 했습니다."); // Console에 참가 보여주기
                ai.ReadyForAction(); // ai들 준비완료
            }
        }

        await StartAuctionChatting(); //진행자 등장 → 경매 시작 대사 → 퇴장
        curtainMove.MoveCurtain(); //커튼 열기
        auctionName.text = stuff.DisplayName; // 경매 물품 이름 출력
        UpdateAuctionCostUI(); // 가격표 보여주기
        await UniTask.Delay(System.TimeSpan.FromSeconds(curtainRevealDelay)); // 커튼 열리는 동안 물건 감상

        await IntroAuction(); // 진행자 재등장 → 경매 물품 소개 → 퇴장

        IsAuctioningFin = false;
        isPlayerGiveUp = false;
        ResetSkillEffects(); // 이전 회차 보스 스킬 효과 제거
        WinnerID  = -1;
        IfPlayerWin = false;
        auctionTime = 60f;


        // 선택된 모드에 맞는 조작 UI 전용 활성화
        if (currentAuctionType == AuctionType.Normal)
        {
            NormalAuctionButtonReady(true);
            EnableFistAuctionUI(false);
            PlayBGM(currentAuctionType);
            AnnounceBriefAsync(ChatList.Instance.GetLines("입찰시작")[0], 1.5f).Forget(); // 입찰 시작 신호 후 자동 퇴장
        }
        else if (currentAuctionType == AuctionType.Fist)
        {
            NormalAuctionButtonReady(false);
            EnableFistAuctionUI(true);
            PlayBGM(currentAuctionType);
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
        if (auctionCost == null)
        {
            Debug.LogError("🔴 [오류] auctionCost 변수가 인스펙터에 연결되어 있지 않습니다!");
            return;
        }
        if (IsInfoHidden) { auctionCost.text = "???"; return; } // 연막탄: 가려진 동안은 계속 가림
        if (IsFakePriceActive) return; // 가짜 호가: 교란 루프가 가짜 숫자를 쓰는 중
        auctionCost.text = CurrentCost.ToString() + " G";
    }

    //=========================보스 스킬 효과용 상태===================
    public const int BasePlayerRaiseStep = 15;
    public int PlayerRaiseTax { get; set; } = 0; // 경매세: 플레이어 레이즈 단가에 추가
    public int PlayerRaiseStep => BasePlayerRaiseStep + PlayerRaiseTax;
    public bool NoTimerRefresh { get; set; } = false; // 시간 동결: 입찰해도 10초로 안 돌아감
    private float playerRaiseLockEnd = 0f;
    private float infoHiddenEnd = 0f;
    private float fakePriceEnd = 0f;
    public bool IsPlayerRaiseLocked => Time.time < playerRaiseLockEnd;
    public bool IsInfoHidden => Time.time < infoHiddenEnd;
    public bool IsFakePriceActive => Time.time < fakePriceEnd;

    private Color? timeBarBaseColor; // 시간 동결 연출로 바뀐 타임바 색 복구용

    private void ResetSkillEffects() // 회차 시작마다 스킬 효과 초기화
    {
        if (timeBar != null)
        {
            if (timeBarBaseColor == null) timeBarBaseColor = timeBar.color;
            else timeBar.color = timeBarBaseColor.Value;
        }
        PlayerRaiseTax = 0;
        NoTimerRefresh = false;
        playerRaiseLockEnd = 0f;
        infoHiddenEnd = 0f;
        fakePriceEnd = 0f;
    }

    // 입찰이 들어오면 남은 시간이 10초 미만일 때 10초로 되돌림 (시간 동결 중이면 안 함)
    public void RefreshTimerOnBid()
    {
        if (!NoTimerRefresh && RemainingTime < 10f) RemainingTime = 10f;
    }

    // 입찰 봉쇄: seconds 동안 플레이어 레이즈 불가
    public void LockPlayerRaise(float seconds)
    {
        playerRaiseLockEnd = Mathf.Max(playerRaiseLockEnd, Time.time + seconds);
        ShowErrorMessage($"입찰이 {seconds:0}초간 봉쇄되었습니다!").Forget();
        LockRaiseButtonAsync().Forget();
    }

    private async UniTask LockRaiseButtonAsync()
    {
        if (raiseButton == null) return;
        raiseButton.interactable = false;
        await UniTask.WaitUntil(() => !IsPlayerRaiseLocked || this == null);
        if (raiseButton != null) raiseButton.interactable = true;
    }

    // 연막탄: seconds 동안 물품 이름/가격/등급을 가림
    public void HideAuctionInfo(float seconds)
    {
        infoHiddenEnd = Time.time + seconds;
        if (auctionName != null) auctionName.text = "메롱 안보이지?";
        if (auctionGrade != null) auctionGrade.text = "등급은 비밀이야~";
        if (auctionCost != null) auctionCost.text = "???";
        SpriteRenderer lot = GetLotItemImage();
        if (lot != null) lot.enabled = false; // 받침대 위 물건도 연막으로 가림
        RestoreInfoWhenDoneAsync(infoHiddenEnd).Forget();
    }

    private async UniTask RestoreInfoWhenDoneAsync(float end)
    {
        await UniTask.WaitUntil(() => Time.time >= end || this == null);
        if (this == null || infoHiddenEnd != end) return; // 그 사이 다시 가려졌으면 그쪽이 복구
        RefreshAuctionInfoUI();
    }

    // 가짜 호가: seconds 동안 가격표에 가짜 숫자가 계속 바뀌며 표시됨
    public void ShowFakePrice(float seconds)
    {
        fakePriceEnd = Time.time + seconds;
        FakePriceLoopAsync(fakePriceEnd).Forget();
    }

    private async UniTask FakePriceLoopAsync(float end)
    {
        while (this != null && Time.time < end && fakePriceEnd == end)
        {
            if (auctionCost != null && !IsInfoHidden)
                auctionCost.text = Mathf.Max(1, Mathf.RoundToInt(CurrentCost * Random.Range(0.6f, 1.6f))).ToString() +" G";
            await UniTask.Delay(250);
        }
        if (this != null && fakePriceEnd == end) UpdateAuctionCostUI();
    }

    // 물품 이름/등급/가격 UI를 현재 상태로 다시 표시
    public void RefreshAuctionInfoUI()
    {
        if (IsInfoHidden) return;
        if (auctionName != null && stuff != null) auctionName.text = stuff.DisplayName;
        if (auctionGrade != null)
        {
            auctionGrade.text = stuffGrade.ToString();
            switch (stuffGrade)
            {
                case PartGrade.Rare:
                    auctionGrade.color = gradeColorRare;
                    break;
                case PartGrade.Epic:
                    auctionGrade.color = gradeColorEpic;
                    break;
                case PartGrade.Legendary:
                    auctionGrade.color = gradeColorLegendary;
                    break;
                default:
                    auctionGrade.color = Color.white;
                    break;
            }
        }
        UpdateAuctionCostUI();
        UpdateLotItemImage();
    }

    //=========================무대 받침대 위 경매 물건 이미지===================
    public SpriteRenderer LotImage => GetLotItemImage(); // 보스 스킬 연출용
    public SpriteRenderer PlayerPortrait => playerImage; // 보스 스킬 연출용 (플레이어 좌석 초상화)

    private SpriteRenderer GetLotItemImage()
    {
        if (createdLotItemImage != null) return createdLotItemImage;
        if (lotItemPoint != null) return CreateLotItemImage(); // 위치만 받아서 생성
        return null;

        // ===== 구 방식: 맵의 '임시_Lot_경매물건/파츠' SpriteRenderer를 찾아서 사용 =====
        // if (lotItemImage != null) return lotItemImage;
        // GameObject lot =string.IsNullOrEmpty(lotObjectName) ? null : GameObject.Find(lotObjectName);
        // if (lot == null) return null;
        // Transform part = lot.transform.Find("파츠");
        // lotItemImage = part != null ? part.GetComponent<SpriteRenderer>() : lot.GetComponentInChildren<SpriteRenderer>(true);
        // return lotItemImage;
    }

    // lotItemPoint 위치에 물건 이미지를 생성. 구조: LotItemAnchor(둥둥 이동) → LotItemImage(SpriteRenderer)
    // 떠다니는 트윈은 부모에, 보스 스킬 연출(뒤집기/떨림)은 자식에 걸려서 서로 덮어쓰지 않음
    private SpriteRenderer CreateLotItemImage()
    {
        Transform anchor = new GameObject("LotItemAnchor").transform; // 씬 루트에 생성
        anchor.position = lotItemPoint.position + lotItemOffset;

        GameObject imageGo = new GameObject("LotItemImage");
        imageGo.transform.SetParent(anchor, false);
        createdLotItemImage = imageGo.AddComponent<SpriteRenderer>();
        createdLotItemImage.sortingLayerName = lotSortingLayerName;
        createdLotItemImage.sortingOrder = lotSortingOrder;

        lotItemBaseScale = Vector3.one;
        lotItemBoxSize = Mathf.Max(0.1f, lotItemSize); // 기준 상자 크기를 인스펙터 값으로 고정

        if (lotFloatHeight > 0f && lotFloatDuration > 0f)
        {
            anchor.DOMoveY(anchor.position.y + lotFloatHeight, lotFloatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(anchor.gameObject);
        }
        return createdLotItemImage;
    }

    // 현재 경매 물품(stuff)의 이미지를 받침대 위에 표시. 크기는 원래 임시 이미지 크기 안에 맞춤
    private void UpdateLotItemImage()
    {
        SpriteRenderer image = GetLotItemImage();
        if (image == null) return;

        // 구 방식: 처음 한 번 맵의 임시 이미지 크기를 기준 상자로 저장 (지금은 CreateLotItemImage에서 lotItemSize로 정함)
        // if (lotItemBoxSize < 0f)
        // {
        //     lotItemBaseScale = image.transform.localScale;
        //     Vector3 size = image.sprite != null ? image.bounds.size : Vector3.one;
        //     lotItemBoxSize = Mathf.Max(0.1f, Mathf.Max(size.x, size.y));
        // }

        Sprite sprite = stuff != null ? stuff.Sprite : null;
        image.sprite = sprite;
        image.enabled = sprite != null && !IsInfoHidden; // 이미지 없는 물품 / 연막탄 중에는 숨김
        if (sprite == null) return;

        // 부모 스케일 영향 제거 후 긴 변이 기준 상자 크기에 맞도록
        Vector3 parentScale = image.transform.parent != null ? image.transform.parent.lossyScale : Vector3.one;
        float spriteMax = Mathf.Max(sprite.bounds.size.x * lotItemBaseScale.x * parentScale.x, sprite.bounds.size.y * lotItemBaseScale.y * parentScale.y);
        float factor = spriteMax > 0.0001f ? lotItemBoxSize / spriteMax : 1f;
        image.transform.localScale = lotItemBaseScale * factor;
    }

    // 바꿔치기: 경매 물품을 다른 물품으로 교체. 가격은 새 물품 시작가보다 낮아지지 않음
    public ComponentDefinition SwapAuctionItem()
    {
        if (auctionItem == null || auctionItem.Count < 2) return null;
        ComponentDefinition next;
        do { next = auctionItem[Random.Range(0, auctionItem.Count)]; } while (next == stuff);

        stuff = next;
        stuffGrade = next.Grade;
        CurrentCost = Mathf.Max(CurrentCost, next.Price / 2);
        RefreshAuctionInfoUI();
        //AppendConsoleLog($"경매 물품이 [{next.DisplayName}]({next.Grade})(으)로 바뀌었습니다!");
        return next;
    }

    // 부품 카탈로그에서 min~max 등급 부품만 골라 경매 물품 리스트를 만든다 (한 번만)
    private void BuildAuctionItems()
    {
        if (auctionItem != null && auctionItem.Count > 0) return;

        ComponentCatalog catalog = componentCatalog;
        if (catalog == null && InventoryHost.Instance != null) catalog = InventoryHost.Instance.ComponentCatalog;
        if (catalog == null || catalog.Components == null) return;

        auctionItem = new List<ComponentDefinition>();
        foreach (ComponentDefinition component in catalog.Components)
        {
            if (component == null) continue;
            if (component.Grade < minAuctionGrade || component.Grade > maxAuctionGrade) continue;
            if (component.Price <= 0)
                Debug.LogWarning($"[Auction] 부품 '{component.DisplayName}'({component.Id})의 가격이 0이라 시작가가 0G가 됩니다. ComponentCatalog에서 가격을 넣어주세요.");
            auctionItem.Add(component);
        }
    }

    //=========================경매 진행자(AuctionGirl) 대사===================
    private void SetupAnnouncer()
    {
        if (announcer == null && chat != null && chat.transform.parent != null)
        {
            GameObject girl = chat.transform.parent.gameObject;
            announcer = girl.GetComponent<AuctionAnnouncer>();
            if (announcer == null) announcer = girl.AddComponent<AuctionAnnouncer>();
        }
        if (announcer != null) announcer.HideInstant();
    }

    // 진행자 등장 후 즉시 대사 표시 (카운트다운처럼 빠르게 바뀌는 대사용). 퇴장은 HideChatAsync로
    public void SetChat(string text)
    {
        if (announcer == null)
        {
            if (chat != null) chat.text = text;
            return;
        }
        if (!announcer.IsVisible) announcer.ShowAsync().Forget();
        announcer.SetLineInstant(text);
    }

    // 진행자 등장 → 타자기로 한 줄 출력 (입력 대기 없음)
    public async UniTask SayAsync(string text)
    {
        if (announcer == null) { SetChat(text); return; }
        await announcer.ShowAsync();
        PlaySound(dialogueEffect);
        await announcer.TypeLineAsync(text);
    }

    // 진행자 등장 → 대사들을 한 줄씩 출력하며 입력 대기 → (hideAfter면) 퇴장
    public async UniTask SpeakAsync(IList<string> lines, bool hideAfter = true)
    {
        if (lines == null || lines.Count == 0) return;

        if (announcer != null) await announcer.ShowAsync();
        foreach (string line in lines)
        {
            PlaySound(dialogueEffect);
            if (announcer != null)
            {
                await announcer.TypeLineAsync(line, IsAdvanceInput); // 입력하면 타자기 즉시 완성
            }
            else if (chat != null)
            {
                chat.text = line;
            }
            await WaitInput(); // 다 나온 뒤 입력하면 다음 대사
        }
        if (hideAfter) await HideChatAsync();
    }

    public async UniTask HideChatAsync()
    {
        if (announcer != null) await announcer.HideAsync();
    }

    // 진행자 등장 → 한 줄 출력 → holdSeconds 뒤 자동 퇴장 (그 사이 다른 대사가 나오면 퇴장 안 함)
    public async UniTask AnnounceBriefAsync(string text, float holdSeconds)
    {
        await SayAsync(text);
        if (announcer == null) return;
        int version = announcer.LineVersion;
        await UniTask.Delay(System.TimeSpan.FromSeconds(holdSeconds), ignoreTimeScale: true);
        if (announcer != null && announcer.IsVisible && announcer.LineVersion == version) await HideChatAsync();
    }

    //public void AppendConsoleLog(string log) // 콘솔 로그 채팅
    //{
    //    if (console != null) console.text += log + "\n";
    //}

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

        ShowTimer(true);
        if (timeBar != null) timeBar.fillAmount = 1f;

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
        ShowTimer(false);
    }

    // 타이머 묶음(막대+테두리) 표시/숨김
    public void ShowTimer(bool show)
    {
        if (timerRoot != null) timerRoot.SetActive(show);
        if (timeBar != null) timeBar.gameObject.SetActive(show);
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
        if (IsShopOpen || IsRepairOpen) return; // 상점/수리 창이 열려 있으면 경매 시작 안 함
        modeSelectTcs?.TrySetResult(AuctionType.Normal);
    }

    public void OnSelectFistMode()
    {
        PlaySound(clickSoundEffect);
        if (IsShopOpen || IsRepairOpen) return;
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
        if (repairWindow != null) repairWindow.SetActive(false);

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

    //=========================수리 창 열기/닫기 (상점 안 수리 버튼)===================
    public void OnOpenRepair()
    {
        PlaySound(clickSoundEffect);
        if (repairWindow == null)
        {
            Debug.LogWarning("[Auction] 수리 창(repairWindow)이 연결되지 않았습니다. 인스펙터에 FixWindow를 넣어주세요.", this);
            return;
        }
        if (IsRepairOpen) return;

        FitRepairWindow(); // 씬에 틀어진 크기/위치 오버라이드가 있어도 부모를 꽉 채우게
        if (RepairInsideShop) repairWindow.transform.SetAsLastSibling(); // 상점 안에 있으면 상점 위로 올려서 표시 (상점을 끄면 수리 창도 같이 꺼짐)
        else if (shopPanel != null) shopPanel.SetActive(false); // 따로 있으면 상점에 가려지지 않게 상점은 잠시 숨김
        repairWindow.SetActive(true);
    }

    public void OnCloseRepair()
    {
        if (!IsRepairOpen) return;
        PlaySound(clickSoundEffect);

        repairWindow.SetActive(false);
        UpdateGoldDisplay(); // 수리비 반영
        if (shopPanel != null && !shopPanel.activeSelf) shopPanel.SetActive(true); // 상점으로 돌아감
    }

    // FixWindow 프리팹 원본처럼 부모 전체를 채우도록 (Stretch, 여백 0). Shop 밑에 넣을 때 생긴 Size Delta(-1920,-1080) 오버라이드 보정
    private void FitRepairWindow()
    {
        if (repairWindow == null || !(repairWindow.transform is RectTransform rt)) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        Vector3 p = rt.localPosition;
        rt.localPosition = new Vector3(p.x, p.y, 0f);
    }

    private bool RepairInsideShop => repairWindow != null && shopPanel != null && repairWindow.transform.IsChildOf(shopPanel.transform);

    // 인스펙터에 연결 안 했으면 상점 안의 'RepairButton' / 씬의 수리 창(FixWindowManager)을 찾아 씀
    private void FindRepairUI()
    {
        if (repairButton == null && shopPanel != null)
        {
            foreach (Button b in shopPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "RepairButton") { repairButton = b; break; }
            }
        }
        if (repairWindow == null)
        {
            FixWindowManager fix = FindFirstObjectByType<FixWindowManager>(FindObjectsInactive.Include);
            if (fix != null) repairWindow = fix.gameObject;
        }
    }

    private void Update()
    {
        if (IsRepairOpen && Input.GetKeyDown(KeyCode.Escape)) OnCloseRepair(); // 수리 창은 Esc로도 닫힘
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
            InventoryHost inventory = InventoryHost.Instance;
            if (inventory != null && inventory.CanGiveComponent(stuff) && inventory.GiveComponent(stuff, 1) > 0)
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
                if (ai != null && ai.NPCID == WinnerID) // 이긴 ai 찾기
                {
                    string winLine = ai.OnWinAuction(CurrentCost, stuff); // 보스는 골드 차감 + 부품 수집(3개면 장비 강화), 일반 NPC는 기록만
                    ai.ShowThinkChatWithText(winLine); // 낙찰 소감 (강화되면 강화 소식)
                    break;
                }
            }
        }
    }

    private bool IsLastRound => currentRound >= totalAuctionRounds;

    // 커튼 열기 전: 첫 회차 = 개장 인사 / 중간 = "다음 경매 물품입니다!" / 마지막 = 마지막 물품 안내
    private async UniTask StartAuctionChatting()
    {
        List<string> lines;
        if (currentRound <= 1) lines = ChatList.Instance.GetLines("개장", totalAuctionRounds);
        else if (IsLastRound) lines = ChatList.Instance.GetLines("마지막경매", currentRound, totalAuctionRounds);
        else lines = ChatList.Instance.GetLines("다음경매", currentRound, totalAuctionRounds);
        await SpeakAsync(lines);
    }

    // 커튼 열린 뒤: 물품 이름 · 등급 · 시작가 소개
    private async UniTask IntroAuction()
    {
        await SpeakAsync(ChatList.Instance.GetLines("물건소개", stuff.DisplayName, ChatList.GradeName(stuffGrade), CurrentCost.ToString("N0")));
    }

    // 모든 회차가 끝난 뒤 마무리 인사
    private async UniTask CloseSessionChatting()
    {
        await SpeakAsync(ChatList.Instance.GetLines("폐장"));
    }

    private async UniTask ActionFinish()
    {
        if (GlobalGold.Instance == null || GlobalGold.Instance.mainCharacterdata == null || GlobalGold.Instance.mainCharacterdata.Count == 0)
        {
            Debug.LogError("[ActionFinish] GlobalGold 데이터가 없습니다.");
            return;
        }
        int finalWinner = IfPlayerWin ? GlobalGold.Instance.mainCharacterdata[0].mainCharacterID : WinnerID;
        bool isFist = currentAuctionType == AuctionType.Fist; // 주먹 경매는 결과 발표를 이미 했으니 축하/정리만

        if (finalWinner == -1)
        {
            await SpeakAsync(ChatList.Instance.GetLines(isFist ? "유찰_주먹" : "유찰"));
        }
        else
        {
            string winnerName = IfPlayerWin ? PlayerName : auctionAiList.Find(ai => ai.NPCID == finalWinner)?.NPCName ?? "알 수 없음";
            List<string> lines = isFist
                ? ChatList.Instance.GetLines("낙찰_주먹", winnerName)
                : ChatList.Instance.GetLines("낙찰", winnerName, CurrentCost.ToString("N0"));
            await SpeakAsync(lines); // 낙찰자 발표 후 퇴장
        }

        Tween closing = curtainMove != null ? curtainMove.CloseCurtain() : null; //커튼 닫기
        StopMusic();
        if (!IsLastRound) AnnounceBriefAsync(ChatList.Instance.GetLines("회차마무리")[0], 1.2f).Forget(); // 다음 물품 예고
        // 커튼이 완전히 닫힐 때까지 기다림 → 그 뒤에 다음 회차(PrepareRound)에서 물품이 바뀜 (닫히는 중에 바뀌어 보이던 문제)
        if (closing != null && closing.IsActive()) await closing.AsyncWaitForCompletion();
        await UniTask.Delay(300); // 닫힌 상태로 잠깐 멈춤
    }

    private async UniTask WaitInput() // 입력대기 (스페이스바 또는 마우스 왼쪽 클릭)
    {
        await UniTask.Yield();
        await UniTask.WaitUntil(IsAdvanceInput);
    }

    private bool IsAdvanceInput() // 대사 넘김 입력인지 (스페이스바 또는 버튼이 아닌 곳 클릭)
    {
        return Input.GetKeyDown(KeyCode.Space) || (Input.GetMouseButtonDown(0) && !IsPointerOverButton());
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
        if (IsPlayerRaiseLocked)
        {
            ShowErrorMessage("입찰이 봉쇄된 상태입니다!").Forget();
            return;
        }
        int needGold = CurrentCost + PlayerRaiseStep; // 경매세가 붙으면 단가가 올라감
        if (GlobalGold.Instance.CanUseGold(GlobalGold.Instance.mainCharacterdata[0].mainCharacterID, needGold))
        {
            CurrentCost = needGold;
            UpdateAuctionCostUI();
            WinnerID = GlobalGold.Instance.mainCharacterdata[0].mainCharacterID;
            IfPlayerWin = true;
            //AppendConsoleLog($"{PlayerName}님이 {CurrentCost}G로 레이즈!"); // 진행자 대사 대신 콘솔에만 기록
            RefreshTimerOnBid();
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
        MoveToNextScene(2000).Forget(); // 경매 종료 시와 같은 곳(exitScenePath)으로 이동
    }
    //================================Canvas에 AI 설치 =======================
    private void SpawnAndPositionAi()
    {
        if (npcs == null || npcs.Length == 0)
        {
            Debug.LogError("[SpawnAndPositionAi] npcs 배열이 비어있거나 null입니다. 인스펙터를 확인하세요.");
            return;
        }

        // 좌석 인덱스를 섞어서 앞에서부터 auctionAiList 순서대로 앉힘 (중복 없음)
        List<int> seatOrder = new List<int>();
        for (int i = 0; i < npcs.Length; i++) seatOrder.Add(i);
        for (int i = seatOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (seatOrder[i], seatOrder[j]) = (seatOrder[j], seatOrder[i]);
        }

        int seatCount = Mathf.Min(npcs.Length, auctionAiList.Count);
        if (auctionAiList.Count > npcs.Length)
            Debug.LogWarning($"[SpawnAndPositionAi] 참가 AI({auctionAiList.Count})가 좌석({npcs.Length})보다 많아 {auctionAiList.Count - npcs.Length}명은 앉지 못합니다.");

        for (int count = 0; count < seatCount; count++)
        {
            int index = seatOrder[count];
            AllAiBase aiPrefab = auctionAiList[count];
            Transform seat = GetSeat(index);
            if (aiPrefab == null || seat == null) continue;

            InsertNPCData(index, count); // 좌석 초상화 먼저 넣어야 머리 위 위치를 잴 수 있음
            float portraitFactor = NormalizePortrait(npcs[index]); // 보스/조무레기 초상화 크기를 플레이어 기준으로 통일

            // 월드 좌석(랭커자리)의 자식으로 생성 → 말풍선이 좌석 초상화 머리 위에 붙음
            AllAiBase spawnedAi = Instantiate(aiPrefab, seat);
            PlaceAboveSeat(spawnedAi.transform, seat, npcs[index]);
            // 초상화 크기 보정만큼 좌석이 커지거나 작아졌으니 AI(말풍선)는 반대로 보정해서 모두 같은 크기로 보이게
            if (portraitFactor > 0.0001f && seat == npcs[index].transform)
                spawnedAi.transform.localScale = Vector3.one / portraitFactor;
            BringAboveSeat(spawnedAi.gameObject, npcs[index]);

            SpriteRenderer seatImage = npcs[index];
            spawnedAi.OnGiveUpTrue += () => SetImageAlpha(seatImage, 0.1f); // 포기 시 NPC 이미지 반투명 처리
            spawnedAiList.Add(spawnedAi);
        }

        // 실제 씬에 생성되어 가동 중인 AI 리스트로 교체
        auctionAiList = new List<AllAiBase>(spawnedAiList);

        // 플레이어 말풍선도 플레이어 좌석 머리 위로 옮김
        if (AuctionPlayer.Instance != null && playerImage != null)
        {
            if (PlayerSeatSprite != null) playerImage.sprite = PlayerSeatSprite;
            NormalizePortrait(playerImage); // 플레이어 초상화도 같은 배율로
            Transform portrait = playerImage.transform; // 초상화 기준 (말풍선이 초상화를 따라다님)
            Vector3 head = GetHeadPosition(portrait, playerImage);
            AuctionPlayer.Instance.AttachBubbleTo(portrait, head, playerImage);
        }
    }

    // 초상화 스프라이트마다 PPU/해상도가 달라(조무레기 PPU 16, 보스 PPU 100) 화면 크기가 제각각이라
    // 좌석 초상화의 월드 높이를 플레이어 초상화 높이(또는 portraitWorldHeight)에 맞춤. 반환값 = 원래 크기 대비 배율
    private float NormalizePortrait(SpriteRenderer portrait)
    {
        if (portrait == null || portrait.sprite == null) return 1f;
        Transform t = portrait.transform;
        if (!seatBaseScales.TryGetValue(t, out Vector3 baseScale))
        {
            baseScale = t.localScale;
            seatBaseScales[t] = baseScale;
        }

        float target = GetTargetPortraitHeight();
        float parentScaleY = t.parent != null ? t.parent.lossyScale.y : 1f;
        float baseWorldHeight = portrait.sprite.bounds.size.y * baseScale.y * parentScaleY;
        if (target <= 0f || baseWorldHeight <= 0.0001f)
        {
            t.localScale = baseScale;
            return 1f;
        }

        float factor = target / baseWorldHeight;
        t.localScale = baseScale * factor;
        return factor;
    }

    private float referencePortraitHeight = -1f; // 플레이어 초상화 원래 높이 (한 번만 잼. 플레이어 좌석도 보정되므로)

    private float GetTargetPortraitHeight()
    {
        if (portraitWorldHeight > 0f) return portraitWorldHeight * portraitSizeMultiplier;
        if (referencePortraitHeight < 0f)
        {
            Sprite playerSprite = PlayerSeatSprite; // 좌석에 실제로 나오는 플레이어 이미지 기준
            if (playerSprite == null || playerImage == null) return 0f; // 기준이 없으면 보정 안 함
            referencePortraitHeight = playerSprite.bounds.size.y * playerImage.transform.lossyScale.y; // 약 1.28m (64px / PPU100 x 좌석 2배)
        }
        return referencePortraitHeight * portraitSizeMultiplier;
    }

    // 맵 프리팹의 위치용 오브젝트에서 위치만 읽어, 그 자리에 랭커 초상화(SpriteRenderer)를 새로 만들어 npcs로 사용
    // 맵 쪽 오브젝트는 건드리지 않고, 생성물은 씬 루트의 별도 오브젝트 아래에 모아둠
    private void BuildRankerSeats()
    {
        // if (rankerSeatPoints == null || rankerSeatPoints.Length == 0) return; // 구 방식: 비어 있으면 인스펙터 npcs 사용
        if (rankerSeatPoints == null || rankerSeatPoints.Length == 0)
        {
            Debug.LogError("[Auction] rankerSeatPoints가 비어있다. 랭커 자리 위치를 인스펙터에 넣어라");
            npcs = new SpriteRenderer[0];
            return;
        }

        if (rankerSeatRoot == null) rankerSeatRoot = new GameObject("RankerSeats").transform;

        List<SpriteRenderer> seats = new List<SpriteRenderer>();
        for (int i = 0; i < rankerSeatPoints.Length; i++)
        {
            Transform point = rankerSeatPoints[i];
            if (point == null) continue;

            GameObject go = new GameObject($"RankerSeat_{i}");
            go.transform.SetParent(rankerSeatRoot, false);
            go.transform.position = point.position + rankerSeatOffset;
            go.transform.rotation = FaceStage(go.transform.position, StagePoint); // 무대 중앙을 바라봄
            go.transform.localScale = rankerSeatScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = rankerSortingLayerName;
            sr.sortingOrder = rankerSortingOrder;
            go.SetActive(false); // NPCImageOn에서 켜짐

            seats.Add(sr);
        }

        npcs = seats.ToArray();
        // npcsPos = null; // 좌석 = 생성한 초상화 Transform (GetSeat이 npcs[i].transform을 사용)
    }

    // 플레이어 좌석 초상화: 인스펙터 playerImage가 비어 있으면 playerImagePos(맵의 '앉는 위치')에 랭커와 똑같이 코드로 생성
    // 맵 오브젝트는 건드리지 않고 RankerSeats 아래에 만듦. 이미지는 ShowPlayerPortrait에서 넣음
    private void BuildPlayerSeat()
    {
        if (playerImage != null || playerImagePos == null) return;

        if (rankerSeatRoot == null) rankerSeatRoot = new GameObject("RankerSeats").transform;
        GameObject go = new GameObject("PlayerSeat");
        go.transform.SetParent(rankerSeatRoot, false);
        go.transform.position = playerImagePos.position + rankerSeatOffset;
        go.transform.rotation = FaceStage(go.transform.position, StagePoint); // 랭커와 똑같이 무대를 바라봄
        go.transform.localScale = rankerSeatScale;

        playerImage = go.AddComponent<SpriteRenderer>();
        playerImage.sortingLayerName = rankerSortingLayerName;
        playerImage.sortingOrder = rankerSortingOrder;
        go.SetActive(false); // 착석/경매 시작 때 켜짐
    }

    // 플레이어 좌석 초상화에 넣을 이미지 (AuctionPlayer의 이미지 → 없으면 인스펙터의 playerSeatSprite)
    private Sprite PlayerSeatSprite => AuctionPlayer.Instance != null && AuctionPlayer.Instance.playerImage != null
        ? AuctionPlayer.Instance.playerImage
        : playerSeatSprite;

    // 좌석 Transform = 생성한 초상화 SpriteRenderer의 Transform
    private Transform GetSeat(int index)
    {
        // if (npcsPos != null && index < npcsPos.Length && npcsPos[index] != null) return npcsPos[index]; // 구 방식
        return npcs[index] != null ? npcs[index].transform : null;
    }

    // 좌석 초상화의 머리 위(월드 좌표). 스프라이트가 없으면 좌석 중심
    private static Vector3 GetHeadPosition(Transform seat, SpriteRenderer portrait)
    {
        if (portrait == null || portrait.sprite == null) return seat.position;
        Bounds bounds = portrait.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, seat.position.z);
    }

    private static void PlaceAboveSeat(Transform target, Transform seat, SpriteRenderer portrait)
    {
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        target.position = GetHeadPosition(seat, portrait);
    }

    // 말풍선이 좌석 초상화/배경에 가려지지 않게 정렬 순서를 초상화보다 위로
    public static void BringAboveSeat(GameObject root, SpriteRenderer portrait)
    {
        int layer = portrait != null ? portrait.sortingLayerID : 0;
        int order = portrait != null ? portrait.sortingOrder : 0;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerID = layer;
            r.sortingOrder = order + (r is SpriteRenderer ? 10 : 11); // 글자(TMP)는 말풍선보다 한 칸 위
        }
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
    private int errorMessageVersion; // 메시지가 새로 뜰 때마다 증가 (이전 메시지의 지연 처리가 새 메시지를 끄지 않게)

    public async UniTask ShowErrorMessage(string message) //에러 메시지 출력 (중앙부 부터 쭉 올라가면서 메시지 출력)
    {
        if (errorMessage == null) return;
        int version = ++errorMessageVersion;

        errorMessage.DOKill();
        errorMessage.rectTransform.DOKill();
        errorMessage.gameObject.SetActive(true);
        errorMessage.text = message;

        Color color = errorMessage.color;
        color.a = 1f;
        errorMessage.color = color;
        errorMessage.rectTransform.anchoredPosition = errorMessagePos;

        await UniTask.Delay(800);
        if (this == null || version != errorMessageVersion) return; // 그 사이 새 메시지가 떴으면 그쪽이 마무리

        float floatTime = 1f;
        float floatHight = 50f;

        _ = errorMessage.rectTransform.DOAnchorPosY(errorMessagePos.y + floatHight, floatTime).SetEase(Ease.OutQuad);
        await errorMessage.DOFade(0f, floatTime).SetEase(Ease.OutQuad).AsyncWaitForCompletion();

        if (this == null || version != errorMessageVersion) return; // 사라지는 중에 새 메시지가 떴으면 끄지 않음
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
        if (playerImage != null && PlayerSeatSprite != null)
            playerImage.sprite = PlayerSeatSprite;
    }
    
    private void NPCImageOn()
    {
        if (playerImage != null) playerImage.gameObject.SetActive(true); // 비어 있으면 건너뜀 (예전엔 여기서 예외로 경매가 멈춤)
        for (int i = 0; i < npcs.Length; i++)
        {
            npcs[i].gameObject.SetActive(true);
            SetImageAlpha(npcs[i], 0f); // 일단 투명, 참여하는 칸만 InsertNPCData에서 불투명 처리
        }
    }

    public void SetImageAlpha(SpriteRenderer image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
    private void NPCImageOff()
    {
        bool keepPlayer = seatLock != null && seatLock.IsSeated; // 착석 중이면 플레이어 모습은 계속 보임
        if (!keepPlayer && playerImage != null) playerImage.gameObject.SetActive(false);
        for (int i = 0; i < npcs.Length; i++)
        {
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
    public void PlayBGM(AuctionType currentAuctionType)
    {
        switch(currentAuctionType)
        {
            case AuctionType.Normal:
                sfxAudioSource.clip = normalBGM;
                break;
            case AuctionType.Fist:
                sfxAudioSource.clip = fistBGM;
                break;
        }
        sfxAudioSource.Play();
    }
    public void StopMusic()
    {
        if (sfxAudioSource.isPlaying || sfxAudioSource.clip != null)
        {
            sfxAudioSource.Stop();
            sfxAudioSource.clip = null; // 클립 해제까지 원하시면 추가
        }
    }
}