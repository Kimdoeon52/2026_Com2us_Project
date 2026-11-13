using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

/*
 경매 진행자(AuctionGirl) 연출 담당
 - 말할 때만 등장(페이드 + 왼쪽에서 슬라이드 + 말풍선 팝업), 다 말하면 퇴장
 - 대사는 타자기 효과로 출력 (입력 시 즉시 완성)
 - 대사가 바뀔 때마다 캐릭터가 살짝 들썩이는 리액션
 AuctionGirl 오브젝트에 붙이면 됨. 비워두면 Auction이 chat의 부모에 자동으로 붙여줌
 */
[DisallowMultipleComponent]
public class AuctionAnnouncer : MonoBehaviour
{
    [Header("구성 요소 (비워두면 자식에서 자동 탐색)")]
    [SerializeField] private RectTransform girlImage; // AuctionGrilImage
    [SerializeField] private RectTransform textBox;   // TextBOx
    [SerializeField] private TextMeshProUGUI chatText; // Chat

    [Header("텍스트 영역 (TextBOx 안쪽 여백)")]
    [Tooltip("켜면 Chat 영역을 TextBox 크기에 맞춰 덮어씀. 끄면 씬에서 잡아둔 Chat 크기/위치 그대로 사용")]
    [SerializeField] private bool fitChatToTextBox = false;
    [SerializeField] private Vector2 textPadding = new Vector2(200f, 30f); // 좌우, 상하 여백
    [SerializeField] private float minFontSize = 24f; // 줄바꿈해도 넘치면 이 크기까지 줄임

    [Header("등장/퇴장 연출")]
    [SerializeField] private float showDuration = 0.4f;
    [SerializeField] private float hideDuration = 0.25f;
    [SerializeField] private float slideDistance = 140f; // 왼쪽에서 미끄러져 들어오는 거리

    [Header("타자기 효과")]
    [SerializeField] private float charsPerSecond = 35f;
    [SerializeField] private float punctuationPause = 0.15f; // 문장부호 뒤 잠깐 쉬기

    private const int ShowAllCharacters = 99999;

    private CanvasGroup canvasGroup;
    private Vector2 girlBasePos;
    private Vector3 girlBaseScale = Vector3.one;
    private Vector3 textBoxBaseScale = Vector3.one;
    private bool isInitialized;
    private float baseFontSize = -1f;

    private Sequence visibilitySeq;
    private Tween hopTween;
    private int lineVersion; // 대사가 바뀔 때마다 증가 (이전 타자기/자동 퇴장 취소용)

    public bool IsVisible { get; private set; }
    public bool IsTyping { get; private set; }
    public TextMeshProUGUI ChatText { get { Init(); return chatText; } }

    private void Awake()
    {
        // Auction이 먼저 초기화해서 꺼둔 경우, 나중에 등장(SetActive)할 때 Awake가 불려도 다시 숨기지 않게
        if (isInitialized) return;
        Init();
        HideInstant(); // 시작할 땐 숨김
    }

    private void OnDestroy()
    {
        visibilitySeq?.Kill();
        hopTween?.Kill();
    }

    private void Init()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (girlImage == null) girlImage = transform.Find("AuctionGrilImage") as RectTransform;
        if (textBox == null) textBox = transform.Find("TextBOx") as RectTransform;
        if (chatText == null) chatText = GetComponentInChildren<TextMeshProUGUI>(true);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false; // 진행자가 레이즈/포기 버튼 클릭을 막지 않게

