using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step112_MidnightTest : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private DialogueEventQueue queue;
        [SerializeField] private TimeSystemUiStack clock;

        [Header("시즌")]
        [SerializeField, Min(8)] private int seasonLength = 90;
        [SerializeField] private RealSteel.BossRace.SeasonRaceCall seasonCall;

        private DialogueDayProcessor processor;

        private void Awake()
        {
            processor = FindFirstObjectByType<DialogueDayProcessor>(FindObjectsInactive.Include);
            if (processor != null) return;

            processor = gameObject.AddComponent<DialogueDayProcessor>();
            processor.Configure(manager, queue, clock, seasonCall, seasonLength);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (manager == null || queue == null)
            {
                Debug.LogError("[Step11-2] Manager, Queue 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            Debug.Log("[Step11-2] Shift+T 즉시 00시 처리 / Shift+U LJS 시계를 23시로 / Y 시즌 일차를 D-8로 / Shift+Y 시즌 마지막 날로 / U 대기열 보기");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !manager.IsReady) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.tKey.wasPressedThisFrame && shift) processor.RunMidnight(1);
            else if (keyboard.uKey.wasPressedThisFrame && shift)
            {
                if (processor.Clock != null) processor.Clock.SetHour(23);
            }
            else if (keyboard.yKey.wasPressedThisFrame && shift) JumpToSeasonLastDay();
            else if (keyboard.yKey.wasPressedThisFrame) JumpToSeasonEnd();
            else if (keyboard.uKey.wasPressedThisFrame) Debug.Log($"[Step11-2] 대기열: {queue.Dump()}", this);
        }

        private void JumpToSeasonLastDay()
        {
            int length = processor.SeasonLength;
            manager.Variables.Set("season.day", length);
            manager.LogState($"시즌 일차 {length}일 / {length}일 (다음 00시에 시즌 종료 → 길드 전화)");
            manager.Save();
        }

        private void JumpToSeasonEnd()
        {
            int length = processor.SeasonLength;
            manager.Variables.Set("season.day", length - 8);
            manager.LogState($"시즌 일차 {length - 8}일 / {length}일 (다음 00시에 종료 D-7)");
            manager.Save();
        }
#endif
    }
}
