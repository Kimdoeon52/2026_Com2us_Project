using TMPro;
using UnityEngine;

/// <summary>
/// 달력 UI를 관리하는 매니저. 싱글톤 아님
/// </summary>
public class CalendarUiManager : MonoBehaviour
{
    public static CalendarUiManager Instance;

    [Header("달력 UI")]
    public GameObject calendarCanvas; // 달력 캔버스
    public TextMeshProUGUI yearMonthText; // 년 월 텍스트
    public CalendarSlot[] daySlots; // 각 날짜를 담을 배열

    private bool isCalendarOpen = false; // 달력이 켜졌는지

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else 
        { 
            Destroy(gameObject);
            return;
        }
        // 시작 시 달력 숨기기
        if (calendarCanvas != null) calendarCanvas.SetActive(false);
    }

    public void ToggleCalendarUI()
    {
        isCalendarOpen = !isCalendarOpen;
        calendarCanvas.SetActive(isCalendarOpen);

        if (isCalendarOpen) 
        {
            TimeSystemManager.Instance.BeginAction();
            RefreshCalendar();
        }
        else TimeSystemManager.Instance.EndAction(0);
    }

    public void RefreshCalendar()
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
