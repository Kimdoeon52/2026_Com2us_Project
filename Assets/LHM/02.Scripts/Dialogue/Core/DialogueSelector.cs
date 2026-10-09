using System;
using System.Collections.Generic;

namespace RealSteel.Dialogue
{
    /// <summary>게임 시스템이 구현: 골드, 전적, 토너먼트, 파츠 상태 등 Game scope 값 제공</summary>
    public interface IGameStateProvider
    {
        bool TryGet(string key, out DVal value);
    }

    public interface IRandomSource
    {
        /// <summary>[0, maxExclusive)</summary>
        int Range(int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _r;
        public SystemRandomSource(int? seed = null) { _r = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public int Range(int maxExclusive) => _r.Next(maxExclusive);
    }

    /// <summary>
    /// 레이어드 변수 조회. 키의 scope(레지스트리)로 출처를 "라우팅"하므로
    /// 이벤트 값이 게임 값을 가리는(섀도잉) 사고가 구조적으로 불가능하다.
    ///   Event    → 트리거가 넘긴 eventArgs
    ///   Dialogue → DialogueState(세이브)
    ///   Game     → IGameStateProvider
    ///   어디에도 없으면 레지스트리 default
    /// </summary>
    public sealed class DialogueContext : IVariableSource
    {
        private readonly VariableRegistry _registry;
        private readonly DialogueState _state;
        private readonly IGameStateProvider _game;
        private readonly IReadOnlyDictionary<string, DVal> _event;

        public DialogueContext(VariableRegistry registry, DialogueState state, IGameStateProvider game, IReadOnlyDictionary<string, DVal> eventArgs)
        {
            _registry = registry; _state = state; _game = game; _event = eventArgs;
        }

        public bool TryGet(string key, out DVal value)
        {
            value = default;
            if (!_registry.TryGet(key, out var def)) return false;

            bool found = false;
            switch (def.scope)
            {
                case VarScope.Event: found = _event != null && _event.TryGetValue(key, out value); break;
                case VarScope.Dialogue: found = _state.TryGetVar(key, out value); break;
                case VarScope.Game: found = _game != null && _game.TryGet(key, out value); break;
            }

            // 게임 코드가 잘못된 타입을 넘기면 default로 폴백 (조건이 엉뚱하게 참이 되는 것 방지)
            if (found && !TypeCompatible(def.type, value)) found = false;
            if (!found) value = def.CompiledDefault;
            return true;
        }

        private static bool TypeCompatible(VarType declared, DVal v) =>
            declared == v.Type || (declared == VarType.Float && v.Type == VarType.Int);
    }

    /// <summary>
    /// 반응형 대사 풀 선택기 (모든 카테고리 공용)
    ///
    /// 1) 자격 필터: once 재생됨 / maxPlays 도달 / 쿨다운 중 / 조건 거짓 → 제외
    /// 2) 정렬 키 (앞이 우선):
    ///      band ↓  →  subPriority ↓  →  specificity(조건 수) ↓  →  plays ↑  →  lastSeq ↑
    /// 3) 위 키가 전부 같은 후보끼리만 무작위 1개 (관중/잡담 다양성)
    /// </summary>
    public sealed class DialogueSelector
    {
        public struct Candidate
        {
            public DialogueEntry Entry;
            public int Plays;
            public long LastSeq;
        }

        private readonly List<Candidate> _buffer = new List<Candidate>(32);

        public DialogueEntry Select(DialoguePool pool, IVariableSource vars, DialogueState state, double now, IRandomSource rng,
                                    List<Candidate> debugEligible = null)
        {
            _buffer.Clear();
            foreach (var e in pool.entries)
            {
                int plays = state.GetPlays(e.id);
                if (e.once && plays > 0) continue;
                if (e.maxPlays > 0 && plays >= e.maxPlays) continue;
                if (state.IsCoolingDown(e, now)) continue;
                if (e.oncePer != null && TryGetOncePerValue(e, vars, out var epoch) && state.GetOncePerMark(e.id) == epoch) continue;
                if (!ConditionEvaluator.Evaluate(e.conditions, vars, state)) continue;
                _buffer.Add(new Candidate { Entry = e, Plays = plays, LastSeq = state.GetLastSeq(e.id) });
            }

            if (_buffer.Count == 0) return null;
            _buffer.Sort(Compare);
            debugEligible?.AddRange(_buffer);

            int tied = 1;
            while (tied < _buffer.Count && Compare(_buffer[0], _buffer[tied]) == 0) tied++;
            return _buffer[tied == 1 ? 0 : rng.Range(tied)].Entry;
        }

        public static bool TryGetOncePerValue(DialogueEntry e, IVariableSource vars, out long value)
        {
            value = 0;
            if (e.oncePer == null || !vars.TryGet(e.oncePer, out var v) || !v.IsNumeric) return false;
            value = (long)v.AsNumber;
            return true;
        }

        /// <summary>음수 = a가 더 우선</summary>
        public static int Compare(Candidate a, Candidate b)
        {
            int c = ((int)b.Entry.band).CompareTo((int)a.Entry.band);
            if (c != 0) return c;
            c = b.Entry.subPriority.CompareTo(a.Entry.subPriority);
            if (c != 0) return c;
            c = b.Entry.Specificity.CompareTo(a.Entry.Specificity);
            if (c != 0) return c;
            c = a.Plays.CompareTo(b.Plays);
            if (c != 0) return c;
            return a.LastSeq.CompareTo(b.LastSeq);
        }
    }
}
