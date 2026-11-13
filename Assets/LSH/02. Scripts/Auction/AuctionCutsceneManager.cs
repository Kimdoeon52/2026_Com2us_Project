using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class AuctionCutsceneManager : MonoBehaviour
{
    [Header("컷씬UI들")]
    [SerializeField] private CanvasGroup cutsceneCanvasGroup; //덩어리
    [SerializeField] private TextMeshProUGUI bossNameText; //보스 이름
    [SerializeField] private TextMeshProUGUI bossSkillName; //보스 스킬이름
    [SerializeField] private RectTransform cutsceneBanner; //배너
    [SerializeField] private Image cutSceneChatacterSprite; //배너
    [SerializeField] private Image cutSceneBG; //배너 배경 기본 하얀색

    private void Awake()
    {
        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 0f; //초기화
            cutsceneCanvasGroup.gameObject.SetActive(false); //일단 끄기
        }
    }

    public async UniTask PlayBossCutsceneAsync(string bossName, string skillName, Sprite bannerSprite = null, Sprite characterSprite = null, Color newColor = default, CutsceneStyle style = CutsceneStyle.SlideLeft)
    {
        if (cutsceneCanvasGroup == null || bossNameText == null || bossSkillName == null || cutsceneBanner == null || characterSprite == null)
        {
            Debug.LogWarning("컷씬 뭐 빠진게 있다 인스펙터 확인해라.");
            return;
        }
        //======================컷씬 시작===================================
        Debug.Log("컷씬 시작: " + bossName + " - " + skillName);
        cutsceneCanvasGroup.gameObject.SetActive(true);
        bossNameText.text = bossName;
        bossSkillName.text = skillName;

        //참여 보스에 맞는 배너로 교체 (지정 안 했으면 기존 이미지 유지)
        if (bannerSprite != null && cutsceneBanner.TryGetComponent(out Image bannerImage))
        {
            bannerImage.sprite = bannerSprite;
        }
        if(characterSprite != null && cutSceneChatacterSprite != null)
        {
            cutSceneChatacterSprite.sprite = characterSprite;
        }
        //연출 시작 (보스마다 등장 방식이 다름)
        cutsceneBanner.DOKill();
        cutsceneBanner.anchoredPosition = Vector2.zero;
        cutsceneBanner.localScale = Vector3.one;
        cutsceneBanner.localRotation = Quaternion.identity;
        cutsceneCanvasGroup.alpha = 0f;
        if (cutSceneBG != null) cutSceneBG.color = newColor;

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        seq.Join(cutsceneCanvasGroup.DOFade(1f, 0.25f));
        switch (style)
        {
            case CutsceneStyle.SlideRight: // 오른쪽에서 슬라이드
                cutsceneBanner.anchoredPosition = new Vector2(1000f, 0f);
                seq.Join(cutsceneBanner.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutBack));
                break;
            case CutsceneStyle.DropDown: // 위에서 떨어지며 쿵
                cutsceneBanner.anchoredPosition = new Vector2(0f, 700f);
                seq.Join(cutsceneBanner.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutBounce));
                break;
            case CutsceneStyle.ZoomPunch: // 작게 시작해서 튀어나옴
                cutsceneBanner.localScale = Vector3.zero;
                seq.Join(cutsceneBanner.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
                seq.Append(cutsceneBanner.DOPunchScale(Vector3.one * 0.12f, 0.3f, 6, 0.5f));
                break;
            case CutsceneStyle.Shake: // 슬라이드 후 덜덜 떨림 (위협)
                cutsceneBanner.anchoredPosition = new Vector2(-1000f, 0f);
                seq.Join(cutsceneBanner.DOAnchorPosX(0f, 0.25f).SetEase(Ease.OutQuad));
                seq.Append(cutsceneBanner.DOShakeAnchorPos(0.6f, 25f, 30));
                break;
            case CutsceneStyle.Spin: // 빙글 돌며 등장 (마술/광기)
                cutsceneBanner.localScale = Vector3.one * 0.3f;
                cutsceneBanner.localRotation = Quaternion.Euler(0f, 0f, -360f);
                seq.Join(cutsceneBanner.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
                seq.Join(cutsceneBanner.DOLocalRotate(Vector3.zero, 0.45f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
                break;
            case CutsceneStyle.Glitch: // 좌우로 튀며 깜빡임 (사기/교란)
                seq.Join(cutsceneBanner.DOShakeAnchorPos(0.5f, new Vector2(60f, 0f), 12, 0f));
                seq.Join(cutsceneCanvasGroup.DOFade(0.3f, 0.06f).SetLoops(6, LoopType.Yoyo));
                seq.Append(cutsceneCanvasGroup.DOFade(1f, 0.05f));
                break;
            case CutsceneStyle.SlowFade: // 천천히 스며들 듯 (냉정/여유)
                cutsceneBanner.anchoredPosition = new Vector2(0f, -40f);
                seq.Join(cutsceneCanvasGroup.DOFade(1f, 0.6f));
                seq.Join(cutsceneBanner.DOAnchorPosY(0f, 0.8f).SetEase(Ease.OutSine));
                break;
            default: // SlideLeft: 왼쪽에서 슬라이드 (기본)
                cutsceneBanner.anchoredPosition = new Vector2(-1000f, 0f);
                seq.Join(cutsceneBanner.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutBack));
                break;
        }

        // 컷씬 지속 시간 (1.5초정도...?)
        await UniTask.Delay(1500);

        // 연출 종료 페이드 아웃
        await cutsceneCanvasGroup.DOFade(0f, 0.3f).AsyncWaitForCompletion();
        cutsceneCanvasGroup.gameObject.SetActive(false);
    }
}

// 보스 컷씬 배너 등장 방식
public enum CutsceneStyle
{
    SlideLeft,
    SlideRight,
    DropDown,
    ZoomPunch,
    Shake,
    Spin,
    Glitch,
    SlowFade,
}
