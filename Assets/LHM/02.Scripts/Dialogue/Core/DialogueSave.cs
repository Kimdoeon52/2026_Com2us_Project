using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealSteel.Dialogue
{
    // =====================================================================
    //  세이브 호환성 원칙
    //   1) 모든 기록은 "문자열 ID" 키. 배열 인덱스/순번 저장 금지.
    //   2) version 필드 + 단계별 마이그레이션(v1→v2→…). 구버전 세이브는 항상 열린다.
    //   3) 데이터에서 사라진 ID 기록은 지우지 않고 보존(orphan) → 엔트리를 되살리면 기록도 복구.
    //   4) 엔트리 ID 변경은 formerIds로 이관.
    //   5) 신버전 세이브를 구버전 빌드가 열면 덮어쓰지 않고 실패를 알린다(데이터 손실 방지).
    // =====================================================================

    public sealed class EntryRecord
    {
        public int plays;
        /// <summary>마지막 재생 순번(전역 단조 증가). "가장 오래전에 본 대사 우선"에 사용</summary>
        public long lastSeq;
        /// <summary>
        /// oncePer 변수의 마지막 재생 시점 값. v2 이후 추가된 필드 → 구세이브에는 없으므로 null(=아직 안 봄)로 읽힌다.
        /// "새 필드는 nullable/기본값으로 추가"가 버전업 없이 호환을 지키는 규칙.
        /// </summary>
        public long? oncePerMark;
    }

    public sealed class DialogueSaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public long sequence;
        public Dictionary<string, EntryRecord> entries = new Dictionary<string, EntryRecord>();
        public HashSet<string> seenLines = new HashSet<string>();
        /// <summary>Dialogue scope 변수(dlg./story.)만 저장. 게임 소유 값은 게임 세이브가 책임진다.</summary>
        public Dictionary<string, JToken> vars = new Dictionary<string, JToken>();
    }

    public enum SaveLoadStatus { Ok, Migrated, NewerVersion, Corrupt }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus Status;
        public DialogueSaveData Data;
        public int FromVersion;
        public string Message;
    }

    public static class DialogueSaveSerializer
    {
        public static string Serialize(DialogueSaveData data) =>
            JsonConvert.SerializeObject(data, Formatting.None, DialogueJson.Lenient);

        /// <summary>
        /// 실패해도 예외를 던지지 않는다. Status가 Ok/Migrated가 아니면
        /// 호출 측(게임 세이브 시스템)이 백업 복구 여부를 결정한다.
        /// </summary>
        public static SaveLoadResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SaveLoadResult { Status = SaveLoadStatus.Ok, Data = new DialogueSaveData(), FromVersion = DialogueSaveData.CurrentVersion, Message = "빈 세이브 → 새로 시작" };

            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException e)
            {
                return new SaveLoadResult { Status = SaveLoadStatus.Corrupt, Data = new DialogueSaveData(), Message = "세이브 파싱 실패: " + e.Message };
            }

            int fromVersion = root.Value<int?>("version") ?? 1;
            if (fromVersion > DialogueSaveData.CurrentVersion)
            {
                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.NewerVersion, Data = null, FromVersion = fromVersion,
                    Message = $"이 빌드보다 새로운 세이브(v{fromVersion})입니다. 덮어쓰지 마세요."
                };
            }

            try
            {
                for (int v = fromVersion; v < DialogueSaveData.CurrentVersion; v++)
                    root = Migrations[v](root);

                var data = root.ToObject<DialogueSaveData>(JsonSerializer.Create(DialogueJson.Lenient)) ?? new DialogueSaveData();
                data.entries = data.entries ?? new Dictionary<string, EntryRecord>();
                data.seenLines = data.seenLines ?? new HashSet<string>();
                data.vars = data.vars ?? new Dictionary<string, JToken>();
                data.version = DialogueSaveData.CurrentVersion;

                return new SaveLoadResult
                {
                    Status = fromVersion == DialogueSaveData.CurrentVersion ? SaveLoadStatus.Ok : SaveLoadStatus.Migrated,
                    Data = data, FromVersion = fromVersion
                };
            }
            catch (Exception e) when (e is JsonException || e is InvalidCastException || e is FormatException)
            {
                return new SaveLoadResult { Status = SaveLoadStatus.Corrupt, Data = new DialogueSaveData(), FromVersion = fromVersion, Message = "세이브 변환 실패: " + e.Message };
            }
        }

        /// <summary>Migrations[n] = vN → vN+1. 새 버전을 만들면 여기에 한 줄 추가.</summary>
        private static readonly Dictionary<int, Func<JObject, JObject>> Migrations = new Dictionary<int, Func<JObject, JObject>>
        {
            { 1, MigrateV1ToV2 },
        };

        /// <summary>
        /// v1(초기 프로토타입 형식 예시): { "seen": ["id", ...], "flags": { "dlg.x": true } }
        /// v2: entries(재생 횟수/순번), seenLines, vars
        /// </summary>
        private static JObject MigrateV1ToV2(JObject v1)
        {
            var v2 = new JObject { ["version"] = 2 };
            var entries = new JObject();
            long seq = 0;
            if (v1["seen"] is JArray seen)
                foreach (var id in seen)
                    entries[id.Value<string>()] = new JObject { ["plays"] = 1, ["lastSeq"] = ++seq };
            v2["entries"] = entries;
            v2["sequence"] = seq;
            v2["seenLines"] = new JArray();
            v2["vars"] = v1["flags"] is JObject flags ? (JObject)flags.DeepClone() : new JObject();
            return v2;
        }
    }

    /// <summary>
    /// 런타임 대화 상태 = 세이브 데이터(영속) + 쿨다운(세션 한정).
    /// 선택기/세션은 이 클래스만 통해 기록을 읽고 쓴다.
    /// </summary>
    public sealed class DialogueState : ISeenSource
    {
        private readonly VariableRegistry _registry;
        private readonly Dictionary<string, double> _lastPlayTime = new Dictionary<string, double>(StringComparer.Ordinal);

        public DialogueSaveData Data { get; private set; }

        /// <summary>현재 데이터에 없는 세이브 기록 (보존만 함, 디버그 표시용)</summary>
        public List<string> OrphanIds { get; } = new List<string>();

        public DialogueState(VariableRegistry registry, DialogueSaveData data = null)
        {
            _registry = registry;
            Data = data ?? new DialogueSaveData();
        }

        public void Replace(DialogueSaveData data)
        {
            Data = data ?? new DialogueSaveData();
            _lastPlayTime.Clear();
        }

        // ---------------------------------------------------------------- 엔트리 기록
        public int GetPlays(string entryId) => Data.entries.TryGetValue(entryId, out var r) ? r.plays : 0;
        public long GetLastSeq(string entryId) => Data.entries.TryGetValue(entryId, out var r) ? r.lastSeq : 0;
        public bool HasSeen(string entryId) => GetPlays(entryId) > 0;

        public void RecordPlay(string entryId, double now, long? oncePerValue = null)
        {
            if (!Data.entries.TryGetValue(entryId, out var r)) Data.entries[entryId] = r = new EntryRecord();
            r.plays++;
            r.lastSeq = ++Data.sequence;
            if (oncePerValue.HasValue) r.oncePerMark = oncePerValue;
            _lastPlayTime[entryId] = now;
        }

        public long? GetOncePerMark(string entryId) => Data.entries.TryGetValue(entryId, out var r) ? r.oncePerMark : null;

        public bool IsCoolingDown(DialogueEntry e, double now) =>
            e.cooldownSec > 0 && _lastPlayTime.TryGetValue(e.id, out var t) && now - t < e.cooldownSec;

        /// <summary>바크처럼 "표시는 했지만 완료 개념이 없는" 경우에도 쿨다운을 걸 때 사용</summary>
        public void MarkCooldown(string entryId, double now) => _lastPlayTime[entryId] = now;

        // ---------------------------------------------------------------- 읽음 표시
        public bool IsLineSeen(string lineId) => Data.seenLines.Contains(lineId);
        public void MarkLineSeen(string lineId) => Data.seenLines.Add(lineId);

        // ---------------------------------------------------------------- 변수 (Dialogue scope)
        public bool TryGetVar(string key, out DVal value)
        {
            value = default;
            if (!Data.vars.TryGetValue(key, out var token)) return false;
            if (!_registry.TryGet(key, out var def)) return false;
            return DVal.TryFromJToken(token, def.type, out value); // 타입이 바뀐 옛 값은 무시 → default로 폴백
        }

        public void SetVar(string key, DVal value) => Data.vars[key] = value.ToJToken();

        public void ApplyEffects(DialogueEntry e)
        {
            foreach (var fx in e.effects)
            {
                if (!_registry.TryGet(fx.key, out var def) || def.scope != VarScope.Dialogue) continue; // 이중 방어
                if (fx.op == EffectOp.Set) { SetVar(fx.key, fx.Compiled); continue; }

                double cur = TryGetVar(fx.key, out var v) ? v.AsNumber : def.CompiledDefault.AsNumber;
                double next = cur + fx.Compiled.AsNumber;
                SetVar(fx.key, def.type == VarType.Int ? DVal.Int((long)next) : DVal.Float(next));
            }
        }

        // ---------------------------------------------------------------- ID 이관
        /// <summary>DB 로드 후 1회 호출. formerIds 이관 + orphan 집계.</summary>
        public int ApplyIdRemap(DialogueDatabase db)
        {
            int moved = 0;
            foreach (var e in db.Entries.Values)
            {
                foreach (var old in e.formerIds)
                {
                    if (!Data.entries.TryGetValue(old, out var oldRec)) continue;
                    if (Data.entries.TryGetValue(e.id, out var cur))
                    {
                        cur.plays += oldRec.plays;
                        cur.lastSeq = Math.Max(cur.lastSeq, oldRec.lastSeq);
                        cur.oncePerMark = cur.oncePerMark ?? oldRec.oncePerMark;
                    }
                    else Data.entries[e.id] = oldRec;
                    Data.entries.Remove(old);
                    moved++;
                }
            }

            OrphanIds.Clear();
            foreach (var id in Data.entries.Keys)
                if (!db.Entries.ContainsKey(id)) OrphanIds.Add(id);
            return moved;
        }
    }
}
