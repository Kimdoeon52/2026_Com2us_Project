using UnityEngine;
using DG.Tweening;
using TMPro;
using System; // Dotween 사용용. UI 깜빡임에 사용

public class TimeSystemManager : MonoBehaviour
{
    public static TimeSystemManager Instance; // 싱글톤

    public enum TimeState { Running, UIOpen, Combat } // 보통, UI 연 상태, 전투 중 조건 enum
    public TimeState currentState = TimeState.Running;

    [Header("Time Data")] // 시간 데이터
    public int year = 2080;
    public int month = 4;
    public int day = 1;
    public int hour = 8;

    [Header("Settings")]
    public float timeToGame = 7.5f; // 현실 시간 7.5초가 게임 시간 1시간
    private float nextHourTime; // 다음 목표 시간

    [Header("UI")]
    public TextMeshProUGUI timeText; // 시간 UI
    public CanvasGroup timeCanvasGroup;

    private Tween blinkTween; // DOTween 애니메이션용 변수

    public static event Action OnWeekMartOpen; // 경매장 오픈 구독변수
    public static event Action OnMonthScrapRestore; // 고물상 고물 리셋변수

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        UpdateTimeUI();                                                     // 시간 UI 최신화
        nextHourTime = Time.unscaledTime + timeToGame;      // 시간 세팅
    }
    private void Update() // 사용자 환경에 맞춘 시간 흐름이기 때문에 update
    {
        if (currentState != TimeState.Running) return;               // 게임이 현재 정상 실행중이면 무시

        if (Time.unscaledTime >=  nextHourTime)                   // 게임 내 시간 증가
        {
            PassTime(1);
            nextHourTime += timeToGame;
        }
    }

    public void PassTime(int inputHours)                             // 시간 증가용
    {
        hour += inputHours;
        while (hour >= 24)
        {
            hour -= 24;
            PassDay();
        }
        UpdateTimeUI();
    }

    public void PassDay() // 저장 및 로드에 대해서 추가 수정 필요함. // 날짜 증가용
    {
        day++;
        if (day > 28)
        {
            day = 1;
            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }
        CheckCalendarEvents();
    }

    public void CheckCalendarEvents() // 해당 날짜 도달시 옵저버 이벤트 발생
    {
        if (day == 1) OnMonthScrapRestore?.Invoke();
        if (day == 1 || day == 8 || day == 15 || day == 22) OnWeekMartOpen?.Invoke();
    }

    private void UpdateTimeUI() // 날짜 새로고침
    {
        timeText.text = $"{year}년 {month:D2}월 {day:D2}일 {hour:D2}시";
    }

    // 시간이 소모되는 행동용 함수. begin은 행동 시작시 시간 멈춤. End는 시간 소모(passtime)후 시간 다시 흐름
    public void BeginAction() { ChangeTimeState(TimeState.UIOpen); }
    public void EndAction(int inputHours) { PassTime(inputHours); ChangeTimeState(TimeState.Running); }

    public void ChangeTimeState(TimeState newState) // 시간 시스템의 상태 변화함수
    {
        currentState = newState;

        // 중요: 상태가 바뀔 때 진행 중이던 깜빡임 애니메이션이 있다면 반드시 강제 종료(Kill)
        if (blinkTween != null)
        {
            blinkTween.Kill();
            blinkTween = null;
        }

        switch (currentState)
        {
            case TimeState.Running: // 게임 정상 실행중
                timeCanvasGroup.alpha = 1f;
                nextHourTime = Time.unscaledTime + timeToGame;
                break;

            case TimeState.UIOpen: // 인벤토리, 설정 등[cite: 1]
                timeCanvasGroup.alpha = 1f; // 깜빡임 시작 전 기본 투명도 보장

                // DOTween 애니메이션 설정
                // 목표 알파값 0.2f로 0.5초 동안 서서히 투명해짐
                blinkTween = timeCanvasGroup.DOFade(0.2f, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo) // -1은 무한 반복, Yoyo는 1 -> 0.2 -> 1 자연스러운 왕복
                    .SetUpdate(true); // 게임 내 시간이 멈추거나 Time.timeScale이 0이어도 현실 시간에 맞춰 애니메이션 재생[cite: 1]
                break;

            case TimeState.Combat: // 전투 중[cite: 1]
                timeCanvasGroup.alpha = 0f; // UI 완전히 가림[cite: 1]
                break;
        }
    }
}
