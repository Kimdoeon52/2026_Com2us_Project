using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    public static class DialogueConditions
    {
        private static readonly string[] CompareOps = { ">", ">=", "<", "<=" };

        public static bool TryParseType(string text, out VariableType type)
        {
            switch (text)
            {
                case "int": type = VariableType.Int; return true;
                case "enum": type = VariableType.Enum; return true;
                case "string": type = VariableType.String; return true;
                default: type = VariableType.String; return false;
            }
        }

        public static bool IsOpAllowed(VariableDef def, string op)
        {
            if (op == "==" || op == "!=") return true;
            return def.parsedType == VariableType.Int && Array.IndexOf(CompareOps, op) >= 0;
        }

        public static bool AcceptsValue(VariableDef def, string value, out string reason)
        {
            reason = null;

            switch (def.parsedType)
            {
                case VariableType.Int:
                    if (int.TryParse(value, out _)) return true;
                    reason = $"'{value}'는 정수가 아닙니다";
                    return false;

                case VariableType.Enum:
                    if (def.allowed != null && def.allowed.Contains(value)) return true;
                    reason = $"'{value}'는 허용값({string.Join(", ", def.allowed ?? new List<string>())})이 아닙니다";
                    return false;

                default:
                    return true;
            }
        }

        public static bool Evaluate(ConditionData condition, DialogueVariables variables)
        {
            string actual = variables.Get(condition.variable) ?? "";

            switch (condition.op)
            {
                case "==": return actual == condition.value;
                case "!=": return actual != condition.value;
            }

            if (!int.TryParse(actual, out int left) || !int.TryParse(condition.value, out int right)) return false;

            switch (condition.op)
            {
                case ">": return left > right;
                case ">=": return left >= right;
                case "<": return left < right;
                case "<=": return left <= right;
                default: return false;
            }
        }
    }
}