        if (girlImage != null)
        {
            girlBasePos = girlImage.anchoredPosition;
            girlBaseScale = girlImage.localScale;
        }
        if (textBox != null) textBoxBaseScale = textBox.localScale;
        FitChatToTextBox();
    }

    // Chat 텍스트 영역을 TextBOx 크기에 맞추고 줄바꿈 + 자동 축소로 박스 밖으로 안 나가게
    private void FitChatToTextBox()
    {
        if (chatText == null) return;

        chatText.textWrappingMode = TextWrappingModes.Normal; // 박스 폭에서 줄바꿈
        chatText.overflowMode = TextOverflowModes.Overflow;
        if (baseFontSize <= 0f) baseFontSize = chatText.fontSize; // 처음 한 번만 (자동 크기 후엔 fontSize가 줄어든 값이라 매번 재면 점점 작아짐)
        float baseSize = baseFontSize;
        chatText.fontSizeMax = baseSize;
        chatText.fontSizeMin = Mathf.Min(minFontSize, baseSize);
        chatText.enableAutoSizing = true; // 줄바꿈해도 높이가 넘치면 글자 크기를 줄임

        // 기본: 씬에서 잡아둔 Chat 영역을 그대로 사용 (지금 씬은 TextBox 100x100 / Chat 1600x200이라 맞추면 세로로 찍힘)
        if (!fitChatToTextBox || textBox == null) return;
        RectTransform chatRect = chatText.rectTransform;
        RectTransform parent = chatRect.parent as RectTransform;
        if (parent == null) return;

        // TextBOx의 실제 영역(스케일 1 기준)을 Chat 부모 좌표로 변환
        Vector3 savedScale = textBox.localScale;
        textBox.localScale = textBoxBaseScale;
        Vector3[] corners = new Vector3[4];
        textBox.GetWorldCorners(corners);
        textBox.localScale = savedScale;

        Vector2 min = parent.InverseTransformPoint(corners[0]);
        Vector2 max = parent.InverseTransformPoint(corners[2]);
        float boxWidth = max.x - min.x;
        float boxHeight = max.y - min.y;
        if (boxWidth < 1f || boxHeight < 1f) return; // 아직 레이아웃 계산 전 → 다음 등장 때 다시 맞춤

        // 여백(좌우 textPadding + 왼쪽 150)이 박스보다 크면 글자 폭이 0이 되어 한 글자씩 세로로 찍힘
        // → 여백은 박스 폭의 최대 40%까지만 쓰고, 글자 영역은 박스 폭의 최소 50%를 보장
        float leftShift = Mathf.Min(150f, boxWidth * 0.15f); //우측으로 조금 조절 하려고
        float padX = Mathf.Min(textPadding.x, boxWidth * 0.2f);
        float padY = Mathf.Min(textPadding.y, boxHeight * 0.2f);
        min.x += leftShift;

        float width = Mathf.Max(boxWidth * 0.5f, (max.x - min.x) - padX * 2f);
        float height = Mathf.Max(boxHeight * 0.5f, (max.y - min.y) - padY * 2f);

        chatRect.anchorMin = chatRect.anchorMax = new Vector2(0.5f, 0.5f);
        chatRect.pivot = new Vector2(0.5f, 0.5f);
        chatRect.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        chatRect.sizeDelta = new Vector2(width, height);
    }

    //=============================등장/퇴장=============================
    public async UniTask ShowAsync()
    {
        Init();
        if (IsVisible) return; // 이미 나와 있으면 그대로 이어서 말함
        IsVisible = true;

        visibilitySeq?.Kill();
        if (!gameObject.activeSelf) // 완전히 꺼져 있었으면 시작 자세부터 세팅
        {
            gameObject.SetActive(true);
            // Awake 시점엔 캔버스 레이아웃(해상도/Canvas Scaler)이 계산 전이라 박스 크기가 틀릴 수 있음 → 켜진 뒤 다시 맞춤
            Canvas.ForceUpdateCanvases();
            ResetPose();
            FitChatToTextBox();
            canvasGroup.alpha = 0f;
            if (girlImage != null) girlImage.anchoredPosition = girlBasePos + Vector2.left * slideDistance;
            if (textBox != null) textBox.localScale = textBoxBaseScale * 0.85f;
        }
        if (chatText != null)
        {
            chatText.text = "";
            chatText.maxVisibleCharacters = ShowAllCharacters;
        }

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.Join(canvasGroup.DOFade(1f, showDuration * 0.6f).SetEase(Ease.OutQuad));
        if (girlImage != null)
            seq.Join(girlImage.DOAnchorPos(girlBasePos, showDuration).SetEase(Ease.OutCubic));
        if (textBox != null)
            seq.Insert(showDuration * 0.2f, textBox.DOScale(textBoxBaseScale, showDuration * 0.8f).SetEase(Ease.OutBack));
        visibilitySeq = seq;

        await seq.AsyncWaitForCompletion();
    }

    public async UniTask HideAsync()
    {
        Init();
        if (!IsVisible) return;
        IsVisible = false;
        lineVersion++; // 진행 중인 타자기 중단

        visibilitySeq?.Kill();
        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        if (girlImage != null)
            seq.Join(girlImage.DOAnchorPos(girlBasePos + Vector2.left * slideDistance * 0.5f, hideDuration).SetEase(Ease.InCubic));
        if (textBox != null)
            seq.Join(textBox.DOScale(textBoxBaseScale * 0.9f, hideDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() =>
        {
            gameObject.SetActive(false); // 다 사라지면 완전히 끔 (중간에 다시 Show되면 Kill되어 호출 안 됨)
            ResetPose();
        });
        visibilitySeq = seq;

        await seq.AsyncWaitForCompletion();
    }

    public void HideInstant()
    {
        Init();
        visibilitySeq?.Kill();
        IsVisible = false;
        lineVersion++;
        ResetPose();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void ResetPose()
    {
        hopTween?.Kill();
        if (girlImage != null)
        {
            girlImage.anchoredPosition = girlBasePos;
            girlImage.localScale = girlBaseScale;
        }
        if (textBox != null) textBox.localScale = textBoxBaseScale;
    }

    //=============================대사 출력=============================
    // 타자기 효과로 한 줄 출력. skipRequested가 true를 반환하면 즉시 전부 보여줌
    public async UniTask TypeLineAsync(string line, Func<bool> skipRequested = null)
    {
        Init();
        if (chatText == null) return;

        int version = ++lineVersion;
        chatText.text = line;
        chatText.maxVisibleCharacters = 0;
        chatText.ForceMeshUpdate();
        int total = chatText.textInfo.characterCount;
        Hop();

        IsTyping = true;
        float visible = 0f;
        float pause = 0f;
        await UniTask.Yield(); // 이 대사를 시작시킨 입력이 곧바로 스킵으로 먹히지 않게 1프레임 대기

        while (visible < total)
        {
            if (this == null || version != lineVersion) { IsTyping = false; return; } // 다른 대사가 끼어듦
            if (skipRequested != null && skipRequested()) break;

            float dt = Time.unscaledDeltaTime;
            if (pause > 0f)
            {
                pause -= dt;
            }
            else
            {
                int before = (int)visible;
                visible = Mathf.Min(visible + dt * charsPerSecond, total);
                int after = (int)visible;
                if (after > before && after < total && IsPauseChar(chatText.textInfo.characterInfo[after - 1].character))
                    pause = punctuationPause;
            }
            chatText.maxVisibleCharacters = (int)visible;
            await UniTask.Yield();
        }

        if (this != null && version == lineVersion) chatText.maxVisibleCharacters = ShowAllCharacters;
        IsTyping = false;
    }

    // 타자기 없이 즉시 출력 (카운트다운 같이 빠르게 바뀌는 대사용)
    public void SetLineInstant(string line)
    {
        Init();
        if (chatText == null) return;
        lineVersion++;
        IsTyping = false;
        chatText.text = line;
        chatText.maxVisibleCharacters = ShowAllCharacters;
        Hop();
    }

    // 지금 대사 버전 (자동 퇴장 시 그 사이에 다른 대사가 나왔는지 확인용)
    public int LineVersion => lineVersion;

    private void Hop() // 대사 바뀔 때 캐릭터가 살짝 들썩
    {
        if (girlImage == null) return;
        hopTween?.Kill();
        girlImage.localScale = girlBaseScale;
        hopTween = girlImage.DOPunchScale(new Vector3(0.03f, 0.06f, 0f), 0.3f, 5, 0.6f)
                            .SetUpdate(true)
                            .SetLink(gameObject);
    }

    private static bool IsPauseChar(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == '…' || c == '~';
    }
}
