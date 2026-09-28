using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 시스템으로 돈이나 이벤트 등이 일어나면 출력해주는 매니저
/// </summary>

public class SystemNewsUiManager : MonoBehaviour
{
    // !중요! 다른 시스템에서 이 이벤트로 문자열을 던지면 UI가 알아서 출력함 !중요!
    // 예: SystemNewsUiManager.OnSystemNews?.Invoke("텍스트");
    public static event Action<string> OnSystemNews;

    [Header("UI 컴포넌트")]
    public CanvasGroup newsGroup; // 투명도 캔버스 그룹
    public TextMeshProUGUI newsText; // 알림 내용

    [Header("세팅")]
    public float showDuration = 2.5f; // 화면에 머무는 시간
    public float fadeDuration = 0.5f; // 페이드 인/아웃 시간

    private Queue<string> newsQueue = new Queue<string>(); // 이벤트 뉴스 담는 큐
    private bool isShowing = false;
    private Sequence currentSequence; // DOTween 이벤트용 변수

    private void Awake()
    {
        newsGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        OnSystemNews += EnqueueNews; // 이벤트 구독
        TimeSystemManager.OnMonthScrapRestore += ScrapRestoreNews;
        TimeSystemManager.OnWeekMartOpen += MartOpenNews;
    }

    private void OnDisable()
    {
        OnSystemNews -= EnqueueNews; // 구독 해제
        TimeSystemManager.OnMonthScrapRestore -= ScrapRestoreNews;
        TimeSystemManager.OnWeekMartOpen -= MartOpenNews;

        if (currentSequence != null && currentSequence.IsActive()) // 해당 오브젝트 비활성화시 DOTween Kill
        {
            currentSequence.Kill();
            currentSequence = null;
        }
        isShowing = false;
    }

    private void ScrapRestoreNews() { EnqueueNews("고물상에 스크랩이 매입되었습니다!"); }
    private void MartOpenNews() { EnqueueNews("경매장에서 경매가 시작되었습니다!"); }

    [ContextMenu("테스트: 알림 띄우기")]
    public void TestNotification() // 컴포넌트에 있는 점 3개 누르고 클릭
    {
        EnqueueNews("테스트: 시스템 알림이 정상적으로 작동합니다!");
    }

    public void EnqueueNews(string news)
    {
        newsQueue.Enqueue(news); // 큐 맨 뒤에 메시지 추가

        // 현재 출력중인 뉴스가 없으면 다음 뉴스 재생
        if (!isShowing)
        {
            ShowNextNews();
        }
    }

    private void ShowNextNews()
    {
        if (newsQueue.Count == 0) // 큐에 뉴스가 없으면 종료
        {
            isShowing = false;
            return;
        }

        isShowing = true;
        newsText.text = newsQueue.Dequeue(); // 큐 맨 앞의 뉴스를 텍스트로 출력

        // DOTween 연출. 깜빡깜빡이
        currentSequence = DOTween.Sequence();
        currentSequence.Append(newsGroup.DOFade(1f, fadeDuration))
           .AppendInterval(showDuration)
           .Append(newsGroup.DOFade(0f, fadeDuration))
           .OnComplete(ShowNextNews);
    }
}
