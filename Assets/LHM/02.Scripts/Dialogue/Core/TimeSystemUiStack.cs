using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class TimeSystemUiStack : MonoBehaviour, IUiStack
    {
        private static readonly string[] Weekdays = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

        [Header("연결")]
        [SerializeField] private TimeSystemManager timeSystem;

        [Header("연동")]
        [SerializeField] private bool countExternalUi = true;
        [SerializeField] private bool syncCombatState = true;

        private readonly List<object> opened = new List<object>();
        private bool pausedByUs;
        private bool combatByUs;
        private int pendingDays;

        public int Count => opened.Count + (ExternalUiOpen ? 1 : 0);
        public TimeSystemManager Clock => timeSystem != null ? timeSystem : timeSystem = TimeSystemManager.Instance;
        // 이 스택 말고 다른 창(지도 · 달력 등)이 시계를 멈춰 두었는지
        public bool ExternalUiOpen => countExternalUi && Clock != null && Clock.PauseCount - (Clock.IsPausedBy(this) ? 1 : 0) > 0;

        public event Action<int> DaysPassed;

        private void OnEnable()
        {
            if (UiStack.Current != null && !ReferenceEquals(UiStack.Current, this))
            {
                Debug.LogWarning("[UiStack] 다른 UI 스택이 이미 등록되어 있어 교체합니다.", this);
            }
            UiStack.Current = this;
            TimeSystemManager.OnDayRankingRestore += OnDayChanged;
        }

        private void Start()
        {
            if (Clock == null) Debug.LogWarning("[UiStack] 이 씬에 LJS TimeSystemManager가 없어 열린 UI 목록만 관리합니다 (게임 시간 정지·00시 처리 없음).", this);
            else Debug.Log($"[UiStack] LJS 시간 시스템 연결 ({Describe(Clock)})", this);
        }

        private void OnDisable()
        {
            TimeSystemManager.OnDayRankingRestore -= OnDayChanged;
            if (ReferenceEquals(UiStack.Current, this)) UiStack.Current = null;

            TimeSystemManager clock = Clock;
            if (clock != null && pausedByUs) clock.Resume(this);
            if (clock != null && combatByUs) clock.SetCombat(false);
            pausedByUs = false;
            combatByUs = false;
        }

        private void Update()
        {
            TimeSystemManager clock = Clock;
            if (clock == null) return;

            if (opened.Count > 0 && !pausedByUs) Pause(clock);
        }

        private void LateUpdate()
        {
            if (syncCombatState && Clock != null) SyncCombat(Clock);
            if (pendingDays <= 0) return;

            int days = pendingDays;
            pendingDays = 0;
            Debug.Log($"[UiStack] 날짜 변경 {days}일 → {Describe(Clock)} — 00시 처리");
            DaysPassed?.Invoke(days);
        }

        public void Push(object ui)
        {
            if (ui == null || opened.Contains(ui)) return;

            opened.Add(ui);
            if (Clock != null) Pause(Clock);
            Debug.Log($"[UiStack] 등록: {Describe(ui)} → 열린 UI {opened.Count}개, 시계 정지");
        }

        public void Remove(object ui)
        {
            if (!opened.Remove(ui)) return;

            if (opened.Count == 0 && Clock != null) Resume(Clock);
            Debug.Log($"[UiStack] 해제: {Describe(ui)} → 열린 UI {opened.Count}개{(opened.Count == 0 ? ", 시계 진행" : "")}");
        }

        public void SetHour(int hour)
        {
            TimeSystemManager clock = Clock;
            if (clock == null) return;

            clock.hour = Mathf.Clamp(hour, 0, 23);
            clock.PassTime(0);
        }

        public bool TryGetDate(out int month, out int day)
        {
            TimeSystemManager clock = Clock;
            month = clock != null ? clock.month : 0;
            day = clock != null ? clock.day : 0;
            return clock != null;
        }

        public bool TryGetCalendar(out int month, out string weekday)
        {
            TimeSystemManager clock = Clock;
            month = clock != null ? clock.month : 0;
            weekday = clock != null ? Weekdays[(Mathf.Max(1, clock.day) - 1) % 7] : null;
            return clock != null;
        }

        private void OnDayChanged()
        {
            pendingDays++;
        }

        private void Pause(TimeSystemManager clock)
        {
            if (pausedByUs) return;

            // 시계가 정지 주체를 세므로 다른 창이 이미 멈춰 두었어도 등록해 둔다
            clock.Pause(this);
            pausedByUs = true;
        }

        private void Resume(TimeSystemManager clock)
        {
            if (!pausedByUs) return;

            pausedByUs = false;
            clock.Resume(this);
        }

        private void SyncCombat(TimeSystemManager clock)
        {
            InputModeController input = InputModeController.Current;
            bool battle = input != null && input.BaseMode == InputMode.Battle;

            if (battle && !combatByUs)
            {
                clock.SetCombat(true);
                combatByUs = true;
                Debug.Log("[UiStack] 전투 시작 → LJS 시간 Combat");
            }
            else if (!battle && combatByUs)
            {
                combatByUs = false;
                clock.SetCombat(false);
                Debug.Log($"[UiStack] 전투 종료 → LJS 시간 {clock.currentState}");
            }
        }

        private static string Describe(object ui)
        {
            if (ui is TimeSystemManager clock && clock != null) return $"{clock.year}년 {clock.month:D2}월 {clock.day:D2}일 {clock.hour:D2}시, {clock.currentState}";
            return ui is UnityEngine.Object unityObject && unityObject != null ? $"{ui.GetType().Name} ({unityObject.name})" : ui.GetType().Name;
        }
    }
}
