using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
/*
 경매장 시스템.
 처음에 랜덤 물품 5개~10개 정도 준비한다.
 첫 물품부터 경매를 시작하고 플레이어는 포기 혹은 입찰을 진행.
 플레이어는 해당 경매장을 나가기 전까지 경매를 진행할 수 있다.
 AI들은 각각 판단에따라 입찰을 진행한다. (본인 로봇 보다 좋은 부품인가 아닌가, 보유 골드가 얼마인가. 등등)

 내가 뜸들이는 동안에도 ai끼리 계속 경매 진행을 해야댐.

 10/4 해야할꺼.
 판매 ui 대충 물건 올리는거 부분 그리기. 기능 필요없음 시각적으로 보이게만.

 AI 성격 5개 기획서 확인하고 만들기.
 물건 바꾸기. 장비들로.
 
 */
public class Auction : PersistentSingleton<Auction>
{
    [Header("물건 이름 나올 곳")]
    [SerializeField] public TextMeshProUGUI auctionName; // 이름 나오는 Text공간
    [Header("물건 가격 나올 곳")]
    [SerializeField] public TextMeshProUGUI auctionCost; // 가격 나오는 Text공간
    [Header("경매 대사 나올 곳")]
    [SerializeField] public TextMeshProUGUI chat; // 채팅 나오는 Text공간
    [Header("오류 메시지 출력")]
    [SerializeField] public TextMeshProUGUI errorMessage; // 오류 메시지 출력 공간
    [Header("내 골드 나올 곳")]
    [SerializeField] public TextMeshProUGUI myGold; // 채팅 나오는 Text공간

    [Header("조작 버튼")]
    [SerializeField] public Button raiseButton;
    [SerializeField] public Button giveUpButton;

    [Header("참여 Ai 리스트")]
    [SerializeField] public List<AiBase> aiList; //참여 Ai 리스트

    [Header("경매 물품 리스트")]
    public List<TestStuff> auctionItem; //경매 물품리스트 나중에 TestStuff를 바꿀것

    private int currentCost = 0; // 현재 가격
    private TestStuff stuff;
    private bool isAuctioningFin = true; // 경매가 끝낫는지 확인
    //=======================시간 제한=================================
    [Header("제한 시간 UI")]
    [SerializeField] public Image timeBar; // 제한 시간 UI
    
    private float auctionTime = 60f; // 경매 제한 시간
    private float remainingTime = 0f; // 남은 시간
    private bool isTimeRunning = false; // 시간 진행중인지 확인
    //=======================대사 & 턴 제어================================
    bool isChatting = false; // 대사 진행중인지 확인
    private Vector2 errorMessagePos;

    private bool isPlayerGiveUp = false;
    private string winnerName = "";
    private bool ifPlayerWin = false;

    //===========================임시 콘솔창================================
    [Header("임시 콘솔창")]
    public TextMeshProUGUI console;
    protected override void Awake()
    {
        base.Awake();
        if(errorMessage != null)
            errorMessagePos = errorMessage.rectTransform.anchoredPosition;

        ButtonReady(false);//내 턴에 나타나는 버튼 비활성화
    }

