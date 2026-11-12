using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

// 주먹 경매에서 참가자 좌석 초상화 아래에 붙는 입찰 명패 (런타임 생성, 월드 공간 SpriteRenderer + TextMeshPro)
// 작성 중 -> 봉인 -> 동시 오픈(뒤집기 + 숫자 굴리기) -> 낙찰/탈락 순서로 연출
public class FistBidPlate : MonoBehaviour
{
    private static readonly Color ThinkingColor = new Color(0.15f, 0.15f, 0.18f, 0.85f); // 작성 중
    private static readonly Color SealedColor = new Color(0.55f, 0.1f, 0.1f, 0.95f); // 봉인된 입찰서
    private static readonly Color OpenColor = new Color(0.1f, 0.1f, 0.12f, 0.95f); // 오픈된 금액
    private static readonly Color WinnerColor = new Color(1f, 0.78f, 0.15f, 1f); // 낙찰자
    private static readonly Color GiveUpTextColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 0원(포기)

    private const float WidthRatio = 0.9f; // 초상화 폭 대비 명패 폭
    private const float HeightRatio = 0.3f; // 명패 폭 대비 높이
    private const float DefaultWidth = 1.2f; // 초상화가 없을 때 명패 폭 (월드 단위)
    private const float OldPixelHeight = 52f; // 예전 UI 명패 높이(px). Shake 세기 환산용
    private const int SortingOffset = 20; // 초상화(+0), 말풍선(+10/11)보다 위

    private static Sprite whiteSprite; // 배경용 1x1 흰색 스프라이트 (모든 명패가 공유)

    private Transform root; // 크기/흔들림 연출 대상
    private SpriteRenderer background;
    private TextMeshPro label;
    private Tween thinkingTween; // 작성 중 깜빡임
    private Vector3 basePos;
    private float plateHeight;
    private float alpha = 1f; // CanvasGroup 대신 쓰는 전체 투명도
    private Color backgroundColor;
    private Color labelColor;

    public float Alpha
    {
        get => alpha;
        set { alpha = value; ApplyColors(); }
    }

    // slot: 좌석 Transform(랭커자리/플레이어). 같은 오브젝트의 SpriteRenderer를 초상화로 보고 그 아래에 붙음
    public static FistBidPlate Create(Transform slot, TMP_FontAsset font, SpriteRenderer portrait = null)
    {
        if (portrait == null) slot.TryGetComponent(out portrait);

        GameObject go = new GameObject("FistBidPlate");
        go.transform.SetParent(slot, false);

        // 좌석 스케일과 상관없이 월드 단위로 크기를 잡기 위해 부모 스케일을 상쇄
        Vector3 parentScale = slot.lossyScale;
        go.transform.localScale = new Vector3(SafeInverse(parentScale.x), SafeInverse(parentScale.y), SafeInverse(parentScale.z));
        go.transform.localRotation = Quaternion.identity;

        float width = DefaultWidth;
        Vector3 worldPos = slot.position;
        if (portrait != null && portrait.sprite != null)
        {
            Bounds bounds = portrait.bounds;
            width = bounds.size.x * WidthRatio;
            worldPos = new Vector3(bounds.center.x, bounds.min.y - width * HeightRatio * 0.6f, slot.position.z);
        }
        float height = width * HeightRatio;
        go.transform.position = worldPos;

        FistBidPlate plate = go.AddComponent<FistBidPlate>();
        plate.root = go.transform;
        plate.baseScale = go.transform.localScale;
        plate.basePos = go.transform.localPosition;
        plate.plateHeight = height;

        int layer = portrait != null ? portrait.sortingLayerID : 0;
        int order = (portrait != null ? portrait.sortingOrder : 0) + SortingOffset;

        // 배경
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        bgGo.transform.localScale = new Vector3(width, height, 1f);
        plate.background = bgGo.AddComponent<SpriteRenderer>();
        plate.background.sprite = GetWhiteSprite();
        plate.background.sortingLayerID = layer;
        plate.background.sortingOrder = order;

        // 글자
        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(width * 0.9f, height * 0.8f);

        plate.label = textGo.AddComponent<TextMeshPro>();
        if (font != null) plate.label.font = font; // 한글 표시를 위해 기존 채팅 폰트 사용
        plate.label.alignment = TextAlignmentOptions.Center;
        plate.label.textWrappingMode = TextWrappingModes.NoWrap;
        plate.label.enableAutoSizing = true;
        plate.label.fontSizeMin = 0.1f;
        plate.label.fontSizeMax = 20f;
        plate.label.fontStyle = FontStyles.Bold;
        plate.label.sortingLayerID = layer;
        plate.label.sortingOrder = order + 1;

        plate.SetThinking();
        return plate;
    }

