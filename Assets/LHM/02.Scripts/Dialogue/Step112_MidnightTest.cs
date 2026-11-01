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
        [SerializeField] private DummyUiStack clock;

        [Header("시즌")]
        [SerializeField, Min(8)] private int seasonLength = 90;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null || queue == null)
            {
                Debug.LogError("[Step11-2] Manager, Queue 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            if (clock != null) clock.DayPassed += RunMidnight;
            Debug.Log("[Step11-2] T 즉시 00시 처리 / Shift+T 시계를 23:58로 / Y 시즌 일차를 D-8로 / U 대기열 보기");
        }

        private void OnDestroy()
        {
            if (clock != null) clock.DayPassed -= RunMidnight;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (shift && clock != null) clock.SetTime(23, 58);
                else RunMidnight();
            }
            else if (keyboard.yKey.wasPressedThisFrame) JumpToSeasonEnd();
            else if (keyboard.uKey.wasPressedThisFrame) Debug.Log($"[Step11-2] 대기열: {queue.Dump()}", this);
        }

        private void RunMidnight()
        {
            DialogueVariables variables = manager.Variables;

            variables.Add("season.day", 1);
            int weekday = Math.Max(0, Array.IndexOf(Weekdays, variables.Get("time.weekday")));
            variables.Set("time.weekday", Weekdays[(weekday + 1) % Weekdays.Length]);
            if (variables.GetInt("season.day") >= seasonLength - 7) variables.Set("season.endingSoon", "yes");

            manager.LogState("00시 처리 1~7번 (더미: 날짜 +1)");
            manager.Save();

            queue.EvaluateMidnight();
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
