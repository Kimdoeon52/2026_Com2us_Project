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
