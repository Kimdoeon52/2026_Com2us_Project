using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 달력의 각 아이콘에 삽입하는 코드
/// </summary>
public class CalendarSlot : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI dayText; // 날짜 텍스트
    public GameObject todayHighlight; // 강조 효과

    [Header("이벤트 아이콘")]
    public GameObject martIcon; // 경매장 아이콘
    public GameObject scrapRestoreIcon; // 고물상 아이콘

    public void UpdateSlot(int day, bool isToday)
    {
        dayText.text = day.ToString(); // 텍스트 갱신
        todayHighlight.SetActive(isToday); // 오늘 날짜 강조효과 true

        bool isScrapRestoreDay = (day == 1); // 매달 1일
        bool isMartDay = (day % 7 == 1); // 1, 8, 15, 22일

        martIcon.SetActive(isMartDay);
        scrapRestoreIcon.SetActive(isScrapRestoreDay);
    }
}
