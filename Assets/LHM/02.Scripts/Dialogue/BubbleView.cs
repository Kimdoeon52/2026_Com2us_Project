using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class BubbleView : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;

        [Header("크기")]
        [SerializeField] private Vector2 padding = new Vector2(28f, 16f);
        [SerializeField, Min(50f)] private float maxWidth = 420f;

        private RectTransform rect;
        private Camera worldCamera;
        private Vector3 offset;
        private float holdSeconds;
        private float fadeSeconds;
        private float timer;

        public Transform Target { get; private set; }
        public string Channel { get; private set; }
        public bool IsShowing { get; private set; }

        private void Awake()
        {
            rect = (RectTransform)transform;
            if (group == null) group = GetComponent<CanvasGroup>();
        }

        public bool IsValid => group != null && text != null;

        public void Show(Transform target, Vector3 worldOffset, string body, string channel, float hold, float fade, Camera cam)
        {
            if (rect == null) rect = (RectTransform)transform;

            Target = target;
            Channel = channel;
            offset = worldOffset;
            holdSeconds = hold;
            fadeSeconds = fade;
            worldCamera = cam;
            timer = 0f;
            IsShowing = true;
            gameObject.SetActive(true);
            group.alpha = 0f;

            text.text = body;
            float inner = maxWidth - padding.x;
            Vector2 size = text.GetPreferredValues(body);
            if (size.x > inner) size = new Vector2(inner, text.GetPreferredValues(body, inner, 0f).y);
            rect.sizeDelta = new Vector2(size.x + padding.x, size.y + padding.y);
            Tick(0f, false);
        }

        public void Hide()
        {
            IsShowing = false;
            Target = null;
            Channel = null;
            gameObject.SetActive(false);
        }

        public void Tick(float deltaTime, bool suppressed)
        {
            if (!IsShowing) return;

            if (Target == null || worldCamera == null)
            {
                Hide();
                return;
            }

            timer += deltaTime;
            float fade = timer <= holdSeconds ? 0f : fadeSeconds > 0f ? (timer - holdSeconds) / fadeSeconds : 1f;
            if (fade >= 1f)
            {
                Hide();
                return;
            }

            Vector3 screen = worldCamera.WorldToScreenPoint(Target.position + offset);
            bool visible = !suppressed && screen.z >= 0f;
            group.alpha = visible ? 1f - fade : 0f;
            rect.position = new Vector3(screen.x, screen.y, 0f);
        }
    }
}
