using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    /// <summary>문자열 테이블 기반 텍스트 해석. textKey 없음 → text(개발용) → "#키#" 순으로 폴백.</summary>
    public sealed class LocalizedTextResolver : ITextResolver
    {
        private readonly DialogueDatabase _db;
        private Dictionary<string, string> _table = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Language { get; private set; } = "";

        public LocalizedTextResolver(DialogueDatabase db) { _db = db; }

        public void SetTable(StringTableFile table)
        {
            Language = table?.lang ?? "";
            _table = table?.strings != null
                ? new Dictionary<string, string>(table.strings, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string ResolveLine(DialogueLine line)
        {
            if (!string.IsNullOrEmpty(line.textKey) && _table.TryGetValue(line.textKey, out var s)) return s;
            if (!string.IsNullOrEmpty(line.text)) return line.text;
            return $"#{line.textKey}#"; // 누락이 화면에서 바로 보이도록
        }

        public string ResolveSpeakerName(string speakerId)
        {
            if (_db.Speakers.TryGetValue(speakerId, out var sp) && !string.IsNullOrEmpty(sp.nameKey)
                && _table.TryGetValue(sp.nameKey, out var name)) return name;
            return speakerId;
        }

        /// <summary>현재 언어 테이블에 없는 textKey 목록 (번역 누락 리포트)</summary>
        public List<string> FindMissingKeys()
        {
            var missing = new List<string>();
            foreach (var e in _db.Entries.Values)
                foreach (var l in e.lines)
                    if (!string.IsNullOrEmpty(l.textKey) && !_table.ContainsKey(l.textKey)) missing.Add(l.textKey);
            return missing;
        }
    }

    public enum BlockingRequestStatus { Started, SkippedByRepeatPolicy, NoCandidate, UnknownPool, WrongMode, Busy }

    public struct BlockingRequestResult
    {
        public BlockingRequestStatus Status;
        public DialogueSession Session;
        public DialogueEntry Entry;
    }

    /// <summary>
    /// Core 오케스트레이터. DB + 상태 + 선택기 + 백로그를 묶는다.
    /// Unity의 DialogueService는 이 클래스를 감싸 입력/UI/시간만 연결한다.
    ///
    /// 커밋 규칙 (재생 기록 + 효과 적용):
    ///   Blocking : 세션이 Completed 또는 Skipped로 끝날 때 (스킵해도 스토리 진행이 막히지 않게)
    ///              Aborted(씬 전환 등)는 커밋하지 않음 → 다음에 다시 나옴
    ///   Bark/Bubble : 표시하는 순간 커밋 (완료 개념 없음)
    /// </summary>
    public sealed class DialogueEngine
    {
        private readonly DialogueSelector _selector = new DialogueSelector();
        private readonly IRandomSource _rng;

        public DialogueDatabase Database { get; }
        public DialogueState State { get; }
        public DialogueBacklog Backlog { get; }
        public DialogueSettings Settings { get; }
        public ITextResolver Text { get; }
        public IGameStateProvider Game { get; set; }

        public DialogueSession ActiveSession { get; private set; }

        public event Action<DialogueEntry> Committed;

        public DialogueEngine(DialogueDatabase db, DialogueState state, DialogueSettings settings, ITextResolver text,
                              IGameStateProvider game, IRandomSource rng = null)
        {
            Database = db; State = state; Settings = settings; Text = text; Game = game;
            _rng = rng ?? new SystemRandomSource();
            Backlog = new DialogueBacklog(settings.backlogCapacity);
        }

        public DialogueContext CreateContext(IReadOnlyDictionary<string, DVal> eventArgs) =>
            new DialogueContext(Database.Variables, State, Game, eventArgs);

        /// <summary>선택만 하고 아무것도 기록하지 않음 (디버그 툴/미리보기용)</summary>
        public DialogueEntry Peek(string poolId, IReadOnlyDictionary<string, DVal> eventArgs, double now,
                                  List<DialogueSelector.Candidate> debugEligible = null)
        {
            if (!Database.TryGetPool(poolId, out var pool)) return null;
            return _selector.Select(pool, CreateContext(eventArgs), State, now, _rng, debugEligible);
        }

        public BlockingRequestResult RequestBlocking(string poolId, IReadOnlyDictionary<string, DVal> eventArgs, double now)
        {
            if (ActiveSession != null && ActiveSession.State != SessionState.Finished)
                return new BlockingRequestResult { Status = BlockingRequestStatus.Busy };
            if (!Database.TryGetPool(poolId, out var pool))
                return new BlockingRequestResult { Status = BlockingRequestStatus.UnknownPool };
            if (pool.mode != PresentationMode.Blocking)
                return new BlockingRequestResult { Status = BlockingRequestStatus.WrongMode };

            var ctx = CreateContext(eventArgs);
            var entry = _selector.Select(pool, ctx, State, now, _rng);
            if (entry == null) return new BlockingRequestResult { Status = BlockingRequestStatus.NoCandidate };

            // 에포크 값은 "요청 시점" 기준으로 고정 (대화 도중 값이 바뀌어도 일관)
            long? epoch = DialogueSelector.TryGetOncePerValue(entry, ctx, out var ep) ? ep : (long?)null;

            bool repeat = State.HasSeen(entry.id);
            if (repeat && entry.repeatPolicy == RepeatPolicy.SkipOnRepeat)
            {
                Commit(entry, now, epoch);
                return new BlockingRequestResult { Status = BlockingRequestStatus.SkippedByRepeatPolicy, Entry = entry };
            }

            bool useShort = repeat && entry.repeatPolicy == RepeatPolicy.ShortOnRepeat;
            var session = new DialogueSession(entry, useShort, State, Backlog, Settings, Text);
            session.Finished += (s, reason) =>
            {
                if (reason != FinishReason.Aborted) Commit(s.Entry, now, epoch);
                if (ReferenceEquals(ActiveSession, s)) ActiveSession = null;
            };
            ActiveSession = session;
            return new BlockingRequestResult { Status = BlockingRequestStatus.Started, Session = session, Entry = entry };
        }

        /// <summary>
        /// Bark/Bubble 선택 + 즉시 커밋. 후보가 없거나 accept가 거절하면 null(기록 안 남음).
        /// accept: 표시 측 사정(이미 더 중요한 바크 표시 중 등)으로 거절할 기회.
        /// </summary>
        public DialogueEntry RequestInstant(string poolId, IReadOnlyDictionary<string, DVal> eventArgs, double now,
                                            Func<DialogueEntry, bool> accept = null)
        {
            if (!Database.TryGetPool(poolId, out var pool) || pool.mode == PresentationMode.Blocking) return null;
            var ctx = CreateContext(eventArgs);
            var entry = _selector.Select(pool, ctx, State, now, _rng);
            if (entry == null) return null;
            if (accept != null && !accept(entry)) return null;
            Commit(entry, now, DialogueSelector.TryGetOncePerValue(entry, ctx, out var ep) ? ep : (long?)null);
            foreach (var l in entry.lines) State.MarkLineSeen(l.ResolvedLineId);
            return entry;
        }

        private void Commit(DialogueEntry entry, double now, long? oncePerValue)
        {
            State.RecordPlay(entry.id, now, oncePerValue);
            State.ApplyEffects(entry);
            Committed?.Invoke(entry);
        }
    }
}
