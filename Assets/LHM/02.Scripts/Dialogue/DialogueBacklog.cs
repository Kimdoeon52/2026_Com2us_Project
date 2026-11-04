using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class DialogueBacklog : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private TMP_Text titleText;

        [Header("설정")]
        [SerializeField, Min(1)] private int maxLines = 200;
        [SerializeField] private Color speakerColor = new Color(1f, 0.82f, 0.5f);
        [SerializeField, Min(1f)] private float keyScrollSpeed = 900f;
        [SerializeField, Min(1f)] private float wheelScrollStep = 80f;

        private readonly Queue<Entry> entries = new Queue<Entry>();
        private float contentHeight;
        private float scroll;

        public bool IsOpen { get; private set; }
        public int Count => entries.Count;
        public int MaxLines => maxLines;

        private struct Entry
        {
            public string speaker;
            public string text;
        }

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        public void Add(string speaker, string text)
        {
            entries.Enqueue(new Entry { speaker = speaker, text = text ?? "" });
            while (entries.Count > maxLines) entries.Dequeue();
        }

        public void Clear()
        {
            entries.Clear();
        }

        public bool Open()
        {
            if (IsOpen) return true;

            if (panel == null || viewport == null || contentText == null)
            {
                Debug.LogWarning("[Backlog] Panel, Viewport, Content Text 중 연결되지 않은 칸이 있습니다. 대화 툴의 13단계 항목을 실행하세요.", this);
                return false;
            }

            panel.SetActive(true);
            IsOpen = true;
            Rebuild();
            ScrollTo(MaxScroll);
            Debug.Log($"[Backlog] 열림 ({entries.Count}/{maxLines}줄)");
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            if (panel != null) panel.SetActive(false);
            Debug.Log("[Backlog] 닫힘");
        }

        public void HandleScroll(float deltaTime)
        {
            if (!IsOpen) return;

            float delta = 0f;
            float page = viewport.rect.height * 0.9f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed) delta -= keyScrollSpeed * deltaTime;
                if (keyboard.downArrowKey.isPressed) delta += keyScrollSpeed * deltaTime;
                if (keyboard.pageUpKey.wasPressedThisFrame) delta -= page;
                if (keyboard.pageDownKey.wasPressedThisFrame) delta += page;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f) delta -= Mathf.Sign(wheel) * wheelScrollStep;
            }

            if (delta != 0f) ScrollTo(scroll + delta);
        }

        private float MaxScroll => Mathf.Max(0f, contentHeight - viewport.rect.height);

        private void Rebuild()
        {
            string color = ColorUtility.ToHtmlStringRGB(speakerColor);
            var builder = new StringBuilder();

            foreach (Entry entry in entries)
            {
                if (builder.Length > 0) builder.Append("\n\n");
                if (!string.IsNullOrEmpty(entry.speaker)) builder.Append("<color=#").Append(color).Append('>').Append(entry.speaker).Append("</color>\n");
                builder.Append(entry.text);
            }

            if (builder.Length == 0) builder.Append("(아직 지나간 대사가 없습니다)");

            Canvas.ForceUpdateCanvases();
            contentText.text = builder.ToString();
            contentHeight = contentText.GetPreferredValues(contentText.text, viewport.rect.width, 0f).y;

            RectTransform rect = contentText.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, contentHeight);

            if (titleText != null) titleText.text = $"백로그  {entries.Count}/{maxLines}줄     방향키, 휠, PgUp/PgDn: 스크롤     L, Esc: 닫기";
        }

        private void ScrollTo(float value)
        {
            scroll = Mathf.Clamp(value, 0f, MaxScroll);
            RectTransform rect = contentText.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, scroll);
        }
    }
}
