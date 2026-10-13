using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    public enum DialogueBand
    {
        Fallback = 0,
        Ambient = 200,
        Progress = 400,
        Reactive = 600,
        Story = 800,
        Critical = 1000,
    }

    public enum VariableType
    {
        Int,
        Enum,
        String,
    }

    [Serializable]
    public class DialoguePoolData
    {
        public string poolId;
        public List<DialogueData> dialogues;
    }

    [Serializable]
    public class DialogueData
    {
        public string id;
        public List<string> formerIds;
        public string band;
        public int subPriority;
        public List<ConditionData> conditions;
        public int maxPlays;
        public string perEvent;
        public string eventGroup;
        public float cooldownSec;
        public List<LineData> lines;

        [NonSerialized] public DialogueBand parsedBand;

        public int ConditionCount => conditions == null ? 0 : conditions.Count;

        public bool IsAlwaysAvailable =>
            ConditionCount == 0 && maxPlays == 0 && string.IsNullOrEmpty(perEvent) && cooldownSec <= 0f;
    }

    [Serializable]
    public class ConditionData
    {
        public string variable;
        public string op;
        public string value;

        public override string ToString() => $"{variable} {op} {value}";
    }

    [Serializable]
    public class LineData
    {
        public string speaker;
        public string textKey;
    }

    [Serializable]
    public class StringTableData
    {
        public List<StringEntry> entries;
    }

    [Serializable]
    public class StringEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class VariableTableData
    {
        public List<VariableDef> variables;
    }

    [Serializable]
    public class VariableDef
    {
        public string name;
        public string type;
        public string defaultValue;
        public List<string> allowed;

        [NonSerialized] public VariableType parsedType;
    }
}
