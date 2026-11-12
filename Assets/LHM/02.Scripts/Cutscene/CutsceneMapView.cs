using TMPro;
using UnityEngine;

namespace RealSteel.Cutscene
{
    public class CutsceneMapView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform map;
        [SerializeField] private RectTransform pin;
        [SerializeField] private CanvasGroup pinGroup;
        [SerializeField] private TMP_Text pinLabel;

        [Header("설정")]
        [SerializeField, Min(1f)] private float maxZoom = 4f;
        [SerializeField, Min(0.05f)] private float pinFadeSeconds = 0.4f;

        private Vector2 fromPosition;
        private Vector2 toPosition;
        private float fromZoom = 1f;
        private float toZoom = 1f;
        private float zoom = 1f;
        private float moveStartedAt;
        private float moveSeconds;
        private float pinTarget;

        public bool IsReady => viewport != null && map != null;

        private void Awake()
        {
            if (!IsReady)
            {
                Debug.LogError("[CutsceneMap] Viewport 또는 Map이 연결되지 않았습니다. 지도 이동 없이 재생합니다.", this);
            }
        }

        private void Update()
        {
            if (!IsReady) return;

            float progress = moveSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - moveStartedAt) / moveSeconds) : 1f;
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            zoom = Mathf.Lerp(fromZoom, toZoom, eased);
            map.localScale = new Vector3(zoom, zoom, 1f);
            map.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, eased);

            if (pin != null) pin.localScale = new Vector3(1f / zoom, 1f / zoom, 1f);
            if (pinGroup != null)
            {
                pinGroup.alpha = Mathf.MoveTowards(pinGroup.alpha, pinTarget, Time.unscaledDeltaTime / pinFadeSeconds);
            }
        }

        public void ResetView()
        {
            fromZoom = toZoom = zoom = 1f;
            fromPosition = toPosition = Vector2.zero;
            moveSeconds = 0f;
            pinTarget = 0f;
            if (pinGroup != null) pinGroup.alpha = 0f;

            if (!IsReady) return;
            map.localScale = Vector3.one;
            map.anchoredPosition = Vector2.zero;
        }

        public void FocusOn(float x, float y, float targetZoom, float seconds)
        {
            if (!IsReady) return;

            fromPosition = map.anchoredPosition;
            fromZoom = zoom;
            toZoom = Mathf.Clamp(targetZoom, 1f, maxZoom);
            toPosition = ClampToViewport(-MapPoint(x, y) * toZoom, toZoom);
            moveStartedAt = Time.unscaledTime;
            moveSeconds = Mathf.Max(0.01f, seconds);
        }

        public void ShowPin(string label, float x, float y)
        {
            if (string.IsNullOrEmpty(label) || pin == null || pinGroup == null || !IsReady)
            {
                pinTarget = 0f;
                return;
            }

            pin.anchoredPosition = MapPoint(x, y);
            if (pinLabel != null) pinLabel.text = label;
            pinGroup.alpha = 0f;
            pinTarget = 1f;
        }

        private Vector2 MapPoint(float x, float y)
        {
            Vector2 size = map.rect.size;
            return new Vector2((Mathf.Clamp01(x) - 0.5f) * size.x, (0.5f - Mathf.Clamp01(y)) * size.y);
        }

        private Vector2 ClampToViewport(Vector2 position, float scale)
        {
            Vector2 limit = Vector2.Max(Vector2.zero, (map.rect.size * scale - viewport.rect.size) * 0.5f);
            return new Vector2(Mathf.Clamp(position.x, -limit.x, limit.x), Mathf.Clamp(position.y, -limit.y, limit.y));
        }
    }
}