    private void Start()
    {
        stuff = auctionItem[Random.Range(0, auctionItem.Count)];
        currentCost = stuff.cost;
        UpdateGoldDisplay(); //내 골드 표시 및 업데이트
    }
    private void Update()
    {
        if(isAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
           StartAuction().Forget();
        }
    }
    private async UniTask StartAuction() //경매 시작 부분.
    {
        isAuctioningFin = false;
        isChatting = true;
        ButtonReady(false); //플레이어 선택 버튼 비활성화
        foreach (var ai in aiList)
        {
            if (ai != null)
            {
                console.text += $"{ai.NpcName}님이 참가 했습니다.\n";
                ai.ReadyForAction(); //AI 준비
            }
        }
        // 첫 대사 하고
        await StartAuctionChatting();
        await UniTask.Delay(1000);
        // 물품 보여주고
        auctionName.text = stuff.stuffName; 
        auctionCost.text = currentCost.ToString();
        await WaitInput(); //입력대기
        // 물품 소개 하고
        await IntroAuction();

        isAuctioningFin = false;
        isPlayerGiveUp = false; //포기 초기화
        winnerName = "";
        ifPlayerWin = false;
        auctionTime = 60f; // 첫 경매 시작 제한시간 재설정

        
        isChatting = false;
        ButtonReady(true); //플레이어 선택 버튼 활성화

        isTimeRunning = true; //이거 해놔야 정상적으로 ai가 돌아감;; 진짜 조건부 힘들다
        RunAllAiAsync().Forget();  // AI들의 독립 입찰 루프 가동
        await StartAuctionTimer(); // 타이머 카운트다운 (시간 다 되면 자동으로 루프 통과)

        //타이머 종료 후 낙찰 처리
        await EndAuction();
        isAuctioningFin = true;
    }
    //================================AI들의 실시간 루프========================================
    private async UniTask RunAllAiAsync()
    {
        // 각 AI마다 독립적인 루프를 비동기로 동시 실행
        List<UniTask> aiTasks = new List<UniTask>();
        foreach (var ai in aiList)
        {
            if (ai != null)
            {
                aiTasks.Add(AiRoutine(ai));
            }
        }

        await UniTask.WhenAll(aiTasks);
    }
    // 개별 AI가 각자의 생각 주기(딜레이)를 갖고 독자적으로 입찰하는 루틴
    private async UniTask AiRoutine(AiBase ai)
    {
        while (isTimeRunning && !isAuctioningFin)
        {
            // 각 AI마다 고민하는 시간을 다르게 부여 (5초~ 20초 사이)
            int thinkDelay = Random.Range(5000, 20000);
            if(!ai.IsReady) //포기 안했으면 고민중 콘솔 출력
            {
                console.text += $"{ai.NpcName}님이 {thinkDelay / 1000}초 동안 고민중.\n";
            }
            await UniTask.Delay(thinkDelay);

            if (!isTimeRunning || isAuctioningFin) break;

            // 이미 자기가 최고 입찰자면 굳이 자기 돈을 또 올릴 필요 없음
            if (winnerName == ai.NpcName)
            {
                console.text += $"{ai.NpcName}은 현재 최고 입찰자라 더이상 레이즈하지 않습니다.\n";
                continue;
            }
                

            // AI가 포기 상태면 제외 (IsReady 혹은 IsGiveUp 플래그 확인)
            if (ai.IsReady) continue;

            // AI의 고유 판단 실행
            if (ai.RaiseThink(currentCost, stuff.cost))
            {
                console.text += $"{ai.NpcName}판단 끝! 결과 레이즈.\n";
                // 실시간 입찰 성공!
                currentCost += 100;
                auctionCost.text = currentCost.ToString();
                winnerName = ai.NpcName;
                ifPlayerWin = false;

                chat.text = $"{ai.NpcName} 님이 {currentCost}G로 레이즈!";

                if (remainingTime < 10f) //10초 미만이면 10초로 초기화
                {
                    remainingTime = 10f;
                }
            }
            else
            {
                console.text += $"{ai.NpcName}판단 끝! 결과 포기.\n";
                // 예산 초과 등으로 포기
                chat.text = $"{ai.NpcName} 님이 입찰을 포기했습니다.";
            }

            // 모든 참가자가 포기했는지 수시로 검사
            if (CheckAllGiveUp())
            {
                isTimeRunning = false; // 타이머를 즉시 종료시켜 경매 마감
                break;
            }
        }
    }
    ////플레이어 턴
    //private async UniTask PlayerTurn()
    //{
    //    isPlayerTurn = false;
    //    ButtonReady(true); //플레이어 선택 버튼 활성화
    //    AuctioningChatting(); //대사 진행
    //    await UniTask.WaitUntil(() => isPlayerTurn || isPlayerGiveUp || !isTimeRunning); //플레이어 턴이 끝날때까지 대기

    //    if(!isTimeRunning) //시간초과
    //    {
    //        isPlayerGiveUp = true;
    //        ShowErrorMessage("시간초과로 포기 처리되었습니다.").Forget();
    //        await UniTask.Delay(2000);
    //    }
    //    ButtonReady(false);
    //}

    ////AI 턴
    //private async UniTask AiTurn()
    //{
    //    foreach (var ai in aiList)
    //    {
    //        if(ai == null || ai.IsReady) continue;

    //        chat.text = $"{ai.NpcName}님이 입찰을 고민중입니다.";
    //        await UniTask.Delay(Random.Range(2000, 4000));

