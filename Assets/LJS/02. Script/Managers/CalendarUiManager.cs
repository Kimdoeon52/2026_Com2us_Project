using TMPro;
using UnityEngine;

/// <summary>
/// 달력 UI를 관리하는 매니저. 
/// </summary>
public class CalendarUiManager : MonoBehaviour
{
    public static CalendarUiManager Instance;

    [Header("달력 UI")]
    public GameObject calendarCanvas; // 달력 캔버스
    public TextMeshProUGUI yearMonthText; // 년 월 텍스트
    public CalendarSlot[] daySlots; // 각 날짜를 담을 배열

    [Header("날짜 스킵 UI")]
    public GameObject confirmPopup;
    public TextMeshProUGUI confirmText;

    private bool isCalendarOpen = false; // 달력이 켜졌는지
    public bool isConfirmOpen = false; // 날짜 스킵이 켜졌는지
    private int selectDay; // 선택된 날짜 저장

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else 
        { 
            Destroy(gameObject);
            return;
        }
        // 시작 시 Ui들 숨기기
        if (calendarCanvas != null) calendarCanvas.SetActive(false);
        if (confirmPopup != null) confirmPopup.SetActive(false);
    }

    private void OnEnable() { CalendarSlot.OnSlotClicked += OpenConfirmPopup; }
    private void OnDisable() { CalendarSlot.OnSlotClicked -= OpenConfirmPopup; }

    public void OpenConfirmPopup(int day) // 날짜 클릭시 팝업 호출
    {
        selectDay = day;
        confirmText.text = $" {day}일 아침까지\n휴식하시겠습니까?";
        confirmPopup.SetActive(true);
        isConfirmOpen = true;
    }

    public void OnClickSleep() // 스킵 확인
    {
        // 시간 워프 실행
        TimeSystemManager.Instance.SkipTime(selectDay);

        // 팝업 닫고 상태 초기화
        confirmPopup.SetActive(false);
        isConfirmOpen = false;

        ToggleCalendarUI();
    }

    public void OnClickCancel() // 취소 클릭
    {
        confirmPopup.SetActive(false);
        isConfirmOpen = false;
    }
    
    public void ToggleCalendarUI() // 달력 UI 껐다 켜기
    {
        isCalendarOpen = !isCalendarOpen;
        calendarCanvas.SetActive(isCalendarOpen);

        if (isCalendarOpen)
        {
            TimeSystemManager.Instance.BeginAction();
            RefreshCalendar();
        }
        else
        {
            TimeSystemManager.Instance.EndAction(0);
            confirmPopup.SetActive(false);
            isConfirmOpen = false;
        }
    }

    public void RefreshCalendar() // 달력 표기된 현 날짜 재정의
    {
        // TimeSystemManager에서 날짜 받아오기
        int currentYear = TimeSystemManager.Instance.year;
        int currentMonth = TimeSystemManager.Instance.month;
        int currentDay = TimeSystemManager.Instance.day;

        // 텍스트 갱신
        yearMonthText.text = $"{currentYear}년 {currentMonth:D2}월 {currentDay:D2}일";

        // 슬롯 갱신
        for (int i = 0; i < daySlots.Length; i++)
        {
            int day = i + 1; // 인덱스 0번은 1일이므로 +1
            bool isToday = (day == currentDay); // 슬롯 날짜와 금일 날짜가 같으면 true

            daySlots[i].UpdateSlot(day, isToday); // 각 날짜에게 데이터 전송
        }
    }
}
