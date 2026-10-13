using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class SaveFile
    {
        public int version;
        public string savedAt;
        public DialogueSaveData dialogue;
        public List<VariableValue> variables;
    }

    [Serializable]
    public class DialogueSaveData
    {
        public int playOrder;
        public List<PlayRecord> records;
        public List<EventMark> eventMarks;
    }

    [Serializable]
    public class EventMark
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class VariableValue
    {
        public string name;
        public string value;
    }
}
