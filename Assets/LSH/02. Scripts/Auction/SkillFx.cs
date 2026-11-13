using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스 스킬 연출 도구 모음 (화면 플래시 / 도장 글자 / 카메라 흔들기·줌 / 초상화·UI 리액션)
// 화면 위 연출은 런타임에 만든 최상단 오버레이 캔버스에 그림 (씬 설정 필요 없음)
public static class SkillFx
{
    private static Canvas overlay;
    private static Image flash;
    private static TextMeshProUGUI stamp;

    //=============================오버레이=============================
    private static void EnsureOverlay()
    {
        if (overlay != null) return;

        GameObject go = new GameObject("SkillFxOverlay");
        overlay = go.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 500; // 컷씬/UI보다 위
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject flashGo = new GameObject("Flash", typeof(RectTransform));
        flashGo.transform.SetParent(go.transform, false);
        RectTransform fr = (RectTransform)flashGo.transform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        flash = flashGo.AddComponent<Image>();
        flash.raycastTarget = false;
        flash.color = Color.clear;

        GameObject stampGo = new GameObject("Stamp", typeof(RectTransform));
        stampGo.transform.SetParent(go.transform, false);
        RectTransform sr = (RectTransform)stampGo.transform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.sizeDelta = new Vector2(1600f, 400f);
        stamp = stampGo.AddComponent<TextMeshProUGUI>();
        stamp.raycastTarget = false;
        stamp.alignment = TextAlignmentOptions.Center;
        stamp.fontStyle = FontStyles.Bold;
        stamp.fontSize = 180f;
        stamp.enableAutoSizing = true;
        stamp.fontSizeMin = 40f;
        stamp.fontSizeMax = 200f;
        stamp.outlineWidth = 0.25f;
        stamp.outlineColor = Color.black;
        if (Auction.Instance != null && Auction.Instance.chat != null) stamp.font = Auction.Instance.chat.font; // 한글 폰트
        stamp.gameObject.SetActive(false);
    }

    // 화면 전체 색 번쩍임
    public static Tween Flash(Color color, float duration = 0.4f, float maxAlpha = 0.55f, int loops = 1)
    {
        EnsureOverlay();
        flash.DOKill();
        flash.color = new Color(color.r, color.g, color.b, 0f);
        return flash.DOFade(maxAlpha, duration * 0.3f).SetLoops(loops * 2, LoopType.Yoyo).SetEase(Ease.OutQuad)
                    .OnComplete(() => { if (flash != null) flash.color = Color.clear; });
    }

    // 화면 전체를 천천히 덮었다가 걷히는 효과 (연막, 동결 등)
    public static async UniTask Veil(Color color, float maxAlpha, float inTime, float hold, float outTime)
    {
        EnsureOverlay();
        flash.DOKill();
        flash.color = new Color(color.r, color.g, color.b, 0f);
        await flash.DOFade(maxAlpha, inTime).AsyncWaitForCompletion();
        await UniTask.Delay((int)(hold * 1000));
        if (flash != null) await flash.DOFade(0f, outTime).AsyncWaitForCompletion();
    }

