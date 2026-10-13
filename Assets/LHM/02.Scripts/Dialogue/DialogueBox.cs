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

        [Header("타자기")]
        [SerializeField, Min(0f)] private float charactersPerSecond = 30f;

        private readonly List<DialogueLine> current = new List<DialogueLine>();
        private Typewriter typewriter;
        private int index;
        private bool isOpen;

        public bool IsOpen => isOpen;

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

            typewriter.Tick(Time.unscaledDeltaTime);

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
            if (!enabled) return;

            if (newLines == null || newLines.Count == 0)
            {
                Debug.LogWarning("[DialogueBox] 표시할 대사가 없습니다.", this);
                return;
            }

            current.Clear();
            current.AddRange(newLines);
            index = 0;

            box.SetActive(true);
            isOpen = true;
            ShowLine();
        }

        public void Close()
        {
            box.SetActive(false);
            isOpen = false;
            Debug.Log("[DialogueBox] 대화창 닫힘");
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
            typewriter.Begin(line.text, charactersPerSecond);

            Debug.Log($"[DialogueBox] {index + 1}/{current.Count} {(hasSpeaker ? line.speaker : "(내레이션)")}: {line.text}");
        }

        private void OnCharacterRevealed(char character)
        {
            if (voice != null) voice.PlayCharacter(character);
        }

        private static bool ConfirmPressed()
        {
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
