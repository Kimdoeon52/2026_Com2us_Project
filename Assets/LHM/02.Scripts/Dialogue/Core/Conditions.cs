using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealSteel.Dialogue
{
    // =====================================================================
    //  조건 스키마
    //
    //  리프:  { "key": "event.partBroken", "op": "eq", "value": "LeftArm" }
    //  그룹:  { "all": [ ... ] }  { "any": [ ... ] }  { "not": { ... } }
    //  특수:  { "op": "seen",    "value": "junk.first_meet" }   // 그 엔트리를 본 적 있음
    //         { "op": "notSeen", "value": "junk.first_meet" }
    //
    //  한 노드에는 all / any / not / 리프 중 정확히 하나만 존재해야 한다(검증기에서 강제).
    // =====================================================================

    public enum ConditionOp { Eq, Neq, Gt, Gte, Lt, Lte, In, Seen, NotSeen }

    public enum ConditionKind { Invalid, Leaf, All, Any, Not }

    public sealed class ConditionNode
    {
        public List<ConditionNode> all;
        public List<ConditionNode> any;
        public ConditionNode not;

        public string key;
        public ConditionOp? op;
        public JToken value;

        [JsonIgnore] internal DVal Compiled;
        [JsonIgnore] internal DVal[] CompiledSet;
        [JsonIgnore] internal string CompiledEntryRef;

        [JsonIgnore]
        public ConditionKind Kind
        {
            get
            {
                int groups = (all != null ? 1 : 0) + (any != null ? 1 : 0) + (not != null ? 1 : 0);
                bool leaf = op.HasValue || key != null;
                if (groups + (leaf ? 1 : 0) != 1) return ConditionKind.Invalid;
                if (all != null) return ConditionKind.All;
                if (any != null) return ConditionKind.Any;
                if (not != null) return ConditionKind.Not;
                return ConditionKind.Leaf;
            }
        }

        /// <summary>구체성(specificity) = 리프 조건 수. 같은 우선순위면 더 구체적인 대사가 이긴다.</summary>
        public int CountLeaves()
        {
            switch (Kind)
            {
                case ConditionKind.Leaf: return 1;
                case ConditionKind.Not: return not.CountLeaves();
                case ConditionKind.All:
                case ConditionKind.Any:
                    int sum = 0;
                    foreach (var c in (all ?? any)) sum += c?.CountLeaves() ?? 0;
                    return sum;
                default: return 0;
            }
        }

        public int Depth()
        {
            switch (Kind)
            {
                case ConditionKind.Not: return 1 + not.Depth();
                case ConditionKind.All:
                case ConditionKind.Any:
                    int max = 0;
                    foreach (var c in (all ?? any)) if (c != null) max = Math.Max(max, c.Depth());
                    return 1 + max;
                default: return 1;
            }
        }
    }

    // =====================================================================
    //  변수 레지스트리 (variables.json)
    //  - 조건/효과에서 쓰는 모든 키는 여기 선언되어야 한다. 미선언 키 = 로드 에러.
    //  - scope에 따라 값의 출처와 쓰기 권한이 정해진다.
    // =====================================================================

    public enum VarScope
    {
        /// <summary>트리거가 넘겨주는 1회성 값. 키는 반드시 "event." 로 시작.</summary>
        Event,
        /// <summary>게임 시스템 소유(골드, 전적, 토너먼트 등). 대화는 읽기만 가능.</summary>
        Game,
        /// <summary>대화 시스템 소유. 효과(effects)로 쓸 수 있고 대화 세이브에 저장. "dlg." / "story." 로 시작.</summary>
        Dialogue
    }

    public sealed class VariableRegistryFile
    {
        public int schemaVersion;
        public List<VariableDef> variables = new List<VariableDef>();
    }

    public sealed class VariableDef
    {
        public string key;
        public VarType type;
        public VarScope scope;
        [JsonProperty("default")] public JToken defaultValue;
        /// <summary>String 타입일 때 허용값 목록 (오타 방지용 열거형 역할)</summary>
        public List<string> allowed;
        public string desc;

        [JsonIgnore] internal DVal CompiledDefault;
        [JsonIgnore] internal HashSet<string> AllowedSet;
    }

    public sealed class VariableRegistry
    {
        private readonly Dictionary<string, VariableDef> _defs = new Dictionary<string, VariableDef>(StringComparer.Ordinal);

        public bool TryGet(string key, out VariableDef def) => _defs.TryGetValue(key, out def);
        public IEnumerable<VariableDef> All => _defs.Values;

        internal bool Add(VariableDef def) { if (_defs.ContainsKey(def.key)) return false; _defs.Add(def.key, def); return true; }

        public static string RequiredPrefixHint(VarScope scope)
        {
            switch (scope)
            {
                case VarScope.Event: return "event.";
                case VarScope.Dialogue: return "dlg. 또는 story.";
                default: return "(event./dlg./story. 제외)";
            }
        }

        public static bool PrefixMatchesScope(string key, VarScope scope)
        {
            bool isEvent = key.StartsWith("event.", StringComparison.Ordinal);
            bool isDialogue = key.StartsWith("dlg.", StringComparison.Ordinal) || key.StartsWith("story.", StringComparison.Ordinal);
            switch (scope)
            {
                case VarScope.Event: return isEvent;
                case VarScope.Dialogue: return isDialogue;
                default: return !isEvent && !isDialogue;
            }
        }
    }

    // =====================================================================
    //  평가
    // =====================================================================

    /// <summary>키 → 값 조회. 구현체는 레이어(이벤트/대화/게임)를 scope에 맞게 라우팅한다.</summary>
    public interface IVariableSource
    {
        bool TryGet(string key, out DVal value);
    }

    public interface ISeenSource
    {
        bool HasSeen(string entryId);
    }

    public static class ConditionEvaluator
    {
        /// <summary>
        /// null 조건 = 참. 값을 못 찾으면 해당 리프는 false (fail-closed).
        /// 레지스트리 default가 있으므로 정상 데이터에서는 "못 찾음"이 발생하지 않는다.
        /// </summary>
        public static bool Evaluate(ConditionNode node, IVariableSource vars, ISeenSource seen)
        {
            if (node == null) return true;
            switch (node.Kind)
            {
                case ConditionKind.All:
                    foreach (var c in node.all) if (!Evaluate(c, vars, seen)) return false;
                    return true;
                case ConditionKind.Any:
                    foreach (var c in node.any) if (Evaluate(c, vars, seen)) return true;
                    return false;
                case ConditionKind.Not:
                    return !Evaluate(node.not, vars, seen);
                case ConditionKind.Leaf:
                    return EvaluateLeaf(node, vars, seen);
                default:
                    return false;
            }
        }

        private static bool EvaluateLeaf(ConditionNode n, IVariableSource vars, ISeenSource seen)
        {
            var op = n.op ?? ConditionOp.Eq;
            if (op == ConditionOp.Seen) return seen != null && seen.HasSeen(n.CompiledEntryRef);
            if (op == ConditionOp.NotSeen) return seen == null || !seen.HasSeen(n.CompiledEntryRef);

            if (!vars.TryGet(n.key, out var actual)) return false;

            switch (op)
            {
                case ConditionOp.Eq: return actual.Equals(n.Compiled);
                case ConditionOp.Neq: return !actual.Equals(n.Compiled);
                case ConditionOp.Gt: return actual.IsNumeric && actual.AsNumber > n.Compiled.AsNumber;
                case ConditionOp.Gte: return actual.IsNumeric && actual.AsNumber >= n.Compiled.AsNumber;
                case ConditionOp.Lt: return actual.IsNumeric && actual.AsNumber < n.Compiled.AsNumber;
                case ConditionOp.Lte: return actual.IsNumeric && actual.AsNumber <= n.Compiled.AsNumber;
                case ConditionOp.In:
                    foreach (var v in n.CompiledSet) if (actual.Equals(v)) return true;
                    return false;
            }
            return false;
        }
    }
}