    // 화면 가운데 도장처럼 쾅 찍히는 큰 글자
    public static async UniTask Stamp(string text, Color color, float hold = 0.7f, float angle = -8f)
    {
        EnsureOverlay();
        RectTransform rt = stamp.rectTransform;
        rt.DOKill();
        stamp.DOKill();
        stamp.gameObject.SetActive(true);
        stamp.text = text;
        stamp.color = color;
        stamp.alpha = 1f;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.localScale = Vector3.one * 2.5f;

        await rt.DOScale(1f, 0.18f).SetEase(Ease.InQuad).AsyncWaitForCompletion();
        ShakeCamera(0.15f, 0.2f);
        rt.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.5f);
        await UniTask.Delay((int)(hold * 1000));
        if (stamp == null) return;
        await stamp.DOFade(0f, 0.25f).AsyncWaitForCompletion();
        if (stamp != null) stamp.gameObject.SetActive(false);
    }

    //=============================카메라=============================
    public static void ShakeCamera(float strength = 0.3f, float duration = 0.4f)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        cam.transform.DOShakePosition(duration, strength, 20, 90f, false, true).SetLink(cam.gameObject);
    }

    // 시야각을 순간 좁혔다(줌 인) 되돌림
    public static void PunchZoom(float fovDelta = -6f, float duration = 0.5f)
    {
        Camera cam = Camera.main;
        if (cam == null || cam.orthographic) return;
        float baseFov = cam.fieldOfView;
        DOTween.Sequence().SetLink(cam.gameObject)
            .Append(DOTween.To(() => cam.fieldOfView, v => cam.fieldOfView = v, baseFov + fovDelta, duration * 0.3f).SetEase(Ease.OutQuad))
            .Append(DOTween.To(() => cam.fieldOfView, v => cam.fieldOfView = v, baseFov, duration * 0.7f).SetEase(Ease.InOutSine));
    }

    //=============================대상 리액션=============================
    public static void Punch(Transform t, float strength = 0.25f, float duration = 0.35f)
    {
        if (t == null) return;
        t.DOComplete();
        t.DOPunchScale(Vector3.one * strength, duration, 8, 0.6f).SetLink(t.gameObject);
    }

    public static void Tremble(Transform t, float strength = 0.08f, float duration = 0.8f)
    {
        if (t == null) return;
        t.DOShakePosition(duration, new Vector3(strength, strength * 0.5f, 0f), 30, 90f, false, true).SetLink(t.gameObject);
    }

    // 깡충 뛰기 (기쁨/기합)
    public static void Hop(Transform t, float height = 0.3f, int jumps = 1, float duration = 0.4f)
    {
        if (t == null) return;
        t.DOComplete();
        t.DOLocalJump(t.localPosition, height, jumps, duration).SetLink(t.gameObject);
    }

    // 스프라이트를 잠깐 다른 색으로 물들였다가 원래 색으로
    public static void Tint(SpriteRenderer sr, Color color, float duration = 0.6f)
    {
        if (sr == null) return;
        Color original = sr.color;
        sr.DOKill();
        sr.color = new Color(color.r, color.g, color.b, original.a);
        sr.DOColor(original, duration).SetEase(Ease.InQuad).SetLink(sr.gameObject);
    }

    // 강제 퇴장: 위로 튕겨 날아가며 빙글 돌고 제자리로 (포기 반투명은 IsGiveUp이 처리)
    public static async UniTask KickOut(Transform t)
    {
        if (t == null) return;
        Vector3 pos = t.localPosition;
        Quaternion rot = t.localRotation;
        t.DOKill();
        Sequence seq = DOTween.Sequence().SetLink(t.gameObject)
            .Append(t.DOLocalMoveY(pos.y + 0.8f, 0.3f).SetEase(Ease.OutQuad))
            .Join(t.DOLocalRotate(new Vector3(0f, 0f, 360f), 0.3f, RotateMode.FastBeyond360))
            .Append(t.DOLocalMoveY(pos.y, 0.25f).SetEase(Ease.InQuad))
            .OnKill(() => { if (t != null) { t.localPosition = pos; t.localRotation = rot; } });
        await seq.AsyncWaitForCompletion();
    }

    // 카드 뒤집기처럼 X축으로 접었다 펴기 (중간에 onMiddle 실행 → 물건 바꿔치기 등)
    public static async UniTask FlipX(Transform t, System.Action onMiddle, float duration = 0.4f)
    {
        if (t == null) { onMiddle?.Invoke(); return; }
        Vector3 scale = t.localScale;
        await t.DOScaleX(0f, duration * 0.5f).SetEase(Ease.InQuad).SetLink(t.gameObject).AsyncWaitForCompletion();
        onMiddle?.Invoke();
        scale = new Vector3(t.localScale.x == 0f ? scale.x : t.localScale.x, t.localScale.y, t.localScale.z); // onMiddle이 크기를 바꿨으면 그 값 유지
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        t.localScale = new Vector3(0f, scale.y, scale.z);
        await t.DOScaleX(scale.x, duration * 0.5f).SetEase(Ease.OutBack).SetLink(t.gameObject).AsyncWaitForCompletion();
    }

    // UI 글자(가격표 등) 강조: 크게 펀치 + 색 번쩍
    public static void PulseText(TMP_Text text, Color color, float strength = 0.4f)
    {
        if (text == null) return;
        Transform t = text.transform;
        t.DOComplete();
        t.DOPunchScale(Vector3.one * strength, 0.4f, 8, 0.6f).SetLink(t.gameObject);
        Color original = text.color;
        text.DOKill();
        text.color = color;
        text.DOColor(original, 0.8f).SetLink(t.gameObject);
    }

    // UI 그래픽(타임바/버튼 등) 흔들기 + 색 번쩍
    public static void PulseGraphic(Graphic g, Color color, float shake = 15f)
    {
        if (g == null) return;
        RectTransform rt = g.rectTransform;
        rt.DOComplete();
        rt.DOShakeAnchorPos(0.4f, shake, 25).SetLink(g.gameObject);
        Color original = g.color;
        g.DOKill();
        g.color = color;
        g.DOColor(original, 0.8f).SetLink(g.gameObject);
    }
}
