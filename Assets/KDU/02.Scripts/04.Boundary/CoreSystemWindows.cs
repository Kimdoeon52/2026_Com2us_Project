using UnityEngine;
using UnityEngine.Events;

// LJS 지도·달력·랭킹 창을 창 매니저에 등록한다. 이 파일 밖에서는 해당 매니저들을 직접 참조하지 않는다
public static class CoreSystemWindows
{
    // 상주 프리팹이 아직 없으면 false
    public static bool TryRegister(ExclusiveWindowManager manager)
    {
        IsoMapSystemManager map = IsoMapSystemManager.Instance;
        if (map == null)
            return false;

        // 닫을 때 시간 정지를 풀어야 해서 토글 함수로만 열고 닫는다
        manager.Register(Create("지도", map.mapCanvas, KeyCode.M, false, map.ToggleMapUI));

        CalendarUiManager calendar = CalendarUiManager.Instance;
        if (calendar != null)
            manager.Register(Create("달력", calendar.calendarCanvas, KeyCode.O, false, calendar.ToggleCalendarUI));

        RankingUiManager ranking = RankingUiManager.Instance;
        if (ranking != null)
            manager.Register(Create("랭킹", ranking.rankingCanvas, KeyCode.R, true, ranking.ToggleRankingUI));

        return true;
    }

    private static ExclusiveWindowManager.Entry Create(string name, GameObject root, KeyCode key, bool shift, UnityAction toggle)
    {
        var entry = new ExclusiveWindowManager.Entry { Name = name, Root = root, Key = key, Shift = shift };
        entry.OnForceClose.AddListener(toggle);
        entry.OnToggle.AddListener(toggle);
        return entry;
    }
}
