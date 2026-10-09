using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RealSteel.Dialogue
{
    /// <summary>플레이어 옵션 + 연출 기본값. 옵션 화면/세이브(설정 파일)와 연결한다.</summary>
    [Serializable]
    public sealed class DialogueSettings
    {
        public float charsPerSecond = 40f;
        /// <summary>빨리감기 중 줄 넘김 간격(초)</summary>
        public float fastForwardInterval = 0.06f;
        /// <summary>false면 빨리감기/전체 스킵이 "읽은 대사"에서만 즉시 동작</summary>
        public bool allowSkipUnread = false;
        public float autoDelayBase = 1.0f;
        public float autoDelayPerChar = 0.05f;
        /// <summary>줄 시작 직후 확인 입력 무시 시간 (연타로 대사를 놓치는 것 방지)</summary>
        public float confirmGuardSec = 0.12f;
        /// <summary>대화 시작 직후 입력 무시 시간 (말 걸기 키가 첫 줄을 넘기는 것 방지)</summary>
        public float openGuardSec = 0.25f;
        public int backlogCapacity = 200;
    }

    public interface ITextResolver
    {
        string ResolveLine(DialogueLine line);
        string ResolveSpeakerName(string speakerId);
    }

    public sealed class BacklogItem
    {
        public string LineId;
        public string SpeakerId;
        public string SpeakerName;
        public string Text;
    }

    /// <summary>대사 로그(백로그). 고정 크기 링버퍼 → 장시간 플레이에도 메모리 일정.</summary>
    public sealed class DialogueBacklog
    {
        private readonly BacklogItem[] _items;
        private int _start, _count;

        public DialogueBacklog(int capacity) { _items = new BacklogItem[Math.Max(1, capacity)]; }

        public int Count => _count;

        public void Add(BacklogItem item)
        {
            int idx = (_start + _count) % _items.Length;
            _items[idx] = item;
            if (_count < _items.Length) _count++;
            else _start = (_start + 1) % _items.Length;
        }

        /// <summary>0 = 가장 오래된 항목</summary>
        public BacklogItem this[int i] => _items[(_start + i) % _items.Length];

        public void Clear() { _start = 0; _count = 0; Array.Clear(_items, 0, _items.Length); }
    }

    public enum SessionState { NotStarted, Revealing, WaitingForInput, Finished }
    public enum FinishReason { Completed, Skipped, Aborted }

    /// <summary>
    /// Blocking 대화 1회 재생의 상태 머신 (Unity 비의존, 시간은 Tick으로 주입).
    /// UI(Presenter)는 이 객체를 구동만 하고 규칙은 모두 여기 있다 → 테스트 가능.
    ///
    ///   Confirm      : 타이핑 중이면 즉시 완성, 완성 상태면 다음 줄
    ///   FastForward  : 누르고 있는 동안 고속 진행 (읽지 않은 줄에서 멈춤, 옵션으로 해제)
    ///   Auto         : 줄 완성 후 길이 비례 대기 후 자동 진행
    ///   SkipAll      : 남은 줄 전부 건너뜀. 안 읽은 줄이 있으면 확인 요청 → ForceSkipAll
    /// </summary>
    public sealed class DialogueSession
    {
        private static readonly Regex RichTag = new Regex("<[^>]*>", RegexOptions.Compiled);

        private readonly DialogueState _state;
        private readonly DialogueBacklog _backlog;
        private readonly DialogueSettings _settings;
        private readonly ITextResolver _text;
        private readonly IReadOnlyList<DialogueLine> _lines;

        private int _index = -1;
        private float _reveal;          // 누적 공개 글자 수(소수)
        private float _lineElapsed;     // 현재 줄 경과 시간
        private float _sessionElapsed;
        private float _waitElapsed;     // 완성 후 경과 (오토/빨리감기)

        public DialogueEntry Entry { get; }
        public bool UsingShortLines { get; }
        public SessionState State { get; private set; } = SessionState.NotStarted;
        public FinishReason? Result { get; private set; }

        public DialogueLine CurrentLine => _index >= 0 && _index < _lines.Count ? _lines[_index] : null;
        public string CurrentText { get; private set; } = string.Empty;
        public string CurrentSpeakerName { get; private set; } = string.Empty;
        public int TotalChars { get; private set; }
        public int VisibleChars => State == SessionState.Revealing ? Math.Min(TotalChars, (int)_reveal) : TotalChars;
        public int LineIndex => _index;
        public int LineCount => _lines.Count;

        public bool AutoMode { get; set; }
        public bool FastForwardHeld { get; set; }

        public event Action<DialogueSession> LineStarted;
        public event Action<DialogueSession> LineCompleted;
        public event Action<DialogueSession, FinishReason> Finished;

        public DialogueSession(DialogueEntry entry, bool useShortLines, DialogueState state, DialogueBacklog backlog,
                               DialogueSettings settings, ITextResolver text)
        {
            Entry = entry;
            UsingShortLines = useShortLines && entry.shortLines.Count > 0;
            _lines = UsingShortLines ? entry.shortLines : entry.lines;
            _state = state; _backlog = backlog; _settings = settings; _text = text;
        }

        public void Begin()
        {
            if (State != SessionState.NotStarted) return;
            if (_lines.Count == 0) { Finish(FinishReason.Completed); return; }
            StartLine(0);
        }

        public bool IsCurrentLineSeen => CurrentLine != null && _state.IsLineSeen(CurrentLine.ResolvedLineId);

        /// <summary>남은 줄 중 안 읽은 줄이 있는가 (스킵 확인 팝업 판단용)</summary>
        public bool HasUnreadRemaining()
        {
            for (int i = Math.Max(0, _index); i < _lines.Count; i++)
                if (!_state.IsLineSeen(_lines[i].ResolvedLineId)) return true;
            return false;
        }

        public void Confirm()
        {
            if (State == SessionState.Finished || State == SessionState.NotStarted) return;
            if (_sessionElapsed < _settings.openGuardSec) return;
            if (_lineElapsed < _settings.confirmGuardSec) return;

            if (State == SessionState.Revealing) CompleteLine();
            else Next();
        }

        /// <summary>true면 바로 스킵됨. false면 안 읽은 대사가 있으니 UI가 확인 후 ForceSkipAll 호출.</summary>
        public bool TrySkipAll()
        {
            if (State == SessionState.Finished) return true;
            if (!_settings.allowSkipUnread && HasUnreadRemaining()) return false;
            ForceSkipAll();
            return true;
        }

        public void ForceSkipAll()
        {
            if (State == SessionState.Finished) return;
            Finish(FinishReason.Skipped);
        }

        /// <summary>씬 전환/강제 종료. 커밋(재생 기록/효과)되지 않으므로 다음에 다시 나온다.</summary>
        public void Abort()
        {
            if (State == SessionState.Finished) return;
            Finish(FinishReason.Aborted);
        }

        public void Tick(float dt)
        {
            if (State == SessionState.Finished || State == SessionState.NotStarted) return;
            _sessionElapsed += dt;
            _lineElapsed += dt;

            bool ffActive = FastForwardHeld && (_settings.allowSkipUnread || IsCurrentLineSeen);

            if (State == SessionState.Revealing)
            {
                if (ffActive) { CompleteLine(); return; }
                _reveal += _settings.charsPerSecond * dt;
                if (_reveal >= TotalChars) CompleteLine();
                return;
            }

            // WaitingForInput
            _waitElapsed += dt;
            if (ffActive && _waitElapsed >= _settings.fastForwardInterval) { Next(); return; }
            if (AutoMode && _waitElapsed >= AutoDelayFor(CurrentLine)) Next();
        }

        public float AutoDelayFor(DialogueLine line)
        {
            if (line != null && line.autoDelay > 0) return line.autoDelay;
            return _settings.autoDelayBase + TotalChars * _settings.autoDelayPerChar;
        }

        // ------------------------------------------------------------------
        private void StartLine(int i)
        {
            _index = i;
            _reveal = 0;
            _lineElapsed = 0;
            _waitElapsed = 0;
            var line = _lines[i];
            CurrentText = _text.ResolveLine(line) ?? string.Empty;
            CurrentSpeakerName = string.IsNullOrEmpty(line.speaker) ? string.Empty : (_text.ResolveSpeakerName(line.speaker) ?? line.speaker);
            TotalChars = VisibleLength(CurrentText);
            State = SessionState.Revealing;

            // 백로그는 "화면에 나온 줄"만 기록
            _backlog?.Add(new BacklogItem { LineId = line.ResolvedLineId, SpeakerId = line.speaker, SpeakerName = CurrentSpeakerName, Text = CurrentText });
            LineStarted?.Invoke(this);

            if (TotalChars == 0) CompleteLine();
        }

        private void CompleteLine()
        {
            if (State != SessionState.Revealing) return;
            State = SessionState.WaitingForInput;
            _waitElapsed = 0;
            _state.MarkLineSeen(CurrentLine.ResolvedLineId); // 끝까지 공개된 줄만 "읽음"
            LineCompleted?.Invoke(this);
        }

        private void Next()
        {
            if (_index + 1 < _lines.Count) StartLine(_index + 1);
            else Finish(FinishReason.Completed);
        }

        private void Finish(FinishReason reason)
        {
            State = SessionState.Finished;
            Result = reason;
            Finished?.Invoke(this, reason);
        }

        /// <summary>TMP 리치 텍스트 태그를 제외한 글자 수 (TMP maxVisibleCharacters와 대응)</summary>
        public static int VisibleLength(string s) => string.IsNullOrEmpty(s) ? 0 : RichTag.Replace(s, string.Empty).Length;
    }
}
