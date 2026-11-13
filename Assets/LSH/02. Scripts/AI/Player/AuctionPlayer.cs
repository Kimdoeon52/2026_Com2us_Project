using DG.Tweening;
using TMPro;
using UnityEngine;

public class AuctionPlayer : Singleton<AuctionPlayer>
{
    [Header("플레이어 말풍선 나오는 곳")]
    [SerializeField] private SpriteRenderer playerThinkImage;
    [SerializeField] private TextMeshPro playerThinkChat;
    public Sprite playerImage;

    [Header("생각 말풍선 연출 설정")]
    [SerializeField] private Vector3 newScale = Vector3.one; //말풍선이 다 커졌을 때 크기 (0이면 씬에 잡힌 크기 사용)
    [SerializeField] private float bubbleSizeMultiplier = 1.17f; // newScale에 곱하는 배율 (AI 말풍선 0.35와 맞춤)
    private Sequence thinkSequence; // DOTween 트윈 중첩 방지용
    private Vector3 originalLocalPos; // 원래 말풍선 위치 저장
    private SpeechBubbleFitter bubbleFitter; // 텍스트 길이에 맞춰 말풍선 크기 조절
    private bool isBubbleReady = false;
    public Transform BubbleTransform => playerThinkImage != null ? playerThinkImage.transform : null;

    // AI 말풍선도 플레이어 말풍선과 똑같이 보이도록 스타일을 공유
    public Sprite BubbleSprite => playerThinkImage != null ? playerThinkImage.sprite : null;
    public SpriteDrawMode BubbleDrawMode => playerThinkImage != null ? playerThinkImage.drawMode : SpriteDrawMode.Simple;
    public Vector2 BubbleSize => playerThinkImage != null ? playerThinkImage.size : Vector2.one;
    public TMP_FontAsset BubbleFont => playerThinkChat != null ? playerThinkChat.font : null;

    // 다 커졌을 때 플레이어 말풍선의 화면(월드) 크기. AI 말풍선이 이 크기에 맞춰 나옴
    public Vector3 BubbleWorldScale
    {
        get
        {
            if (playerThinkImage == null) return Vector3.zero;
            InitBubble();
            Transform parent = playerThinkImage.transform.parent;
            return parent != null ? Vector3.Scale(newScale, parent.lossyScale) : newScale;
        }
    }
    public float BubbleFontSize { get; private set; } // 피터가 키우기 전의 원래 글자 크기 (AI도 같은 기준으로 키움)

    protected override void Awake()
    {
        base.Awake();
        InitBubble();
    }

    private void InitBubble()
    {
        if (isBubbleReady || playerThinkImage == null) return;
        isBubbleReady = true;

        originalLocalPos = playerThinkImage.transform.localPosition; // 씬에 배치한 위치 그대로 사용
        if (newScale == Vector3.zero) newScale = playerThinkImage.transform.localScale;
        if (newScale == Vector3.zero) newScale = Vector3.one;
        newScale *= bubbleSizeMultiplier; // 씬에 저장된 0.3 → 약 0.35 (AI 말풍선과 같은 크기)
        if (playerThinkChat != null)
            BubbleFontSize = playerThinkChat.enableAutoSizing ? playerThinkChat.fontSizeMax : playerThinkChat.fontSize;
        bubbleFitter = SpeechBubbleFitter.Attach(playerThinkImage, playerThinkChat); // 글이 길면 말풍선이 늘어나게

        // 초기 상태 비활성화 및 초기화
        ResetThinkBubble();
    }

    // 경매 시작 시 플레이어 좌석 머리 위로 말풍선을 옮김 (월드 크기는 유지)
    public void AttachBubbleTo(Transform seat, Vector3 worldPosition, SpriteRenderer seatPortrait)
    {
        if (playerThinkImage == null || seat == null) return;
        InitBubble();
        thinkSequence?.Kill();

        Transform bubble = playerThinkImage.transform;
        Vector3 worldScale = Vector3.Scale(newScale, bubble.parent != null ? bubble.parent.lossyScale : Vector3.one);
        bubble.SetParent(seat, true);
        bubble.position = worldPosition;
        originalLocalPos = bubble.localPosition;

        Vector3 seatScale = seat.lossyScale;
        newScale = new Vector3(
            seatScale.x != 0f ? worldScale.x / seatScale.x : newScale.x,
            seatScale.y != 0f ? worldScale.y / seatScale.y : newScale.y,
            seatScale.z != 0f ? worldScale.z / seatScale.z : newScale.z);

        Auction.BringAboveSeat(playerThinkImage.gameObject, seatPortrait);
        if (!bubble.TryGetComponent<BillboardToCamera>(out _)) bubble.gameObject.AddComponent<BillboardToCamera>(); // AI 말풍선과 같은 방식으로 카메라 정면
        ResetThinkBubble();
    }

    //===============================플레이어 생각===========================
    public void ShowThinkChatWithText(string message)
    {
        if (playerThinkImage == null || playerThinkChat == null) return;
        InitBubble();

        // 이전 애니메이션 진행 중이면 중단
        thinkSequence?.Kill();

        // 텍스트 적용 및 초기 상태 설정
        if (bubbleFitter != null) bubbleFitter.GetSize(message);
        else playerThinkChat.text = message;
        playerThinkImage.gameObject.SetActive(true);
        playerThinkImage.transform.localPosition = originalLocalPos;
        playerThinkImage.transform.localScale = Vector3.zero; // 0 크기에서 시작
        SetAlpha(1f);
        // DOTween 연출 시퀀스 생성
        thinkSequence = DOTween.Sequence();

        thinkSequence
            // 1. 통튀듯이 말풍선 튀어나오기 (Ease.OutBack)
            .Append(playerThinkImage.transform.DOScale(newScale, 1f).SetEase(Ease.OutBack))
            // 2. 대기하면서 말풍선이 둥실둥실 살짝 올라감
            .Append(playerThinkImage.transform.DOLocalMoveY(originalLocalPos.y + 0.3f, 2f).SetEase(Ease.OutQuad))
            // 3. 서서히 사라짐 (말풍선 + 글자)
            .Join(DOTween.To(() => 1f, SetAlpha, 0f, 2f))
            .OnComplete(ResetThinkBubble);
    }

    private void SetAlpha(float alpha)
    {
        if (playerThinkImage != null)
        {
            Color color = playerThinkImage.color;
            color.a = alpha;
            playerThinkImage.color = color;
        }
        if (playerThinkChat != null) playerThinkChat.alpha = alpha;
    }

    private void ResetThinkBubble()
    {
        if (playerThinkImage != null)
        {
            playerThinkImage.transform.localPosition = originalLocalPos;
            playerThinkImage.transform.localScale = Vector3.zero;
            SetAlpha(0f);
            playerThinkImage.gameObject.SetActive(false);
        }
    }

    protected override void OnDestroy()
    {
        // 오브젝트 파괴 시 DOTween 메모리 정리
        base.OnDestroy();
        thinkSequence?.Kill();
    }
}