    private static float SafeInverse(float v) => Mathf.Approximately(v, 0f) ? 1f : 1f / v;

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width); // 1 x 1 월드 단위
            whiteSprite.name = "FistBidPlate_White";
        }
        return whiteSprite;
    }

    private void SetBackground(Color color) { backgroundColor = color; ApplyColors(); }
    private void SetLabelColor(Color color) { labelColor = color; ApplyColors(); }

    private void ApplyColors()
    {
        if (background != null)
        {
            Color bg = backgroundColor;
            bg.a *= alpha;
            background.color = bg;
        }
        if (label != null)
        {
            Color lb = labelColor;
            lb.a *= alpha;
            label.color = lb;
        }
    }

    private Tween FadeTo(float target, float time)
    {
        return DOTween.To(() => alpha, v => Alpha = v, target, time).SetLink(gameObject);
    }

    public void SetThinking() // 입찰서 작성 중
    {
        SetBackground(ThinkingColor);
        SetLabelColor(Color.white);
        label.text = "작성 중...";

        thinkingTween?.Kill();
        Alpha = 1f;
        thinkingTween = FadeTo(0.5f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    public void Seal() // 입찰서 제출 완료 (금액은 숨김)
    {
        thinkingTween?.Kill();
        Alpha = 1f;
        SetBackground(SealedColor);
        SetLabelColor(Color.white);
        label.text = "선택 완료";
        root.DOKill(true);
        root.localScale = BaseScale;
        root.DOPunchScale(BaseScale * 0.25f, 0.35f, 8, 0.6f).SetLink(gameObject);
    }

    public void Shake(float strength) // 카운트다운 중 들썩임 (strength는 예전 UI 픽셀 기준)
    {
        root.DOKill(true);
        root.localPosition = basePos;
        float worldStrength = strength / OldPixelHeight * plateHeight;
        root.DOShakePosition(0.3f, new Vector3(worldStrength, worldStrength, 0f), 20, 90f, false, true).SetLink(gameObject);
    }

    public async UniTask RevealAsync(int bid, float rollTime) // 뒤집으면서 금액 오픈
    {
        root.DOKill(true);
        root.localPosition = basePos;
        root.localScale = BaseScale;

        // 1. 카드 뒤집기 (반쯤 접고 -> 펼치면서 앞면)
        await root.DOScaleX(0f, 0.12f).SetEase(Ease.InQuad).SetLink(gameObject).AsyncWaitForCompletion();
        SetBackground(OpenColor);
        SetLabelColor(Color.white);
        label.text = "0G";
        await root.DOScaleX(BaseScale.x, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject).AsyncWaitForCompletion();

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
            SetLabelColor(GiveUpTextColor);
            label.text = "포기";
        }

        root.DOPunchScale(BaseScale * 0.15f, 0.25f, 6, 0.5f).SetLink(gameObject);
    }

    public void Win(int bid) // 낙찰자 강조
    {
        root.DOKill(true);
        root.localScale = BaseScale;
        background.sortingOrder += 2; // 다른 명패보다 앞으로
        label.sortingOrder += 2;
        SetBackground(WinnerColor);
        SetLabelColor(new Color(0.25f, 0.12f, 0f, 1f));
        label.text = $"낙찰! {bid:N0}G";

        // 시퀀스 안에는 무한 루프를 넣을 수 없어서 끝난 뒤 숨쉬기 트윈을 따로 시작
        DOTween.Sequence().SetLink(gameObject)
            .Append(root.DOScale(BaseScale * 1.35f, 0.3f).SetEase(Ease.OutBack))
            .Append(root.DOScale(BaseScale * 1.2f, 0.15f))
            .OnComplete(() => root.DOScale(BaseScale * 1.28f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject));

        // 번쩍임 (흰색 <-> 금색)
        DOTween.To(() => 0f, t => SetBackground(Color.Lerp(WinnerColor, Color.white, t)), 1f, 0.12f)
            .SetLoops(4, LoopType.Yoyo).SetLink(gameObject);
    }

    public void Lose() // 탈락자는 흐리게
    {
        root.DOKill(true);
        root.localScale = BaseScale;
        FadeTo(0.4f, 0.4f);
        root.DOScale(BaseScale * 0.9f, 0.4f).SetLink(gameObject);
    }

    // 부모 스케일을 상쇄한 기본 크기 (생성 시 정해짐)
    private Vector3 baseScale = Vector3.one;
    private Vector3 BaseScale => baseScale;

    private void OnDestroy()
    {
        thinkingTween?.Kill();
    }
}
