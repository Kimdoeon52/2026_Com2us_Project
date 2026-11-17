using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    // 대화창 화면 버튼(LOG · AUTO · SKIP)과 "다음" 표시(▼)
    // 클릭은 DialogueBox가 매 프레임 PollClick()으로 직접 물어봄
    // → 버튼을 누른 클릭이 '다음 줄' 입력으로 같이 처리되지 않고, EventSystem 실행 순서에도 영향받지 않음
    public class DialogueBoxControls : MonoBehaviour
    {
        public enum Click
        {
            None,
            Log,
            Auto,
            Skip,
        }

        [Header("연결")]
        [SerializeField] private DialogueBox box;
        [SerializeField] private Image logButton;
        [SerializeField] private Image autoButton;
        [SerializeField] private Image skipButton;
        [SerializeField] private GameObject nextHint;
        [SerializeField] private RectTransform nextArrow;

        [Header("색")]
        [SerializeField] private Color normalColor = new Color(0.17f, 0.18f, 0.2f, 0.9f);
        [SerializeField] private Color hoverColor = new Color(0.28f, 0.29f, 0.32f, 0.95f);
        [SerializeField] private Color activeColor = new Color(1f, 0.898f, 0f, 1f);
        [SerializeField] private Color labelColor = new Color(0.85f, 0.855f, 0.86f, 1f);
        [SerializeField] private Color activeLabelColor = new Color(0.067f, 0.07f, 0.078f, 1f);

        [Header("▼ 움직임")]
        [SerializeField, Min(0f)] private float arrowBob = 4f;
        [SerializeField, Min(0f)] private float arrowSpeed = 4f;

        private Canvas canvas;
        private Vector2 arrowBase;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            if (nextArrow != null) arrowBase = nextArrow.anchoredPosition;
        }

        private void OnEnable()
        {
            if (nextHint != null) nextHint.SetActive(false);
        }

        public Click PollClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return Click.None;

            Vector2 point = mouse.position.ReadValue();
            if (IsOver(logButton, point)) return Click.Log;
            if (IsOver(autoButton, point)) return Click.Auto;
            if (IsOver(skipButton, point)) return Click.Skip;
            return Click.None;
        }

        private void Update()
        {
            Vector2 point = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            Paint(logButton, false, point);
            Paint(autoButton, box != null && box.IsAutoMode, point);
            Paint(skipButton, false, point);

            bool waiting = box != null && box.IsWaitingForInput;
            if (nextHint != null && nextHint.activeSelf != waiting) nextHint.SetActive(waiting);

            if (waiting && nextArrow != null)
            {
                float offset = Mathf.Abs(Mathf.Sin(Time.unscaledTime * arrowSpeed)) * arrowBob;
                nextArrow.anchoredPosition = arrowBase - new Vector2(0f, offset);
            }
        }

        private void Paint(Image button, bool active, Vector2 point)
        {
            if (button == null) return;

            Color fill = active ? activeColor : IsOver(button, point) ? hoverColor : normalColor;
            if (button.color != fill) button.color = fill;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            Color text = active ? activeLabelColor : labelColor;
            if (label != null && label.color != text) label.color = text;
        }

        private bool IsOver(Image button, Vector2 point)
        {
            if (button == null || !button.isActiveAndEnabled) return false;

            Camera cam = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, point, cam);
        }
    }
}
