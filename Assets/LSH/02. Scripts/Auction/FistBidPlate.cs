using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 주먹 경매에서 참가자 초상화 아래에 붙는 입찰 명패 (런타임 생성)
// 작성 중 -> 봉인 -> 동시 오픈(뒤집기 + 숫자 굴리기) -> 낙찰/탈락 순서로 연출
public class FistBidPlate : MonoBehaviour
{
    private static readonly Color ThinkingColor = new Color(0.15f, 0.15f, 0.18f, 0.85f); // 작성 중
    private static readonly Color SealedColor = new Color(0.55f, 0.1f, 0.1f, 0.95f); // 봉인된 입찰서
    private static readonly Color OpenColor = new Color(0.1f, 0.1f, 0.12f, 0.95f); // 오픈된 금액
    private static readonly Color WinnerColor = new Color(1f, 0.78f, 0.15f, 1f); // 낙찰자
    private static readonly Color GiveUpTextColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 0원(포기)

    private RectTransform rect;
    private Image background;
    private TextMeshProUGUI label;
    private CanvasGroup group;
    private Tween thinkingTween; // 작성 중 깜빡임
    private Vector2 basePos;

    public static FistBidPlate Create(RectTransform slot, TMP_FontAsset font, Vector2 offset)
    {
        GameObject go = new GameObject("FistBidPlate", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(slot, false);

        FistBidPlate plate = go.AddComponent<FistBidPlate>();
        plate.rect = go.GetComponent<RectTransform>();
        plate.group = go.GetComponent<CanvasGroup>();
        plate.background = go.GetComponent<Image>();
        plate.background.raycastTarget = false;
        plate.group.blocksRaycasts = false;

        plate.rect.anchorMin = plate.rect.anchorMax = new Vector2(0.5f, 0.5f);
        plate.rect.sizeDelta = new Vector2(170f, 52f);
        plate.rect.anchoredPosition = offset;
        plate.basePos = offset;

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        plate.label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) plate.label.font = font; // 한글 표시를 위해 기존 채팅 폰트 사용
        plate.label.alignment = TextAlignmentOptions.Center;
        plate.label.enableAutoSizing = true;
        plate.label.fontSizeMin = 14f;
        plate.label.fontSizeMax = 30f;
        plate.label.fontStyle = FontStyles.Bold;
        plate.label.raycastTarget = false;

        plate.SetThinking();
        return plate;
    }

    public void SetThinking() // 입찰서 작성 중
    {
        background.color = ThinkingColor;
        label.color = Color.white;
        label.text = "작성 중...";

        thinkingTween?.Kill();
        group.alpha = 1f;
        thinkingTween = group.DOFade(0.5f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
    }

    public void Seal() // 입찰서 제출 완료 (금액은 숨김)
    {
        thinkingTween?.Kill();
        group.alpha = 1f;
        background.color = SealedColor;
        label.color = Color.white;
        label.text = "선택 완료";
        rect.DOKill(true);
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * 0.25f, 0.35f, 8, 0.6f).SetLink(gameObject);
    }

    public void Shake(float strength) // 카운트다운 중 들썩임
    {
        rect.DOKill(true);
        rect.anchoredPosition = basePos;
        rect.DOShakeAnchorPos(0.3f, strength, 20, 90f, false, true).SetLink(gameObject);
    }

    public async UniTask RevealAsync(int bid, float rollTime) // 뒤집으면서 금액 오픈
    {
        rect.DOKill(true);
        rect.anchoredPosition = basePos;

        // 1. 카드 뒤집기 (반쯤 접고 -> 펼치면서 앞면)
        await rect.DOScaleX(0f, 0.12f).SetEase(Ease.InQuad).SetLink(gameObject).AsyncWaitForCompletion();
        background.color = OpenColor;
        label.color = Color.white;
        label.text = "0G";
        await rect.DOScaleX(1f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject).AsyncWaitForCompletion();

        // 2. 숫자 굴리기 (모두 같은 시간에 멈추도록 rollTime 고정)
        if (bid > 0)
        {
            int shown = 0;
            await DOTween.To(() => shown, v => { shown = v; label.text = $"{v:N0}G"; }, bid, rollTime)
                .SetEase(Ease.OutCubic).SetLink(gameObject).AsyncWaitForCompletion();
            label.text = $"{bid:N0}G";
        }
        else
        {
            await UniTask.Delay((int)(rollTime * 1000));
            label.color = GiveUpTextColor;
            label.text = "포기";
        }

        rect.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.5f).SetLink(gameObject);
    }

    public void Win(int bid) // 낙찰자 강조
    {
        rect.DOKill(true);
        transform.SetAsLastSibling();
        background.color = WinnerColor;
        label.color = new Color(0.25f, 0.12f, 0f, 1f);
        label.text = $"낙찰! {bid:N0}G";

        // 시퀀스 안에는 무한 루프를 넣을 수 없어서 끝난 뒤 숨쉬기 트윈을 따로 시작
        DOTween.Sequence().SetLink(gameObject)
            .Append(rect.DOScale(1.35f, 0.3f).SetEase(Ease.OutBack))
            .Append(rect.DOScale(1.2f, 0.15f))
            .OnComplete(() => rect.DOScale(1.28f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject));
        background.DOColor(Color.white, 0.12f).SetLoops(4, LoopType.Yoyo).SetLink(gameObject); // 번쩍임
    }

    public void Lose() // 탈락자는 흐리게
    {
        rect.DOKill(true);
        group.DOFade(0.4f, 0.4f).SetLink(gameObject);
        rect.DOScale(0.9f, 0.4f).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        thinkingTween?.Kill();
    }
}
