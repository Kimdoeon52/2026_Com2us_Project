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

    private class NewsData
    {
        public string message;
        public int month;
        public int day;
        public int count;
    } private List<NewsData> pendingNews = new List<NewsData>(); // 바구니

    private void Awake()
    {
        newsGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        OnSystemNews += EnqueueNews; // 이벤트 구독
        TimeSystemManager.OnMonthScrapRestore += ScrapRestoreNews;
        TimeSystemManager.OnMonthNpcFightmoneyRestore += NpcFightmoneyRestoreNews;
        TimeSystemManager.OnWeekMartOpen += MartOpenNews;
        TimeSystemManager.OnWeekMartClose += MartCloseNews;
    }

    private void OnDisable()
    {
        OnSystemNews -= EnqueueNews; // 구독 해제
        TimeSystemManager.OnMonthScrapRestore -= ScrapRestoreNews;
        TimeSystemManager.OnMonthNpcFightmoneyRestore -= NpcFightmoneyRestoreNews;
        TimeSystemManager.OnWeekMartOpen -= MartOpenNews;
        TimeSystemManager.OnWeekMartClose -= MartCloseNews;

        if (currentSequence != null && currentSequence.IsActive()) // 해당 오브젝트 비활성화시 DOTween Kill
        {
            currentSequence.Kill();
            currentSequence = null;
        }
        isShowing = false;
    }

    private void NpcFightmoneyRestoreNews() { EnqueueNews("경기장 도전자들이 베팅금을 충전했습니다!"); }
    private void ScrapRestoreNews() { EnqueueNews("고물상에 월간 스크랩이 매입되었습니다!"); }
    private void MartOpenNews() { EnqueueNews("경매장에서 경매가 시작되었습니다!"); }
    private void MartCloseNews() { EnqueueNews("금일의 경매가 종료되었습니다."); }

    [ContextMenu("테스트: 알림 띄우기")]
    public void TestNotification() // 컴포넌트에 있는 점 3개 누르고 클릭
    {
        EnqueueNews("테스트: 시스템 알림이 정상적으로 작동합니다!");
    }

    public void EnqueueNews(string news) // 큐에 임시저장
    {
        int m = TimeSystemManager.Instance != null ? TimeSystemManager.Instance.month : 0;
        int d = TimeSystemManager.Instance != null ? TimeSystemManager.Instance.day : 0;

        // 이미 바구니에 같은 내용의 알림이 있는지 확인
        NewsData existing = pendingNews.Find(x => x.message == news);

        if (existing != null)
        {
            // 중복이면 날짜최신 후 카운트만 +1 증가
            existing.month = m;
            existing.day = d;
            existing.count++;
        }
        else
        {
            // 처음이면 추가
            pendingNews.Add(new NewsData { message = news, month = m, day = d, count = 1 });
        }
    }

    private void LateUpdate()
    {
        if (pendingNews.Count > 0)
        {
            List<string> lines = new List<string>();

            foreach (var n in pendingNews)
            {
                string line = $"[{n.month}월 {n.day}일]: {n.message}";

                // 중복이면 끝에 곱하기 추가
                if (n.count > 1)
                {
                    line += $" x{n.count}";
                }
                lines.Add(line);
            }

            string finalBlock = string.Join("\n", lines); // 줄 합치기

            // 완성된 목록을 큐에 등록
            newsQueue.Enqueue(finalBlock);
            pendingNews.Clear();

            // 출력이 쉬고 있다면 재생 시작
            if (!isShowing)
            {
                ShowNextNews();
            }
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
