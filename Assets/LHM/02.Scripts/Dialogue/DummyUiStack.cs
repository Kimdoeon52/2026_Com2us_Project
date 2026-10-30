using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DummyUiStack : MonoBehaviour, IUiStack
    {
        [Header("연결")]
        [SerializeField] private TMP_Text clockText;

        [Header("시계")]
        [SerializeField, Range(0, 23)] private int startHour = 8;
        [SerializeField, Min(0f)] private float minutesPerSecond = 10f;

        private readonly List<object> opened = new List<object>();
        private float minutes;

        public int Count => opened.Count;

        private void Awake()
        {
            minutes = startHour * 60f;
        }

        private void OnEnable()
        {
            if (UiStack.Current != null && !ReferenceEquals(UiStack.Current, this))
            {
                Debug.LogWarning("[UiStack] 다른 UI 스택이 이미 등록되어 있어 교체합니다.", this);
            }
            UiStack.Current = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(UiStack.Current, this)) UiStack.Current = null;
        }

        private void Update()
        {
            if (opened.Count == 0)
            {
                minutes += Time.deltaTime * minutesPerSecond;
                if (minutes >= 1440f) minutes -= 1440f;
            }

            RefreshClock();
        }

        public void Push(object ui)
        {
            if (ui == null || opened.Contains(ui)) return;

            opened.Add(ui);
            Debug.Log($"[UiStack] 등록: {Describe(ui)} → 열린 UI {opened.Count}개, 시계 정지");
        }

        public void Remove(object ui)
        {
            if (!opened.Remove(ui)) return;

            Debug.Log($"[UiStack] 해제: {Describe(ui)} → 열린 UI {opened.Count}개{(opened.Count == 0 ? ", 시계 진행" : "")}");
        }

        private void RefreshClock()
        {
            if (clockText == null) return;

            int total = (int)minutes;
            clockText.text = $"{total / 60:00}:{total % 60:00}{(opened.Count > 0 ? "  정지" : "")}";
        }

        private static string Describe(object ui)
        {
            return ui is Object unityObject && unityObject != null ? $"{ui.GetType().Name} ({unityObject.name})" : ui.GetType().Name;
        }
    }
}
