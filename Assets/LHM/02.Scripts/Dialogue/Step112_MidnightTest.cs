using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step112_MidnightTest : MonoBehaviour
    {
        private static readonly string[] Weekdays = { "mon", "tue", "wed", "thu", "fri", "sat", "sun" };

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private TimeSystemUiStack clock;

        [Header("시즌")]
        [SerializeField, Min(8)] private int seasonLength = 90;
        [SerializeField] private RealSteel.BossRace.SeasonRaceCall seasonCall;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null || queue == null)
            {
                Debug.LogError("[Step11-2] Manager, Queue 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            if (clock != null) clock.DaysPassed += RunMidnight;
            Debug.Log("[Step11-2] T 즉시 00시 처리 / Shift+T LJS 시계를 23시로 / Y 시즌 일차를 D-8로 / Shift+Y 시즌 마지막 날로 / U 대기열 보기");
        }

        private void OnDestroy()
        {
            if (clock != null) clock.DaysPassed -= RunMidnight;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (shift && clock != null) clock.SetHour(23);
                else RunMidnight(1);
            }
            else if (keyboard.yKey.wasPressedThisFrame && shift) JumpToSeasonLastDay();
            else if (keyboard.yKey.wasPressedThisFrame) JumpToSeasonEnd();
            else if (keyboard.uKey.wasPressedThisFrame) Debug.Log($"[Step11-2] 대기열: {queue.Dump()}", this);
        }

        private void RunMidnight(int days)
        {
            DialogueVariables variables = manager.Variables;

            variables.Add("season.day", days);
            if (clock != null && clock.TryGetCalendar(out int month, out string weekdayId))
            {
                variables.Set("time.month", month);
                variables.Set("time.weekday", weekdayId);
            }
            else
            {
                int weekday = Math.Max(0, Array.IndexOf(Weekdays, variables.Get("time.weekday")));
                variables.Set("time.weekday", Weekdays[(weekday + days) % Weekdays.Length]);
            }
            if (variables.GetInt("season.day") >= seasonLength - 7) variables.Set("season.endingSoon", "yes");

            manager.LogState($"00시 처리 1~7번 (더미: 시즌 일차 +{days}{(clock != null ? ", 요일·월은 LJS 달력" : "")})");
            manager.Save();

            queue.EvaluateMidnight();

            if (variables.GetInt("season.day") > seasonLength)
            {
                if (seasonCall != null) seasonCall.Ring();
                else Debug.Log("[Step11-2] 시즌 종료 — Season Call이 연결되지 않아 전화 없음", this);
            }
        }

        private void JumpToSeasonLastDay()
        {
            manager.Variables.Set("season.day", seasonLength);
            manager.LogState($"시즌 일차 {seasonLength}일 / {seasonLength}일 (다음 00시에 시즌 종료 → 길드 전화)");
            manager.Save();
        }

        private void JumpToSeasonEnd()
        {
            manager.Variables.Set("season.day", seasonLength - 8);
            manager.LogState($"시즌 일차 {seasonLength - 8}일 / {seasonLength}일 (다음 00시에 종료 D-7)");
            manager.Save();
        }
#endif
    }
}
