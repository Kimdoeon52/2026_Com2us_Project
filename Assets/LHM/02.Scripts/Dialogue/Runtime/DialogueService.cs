using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 대화 시스템의 Unity 진입점(파사드). 게임 코드는 이 클래스만 호출한다.
    ///
    ///   DialogueService.Instance.RequestBlocking("hub.junkshop.talk");
    ///   DialogueService.Instance.RequestBlocking("boss.wheel01.intro", onDone: _ => StartFight());
    ///   BarkManager.Instance.PlayPartBreak(PartSlot.LeftArm);
    ///   crowdSpawner.Emit(CrowdMoment.BossStunned);
    ///
    /// 책임: 데이터 로드 / Core 엔진 보유 / 입력 컨텍스트 전환 / 대화창 연결 / 세이브 입출력 / 씬 전환 정리
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DialogueService : MonoBehaviour
    {
        public static DialogueService Instance { get; private set; }

        [SerializeField] private DialogueDataManifest manifest;
        [SerializeField] private DialogueSettings settings = new DialogueSettings();
        [SerializeField] private DialogueBoxPresenter dialogueBox;
        [SerializeField] private GameStateBlackboard blackboard;
        [SerializeField] private string language = "ko";

        [Tooltip("Blocking 대화 중 Time.timeScale = 0 (보스/투사체 정지). 대화창은 unscaled time으로 동작")]
        [SerializeField] private bool freezeTimeDuringBlocking = true;

        [Tooltip("플레이어가 회피/공격 중이면 대화 시작을 미루는 대기열 길이")]
        [SerializeField] private int maxPendingRequests = 4;

        public DialogueEngine Engine { get; private set; }
        public ValidationReport LoadReport { get; private set; }
        public LocalizedTextResolver Text { get; private set; }
        public DialogueSettings Settings => settings;
        public GameStateBlackboard Blackboard => blackboard;

        public bool IsBlockingActive => _active != null;

        /// <summary>신버전 세이브를 읽은 경우 true → 저장 금지 (구버전 빌드가 신버전 세이브를 망가뜨리지 않게)</summary>
        public bool SaveLocked { get; private set; }

        /// <summary>
        /// 대화 시작 허용 여부. 플레이어 컨트롤러가 설정 (예: () => player.IsGrounded && !player.IsDodging).
        /// false인 동안 요청은 대기열에서 기다린다.
        /// </summary>
        public Func<bool> CanStartBlocking = () => true;

        public event Action<DialogueEntry> BlockingStarted;
        public event Action<DialogueEntry, FinishReason> BlockingEnded;

        private sealed class Pending
        {
            public string PoolId;
            public IReadOnlyDictionary<string, DVal> Args;
            public Action<FinishReason> OnDone;
        }

        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private DialogueSession _active;
        private Action<FinishReason> _activeOnDone;
        private int _inputToken;
        private float _savedTimeScale = 1f;

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDatabase(preserveState: null);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Instance = null;
        }

        private void LoadDatabase(DialogueSaveData preserveState)
        {
            LoadReport = new ValidationReport();
            var db = DialogueDatabase.Load(manifest.ToSourceSet(), LoadReport);

            if (LoadReport.HasErrors) Debug.LogError(LoadReport.ToText(), this);
            else if (LoadReport.WarningCount > 0) Debug.LogWarning(LoadReport.ToText(), this);

            Text = new LocalizedTextResolver(db);
            SetLanguage(language);

            var state = new DialogueState(db.Variables, preserveState);
            state.ApplyIdRemap(db);
            Engine = new DialogueEngine(db, state, settings, Text, blackboard);
        }

        public void SetLanguage(string lang)
        {
            language = lang;
            var asset = manifest.FindTable(lang);
            if (asset == null) { Debug.LogWarning($"[Dialogue] 문자열 테이블 없음: {lang}", this); return; }
            try
            {
                Text.SetTable(JsonConvert.DeserializeObject<StringTableFile>(asset.text, DialogueJson.Strict));
            }
            catch (JsonException e)
            {
                Debug.LogError($"[Dialogue] 문자열 테이블 파싱 실패({asset.name}): {e.Message}", this);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>플레이 중 JSON 수정 → 즉시 반영 (진행 상태 유지). 개발 빌드 전용.</summary>
        [ContextMenu("Reload Dialogue Data")]
        public void ReloadData()
        {
            if (IsBlockingActive) { Debug.LogWarning("[Dialogue] 대화 중에는 리로드하지 않습니다."); return; }
            LoadDatabase(Engine.State.Data);
            Debug.Log("[Dialogue] 데이터 리로드 완료");
        }
#endif

        // ------------------------------------------------------------------ Blocking
        /// <summary>
        /// Blocking 대화 요청. onDone은 어떤 경우에도(후보 없음/반복 스킵/완료/스킵/중단) 반드시 1회 호출된다.
        /// → 보스전 시작처럼 "대화 다음 단계"를 onDone에 걸어도 진행이 막히지 않는다.
        /// </summary>
        public void RequestBlocking(string poolId, IReadOnlyDictionary<string, DVal> eventArgs = null, Action<FinishReason> onDone = null)
        {
            if (IsBlockingActive || !CanStartBlocking())
            {
                if (_pending.Count >= maxPendingRequests)
                {
                    Debug.LogWarning($"[Dialogue] 대기열 초과로 요청 폐기: {poolId}", this);
                    onDone?.Invoke(FinishReason.Aborted);
                    return;
                }
                _pending.Enqueue(new Pending { PoolId = poolId, Args = eventArgs, OnDone = onDone });
                return;
            }
            StartBlocking(poolId, eventArgs, onDone);
        }

        private void StartBlocking(string poolId, IReadOnlyDictionary<string, DVal> args, Action<FinishReason> onDone)
        {
            var r = Engine.RequestBlocking(poolId, args, Time.timeAsDouble);
            switch (r.Status)
            {
                case BlockingRequestStatus.Started:
                    break;
                case BlockingRequestStatus.SkippedByRepeatPolicy:
                    onDone?.Invoke(FinishReason.Skipped);
                    return;
                case BlockingRequestStatus.NoCandidate:
                    onDone?.Invoke(FinishReason.Completed); // 할 말이 없으면 그냥 통과
                    return;
                default:
                    Debug.LogError($"[Dialogue] '{poolId}' 요청 실패: {r.Status}", this);
                    onDone?.Invoke(FinishReason.Aborted);
                    return;
            }

            _active = r.Session;
            _activeOnDone = onDone;
            _active.Finished += OnSessionFinished;

            _inputToken = InputContextManager.Instance != null ? InputContextManager.Instance.Push(InputContext.Dialogue) : 0;
            if (freezeTimeDuringBlocking) { _savedTimeScale = Time.timeScale; Time.timeScale = 0f; }

            BlockingStarted?.Invoke(r.Entry);
            dialogueBox.Show(_active);
            _active.Begin();
        }

        private void OnSessionFinished(DialogueSession s, FinishReason reason)
        {
            s.Finished -= OnSessionFinished;
            dialogueBox.Hide();

            if (freezeTimeDuringBlocking) Time.timeScale = _savedTimeScale;
            if (_inputToken != 0 && InputContextManager.Instance != null) InputContextManager.Instance.Pop(_inputToken);
            _inputToken = 0;

            var onDone = _activeOnDone;
            _active = null;
            _activeOnDone = null;

            BlockingEnded?.Invoke(s.Entry, reason);
            try { onDone?.Invoke(reason); }
            catch (Exception e) { Debug.LogException(e, this); } // 콜백 예외가 대화 시스템 상태를 꼬지 않게
        }

        private void Update()
        {
            if (IsBlockingActive || _pending.Count == 0 || !CanStartBlocking()) return;
            var p = _pending.Dequeue();
            StartBlocking(p.PoolId, p.Args, p.OnDone);
        }

        private void OnSceneChanged(Scene from, Scene to)
        {
            // 씬이 바뀌면 진행 중 대화는 중단(커밋 안 됨 → 다음에 다시 나옴), 대기열 폐기
            _active?.Abort();
            while (_pending.Count > 0) _pending.Dequeue().OnDone?.Invoke(FinishReason.Aborted);
            InputContextManager.Instance?.ResetToGameplay();
        }

        // ------------------------------------------------------------------ Bark / Bubble
        /// <summary>BarkManager / CrowdBubbleSpawner 전용. Blocking 대화 중에는 출력하지 않는다.</summary>
        public DialogueEntry RequestInstant(string poolId, IReadOnlyDictionary<string, DVal> args, Func<DialogueEntry, bool> accept = null)
        {
            if (IsBlockingActive) return null;
            return Engine.RequestInstant(poolId, args, Time.timeAsDouble, accept);
        }

        // ------------------------------------------------------------------ Save
        public string ExportSave() => DialogueSaveSerializer.Serialize(Engine.State.Data);

        public SaveLoadResult ImportSave(string json)
        {
            var r = DialogueSaveSerializer.Deserialize(json);
            ApplyLoadResult(r);
            return r;
        }

        public void SaveToSlot(int slot)
        {
            if (SaveLocked) { Debug.LogError("[Dialogue] 신버전 세이브 보호를 위해 저장이 잠겨 있습니다."); return; }
            DialogueSaveIO.WriteAtomic(DialogueSaveIO.DefaultPath(slot), ExportSave());
        }

        public SaveLoadResult LoadFromSlot(int slot)
        {
            var r = DialogueSaveIO.ReadWithRecovery(DialogueSaveIO.DefaultPath(slot));
            ApplyLoadResult(r);
            return r;
        }

        private void ApplyLoadResult(SaveLoadResult r)
        {
            SaveLocked = r.Status == SaveLoadStatus.NewerVersion;
            if (r.Data == null) { Debug.LogError("[Dialogue] " + r.Message); return; }
            if (!string.IsNullOrEmpty(r.Message)) Debug.LogWarning("[Dialogue] " + r.Message);

            Engine.State.Replace(r.Data);
            int moved = Engine.State.ApplyIdRemap(Engine.Database);
            if (moved > 0) Debug.Log($"[Dialogue] formerIds 이관 {moved}건");
            if (Engine.State.OrphanIds.Count > 0)
                Debug.Log($"[Dialogue] 현재 데이터에 없는 세이브 기록 {Engine.State.OrphanIds.Count}건 (보존됨)");
        }
    }
}
