using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class BattleBarkSubtitle : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;

        [Header("표시")]
        [SerializeField, Min(0f)] private float holdSeconds = 2f;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;
        [SerializeField] private Color speakerColor = new Color(1f, 0.82f, 0.5f);

        private readonly Queue<DialogueLine> sequence = new Queue<DialogueLine>();
        private float timer;
        private bool showing;

        public bool IsShowing => showing;
        public bool IsSequencePlaying => sequence.Count > 0 || (showing && playingSequence);

        private bool playingSequence;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null || text == null)
            {
                Debug.LogError("[BarkSubtitle] Group, Text 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        public void Show(string speaker, string body)
        {
            if (!enabled) return;

            sequence.Clear();
            playingSequence = false;
            Display(speaker, body);
        }

        public void ShowSequence(IEnumerable<DialogueLine> lines)
        {
            if (!enabled) return;

            sequence.Clear();
            foreach (DialogueLine line in lines) sequence.Enqueue(line);
            ShowNext();
        }

        public void Hide()
        {
            sequence.Clear();
            playingSequence = false;
            showing = false;
            if (group != null) group.alpha = 0f;
        }

        private void ShowNext()
        {
            if (sequence.Count == 0)
            {
                playingSequence = false;
                return;
            }

            DialogueLine line = sequence.Dequeue();
            playingSequence = true;
            Display(line.speaker, line.text);
        }

        private void Display(string speaker, string body)
        {
            string color = ColorUtility.ToHtmlStringRGB(speakerColor);
            text.text = string.IsNullOrEmpty(speaker) ? body : $"<color=#{color}>{speaker}</color>   {body}";
            group.alpha = 1f;
            timer = 0f;
            showing = true;
        }

        private void Update()
        {
            if (!showing) return;

            timer += Time.unscaledDeltaTime;
            if (timer <= holdSeconds) return;

            if (sequence.Count > 0)
            {
                ShowNext();
                return;
            }

            float fade = fadeSeconds > 0f ? (timer - holdSeconds) / fadeSeconds : 1f;
            group.alpha = Mathf.Clamp01(1f - fade);
            if (fade >= 1f)
            {
                showing = false;
                playingSequence = false;
            }
        }
    }
}
