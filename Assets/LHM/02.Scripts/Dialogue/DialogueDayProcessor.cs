using System;
using RealSteel.BossRace;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueDayProcessor : MonoBehaviour
    {
        private const int EndingSoonDays = 7;
        private static readonly string[] Weekdays = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private TimeSystemUiStack clock;
        [SerializeField] private SeasonRaceCall seasonCall;

        [Header("시즌")]
        [SerializeField, Min(8)] private int seasonLength = 90;
        [Tooltip("첫 시즌 시작 날짜 (LJS 달력). 이날 00시에 길드 전화 → 지역 보스 선택. 그 전에는 프리시즌 (race.id 기본값 = 지역 1)")]
        [SerializeField, Range(1, 12)] private int firstSeasonMonth = 4;
        [SerializeField, Range(1, 31)] private int firstSeasonDay = 1;

        private const string StartedVariable = "season.started";
        private const float ReadyTimeout = 10f;

        private TimeSystemUiStack subscribedClock;

        public int SeasonLength => seasonLength;
        public TimeSystemUiStack Clock => clock;

        public void Configure(DialogueManager newManager, DialogueEventQueue newQueue, TimeSystemUiStack newClock, SeasonRaceCall newSeasonCall, int newSeasonLength)
        {
            manager = newManager;
            queue = newQueue;
            clock = newClock;
            seasonCall = newSeasonCall;
            seasonLength = newSeasonLength;
        }

        private void Start()
        {
            if (manager == null || queue == null)
            {
                Debug.LogError("[DayProcessor] Manager, Queue 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            if (clock == null) return;

            subscribedClock = clock;
            subscribedClock.DaysPassed += RunMidnight;
            StartCoroutine(CheckFirstSeasonWhenReady());
        }

        // 저장된 날짜가 이미 시즌 시작일을 지났는데 아직 지역을 안 골랐으면 (불러오기 · 씬 이동) 바로 전화
        private System.Collections.IEnumerator CheckFirstSeasonWhenReady()
        {
            float waited = 0f;
            while (!manager.IsReady && !manager.IsFallback && waited < ReadyTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (manager.IsReady && !manager.IsFallback) TryStartFirstSeason();
        }

        public bool SeasonStarted => manager != null && manager.Variables != null && manager.Variables.Get(StartedVariable) == "yes";

        // 프리시즌 = 달력(LJS 시계)이 있는데 아직 첫 시즌 지역을 안 고름. 달력이 없는 테스트 씬은 날짜를 몰라 프리시즌으로 보지 않음
        public bool IsPreseason => manager != null && manager.IsReady && !manager.IsFallback && !SeasonStarted && clock != null && clock.TryGetDate(out _, out _);

        private bool ReachedFirstSeasonDate()
        {
            if (clock == null || !clock.TryGetDate(out int month, out int day)) return false;
            return month > firstSeasonMonth || (month == firstSeasonMonth && day >= firstSeasonDay);
        }

        // 첫 시즌: 시작일이 되면 길드 전화 → 지역 보스 선택 (SeasonRaceCall이 끝나면 season.started = yes)
        private bool TryStartFirstSeason()
        {
            if (SeasonStarted || !ReachedFirstSeasonDate()) return false;

            if (seasonCall == null)
            {
                Debug.Log($"[DayProcessor] {firstSeasonMonth}월 {firstSeasonDay}일 — 첫 시즌 시작일이지만 Season Call이 연결되지 않아 전화 없음", this);
                return false;
            }

            if (seasonCall.Ring()) Debug.Log($"[DayProcessor] {firstSeasonMonth}월 {firstSeasonDay}일 — 첫 시즌 시작, 필드에서 길드 전화 → 지역 보스 선택", this);
            return true;
        }

        private void OnDestroy()
        {
            if (subscribedClock != null) subscribedClock.DaysPassed -= RunMidnight;
        }

        public void RunMidnight(int days)
        {
            if (manager == null || queue == null) return;
            if (manager.IsFallback) return;
            if (!manager.IsReady)
            {
                Debug.LogWarning($"[DayProcessor] 대사 데이터가 준비되기 전이라 00시 처리 {days}일을 건너뜁니다.", this);
                return;
            }

            DialogueVariables variables = manager.Variables;

            variables.Add("season.day", days);
            UpdateCalendar(variables, days);
            if (variables.GetInt("season.day") >= seasonLength - EndingSoonDays) variables.Set("season.endingSoon", "yes");

            manager.LogState($"00시 처리 (시즌 일차 +{days}{(clock != null ? ", 요일·월은 LJS 달력" : "")})");
            manager.Save();

            queue.EvaluateMidnight();

            // 프리시즌 (첫 시즌 지역을 아직 안 고름): 시즌 종료 판정 대신 시작일 확인
            if (!SeasonStarted)
            {
                TryStartFirstSeason();
                return;
            }

            if (variables.GetInt("season.day") <= seasonLength) return;

            if (seasonCall != null) seasonCall.Ring();
            else Debug.Log("[DayProcessor] 시즌 종료 — Season Call이 연결되지 않아 전화 없음", this);
        }

        private void UpdateCalendar(DialogueVariables variables, int days)
        {
            if (clock != null && clock.TryGetCalendar(out int month, out string weekdayId))
            {
                variables.Set("time.month", month);
                variables.Set("time.weekday", weekdayId);
                return;
            }

            int weekday = Math.Max(0, Array.IndexOf(Weekdays, variables.Get("time.weekday")));
            variables.Set("time.weekday", Weekdays[(weekday + days) % Weekdays.Length]);
        }
    }
}
