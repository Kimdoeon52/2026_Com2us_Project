using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 달력의 각 아이콘에 삽입하는 코드
/// </summary>
public class CalendarSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static event Action<int> OnSlotClicked; // 슬롯 클릭시 방송되는 이벤트

    [Header("UI 연결")]
    public TextMeshProUGUI dayText; // 날짜 텍스트
    public GameObject todayHighlight; // 강조 효과

    [Header("이벤트 아이콘")]
    public GameObject martIcon; // 경매장 아이콘
    public GameObject scrapRestoreIcon; // 고물상 아이콘

    [Header("호버링 색상")]
    public Color normalColor = new Color(1f, 1f, 1f, 0f);
    public Color hoverColor = new Color(1f, 0.95f, 0.6f, 1f);

    private Image backgroundImage;
    private int myDay; // 슬롯의 담당날짜 기억변수

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
        backgroundImage.color = normalColor;
    }

    public void UpdateSlot(int day, bool isToday)
    {
        myDay = day;
        dayText.text = day.ToString(); // 텍스트 갱신
        todayHighlight.SetActive(isToday); // 오늘 날짜 강조효과 true

        bool isScrapRestoreDay = (day == 1); // 매달 1일
        bool isMartDay = (day % 7 == 1); // 1, 8, 15, 22일

        martIcon.SetActive(isMartDay);
        scrapRestoreIcon.SetActive(isScrapRestoreDay);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 선택창이 켜져있으면 호버링 무시
        if (CalendarUiManager.Instance.isConfirmOpen) return;

        backgroundImage.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        backgroundImage.color = normalColor;
    }

    // 날짜를 클릭했을 때 방송
    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(myDay);
    }
    private void OnDisable()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = normalColor;
        }
    }
}
