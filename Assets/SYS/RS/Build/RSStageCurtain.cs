// RE:AL STEEL - 무대 막 (여닫는 커튼)
//
// 양쪽으로 갈라지는 무대 커튼. 왼쪽 · 오른쪽 반쪽을 바깥 끝을 기준으로 가로로 줄여서(주름이 촘촘해지며 옆으로 몰린다) 연다.
//  · 인스펙터의 '열림' 을 켜고 끄면 에디터에서 바로 보인다. 플레이 중에는 '걸리는 시간' 동안 부드럽게 움직인다
//  · 코드: GetComponent<RSStageCurtain>().Open() / Close() / Toggle() / SetOpen(true, instant: true)
//  · 다 열렸을 때 · 다 닫혔을 때 이벤트 (경매 시작 연출 등을 연결)
//  · 왼쪽 · 오른쪽 오브젝트의 기준점(원점)이 커튼 바깥 끝에 있어야 한다 (메뉴가 그렇게 만든다)
using UnityEngine;
using UnityEngine.Events;

namespace RealSteel.Build
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Build/무대 막 (여닫는 커튼)")]
    public class RSStageCurtain : MonoBehaviour
    {
        [Tooltip("왼쪽 반쪽 (기준점 = 바깥 끝)")]
        public Transform left;
        [Tooltip("오른쪽 반쪽 (기준점 = 바깥 끝)")]
        public Transform right;

        [Tooltip("열림 / 닫힘. 에디터에서 바꾸면 바로 보이고, 플레이 중에는 부드럽게 움직인다")]
        public bool open = false;
        [Range(0.05f, 1f), Tooltip("다 열렸을 때 남는 폭 (닫힌 폭 대비). 0.17 = 옆에 1 m 쯤 몰려 있음")]
        public float openWidth = 0.17f;
        [Min(0f), Tooltip("여닫는 데 걸리는 시간 (초). 0 = 즉시")]
        public float duration = 1.8f;
        [Tooltip("움직임 곡선 (가로 0 → 1 = 닫힘 → 열림)")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("다 열렸을 때")]
        public UnityEvent onOpened = new UnityEvent();
        [Tooltip("다 닫혔을 때")]
        public UnityEvent onClosed = new UnityEvent();

        float amount = -1f;   // 0 = 닫힘, 1 = 열림

        /// <summary>지금 열린 정도 (0 닫힘 ~ 1 열림)</summary>
        public float Amount { get { return Mathf.Clamp01(amount); } }
        /// <summary>움직이는 중인지</summary>
        public bool IsMoving { get { return !Mathf.Approximately(amount, open ? 1f : 0f); } }

        public void Open() { open = true; }
        public void Close() { open = false; }
        public void Toggle() { open = !open; }
        public void SetOpen(bool value, bool instant = false)
        {
            open = value;
            if (instant) { amount = open ? 1f : 0f; Apply(); }
        }

        [ContextMenu("열기 / 닫기")]
        void ToggleFromMenu() { Toggle(); }

        void OnEnable() { amount = open ? 1f : 0f; Apply(); }

        void Update()
        {
            float target = open ? 1f : 0f;
            if (!Application.isPlaying || duration <= 0f)
            {
                if (amount != target) { amount = target; Apply(); }
                return;
            }
            if (Mathf.Approximately(amount, target)) return;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / duration);
            Apply();
            if (Mathf.Approximately(amount, target))
            {
                amount = target;
                if (open) onOpened.Invoke(); else onClosed.Invoke();
            }
        }

        void Apply()
        {
            float k = ease != null ? Mathf.Clamp01(ease.Evaluate(Mathf.Clamp01(amount))) : Mathf.Clamp01(amount);
            float sx = Mathf.Lerp(1f, openWidth, k);
            SetScaleX(left, sx);
            SetScaleX(right, sx);
        }

        static void SetScaleX(Transform t, float sx)
        {
            if (t == null) return;
            Vector3 s = t.localScale;
            if (Mathf.Approximately(s.x, sx)) return;
            s.x = sx;
            t.localScale = s;
        }
    }
}
