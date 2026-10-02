using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Build;
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
 보스 세명.
 조무레기 다섯명. 골드 상관x
 물건 바꾸기. 장비들로.

 보스 일정 조건에 달성하면서 큰 레이즈하면 컷씬 연출하기.
 
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
    [SerializeField] public List<NpcAiBase> npcAiList; //참여 Ai 리스트

    [Header("참여 Boss 리스트")]
    [SerializeField] public List<BossAiBase> bossAiList; //참여 Boss 리스트

    [Header("참여 Ai + Boss 리스트")]
    [SerializeField] private List<AllAiBase> auctionAiList; //참여 Ai + Boss 리스트
    [SerializeField] private int dayOfEnterNpc; //그날 하루 Npc 참여 수

    [Header("경매 물품 리스트")]
    public List<PartsDefinition> auctionItem; //경매 물품리스트 나중에 TestStuff를 바꿀것

    [Header("플레이어 이름")]
    private string playerName; //플레이어 이름


    //=====================================컷씬 용도======================================================
    [Header("컷씬 매니저")]
    [SerializeField] private AuctionCutsceneManager cutsceneManager; //컷씬 매니저

    private bool isCutscenePlaying = false; //컷씬 진행중인지 확인

    //=================================경매진행변수==============================================
    private int currentCost = 0; // 현재 가격
    private PartsDefinition stuff;
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

    //=============================컷씬용 이벤트 ================================
    private void OnEnable()
    {
        BossAiBase.OnBossBigRaiseCutscene += HandleBossBigRaiseCutscene;
    }
    private void OnDisable()
    {
        BossAiBase.OnBossBigRaiseCutscene -= HandleBossBigRaiseCutscene;
    }

    private async UniTask HandleBossBigRaiseCutscene(string bossName, string skillName)
    {
        isCutscenePlaying = true;

        if (cutsceneManager != null)
        {
            await cutsceneManager.PlayBossCutsceneAsync(bossName, skillName); //컷씬 재생 동안 대기
        }

        isCutscenePlaying = false;
    }
    //===================================================================================
    protected override void Awake()
    {
        base.Awake();
        if(errorMessage != null)
            errorMessagePos = errorMessage.rectTransform.anchoredPosition;

        ButtonReady(false);//내 턴에 나타나는 버튼 비활성화
    }

    private void Start()
    {
        // 1. 플레이어 데이터 리스트 체크
        if (GlobalGold.Instance.mainCharacterdata != null && GlobalGold.Instance.mainCharacterdata.Count > 0)
        {
            playerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName;
        }
        else
        {
            Debug.LogError("GlobalGold에 mainCharacterdata가 읍다");
            playerName = "Player";
        }
        if (auctionItem != null && auctionItem.Count > 0)
        {
            stuff = auctionItem[Random.Range(0, auctionItem.Count)];
            currentCost = stuff.Cost;
        }
        else
        {
            Debug.LogError("경매 물품 리스트가 읍다");
            return;
        }

        UpdateGoldDisplay();
        DayOfAuctionEnter();
        GetAuctionNpc(dayOfEnterNpc);
    }
    private void Update()
    {
        if(isAuctioningFin && !isChatting && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
        {
           StartAuction().Forget();
        }
    }

    //========================================경매 시작시 참여 npc목록 ========================================
    private void DayOfAuctionEnter()
    {
        dayOfEnterNpc = Random.Range(3, 5); //그날 하루 참여 Npc 수 랜덤 7명까지 현재 임시로 4명까지
    }
    private void GetAuctionNpc(int enterNpc)//그날 하루 참여 Npc 목록
    {
        // 경매 참가 인원수가 전체 NPC 수보다 많으면 전체 수로 보정
        auctionAiList.Clear(); 
        bool isBossEnter = Random.value < 1f; //35%확률로 보스 참여 임시로 100%로 설정
        if (isBossEnter && bossAiList.Count > 0)
        {
            int randomBossIndex = Random.Range(0, bossAiList.Count);
            auctionAiList.Add(bossAiList[randomBossIndex]); //보스 참여
        }
        HashSet<int> selectedIndices = new HashSet<int>();
        int targetCount = Mathf.Min(enterNpc, npcAiList.Count); // npcAiList 기준으로 제한

        while (selectedIndices.Count < targetCount)
        {
            int randomIndex = Random.Range(0, npcAiList.Count);
            selectedIndices.Add(randomIndex);
        }

        foreach (int index in selectedIndices)
        {
            auctionAiList.Add(npcAiList[index]); //auctionAiList에서 랜덤으로 선택된 애들 추가.
        }
    }

    //============================================경매시작===========================================================

    private async UniTask StartAuction() //경매 시작 부분.
    {
        isAuctioningFin = false;
        isChatting = true;
        ButtonReady(false); //플레이어 선택 버튼 비활성화
        foreach (var ai in auctionAiList)
        {
            if (ai != null)
            {
                console.text += $"{ai.NPCName}님이 참가 했습니다.\n";
                ai.ReadyForAction(); //AI 준비
            }
        }
        // 첫 대사 하고
        await StartAuctionChatting();
        await UniTask.Delay(1000);
        // 물품 보여주고
        auctionName.text = stuff.DisplayName; 
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
        foreach (var ai in auctionAiList)
        {
            if (ai != null)
            {
                aiTasks.Add(AiRoutine(ai));
            }
        }

        await UniTask.WhenAll(aiTasks);
    }
    // 개별 AI가 각자의 생각 주기(딜레이)를 갖고 독자적으로 입찰하는 루틴
    private async UniTask AiRoutine(AllAiBase ai)
    {
        while (isTimeRunning && !isAuctioningFin)
        {
            // 각 AI마다 고민하는 시간을 다르게 부여 (5초~ 20초 사이)
            int thinkDelay = Random.Range(5000, 20000);
            if(!ai.IsReady) //포기 안했으면 고민중 콘솔 출력
            {
                console.text += $"{ai.NPCName}님이 {thinkDelay / 1000}초 동안 고민중.\n";
            }
            await UniTask.Delay(thinkDelay);

            await UniTask.WaitWhile(() => isCutscenePlaying); //보스 컷씬 진행중이면 대기

            if (!isTimeRunning || isAuctioningFin) break;

            // 이미 자기가 최고 입찰자면 굳이 자기 돈을 또 올릴 필요 없음
            if (winnerName == ai.NPCName)
            {
                console.text += $"{ai.NPCName}은 현재 최고 입찰자라 더이상 레이즈하지 않습니다.\n";
                continue;
            }
                

            // AI가 포기 상태면 제외 (IsReady 혹은 IsGiveUp 플래그 확인)
            if (ai.IsReady) continue;

            // AI의 고유 판단 실행
            if (ai.RaiseThink(currentCost, stuff.Cost))
            {
                int raiseStep = 100; //기본 입찰 단위
                // 보스인지 확인 후 큰 레이즈 조건 체크
                if (ai is BossAiBase boss)
                {
                    // 필요 시 특정 조건에서 금액을 대폭 증액 (예: 500G)
                    if (boss.IsBigRaiseThink(currentCost, stuff.Cost))
                    {//임시임 현재 물건이 400원이고 원래 가격이 200원이라면 1.5배 이상이므로 컷씬 연출
                        raiseStep = boss.BigRaiseGold();
                        currentCost += raiseStep; //조건 성립시 컷씬과 함꼐 500원증가

                        // 옵저버 이벤트 발동 -> 모든 보스 공통 컷씬 실행 및 대기
                        await boss.TriggerCutscene(boss.NPCName, boss.BigRaiseSkillName());
                    }
                    else
                    {
                        currentCost += raiseStep; //조건 미성립시 100원증가
                    }
                }
                else
                {
                    currentCost += raiseStep; //일반 잡몹은 100원증가
                }

                auctionCost.text = currentCost.ToString();
                winnerName = ai.NPCName;
                ifPlayerWin = false;

                chat.text = $"{ai.NPCName} 님이 {currentCost}G로 레이즈!";

                if (remainingTime < 10f) //10초 미만이면 10초로 초기화
                {
                    remainingTime = 10f;
                }
            }
            else
            {
                console.text += $"{ai.NPCName}판단 끝! 결과 포기.\n";
                // 예산 초과 등으로 포기
                chat.text = $"{ai.NPCName} 님이 입찰을 포기했습니다.";
            }

            // 모든 참가자가 포기했는지 수시로 검사
            if (CheckAllGiveUp())
            {
                isTimeRunning = false; // 타이머를 즉시 종료시켜 경매 마감
                break;
            }
        }
    }

    private bool CheckAllGiveUp() //모두 포기했는지 확인
    {
        int inGamePlayer = 0;
        if(!isPlayerGiveUp) inGamePlayer++; //플레이어가 포기 안했으면 1더해주고
        foreach(var ai in auctionAiList)
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
            GlobalGold.Instance.UseGold(playerName, currentCost); //플레이어 골드 차감
            UpdateGoldDisplay(); //내 골드 표시 및 업데이트
            GlobalGold.Instance.SaveGame(); //할지는 일단 대기
        }
        else
        {
            foreach (var ai in auctionAiList)
            {
                if (ai != null && ai.NPCName == winnerName)
                {
                    GlobalGold.Instance.UseGold(ai.NPCName, currentCost); //AI 골드 차감
                    GlobalGold.Instance.SaveGame(); //AI 골드 저장
                    break;
                }
            }
        }
        
    }
    //======================채팅====================================
    private async UniTask StartAuctionChatting() //경매 시작 부분 대사 함수
    {
        List<string> chatList = ChatList.Instance.GetChat("경매시작", stuff.DisplayName);
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
        List<string> chatList = ChatList.Instance.GetChat("물건소개", stuff.DisplayName);
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
        string winerName = ifPlayerWin ? GlobalGold.Instance.mainCharacterdata[0].mainCharacterName : this.winnerName; //낙찰자 이름 결정
       
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
            if (!isCutscenePlaying)
            {
                remainingTime -= Time.deltaTime; //남은시간 감소
                if (timeBar != null)
                {
                    timeBar.fillAmount = Mathf.Clamp01(remainingTime / auctionTime); //시간바 업데이트
                }
            }
            await UniTask.Yield(); //다음 프레임까지 대기
        }
        isTimeRunning = false; //시간 진행중 아님
        //if(timeBar != null)
        //{
        //    timeBar.gameObject.SetActive(false); //시간바 비활성화
        //}
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
            myGold.text = "보유골드: " + GlobalGold.Instance.mainCharacterdata[0].mainCharacterGold.ToString(); //내 골드 가져오기
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
        if(winnerName == GlobalGold.Instance.mainCharacterdata[0].mainCharacterName) //이미 내가 최고 입찰자면 레이즈 불가
        {
            ShowErrorMessage("이미 최고 입찰자입니다.").Forget();
            return;
        }
        int needGold = currentCost + 100; //다음 입찰 가격
        if (GlobalGold.Instance.CanUseGold(playerName, needGold)) //가격비교
        {
            currentCost = needGold;
            auctionCost.text = currentCost.ToString();

            winnerName = GlobalGold.Instance.mainCharacterdata[0].mainCharacterName; //입찰자 이름 업데이트
            ifPlayerWin = true;
            chat.text = $"{GlobalGold.Instance.mainCharacterdata[0].mainCharacterName} 님이 {currentCost}G로 레이즈!";
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
