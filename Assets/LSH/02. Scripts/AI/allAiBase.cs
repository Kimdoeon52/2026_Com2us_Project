using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class AllAiBase : MonoBehaviour
{
    public abstract string NPCName { get; }
    public abstract int Gold { get; }
    public abstract int NPCID { get; }

    [SerializeField] private bool isGiveUp;
    public event Action OnGiveUpTrue;
    public virtual bool IsGiveUp
    {
        get => isGiveUp;
        set
        {
            if (isGiveUp != value)
            {
                isGiveUp = value;
                if (isGiveUp)
                {
                    OnGiveUpTrue?.Invoke();
                }
            }
        }
    }
    public abstract int ThinkDelay { get;} //ai마다 생각하는 시간이 다르게 설정
    public virtual int RaiseGold { get; set; } = 10;
    public abstract void ReadyForAction();
    public virtual string GetRaiseLine(int cost) => $"{cost}G 가본다!"; // 레이즈 말풍선 (보스는 개성 대사로 덮어씀)
    public abstract void OnWinAuction(int finalPrice, PartGrade stuffGrade); // 낙찰 정산 (보스: GlobalGold 차감/장비 교체, NPC: 기록만)
    public abstract bool RaiseThink(int actionPrise, int actionRealPrise, PartGrade stuffGrade);

    //==================================공통 경매 판단===================================
    public const float MinBidLimitRatio = 2f; // 원가의 최소 2배까지
    public const float MaxBidLimitRatio = 3f; // 원가의 최대 3배까지
    public abstract PartGrade[] Parts { get; } // 현재 장착 중인 장비 등급들

    private float bidLimitRatio = MinBidLimitRatio; // 이번 경매에서 원가의 몇 배까지 낼지 (경매마다 새로 굴림)

    // 경매 물품이 내 장비 중 어느 하나라도 등급이 높으면 필요한 물건
    public bool NeedsItem(PartGrade stuffGrade)
    {
        PartGrade[] parts = Parts;
        if (parts == null || parts.Length == 0) return true; // 장비가 없으면 뭐든 필요함
        foreach (PartGrade myPart in parts)
        {
            if (stuffGrade > myPart) return true;
        }
        return false;
    }

    // 경매 시작 시 호출. 이번 물품에 원가의 2배~4배 중 몇 배까지 낼지 결정
    public void RollBidLimit()
    {
        bidLimitRatio = UnityEngine.Random.Range(MinBidLimitRatio, MaxBidLimitRatio);
    }

    public int MaxBidPrice(int baseCost) => Mathf.FloorToInt(baseCost * bidLimitRatio);

    // 일반 경매 공통 판단: 필요 없는 물건이거나, 다음 레이즈가 한도(원가 x 2~4배)를 넘으면 포기
    public bool PassCommonRaiseRule(int currentCost, int baseCost, PartGrade stuffGrade)
    {
        if (!NeedsItem(stuffGrade)) return false;
        return currentCost + RaiseGold <= MaxBidPrice(baseCost);
    }

    // 주먹 경매 공통 판단: 필요 없으면 0G, 필요하면 원가의 2배~4배 사이 금액 (보유 골드 이내)
    public int CalculateFistBid(int baseCost, PartGrade stuffGrade)
    {
        if (!NeedsItem(stuffGrade)) return 0;
        int bid = Mathf.FloorToInt(baseCost * UnityEngine.Random.Range(MinBidLimitRatio, MaxBidLimitRatio));
        return Mathf.Clamp(bid, 0, Mathf.Max(0, Gold));
    }
    [Header("Npc UI들 나오는 곳")]
    [SerializeField] private GameObject dialogueBoxOB;
    [SerializeField] private TextMeshPro npcThinkChat;
    public Sprite npcImage;

    [Header("생각 말풍선 연출 설정")]
    [SerializeField] protected CanvasGroup thinkCanvasGroup; // 말풍선 전체 투명도 제어용
    protected Sequence thinkSequence; // DOTween 트윈 중첩 방지용
    protected Vector3 originalLocalPos; // 원래 말풍선 위치 저장
    protected SpriteRenderer dialogueBoxRenderer;



    [System.Serializable]
    public struct ChatList
    {
        [TextArea(2, 5)]
        public string thinkChat;
    }

    [Header("AI 고민 대사 모음")]
    [SerializeField]
    protected List<ChatList> thinkChatList = new List<ChatList>(); // 모든 AI가 인스펙터에서 각자 설정 가능

    [SerializeField] protected Vector3 newScale = new Vector3(0.2f, 0.2f, 0.2f); // 말풍선이 다 커졌을 때 크기 (플레이어와 동일하게 인스펙터에서 조절)
    protected Vector3 originalLocalScale = Vector3.one; // 프리팹에 잡힌 말풍선 크기
    private bool isBubbleReady = false;

    // Instantiate 직후 바로 실행됨. Start보다 먼저 ReadyForAction/ResetThinkBubble이 불려도
    // 프리팹에 잡아둔 말풍선 위치/크기를 잃지 않도록 Awake에서 초기화
    protected virtual void Awake()
    {
        InitThinkBubble();
    }

    public virtual void Start()
    {
        InitThinkBubble(); // Awake를 덮어쓴 자식이 있어도 안전하게 (중복 실행 안 됨)
    }

    [Header("옛 UI 말풍선 (월드 말풍선이 없을 때 스프라이트만 가져다 씀)")]
    [SerializeField] private Image thinkImage; // 예전 UI 프리팹(Npc1~6, Stage2 보스 등)에 연결돼 있던 말풍선
    private SpeechBubbleFitter bubbleFitter; // 글자가 말풍선을 넘지 않게 맞춤

    private void InitThinkBubble()
    {
        if (isBubbleReady) return;
        if (dialogueBoxOB == null) BuildWorldBubbleFromLegacy(); // 옛 UI 프리팹이면 월드 말풍선을 만들어 씀
        if (dialogueBoxOB == null) return;
        isBubbleReady = true;

        dialogueBoxRenderer = dialogueBoxOB.GetComponent<SpriteRenderer>();
        bubbleFitter = SpeechBubbleFitter.Attach(dialogueBoxRenderer, npcThinkChat);
        if (thinkCanvasGroup == null)
        {
            dialogueBoxOB.TryGetComponent<CanvasGroup>(out thinkCanvasGroup);
        }
        originalLocalPos = dialogueBoxOB.transform.localPosition;
        originalLocalScale = dialogueBoxOB.transform.localScale;
        if (originalLocalScale == Vector3.zero) originalLocalScale = Vector3.one; // 0으로 저장된 프리팹 방어
        OnGiveUpTrue += HandleGiveUp;
        // 초기 상태 비활성화 및 초기화
        ResetThinkBubble();
    }
    // 옛 UI 프리팹: UI 말풍선은 Canvas가 없어 안 보이므로 숨기고,
    // 같은 스프라이트로 SpriteRenderer + TextMeshPro 월드 말풍선을 자식으로 생성
    private void BuildWorldBubbleFromLegacy()
    {
        Sprite sprite = thinkImage != null ? thinkImage.sprite : null;
        if (thinkImage != null) thinkImage.gameObject.SetActive(false);
        if (TryGetComponent(out Image rootImage)) rootImage.enabled = false; // 루트의 빈 UI 이미지도 끔
        if (sprite == null)
        {
            Debug.LogWarning($"[{name}] 말풍선 스프라이트가 없어 월드 말풍선을 만들지 못함. dialogueBoxOB를 연결해주세요.");
            return;
        }

        GameObject box = new GameObject("WorldDialogueBox");
        box.transform.SetParent(transform, false);
        box.transform.localPosition = Vector3.zero;
        SpriteRenderer boxRenderer = box.AddComponent<SpriteRenderer>();
        boxRenderer.sprite = sprite;

        GameObject textGo = new GameObject("Chat", typeof(RectTransform));
        textGo.transform.SetParent(box.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        TextMeshPro text = textGo.AddComponent<TextMeshPro>();
        if (Auction.Instance != null && Auction.Instance.chat != null) text.font = Auction.Instance.chat.font; // 한글 폰트
        text.color = Color.black;
        text.fontSize = 2f;

        dialogueBoxOB = box;
        npcThinkChat = text;
    }

    public virtual string GetRandomThinkChat()
    {
        if (thinkChatList != null && thinkChatList.Count > 0)
        {
            int ranIndex = UnityEngine.Random.Range(0, thinkChatList.Count);
            return thinkChatList[ranIndex].thinkChat;
        }
        return string.Empty; // 대사가 없을 때 빈 문자열 반환
    }

    //==================================생각 말풍선 연출===================================
    public virtual void ShowThinkChat()
    {
        if (IsGiveUp) return;
        ShowThinkChatWithText(GetRandomThinkChat());
    }

    public virtual void ShowThinkChatWithText(string message)
    {
        InitThinkBubble(); // 옛 프리팹이면 여기서 월드 말풍선이 만들어짐
        if (dialogueBoxOB == null || npcThinkChat == null) return;

        // 이전 애니메이션 진행 중이면 중단
        thinkSequence?.Kill();
        if (bubbleFitter != null) bubbleFitter.GetSize(message); // 글자가 말풍선 안에 들어가도록
        else npcThinkChat.text = message;
        dialogueBoxOB.gameObject.SetActive(true);
        dialogueBoxOB.transform.localPosition = originalLocalPos;
        dialogueBoxOB.transform.localScale = Vector3.zero; // 0 크기에서 시작
        SetAlpha(1f);
        Auction.Instance.PlaySound(Auction.Instance.dialogueEffect);
        // DOTween 연출 시퀀스 생성
        thinkSequence = DOTween.Sequence();

        thinkSequence
            // 1. 0.3초 동안 통튀듯이 말풍선 튀어나오기 (Ease.OutBack)
            .Append(dialogueBoxOB.transform.DOScale(newScale != Vector3.zero ? newScale : originalLocalScale, 1f).SetEase(Ease.OutBack))
            // 2. 2.5초 동안 대기하면서 말풍선이 둥실둥실 살짝 올라감
            .Append(dialogueBoxOB.transform.DOLocalMoveY(originalLocalPos.y + 0.3f, 5f).SetEase(Ease.OutQuad))
            // 3. 서서히 사라지면서(Fade Out) 조금 더 위로 떠오름
            .Join(AlphaFade(0f, 3f)) //알파값 페이드
            .OnComplete(() =>
            {
                ResetThinkBubble();
            });
    }
    private void SetAlpha(float alphaAmount)
    {
        if (thinkCanvasGroup != null)
        {
            thinkCanvasGroup.alpha = alphaAmount;
            return;
        }
        if (dialogueBoxRenderer != null)
        {
            Color color = dialogueBoxRenderer.color;
            color.a = alphaAmount;
            dialogueBoxRenderer.color = color;
        }
        if (npcThinkChat != null) npcThinkChat.alpha = alphaAmount; // 월드 말풍선은 글자도 같이 투명도 조절
    }
    private Tween AlphaFade(float targetAlpha, float time)
    {
        if (thinkCanvasGroup != null)
        {
            return thinkCanvasGroup.DOFade(targetAlpha, time);
        }
        // SpriteRenderer 말풍선 + TextMeshPro 글자를 함께 페이드
        float startAlpha = dialogueBoxRenderer != null ? dialogueBoxRenderer.color.a : 1f;
        return DOTween.To(() => startAlpha, SetAlpha, targetAlpha, time);
    }
    protected void ResetThinkBubble()
    {
        InitThinkBubble(); // 초기화 전에 불려도 원래 위치/크기를 먼저 저장 (재진입은 isBubbleReady로 막힘)
        if (dialogueBoxOB != null)
        {
            dialogueBoxOB.transform.localPosition = originalLocalPos;
            dialogueBoxOB.transform.localScale = Vector3.zero;
            SetAlpha(0f);
            dialogueBoxOB.SetActive(false);
        }
    }
    protected virtual void HandleGiveUp()
    {
        ShowThinkChatWithText("쳇 난 포기 하겠어.");
    }
    private void OnDestroy()
    {
        thinkSequence?.Kill();
        // 메모리 누수 방지를 위한 이벤트 구독 해제
        OnGiveUpTrue -= HandleGiveUp;
    }
}
