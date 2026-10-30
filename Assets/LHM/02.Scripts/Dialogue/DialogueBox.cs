using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private readonly List<DialogueLine> current = new List<DialogueLine>();
        private Typewriter typewriter;
        private int index;
        private bool isOpen;
        private float effectWait;
        private string pendingText;
        private int openedFrame = -1;

        public bool IsOpen => isOpen;

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
        }

        private void OnDestroy()
        {
            if (typewriter != null) typewriter.CharacterRevealed -= OnCharacterRevealed;
        }

        private void Update()
        {
            if (!isOpen) return;

            if (effectWait > 0f)
            {
                effectWait -= Time.unscaledDeltaTime;
                if (effectWait > 0f) return;

                effectWait = 0f;
                typewriter.Begin(pendingText, charactersPerSecond);
                pendingText = null;
            }

            typewriter.Tick(Time.unscaledDeltaTime);

            if (Time.frameCount == openedFrame) return;
            if (!ConfirmPressed()) return;

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
            openedFrame = Time.frameCount;
            ShowLine();
        }

        public void Close()
        {
            if (!isOpen) return;

            box.SetActive(false);
            isOpen = false;
            UiStack.Remove(this);
            effectWait = 0f;
            pendingText = null;

            if (portrait != null) portrait.Hide();
            if (stage != null) stage.Exit();
            if (director != null) director.EndDialogue();

            Debug.Log("[DialogueBox] 대화창 닫힘");
            Closed?.Invoke();
        }

        private void Next()
        {
            index++;

            if (index >= current.Count)
            {
                Close();
                return;
            }

            ShowLine();
        }

        private void ShowLine()
        {
            DialogueLine line = current[index];
            bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);

            speakerNameText.gameObject.SetActive(hasSpeaker);
            speakerNameText.text = line.speaker;

            if (voice != null) voice.SetSpeaker(line.speakerId);
            if (portrait != null) portrait.Show(line.speakerId, line.expression);

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
            Debug.Log($"[DialogueBox] {index + 1}/{current.Count} {(hasSpeaker ? line.speaker : "(내레이션)")}: {line.text}{effectLog}");
        }

        private void OnCharacterRevealed(char character)
        {
            if (voice != null) voice.PlayCharacter(character);
        }

        private static bool ConfirmPressed()
        {
            InputModeController input = InputModeController.Current;
            if (input != null) return input.WasPressed(InputMode.Dialogue, "Confirm");

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
