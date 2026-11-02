using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 필드 HUD. 위치·시간 카드, 도착 배너, 재화, 미션 패널을 그린다
public class FieldHudView : MonoBehaviour
{
    [Serializable]
    private class MissionRow
    {
        public GameObject Root;
        public Image Accent;
        public TextMeshProUGUI Tag;
        public TextMeshProUGUI Text;
    }

    [Header("데이터")]
    [Tooltip("IFieldHudSource 구현. 시간 시스템이 없으면 IFieldClock도 여기서 읽는다")]
    [SerializeField] private MonoBehaviour _source;

    [Tooltip("장소 이름")]
    [SerializeField] private string _locationName = "고물상";

    [Tooltip("요일 표기. 1일이 첫 칸")]
    [SerializeField] private string[] _weekdays = { "MON · 월요일", "TUE · 화요일", "WED · 수요일", "THU · 목요일", "FRI · 금요일", "SAT · 토요일", "SUN · 일요일" };

    [Tooltip("타임라인 시작 시")]
    [SerializeField] private int _dayStartHour = 6;

    [Tooltip("타임라인 끝 시. 24 이상이면 다음 날")]
    [SerializeField] private int _dayEndHour = 26;

    [Header("위치·시간 카드")]
    [SerializeField] private TextMeshProUGUI _locationTitle;
    [SerializeField] private TextMeshProUGUI _dayChip;
    [SerializeField] private TextMeshProUGUI _dayLabel;
    [SerializeField] private TextMeshProUGUI _clock;
    [SerializeField] private TextMeshProUGUI _remain;
    [SerializeField] private RectTransform _timelineTrack;
    [SerializeField] private RectTransform _timelineMarker;

    [Header("도착 배너")]
    [SerializeField] private CanvasGroup _banner;
    [SerializeField] private TextMeshProUGUI _bannerTitle;
    [SerializeField] private TextMeshProUGUI _bannerOmen;
    [SerializeField] private TextMeshProUGUI _bannerClock;
    [SerializeField] private TextMeshProUGUI _bannerTravel;

    [Tooltip("배너 유지 초")]
    [Min(0f)][SerializeField] private float _bannerDuration = 4f;

    [Tooltip("배너 사라지는 초")]
    [Min(0.01f)][SerializeField] private float _bannerFade = 0.6f;

    [Header("재화")]
    [SerializeField] private TextMeshProUGUI _credit;
    [SerializeField] private TextMeshProUGUI _scrap;
    [SerializeField] private TextMeshProUGUI _refined;

    [Header("미션")]
    [SerializeField] private TextMeshProUGUI _missionDay;
    [SerializeField] private MissionRow[] _missionRows;
    [SerializeField] private Color _deadlineColor = new Color(1f, 0.29f, 0.23f);
    [SerializeField] private Color _omenColor = new Color(1f, 0.54f, 0.12f);
    [SerializeField] private Color _todoColor = new Color(0.21f, 0.82f, 0.87f);

    private readonly TimeSystemFieldClock _timeSystem = new TimeSystemFieldClock();
    private IFieldHudSource _hud;
    private IFieldClock _fallbackClock;
    private IGoldSource _gold;
    private int _shownHour = -1;
    private int _shownDay = -1;
    private int _shownGold = int.MinValue;

    private void Awake()
    {
        _hud = _source as IFieldHudSource;
        _fallbackClock = _source as IFieldClock;

        if (_source != null && _hud == null)
            Debug.LogWarning($"[FieldHudView] {_source.name}: IFieldHudSource 아님", this);
    }

    private void Start()
    {
        SetText(_locationTitle, _locationName);
        SetText(_bannerTitle, _locationName);
        RefreshMissions();
        RefreshResources();
        Tick();

        if (_banner != null)
            StartCoroutine(PlayBanner());
    }

    private void Update()
    {
        Tick();
        RefreshGold();
    }

