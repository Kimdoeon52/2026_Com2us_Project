using UnityEngine;

/// <summary>
/// 전반적인 UI의 온오프 담당하는 매니저. 
/// </summary>
public class SystemUiOnOffManager : MonoBehaviour
{
    private void Update()
    {
        if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Combat)
            return;

        // 각 캔버스의 활성화 상태 체크
        bool isMapActive = IsoMapSystemManager.Instance.mapCanvas.activeSelf;
        bool isCalendarActive = CalendarUiManager.Instance.calendarCanvas.activeSelf;
        bool isRankingActive = RankingUiManager.Instance.rankingCanvas.activeSelf;

        // 지도
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (isMapActive)
            {
                IsoMapSystemManager.Instance.ToggleMapUI();
            }
            else if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Running || isCalendarActive || isRankingActive)
            {
                if (isCalendarActive) CalendarUiManager.Instance.ToggleCalendarUI(); // 켜져있는 달력, 랭킹창 강제 종료
                if (isRankingActive) RankingUiManager.Instance.ToggleRankingUI();
                IsoMapSystemManager.Instance.ToggleMapUI(); // 지도 켜기
            }
        }

        // 달력
        else if (Input.GetKeyDown(KeyCode.O))
        {
            if (isCalendarActive)
            {
                CalendarUiManager.Instance.ToggleCalendarUI();
            }
            else if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Running || isMapActive || isRankingActive)
            {
                if (isMapActive) IsoMapSystemManager.Instance.ToggleMapUI(); // 켜져있는 지도, 랭킹 강제 종료
                if (isRankingActive) RankingUiManager.Instance.ToggleRankingUI();
                CalendarUiManager.Instance.ToggleCalendarUI(); // 달력 켜기
            }
        }

        else if (Input.GetKeyDown(KeyCode.R) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            if (isRankingActive)
            {
                RankingUiManager.Instance.ToggleRankingUI();
            }
            else if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Running || isMapActive || isCalendarActive)
            {
                if (isMapActive) IsoMapSystemManager.Instance.ToggleMapUI();
                if (isCalendarActive) CalendarUiManager.Instance.ToggleCalendarUI();
                RankingUiManager.Instance.ToggleRankingUI(); // 랭킹 켜기
            }
        }
    }
}
