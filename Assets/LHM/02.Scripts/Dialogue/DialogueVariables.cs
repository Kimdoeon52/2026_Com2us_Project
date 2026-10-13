using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueVariables
    {
        private readonly IReadOnlyDictionary<string, VariableDef> definitions;
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public DialogueVariables(IReadOnlyDictionary<string, VariableDef> definitions)
        {
            this.definitions = definitions;
            Reset();
        }

        public void Reset()
        {
            values.Clear();
            foreach (KeyValuePair<string, VariableDef> pair in definitions)
            {
                values[pair.Key] = pair.Value.defaultValue ?? "";
            }
        }

        public string Get(string name)
        {
            return values.TryGetValue(name, out string value) ? value : null;
        }

        public int GetInt(string name)
        {
            return int.TryParse(Get(name), out int value) ? value : 0;
        }

        public bool Set(string name, string value)
        {
            if (!definitions.TryGetValue(name, out VariableDef def))
            {
                Debug.LogError($"[Variables] 정의되지 않은 변수 '{name}'");
                return false;
            }

            if (!DialogueConditions.AcceptsValue(def, value, out string reason))
            {
                Debug.LogError($"[Variables] {name}: {reason}");
                return false;
            }

            values[name] = value;
            return true;
        }

        public bool Set(string name, int value)
        {
            return Set(name, value.ToString());
        }

        public bool Add(string name, int delta)
        {
            return Set(name, GetInt(name) + delta);
        }

        public List<VariableValue> ToSaveList()
        {
            List<VariableValue> list = new List<VariableValue>();
            foreach (KeyValuePair<string, string> pair in values)
            {
                list.Add(new VariableValue { name = pair.Key, value = pair.Value });
            }
            return list;
        }

        public void LoadFrom(List<VariableValue> saved)
        {
            Reset();
            if (saved == null) return;

            foreach (VariableValue item in saved)
            {
                if (!definitions.ContainsKey(item.name))
                {
                    Debug.LogWarning($"[Variables] 세이브에 있는 '{item.name}'는 현재 변수 목록에 없어 건너뜁니다.");
                    continue;
                }

                Set(item.name, item.value ?? "");
            }
        }

        public string Dump()
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (builder.Length > 0) builder.Append(", ");
                builder.Append(pair.Key).Append('=').Append(pair.Value == "" ? "\"\"" : pair.Value);
            }
            return builder.ToString();
        }
    }
}
