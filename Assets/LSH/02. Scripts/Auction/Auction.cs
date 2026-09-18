using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/*
 경매장 시스템.
 처음에 랜덤 물품 5개~10개 정도 준비한다.
 첫 물품부터 경매를 시작하고 플레이어는 포기 혹은 입찰을 진행.
 플레이어는 해당 경매장을 나가기 전까지 경매를 진행할 수 있다.
 AI들은 각각 판단에따라 입찰을 진행한다. (본인 로봇 보다 좋은 부품인가 아닌가, 보유 골드가 얼마인가. 등등)
 */
public class Auction : PersistentSingleton<Auction>
{
    [Header("물건 이름 나올 곳")]
    [SerializeField] public Text auctionName; // 이름 나오는 Text공간
    [Header("물건 가격 나올 곳")]
    [SerializeField] public Text auctionCost; // 가격 나오는 Text공간
    [Header("경매 대사 나올 곳")]
    [SerializeField] public Text chat; // 채팅 나오는 Text공간
    [Header("오류 메시지 출력")]
    [SerializeField] public Text errorMessage; // 오류 메시지 출력 공간
    [Header("내 골드 나올 곳")]
    [SerializeField] public Text myGold; // 채팅 나오는 Text공간
    public List<TestStuff> auctionItem; //경매 물품리스트 나중에 TestStuff를 바꿀것
    private int currentCost = 0; // 현재 가격
    private TestStuff stuff;
    private bool isAuctioningFin = false; // 경매가 끝낫는지 확인
    //=======================시간 제한=================================
    [Header("제한 시간 UI")]
    [SerializeField] public Image timeBar; // 제한 시간 UI

    private float auctionTime = 60f; // 경매 제한 시간
    private float remainingTime = 0f; // 남은 시간
    private bool isTimeRunning = false; // 시간 진행중인지 확인
    //=======================대사 부분================================
    bool isChatting = false; // 대사 진행중인지 확인
    private Vector2 errorMessagePos;

    protected override void Awake()
    {
        base.Awake();
        errorMessagePos = errorMessage.rectTransform.anchoredPosition;
    }

    private void Start()
    {
        stuff = auctionItem[Random.Range(0, auctionItem.Count)];
        currentCost = stuff.cost;
        UpdateGoldDisplay(); //내 골드 표시 및 업데이트
    }
    private void Update()
    {
        if(!isAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
           StartAuction().Forget();
        }
    }
    private async UniTask StartAuction() //경매 시작 부분.
    {
        isChatting = true;
        // 첫 대사 하고
        await StartAuctionChatting();
        await UniTask.Delay(1000);
        // 물품 보여주고
        auctionName.text = stuff.stuffName; 
        auctionCost.text = currentCost.ToString();
        await WaitInput(); //입력대기
        // 물품 소개 하고
        await IntroAuction();
        StartAuctionTimer().Forget(); //시간 진행
        // 경매가 끝날 때까지
        //while (!isAuctioningFin)
        //{
        //    await AuctioningChatting(); //대사 진행
        //    //여기서 AI나 플레이어 선택 등등 넣기.
        //}
    }

    //======================채팅====================================
    private async UniTask StartAuctionChatting() //경매 시작 부분 대사 함수
    {
        List<string> chatList = ChatList.Instance.GetChat("경매시작", stuff.stuffName);
        if (chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            {
                chat.text = chatMessage;
                await WaitInput(); //입력대기
            }
        }
    }
    private async UniTask IntroAuction() //물품 소개
    {
        List<string> chatList = ChatList.Instance.GetChat("물건소개", stuff.stuffName);
        if (chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            {
                chat.text = chatMessage;
                await WaitInput(); //입력대기
            }
        }
    }
    private async UniTask AuctioningChatting() //경매중 대사 함수
    {
        List<string> chatList = ChatList.Instance.GetChat("경매중");
        if(chatList != null && chatList.Count > 0)
        {
            chat.text = chatList[Random.Range(0, chatList.Count)]; //랜덤으로 대사 출력
            await WaitInput(); //입력대기
        }
    }

    //============================================================================

    private async UniTask WaitInput() //입력 대기
    {
        // 다음 대사까지 즉시 스킵되는 현상 방지
        await UniTask.Yield();

        // Space 키 눌릴 때까지 대기
        await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Space));
    }
    //=============================== 제한 시간 ============================================================

    private async UniTask StartAuctionTimer()
    {
        remainingTime = auctionTime; //남은시간초기화
        isTimeRunning = true; //시간 진행중

        if (timeBar != null) //시간바가 있으면(있겠지.)
        {
            timeBar.gameObject.SetActive(true); //시간바 활성화
            timeBar.fillAmount = 1f; //시간바 초기화
        }
        while (remainingTime > 0f && isTimeRunning)
        {
            remainingTime -= Time.deltaTime; //남은시간 감소
            if (timeBar != null)
            {
                timeBar.fillAmount = Mathf.Clamp01(remainingTime / auctionTime); //시간바 업데이트
            }
            await UniTask.Yield(); //다음 프레임까지 대기
        }
        isTimeRunning = false; //시간 진행중 아님
        if(timeBar != null)
        {
            timeBar.gameObject.SetActive(false); //시간바 비활성화
        }
    }
    //=============================== 내 보유 골드 표시 및 업데이트 ===========================================
    private void UpdateGoldDisplay()
    {
        myGold.text = "보유골드: " + GlobalGold.Instance.data[0].gold.ToString(); //내 골드 가져오기
        //여기서 내 골드 표시 UI 업데이트 코드 추가 가능
    }
    //=============================== 포기 or 레이즈 버튼클릭==================================================
    public void GiveUpButton() //포기 버튼 클릭시
    {
        isAuctioningFin = true;
        isChatting = false;
    }
    public void OnRaiseButton() //레이즈 버튼 클릭시
    {
        if (GlobalGold.Instance.data[0].gold >= currentCost + 100) //가격비교
        {
            currentCost += 100;
            auctionCost.text = currentCost.ToString();
            if(remainingTime < 10f) //10초 미만이면 10초로 초기화
            {
                remainingTime = 10f;
            }
        }
        else
        {
            ShowErrorMessage("골드가 부족합니다.").Forget();
        }
    }

    //========================================오류 메시지 출력==================================================
    private async UniTask ShowErrorMessage(string message) //오류 메시지 출력
    {
        errorMessage.DOKill(); // 이전 애니메이션 중지
        errorMessage.rectTransform.DOKill(); // 중지
        errorMessage.gameObject.SetActive(true);
        errorMessage.text = message;
        
        //원래 위치로
        Color color = errorMessage.color;
        color.a = 1f; // 불투명하게 설정
        errorMessage.color = color;
        errorMessage.rectTransform.anchoredPosition = errorMessagePos; // 원래 위치로 초기화

        await UniTask.Delay(500);

        float floatTime = 1f;
        float floatHight = 50f;

        errorMessage.rectTransform.DOAnchorPosY(errorMessagePos.y + floatHight, floatTime).SetEase(Ease.OutQuad); //Y축으로 올라가유

        await errorMessage.DOFade(0f, floatTime).SetEase(Ease.OutQuad).AsyncWaitForCompletion();//페이드 아웃 트윈

        errorMessage.gameObject.SetActive(false);
    }
}
