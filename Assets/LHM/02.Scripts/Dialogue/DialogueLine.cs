using System;
using System.Collections.Generic;
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

        public string expression;
        public List<EffectData> effects;

        public DialogueLine(string speakerId, string speaker, string text)
        {
            this.speakerId = speakerId;
            this.speaker = speaker;
            this.text = text;
            expression = "";
            effects = null;
        }

        public DialogueLine(string speaker, string text) : this("", speaker, text)
        {
        }
    }
}
