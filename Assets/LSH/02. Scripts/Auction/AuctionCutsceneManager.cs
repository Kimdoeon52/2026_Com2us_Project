using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public class AuctionCutsceneManager : MonoBehaviour
{
    [Header("컷씬UI들")]
    [SerializeField] private CanvasGroup cutsceneCanvasGroup; //덩어리
    [SerializeField] private TextMeshProUGUI bossNameText; //보스 이름
    [SerializeField] private TextMeshProUGUI bossSkillName; //보스 스킬이름
    [SerializeField] private RectTransform cutsceneBanner; //배너

    private void Awake()
    {
        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 0f; //초기화
            cutsceneCanvasGroup.gameObject.SetActive(false); //일단 끄기
        }
    }

    public async UniTask PlayBossCutsceneAsync(string bossName, string skillName)
    {
        if (cutsceneCanvasGroup == null || bossNameText == null || bossSkillName == null || cutsceneBanner == null)
        {
            Debug.LogWarning("컷씬 뭐 빠진게 있다 인스펙터 확인해라.");
            return;
        }
        //======================컷씬 시작===================================
        Debug.Log("컷씬 시작: " + bossName + " - " + skillName);
        cutsceneCanvasGroup.gameObject.SetActive(true);
        bossNameText.text = bossName;
        bossSkillName.text = skillName;

        //연출 시작
        cutsceneBanner.anchoredPosition = new Vector2(-1000f, 0f);
        cutsceneCanvasGroup.alpha = 0f;
        //나중에 맘에 안들면 방식 수정해야지
        DOTween.Sequence()
            .Join(cutsceneCanvasGroup.DOFade(1f, 0.25f))
            .Join(cutsceneBanner.DOAnchorPosX(0f, 0.35f).SetEase(Ease.OutBack)); // 배너 슬라이드 인

        // 컷씬 지속 시간 (1.5초정도...?)
        await UniTask.Delay(1500);

        // 연출 종료 페이드 아웃
        await cutsceneCanvasGroup.DOFade(0f, 0.3f).AsyncWaitForCompletion();
        cutsceneCanvasGroup.gameObject.SetActive(false);
    }
}
