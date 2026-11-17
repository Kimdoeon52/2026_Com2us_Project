using System;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class Typewriter
    {
        private readonly TMP_Text target;
        private float charactersPerSecond;
        private float progress;
        private int totalCharacters;

        public event Action<char> CharacterRevealed;

        public bool IsTyping => target.maxVisibleCharacters < totalCharacters;

        public Typewriter(TMP_Text target)
        {
            this.target = target;
        }

        public void Begin(string content, float speed)
        {
            target.text = content;
            target.maxVisibleCharacters = 0;
            target.ForceMeshUpdate();

            totalCharacters = target.textInfo.characterCount;
            charactersPerSecond = speed;
            progress = 0f;

            if (charactersPerSecond <= 0f) Complete();
        }

        public void Tick(float deltaTime)
        {
            if (!IsTyping) return;

            progress += deltaTime * charactersPerSecond;
            int reveal = Mathf.Min(totalCharacters, Mathf.FloorToInt(progress));

            while (target.maxVisibleCharacters < reveal)
            {
                char character = target.textInfo.characterInfo[target.maxVisibleCharacters].character;
                target.maxVisibleCharacters++;
                CharacterRevealed?.Invoke(character);
            }
        }

        public void Complete()
        {
            target.maxVisibleCharacters = totalCharacters;
            progress = totalCharacters;
        }
    }
}
