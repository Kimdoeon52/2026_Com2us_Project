using System;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [Serializable]
    public struct DialogueLine
    {
        public string speakerId;
        public string speaker;

        [TextArea(2, 4)]
        public string text;

        public DialogueLine(string speakerId, string speaker, string text)
        {
            this.speakerId = speakerId;
            this.speaker = speaker;
            this.text = text;
        }

        public DialogueLine(string speaker, string text) : this("", speaker, text)
        {
        }
    }
}