    // 시나 날이 바뀔 때만 다시 그린다
    private void Tick()
    {
        IFieldClock clock = TimeSystemFieldClock.IsAvailable ? _timeSystem : _fallbackClock;
        if (clock == null)
            return;

        if (clock.Hour == _shownHour && clock.Day == _shownDay)
            return;

        _shownHour = clock.Hour;
        _shownDay = clock.Day;

        string time = $"{clock.Hour:D2}:00";
        SetText(_clock, time);
        SetText(_bannerClock, time);
        SetText(_dayChip, $"DAY {clock.Day}");
        SetText(_missionDay, $"DAY {clock.Day}");
        SetText(_dayLabel, BuildDayLabel(clock.Day));

        int hour = clock.Hour < _dayStartHour ? clock.Hour + 24 : clock.Hour;
        int remain = Mathf.Max(0, _dayEndHour - hour);
        SetText(_remain, $"{remain}H");

        float span = Mathf.Max(1, _dayEndHour - _dayStartHour);
        PlaceMarker(Mathf.Clamp01((hour - _dayStartHour) / span));
    }

    private string BuildDayLabel(int day)
    {
        string weekday = _weekdays != null && _weekdays.Length > 0 ? _weekdays[Mathf.Abs(day - 1) % _weekdays.Length] : "";
        string weather = _hud != null ? _hud.Weather : null;
        return string.IsNullOrEmpty(weather) ? weekday : $"{weekday} · {weather}";
    }

    private void PlaceMarker(float t)
    {
        if (_timelineTrack == null || _timelineMarker == null)
            return;

        Vector2 position = _timelineMarker.anchoredPosition;
        position.x = _timelineTrack.anchoredPosition.x + _timelineTrack.rect.width * t;
        _timelineMarker.anchoredPosition = position;
    }

    private void RefreshResources()
    {
        if (_hud == null)
            return;

        SetText(_scrap, _hud.Scrap.ToString("N0"));
        SetText(_refined, _hud.Refined.ToString("N0"));
        SetText(_bannerOmen, string.IsNullOrEmpty(_hud.Omen) ? "" : $"오늘의 징후: {_hud.Omen}");
        SetText(_bannerTravel, $"TRAVEL {_hud.TravelMinutes} MIN");
    }

    private void RefreshGold()
    {
        if (!GoldSources.IsAlive(_gold))
            _gold = GoldSources.Find();

        int gold = GoldSources.IsAlive(_gold) ? _gold.PlayerGold : 0;
        if (gold == _shownGold)
            return;

        _shownGold = gold;
        SetText(_credit, gold.ToString("N0"));
    }

    private void RefreshMissions()
    {
        if (_missionRows == null)
            return;

        IReadOnlyList<HudMission> missions = _hud != null ? _hud.Missions : null;
        for (int i = 0; i < _missionRows.Length; i++)
        {
            MissionRow row = _missionRows[i];
            bool show = missions != null && i < missions.Count;
            if (row.Root != null)
                row.Root.SetActive(show);

            if (!show)
                continue;

            SetText(row.Tag, missions[i].Tag);
            SetText(row.Text, missions[i].Text);
            if (row.Accent != null)
                row.Accent.color = GetMissionColor(missions[i].Kind);
        }
    }

    private Color GetMissionColor(HudMissionKind kind)
    {
        switch (kind)
        {
            case HudMissionKind.Deadline:
                return _deadlineColor;
            case HudMissionKind.Omen:
                return _omenColor;
            default:
                return _todoColor;
        }
    }

    // 도착 직후 잠깐 보였다가 사라진다
    private IEnumerator PlayBanner()
    {
        _banner.alpha = 1f;
        yield return new WaitForSecondsRealtime(_bannerDuration);

        float t = 0f;
        while (t < _bannerFade)
        {
            t += Time.unscaledDeltaTime;
            _banner.alpha = 1f - MathUtils.EaseInQuad(Mathf.Clamp01(t / _bannerFade));
            yield return null;
        }

        _banner.gameObject.SetActive(false);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }
}