    //        if(ai.RaiseThink(currentCost, stuff.cost)) //레이즈 판단
    //        {
    //            currentCost += 100;
    //            auctionCost.text = currentCost.ToString();
    //            winnerName = ai.NpcName; //입찰자 이름 업데이트
    //            ifPlayerWin = false;
    //            chat.text = $"{ai.NpcName}님이 {currentCost}골드로 입찰하셨습니다.";
    //            await UniTask.Delay(2000);
    //            if (remainingTime < 10f) //10초 미만이면 10초로 초기화
    //            {
    //                remainingTime = 10f;
    //            }
    //        }
    //        else
    //        {
    //            chat.text = $"{ai.NpcName}님이 포기하셨습니다.";
    //            await UniTask.Delay(2000);
    //        }
    //    }
    //}

    private bool CheckAllGiveUp() //모두 포기했는지 확인
    {
        int inGamePlayer = 0;
        if(!isPlayerGiveUp) inGamePlayer++; //플레이어가 포기 안했으면 1더해주고
        foreach(var ai in aiList)
        {
            if(ai != null && !ai.IsReady) //AI가 포기 안했으면 또 더해주고
            {
                inGamePlayer++;
            }
        }
        if (inGamePlayer == 0) return true;
        return inGamePlayer <= 1 && !string.IsNullOrEmpty(winnerName); //1명 이하이면 경매 종료
    }
    //==============================경매 종료========================================

    private async UniTask EndAuction()
    {
        isAuctioningFin = true;
        isChatting = true;
        isTimeRunning = false;
        ButtonReady(false);

        await ActionFinish();

        if (ifPlayerWin)
        {
            GlobalGold.Instance.UseGold(1, currentCost); //플레이어 골드 차감
            UpdateGoldDisplay(); //내 골드 표시 및 업데이트
            GlobalGold.Instance.SaveGame(); //할지는 일단 대기
        }
        else
        {
            foreach (var ai in aiList)
            {
                if (ai != null && ai.NpcName == winnerName)
                {
                    GlobalGold.Instance.UseGold(ai.data.ID, currentCost); //AI 골드 차감
                    GlobalGold.Instance.SaveGame(); //AI 골드 저장
                    break;
                }
            }
        }
        
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
    private async UniTask ActionFinish() //경매완료
    { 
        string winerName = ifPlayerWin ? GlobalGold.Instance.data[0].npcName : this.winnerName; //낙찰자 이름 결정
       
        if (string.IsNullOrEmpty(winerName)) //낙찰자가 없으면
        {
            chat.text = "낙찰자가 없습니다.";
            await WaitInput();
            return;
        }
        List<string> chatList = ChatList.Instance.GetChat("경매완료", winerName);
        if(chatList != null && chatList.Count > 0)
        {
            foreach (var chatMessage in chatList)
            { 
                chat.text = chatMessage;
                await WaitInput(); //입력대기
            }
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
    //============================== 버튼 활성화/비활성화 ============================================================
    private void ButtonReady(bool active)
    {
        if(raiseButton != null)
            raiseButton.gameObject.SetActive(active);
        if(giveUpButton != null)
            giveUpButton.gameObject.SetActive(active);
    }
    //=============================== 내 보유 골드 표시 및 업데이트 ===========================================
    private void UpdateGoldDisplay()
    {
        if(myGold != null)
            myGold.text = "보유골드: " + GlobalGold.Instance.data[0].gold.ToString(); //내 골드 가져오기
        //여기서 내 골드 표시 UI 업데이트 코드 추가 가능
    }
    //=============================== 포기 or 레이즈 버튼클릭==================================================
    public void GiveUpButton() //포기 버튼 클릭시
    {
        if (!isTimeRunning || isPlayerGiveUp) return;
        isPlayerGiveUp = true;
        ButtonReady(false); //버튼 비활성화
        ShowErrorMessage("입찰을 포기하셨습니다.").Forget();
        //isAuctioningFin = true;
        if(CheckAllGiveUp())
        {
            isTimeRunning = false;
        }
    }
    public void OnRaiseButton() //레이즈 버튼 클릭시
    {
        if (!isTimeRunning || isPlayerGiveUp) return;
        if(winnerName == GlobalGold.Instance.data[0].npcName) //이미 내가 최고 입찰자면 레이즈 불가
        {
            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
            return;
        }
        int needGold = currentCost + 100; //다음 입찰 가격
        if (GlobalGold.Instance.CanUseGold(1, needGold)) //가격비교
        {
            currentCost = needGold;
            auctionCost.text = currentCost.ToString();

            winnerName = GlobalGold.Instance.data[0].npcName; //입찰자 이름 업데이트
            ifPlayerWin = true;
            chat.text = $"{GlobalGold.Instance.data[0].npcName} 님이 {currentCost}G로 레이즈!";
            if (remainingTime < 10f) //10초 미만이면 10초로 초기화
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
