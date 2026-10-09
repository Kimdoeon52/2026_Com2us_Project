using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace RealSteel.Dialogue
{
    /// <summary>로더 입력. Unity 쪽에서 TextAsset 등으로 읽어 문자열로 넘긴다(Core는 파일 IO를 모른다).</summary>
    public sealed class DialogueSource
    {
        public string Name;
        public string Json;
        public DialogueSource(string name, string json) { Name = name; Json = json; }
    }

    public sealed class DialogueSourceSet
    {
        public DialogueSource Variables;
        public DialogueSource Speakers;
        public List<DialogueSource> DialogueFiles = new List<DialogueSource>();
    }

    public static class DialogueJson
    {
        public const int SupportedSchemaVersion = 1;
        public const int MaxConditionDepth = 8;

        /// <summary>
        /// 데이터 로드용 엄격한 설정.
        /// - MissingMemberHandling.Error : "prioirty" 같은 오타 키를 조용히 무시하지 않는다.
        /// - MaxDepth : 비정상적으로 깊은 JSON으로 인한 스택 문제 방지.
        /// </summary>
        public static readonly JsonSerializerSettings Strict = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
            Converters = { new StringEnumConverter() }
        };

        /// <summary>세이브용 설정. 구버전/신버전 필드를 관대하게 처리한다.</summary>
        public static readonly JsonSerializerSettings Lenient = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 64,
            Converters = { new StringEnumConverter() }
        };

        public static bool TryParse<T>(DialogueSource src, ValidationReport report, out T result) where T : class
        {
            result = null;
            if (src == null || string.IsNullOrWhiteSpace(src.Json))
            {
                report.Error(src?.Name, null, "파일이 비어 있습니다.");
                return false;
            }
            try
            {
                result = JsonConvert.DeserializeObject<T>(src.Json, Strict);
                if (result == null) { report.Error(src.Name, null, "JSON 루트가 null입니다."); return false; }
                return true;
            }
            catch (JsonException e)
            {
                report.Error(src.Name, null, "JSON 파싱 실패: " + e.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// 모든 대화 데이터를 메모리에 올린 읽기 전용 DB.
    /// 로드 = 파싱 → 병합(중복 ID 차단) → 컴파일(조건/효과 타입 확정) → 의미 검증 순서.
    /// 파일 하나가 깨져도 나머지는 로드된다(부분 실패 허용).
    /// </summary>
    public sealed class DialogueDatabase
    {
        private static readonly Regex IdPattern = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$", RegexOptions.Compiled);

        private readonly Dictionary<string, DialoguePool> _pools = new Dictionary<string, DialoguePool>(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueEntry> _entries = new Dictionary<string, DialogueEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpeakerDef> _speakers = new Dictionary<string, SpeakerDef>(StringComparer.Ordinal);

        public VariableRegistry Variables { get; } = new VariableRegistry();
        public IReadOnlyDictionary<string, DialoguePool> Pools => _pools;
        public IReadOnlyDictionary<string, DialogueEntry> Entries => _entries;
        public IReadOnlyDictionary<string, SpeakerDef> Speakers => _speakers;

        public bool TryGetPool(string poolId, out DialoguePool pool) => _pools.TryGetValue(poolId, out pool);
        public bool TryGetEntry(string id, out DialogueEntry entry) => _entries.TryGetValue(id, out entry);

        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && IdPattern.IsMatch(id);

        public static DialogueDatabase Load(DialogueSourceSet set, ValidationReport report)
        {
            var db = new DialogueDatabase();
            db.LoadVariables(set.Variables, report);
            db.LoadSpeakers(set.Speakers, report);
            foreach (var f in set.DialogueFiles) db.LoadDialogueFile(f, report);
            db.CompileAll(report);
            DialogueValidator.Validate(db, report);
            return db;
        }

        // ---------------------------------------------------------------- 변수
        private void LoadVariables(DialogueSource src, ValidationReport report)
        {
            if (!DialogueJson.TryParse(src, report, out VariableRegistryFile file)) return;
            CheckSchema(src.Name, file.schemaVersion, report);

            foreach (var def in file.variables)
            {
                if (string.IsNullOrEmpty(def.key)) { report.Error(src.Name, null, "key가 비어 있는 변수 정의"); continue; }
                if (!VariableRegistry.PrefixMatchesScope(def.key, def.scope))
                {
                    report.Error(src.Name, def.key, $"scope {def.scope} 변수의 키는 {VariableRegistry.RequiredPrefixHint(def.scope)} 접두사여야 합니다.");
                    continue;
                }
                if (def.defaultValue == null || !DVal.TryFromJToken(def.defaultValue, def.type, out def.CompiledDefault))
                {
                    report.Error(src.Name, def.key, $"default 값이 없거나 타입({def.type})과 맞지 않습니다.");
                    continue;
                }
                if (def.allowed != null)
                {
                    if (def.type != VarType.String) { report.Error(src.Name, def.key, "allowed는 String 타입에만 쓸 수 있습니다."); continue; }
                    def.AllowedSet = new HashSet<string>(def.allowed, StringComparer.Ordinal);
                    if (!def.AllowedSet.Contains(def.CompiledDefault.AsString))
                        report.Error(src.Name, def.key, $"default '{def.CompiledDefault}'가 allowed 목록에 없습니다.");
                }
                if (!Variables.Add(def)) report.Error(src.Name, def.key, "변수 키 중복");
            }
        }

        // ---------------------------------------------------------------- 화자
        private void LoadSpeakers(DialogueSource src, ValidationReport report)
        {
            if (!DialogueJson.TryParse(src, report, out SpeakerFile file)) return;
            CheckSchema(src.Name, file.schemaVersion, report);
            foreach (var s in file.speakers)
            {
                if (string.IsNullOrEmpty(s.id)) { report.Error(src.Name, null, "id가 비어 있는 화자"); continue; }
                if (_speakers.ContainsKey(s.id)) { report.Error(src.Name, s.id, "화자 ID 중복"); continue; }
                _speakers.Add(s.id, s);
            }
        }

        // ---------------------------------------------------------------- 대화
        private void LoadDialogueFile(DialogueSource src, ValidationReport report)
        {
            if (!DialogueJson.TryParse(src, report, out DialogueFile file)) return;
            CheckSchema(src.Name, file.schemaVersion, report);

            foreach (var pool in file.pools)
            {
                if (!IsValidId(pool.poolId)) { report.Error(src.Name, pool.poolId, "poolId 형식 오류 (소문자/숫자/_/. 만 허용)"); continue; }
                if (_pools.ContainsKey(pool.poolId))
                {
                    report.Error(src.Name, pool.poolId, $"poolId 중복 (먼저 로드된 파일: {_pools[pool.poolId].SourceFile}) → 이 풀은 무시됩니다.");
                    continue;
                }
                pool.SourceFile = src.Name;

                var accepted = new List<DialogueEntry>();
                foreach (var e in pool.entries)
                {
                    if (!IsValidId(e.id)) { report.Error(src.Name, e.id, "엔트리 id 형식 오류 (소문자/숫자/_/. 만 허용)"); continue; }
                    if (_entries.TryGetValue(e.id, out var dup))
                    {
                        report.Error(src.Name, e.id, $"엔트리 id 중복 (기존: {dup.Pool.poolId}) → 이 엔트리는 무시됩니다.");
                        continue;
                    }
                    e.Pool = pool;
                    _entries.Add(e.id, e);
                    accepted.Add(e);
                }
                pool.entries = accepted;
                _pools.Add(pool.poolId, pool);
            }
        }

        private static void CheckSchema(string file, int version, ValidationReport report)
        {
            if (version != DialogueJson.SupportedSchemaVersion)
                report.Error(file, null, $"schemaVersion {version} 미지원 (지원: {DialogueJson.SupportedSchemaVersion})");
        }

        // ---------------------------------------------------------------- 컴파일
        private void CompileAll(ValidationReport report)
        {
            foreach (var pool in _pools.Values)
            {
                foreach (var e in pool.entries)
                {
                    string file = pool.SourceFile;

                    if (e.conditions != null)
                    {
                        if (e.conditions.Depth() > DialogueJson.MaxConditionDepth)
                            report.Error(file, e.id, $"조건 중첩이 너무 깊습니다 (최대 {DialogueJson.MaxConditionDepth}).");
                        CompileCondition(e.conditions, file, e.id, report);
                    }
                    e.Specificity = e.conditions?.CountLeaves() ?? 0;

                    foreach (var fx in e.effects) CompileEffect(fx, file, e.id, report);

                    if (e.oncePer != null)
                    {
                        if (!Variables.TryGet(e.oncePer, out var ep)) report.Error(file, e.id, $"oncePer 키 '{e.oncePer}'가 variables.json에 없습니다.");
                        else if (ep.type != VarType.Int) report.Error(file, e.id, $"oncePer 키 '{e.oncePer}'는 Int 타입이어야 합니다.");
                    }

                    ResolveLineIds(e.lines, e.id, "");
                    ResolveLineIds(e.shortLines, e.id, "s");
                }
            }
        }

        private static void ResolveLineIds(List<DialogueLine> lines, string entryId, string tag)
        {
            for (int i = 0; i < lines.Count; i++)
                lines[i].ResolvedLineId = string.IsNullOrEmpty(lines[i].lineId) ? $"{entryId}#{tag}{i}" : lines[i].lineId;
        }

        private void CompileCondition(ConditionNode n, string file, string ctx, ValidationReport report)
        {
            if (n == null) { report.Error(file, ctx, "조건 목록에 null이 있습니다."); return; }

            switch (n.Kind)
            {
                case ConditionKind.Invalid:
                    report.Error(file, ctx, "조건 노드에는 all / any / not / (key,op,value) 중 하나만 있어야 합니다.");
                    return;
                case ConditionKind.All:
                case ConditionKind.Any:
                    var list = n.all ?? n.any;
                    if (list.Count == 0) report.Error(file, ctx, "빈 all/any 그룹");
                    foreach (var c in list) CompileCondition(c, file, ctx, report);
                    return;
                case ConditionKind.Not:
                    CompileCondition(n.not, file, ctx, report);
                    return;
            }

            // ---- Leaf
            var op = n.op ?? ConditionOp.Eq;
            if (!n.op.HasValue) report.Error(file, ctx, $"조건 '{n.key}'에 op가 없습니다.");

            if (op == ConditionOp.Seen || op == ConditionOp.NotSeen)
            {
                if (n.key != null) report.Error(file, ctx, "seen/notSeen 조건에는 key를 쓰지 않습니다 (value에 엔트리 id).");
                if (n.value == null || n.value.Type != JTokenType.String)
                    report.Error(file, ctx, "seen/notSeen의 value는 엔트리 id 문자열이어야 합니다.");
                else
                    n.CompiledEntryRef = n.value.Value<string>(); // 존재 여부는 Validator에서(모든 파일 로드 후)
                return;
            }

            if (string.IsNullOrEmpty(n.key)) { report.Error(file, ctx, "조건에 key가 없습니다."); return; }
            if (!Variables.TryGet(n.key, out var def)) { report.Error(file, ctx, $"variables.json에 없는 키: '{n.key}'"); return; }

            bool numericOp = op == ConditionOp.Gt || op == ConditionOp.Gte || op == ConditionOp.Lt || op == ConditionOp.Lte;
            if (numericOp && def.type != VarType.Int && def.type != VarType.Float)
            {
                report.Error(file, ctx, $"'{n.key}'({def.type})에 대소 비교({op})를 쓸 수 없습니다.");
                return;
            }
            if (op == ConditionOp.In && def.type == VarType.Bool)
            {
                report.Error(file, ctx, $"Bool 변수 '{n.key}'에는 in을 쓸 수 없습니다.");
                return;
            }

            if (op == ConditionOp.In)
            {
                if (!(n.value is JArray arr) || arr.Count == 0) { report.Error(file, ctx, $"'{n.key}' in 조건의 value는 비어있지 않은 배열이어야 합니다."); return; }
                var set = new DVal[arr.Count];
                for (int i = 0; i < arr.Count; i++)
                {
                    if (!DVal.TryFromJToken(arr[i], def.type, out set[i])) { report.Error(file, ctx, $"'{n.key}' in 목록의 {i}번째 값 타입 오류 (기대: {def.type})"); return; }
                    CheckAllowed(def, set[i], file, ctx, report);
                }
                n.CompiledSet = set;
                return;
            }

            if (!DVal.TryFromJToken(n.value, def.type, out n.Compiled))
            {
                report.Error(file, ctx, $"'{n.key}' 값 타입 오류 (기대: {def.type}, 실제: {n.value?.Type.ToString() ?? "없음"})");
                return;
            }
            CheckAllowed(def, n.Compiled, file, ctx, report);
        }

        private static void CheckAllowed(VariableDef def, DVal v, string file, string ctx, ValidationReport report)
        {
            if (def.AllowedSet != null && !def.AllowedSet.Contains(v.AsString))
                report.Error(file, ctx, $"'{def.key}'에 허용되지 않은 값 '{v}' (allowed: {string.Join(", ", def.allowed)})");
        }

        private void CompileEffect(DialogueEffect fx, string file, string ctx, ValidationReport report)
        {
            if (string.IsNullOrEmpty(fx.key) || !Variables.TryGet(fx.key, out var def))
            {
                report.Error(file, ctx, $"효과 키 '{fx.key}'가 variables.json에 없습니다.");
                return;
            }
            // 보안/안정성: 데이터가 골드·파츠 등 게임 소유 값을 직접 바꾸지 못하게 차단
            if (def.scope != VarScope.Dialogue)
            {
                report.Error(file, ctx, $"효과는 Dialogue scope 변수(dlg./story.)만 쓸 수 있습니다: '{fx.key}'는 {def.scope}");
                return;
            }
            if (fx.op == EffectOp.Add && def.type != VarType.Int && def.type != VarType.Float)
            {
                report.Error(file, ctx, $"add 효과는 숫자 변수에만 쓸 수 있습니다: '{fx.key}'");
                return;
            }
            if (!DVal.TryFromJToken(fx.value, def.type, out fx.Compiled))
            {
                report.Error(file, ctx, $"효과 '{fx.key}' 값 타입 오류 (기대: {def.type})");
                return;
            }
            CheckAllowed(def, fx.Compiled, file, ctx, report);
        }
    }
}
