using UnityEngine;

/// <summary>
/// 전반적인 UI의 온오프 담당하는 매니저. 현재는 맵과 달력뿐임
/// </summary>
public class SystemUiOnOffManager : MonoBehaviour
{

    private void Update()
    {
        if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Combat)
            return;

        // 각 캔버스의 활성화 상태 체크
        bool isMapActive = MapSystemManager.Instance.mapCanvas.activeSelf;
        bool isCalendarActive = CalendarUiManager.Instance.calendarCanvas.activeSelf;

        if (Input.GetKeyDown(KeyCode.M))
        {
            if (isMapActive)
            {
                MapSystemManager.Instance.ToggleMapUI();
            }
            else if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Running || isCalendarActive)
            {
                if (isCalendarActive) CalendarUiManager.Instance.ToggleCalendarUI(); // 켜져있는 달력 강제 종료
                MapSystemManager.Instance.ToggleMapUI(); // 지도 켜기
            }
        }

        else if (Input.GetKeyDown(KeyCode.C))
        {
            if (isCalendarActive)
            {
                CalendarUiManager.Instance.ToggleCalendarUI();
            }
            else if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Running || isMapActive)
            {
                if (isMapActive) MapSystemManager.Instance.ToggleMapUI(); // 켜져있는 지도 강제 종료
                CalendarUiManager.Instance.ToggleCalendarUI(); // 달력 켜기
            }
        }
    }
}
