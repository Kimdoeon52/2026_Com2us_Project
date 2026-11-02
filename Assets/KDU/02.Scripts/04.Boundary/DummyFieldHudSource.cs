using System.Collections.Generic;
using UnityEngine;

// 담당 시스템이 붙기 전 진행용 더미. 시간 시스템이 없을 때 시계도 대신한다
public class DummyFieldHudSource : MonoBehaviour, IFieldHudSource, IFieldClock
{
    [Header("시간 (시간 시스템 없을 때)")]
    [Tooltip("월")]
    [SerializeField] private int _month = 3;

    [Tooltip("일")]
    [SerializeField] private int _day = 12;

    [Tooltip("시")]
    [Range(0, 23)][SerializeField] private int _hour = 14;

    [Header("표시")]
    [Tooltip("날씨")]
    [SerializeField] private string _weather = "맑음";

    [Tooltip("오늘의 징후")]
    [SerializeField] private string _omen = "대형 잔해 반입 · 파츠 +28%";

    [Tooltip("이동 소요 분")]
    [SerializeField] private int _travelMinutes = 25;

    [Tooltip("스크랩")]
    [SerializeField] private int _scrap = 86;

    [Tooltip("정제 부품")]
    [SerializeField] private int _refined = 7;

    [Tooltip("미션")]
    [SerializeField] private List<HudMission> _missions = new List<HudMission>
    {
        new HudMission { Tag = "D-3", Text = "3부 리그 2라운드 · 경기장", Kind = HudMissionKind.Deadline },
        new HudMission { Tag = "OMEN", Text = "고물상: 대형 잔해 반입 (파츠 +28%)", Kind = HudMissionKind.Omen },
        new HudMission { Tag = "TODO", Text = "좌팔 수리 · 마모 64%", Kind = HudMissionKind.Todo },
    };

    public int Month => _month;
    public int Day => _day;
    public int Hour => _hour;
    public string Weather => _weather;
    public string Omen => _omen;
    public int TravelMinutes => _travelMinutes;
    public int Scrap => _scrap;
    public int Refined => _refined;
    public IReadOnlyList<HudMission> Missions => _missions;
}
