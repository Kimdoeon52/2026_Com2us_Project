using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuctionPlayer : PersistentSingleton<AuctionPlayer>
{
    [Header("플레이어 말풍선 나오는 곳")]
    [SerializeField] private Image playerThinkImage;
    [SerializeField] private TextMeshProUGUI playerThinkChat;
    public Sprite playerImage;

    [Header("생각 말풍선 연출 설정")]
    [SerializeField] private CanvasGroup thinkCanvasGroup; // 말풍선 전체 투명도 제어용
    private Sequence thinkSequence; // DOTween 트윈 중첩 방지용
    private Vector3 originalLocalPos; // 원래 말풍선 위치 저장


    private void Start()
    {
        // CanvasGroup이 안 붙어 있으면 자동으로 가져오거나 추가
        if (playerThinkImage != null)
        {
            if (!playerThinkImage.TryGetComponent<CanvasGroup>(out thinkCanvasGroup))
            {
                thinkCanvasGroup = playerThinkImage.gameObject.AddComponent<CanvasGroup>();
            }
            originalLocalPos = new Vector3(0f, 200f, 0f);

            // 초기 상태 비활성화 및 초기화
            ResetThinkBubble();
        }
    }


    //===============================플레이어 생각===========================
    public void ShowThinkChatWithText(string message)
    {
        if (playerThinkImage == null || playerThinkChat == null) return;

        // 이전 애니메이션 진행 중이면 중단
        thinkSequence?.Kill();

        // 텍스트 적용 및 초기 상태 설정
        playerThinkChat.text = message;
        playerThinkImage.gameObject.SetActive(true);
        playerThinkImage.transform.localPosition = originalLocalPos;
        playerThinkImage.transform.localScale = Vector3.zero; // 0 크기에서 시작
        thinkCanvasGroup.alpha = 1f;

        // DOTween 연출 시퀀스 생성
        thinkSequence = DOTween.Sequence();

        thinkSequence
            // 1. 0.3초 동안 통튀듯이 말풍선 튀어나오기 (Ease.OutBack)
            .Append(playerThinkImage.transform.DOScale(Vector3.one, 1f).SetEase(Ease.OutBack))
            // 2. 2.5초 동안 대기하면서 말풍선이 둥실둥실 살짝 올라감
            .Append(playerThinkImage.transform.DOLocalMoveY(originalLocalPos.y + 50f, 5f).SetEase(Ease.OutQuad))
            // 3. 서서히 사라지면서(Fade Out) 조금 더 위로 떠오름
            .Join(thinkCanvasGroup.DOFade(0f, 2f))
            .OnComplete(() =>
            {
                ResetThinkBubble();
            });
    }

    private void ResetThinkBubble()
    {
        if (playerThinkImage != null)
        {
            playerThinkImage.transform.localPosition = originalLocalPos;
            playerThinkImage.transform.localScale = Vector3.zero;
            if (thinkCanvasGroup != null) thinkCanvasGroup.alpha = 0f;
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
