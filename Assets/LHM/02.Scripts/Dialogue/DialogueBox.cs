using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueBox : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameObject box;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private DialogueVoice voice;
        [SerializeField] private DialoguePortrait portrait;
        [SerializeField] private DialogueDirector director;
        [SerializeField] private DialogueStage stage;

        [Header("타자기")]
        [SerializeField, Min(0f)] private float charactersPerSecond = 30f;

        [Header("스킵 · 오토 · 빨리감기")]
        [SerializeField] private GameObject skipConfirm;
        [SerializeField] private TMP_Text statusText;
        [SerializeField, Min(0f)] private float openInputDelay = 0.25f;
        [SerializeField, Min(0f)] private float lineInputDelay = 0.12f;
        [SerializeField, Min(0.01f)] private float fastForwardInterval = 0.06f;
        [SerializeField, Min(0f)] private float autoBaseSeconds = 1f;
        [SerializeField, Min(0f)] private float autoSecondsPerChar = 0.05f;

        [Header("백로그")]
        [SerializeField] private DialogueBacklog backlog;

        private readonly List<DialogueLine> current = new List<DialogueLine>();
        private Typewriter typewriter;
        private DialogueHistory readLog;
        private int index;
        private bool isOpen;
        private float effectWait;
        private string pendingText;
        private float inputReadyAt;
        private float lineTimer;
        private bool autoMode;
        private bool confirmingSkip;

        public bool IsOpen => isOpen;
        public bool IsAutoMode => autoMode;

        public event Action Closed;

        private void Awake()
        {
            if (box == null || speakerNameText == null || bodyText == null)
            {
                Debug.LogError("[DialogueBox] Box, Speaker Name Text, Body Text 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            typewriter = new Typewriter(bodyText);
            typewriter.CharacterRevealed += OnCharacterRevealed;

            box.SetActive(false);
            HideSkipConfirm();
            RefreshStatus(false);
        }

        private void OnDestroy()
        {
            if (typewriter != null) typewriter.CharacterRevealed -= OnCharacterRevealed;
        }

        public void SetReadLog(DialogueHistory history)
        {
            readLog = history;
        }

        private void Update()
        {
            if (!isOpen) return;

            float deltaTime = Time.unscaledDeltaTime;

            if (backlog != null && backlog.IsOpen)
            {
                HandleBacklog(deltaTime);
                return;
            }

            if (DialogueInput.AutoPressed()) SetAuto(!autoMode);

            if (confirmingSkip)
            {
                HandleSkipConfirm();
                return;
            }

            bool inputReady = Time.unscaledTime >= inputReadyAt;
            if (inputReady && backlog != null && DialogueInput.BacklogPressed())
            {
                if (backlog.Open()) RefreshStatus(false);
                return;
            }

            if (inputReady && DialogueInput.SkipPressed())
            {
                RequestSkip();
                return;
            }

            bool fastForward = DialogueInput.FastForwardHeld() && IsLineRead(index);
            RefreshStatus(fastForward);

            if (effectWait > 0f)
            {
                effectWait = fastForward ? 0f : effectWait - deltaTime;
                if (effectWait > 0f) return;

                effectWait = 0f;
                typewriter.Begin(pendingText, charactersPerSecond);
                pendingText = null;
            }

            if (fastForward)
            {
                typewriter.Complete();
                lineTimer += deltaTime;
                if (lineTimer >= fastForwardInterval) Next();
                return;
            }

            typewriter.Tick(deltaTime);

            if (autoMode && !typewriter.IsTyping)
            {
                lineTimer += deltaTime;
                if (lineTimer >= AutoDelay())
                {
                    Next();
                    return;
                }
            }

            if (!inputReady || !DialogueInput.ConfirmPressed()) return;

            if (typewriter.IsTyping)
            {
                typewriter.Complete();
            }
            else
            {
                Next();
            }
        }

        public void Open(IReadOnlyList<DialogueLine> newLines)
        {
            Open(newLines, DialogueDisplay.Bottom, null);
        }

        public void Open(IReadOnlyList<DialogueLine> newLines, DialogueDisplay display, string background)
        {
            if (!enabled) return;

            if (newLines == null || newLines.Count == 0)
            {
                Debug.LogWarning("[DialogueBox] 표시할 대사가 없습니다.", this);
                return;
            }

            current.Clear();
            current.AddRange(newLines);
            index = 0;

            if (director != null) director.BeginDialogue();
            if (stage != null) stage.Enter(display, background);

            box.SetActive(true);
            isOpen = true;
            UiStack.Push(this);
            inputReadyAt = Time.unscaledTime + openInputDelay;
            HideSkipConfirm();
            ShowLine();
            RefreshStatus(false);
        }

        public void Close()
        {
            if (!isOpen) return;

            box.SetActive(false);
            isOpen = false;
            UiStack.Remove(this);
            effectWait = 0f;
            pendingText = null;
            HideSkipConfirm();
            RefreshStatus(false);
            if (backlog != null) backlog.Close();

            if (portrait != null) portrait.Hide();
            if (stage != null) stage.Exit();
            if (director != null) director.EndDialogue();

            Debug.Log("[DialogueBox] 대화창 닫힘");
            Closed?.Invoke();
        }

        private void Next()
        {
            if (readLog != null) readLog.MarkRead(current[index].textKey);

            index++;

            if (index >= current.Count)
            {
                Close();
                return;
            }

            ShowLine();
            inputReadyAt = Time.unscaledTime + lineInputDelay;
        }

        private void ShowLine()
        {
            DialogueLine line = current[index];
            bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
            lineTimer = 0f;

            speakerNameText.gameObject.SetActive(hasSpeaker);
            speakerNameText.text = line.speaker;

            if (voice != null) voice.SetSpeaker(line.speakerId);
            if (portrait != null) portrait.Show(line.speakerId, line.expression);
            if (backlog != null) backlog.Add(line.speaker, line.text);

            float wait = director != null ? director.Play(line.effects) : 0f;

            if (wait > 0f)
            {
                effectWait = wait;
                pendingText = line.text;
                typewriter.Begin("", 0f);
            }
            else
            {
                effectWait = 0f;
                pendingText = null;
                typewriter.Begin(line.text, charactersPerSecond);
            }

            string effectLog = line.effects != null && line.effects.Count > 0 ? $" [연출: {string.Join(", ", line.effects)}]" : "";
            string readMark = IsLineRead(index) ? " (읽음)" : "";
            Debug.Log($"[DialogueBox] {index + 1}/{current.Count}{readMark} {(hasSpeaker ? line.speaker : "(내레이션)")}: {line.text}{effectLog}");
        }

        private void RequestSkip()
        {
            int unread = CountUnread();
            if (unread == 0)
            {
                Skip();
                return;
            }

            confirmingSkip = true;
            if (skipConfirm != null) skipConfirm.SetActive(true);
            Debug.Log($"[DialogueBox] 안 읽은 대사 {unread}줄 — Esc 한 번 더: 스킵 / 확인 키: 계속 읽기");
        }

        private void HandleSkipConfirm()
        {
            if (DialogueInput.SkipPressed())
            {
                HideSkipConfirm();
                Skip();
            }
            else if (DialogueInput.ConfirmPressed())
            {
                HideSkipConfirm();
                inputReadyAt = Time.unscaledTime + lineInputDelay;
                Debug.Log("[DialogueBox] 스킵 취소");
            }
        }

        private void HandleBacklog(float deltaTime)
        {
            if (DialogueInput.BacklogPressed() || DialogueInput.SkipPressed())
            {
                backlog.Close();
                inputReadyAt = Time.unscaledTime + lineInputDelay;
                return;
            }

            backlog.HandleScroll(deltaTime);
        }

        private void Skip()
        {
            Debug.Log($"[DialogueBox] 스킵 — {current.Count - index}줄 건너뜀");
            Close();
        }

        private void HideSkipConfirm()
        {
            confirmingSkip = false;
            if (skipConfirm != null) skipConfirm.SetActive(false);
        }

        private void SetAuto(bool on)
        {
            autoMode = on;
            lineTimer = 0f;
            Debug.Log($"[DialogueBox] 오토 {(on ? "켜짐" : "꺼짐")}");
        }

        private float AutoDelay()
        {
            string text = current[index].text;
            return autoBaseSeconds + (text != null ? text.Length : 0) * autoSecondsPerChar;
        }

        private bool IsLineRead(int lineIndex)
        {
            return readLog != null && lineIndex >= 0 && lineIndex < current.Count && readLog.IsRead(current[lineIndex].textKey);
        }

        private int CountUnread()
        {
            int count = 0;
            for (int i = index; i < current.Count; i++)
            {
                if (!IsLineRead(i)) count++;
            }
            return count;
        }

        private void RefreshStatus(bool fastForward)
        {
            if (statusText == null) return;

            string label = !isOpen ? "" : fastForward ? "빨리감기 >>" : autoMode ? "AUTO" : "";
            if (statusText.gameObject.activeSelf != (label != "")) statusText.gameObject.SetActive(label != "");
            if (statusText.text != label) statusText.text = label;
        }

        private void OnCharacterRevealed(char character)
        {
            if (voice != null) voice.PlayCharacter(character);
        }
    }
}
