using System;
using System.Collections;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Cutscene
{
    public class CutscenePlayer : MonoBehaviour
    {
        private const string SeenPrefix = "cutscene.seen.";
        private const float SkipFadeSeconds = 0.4f;
        private const float SkipHintFadeSeconds = 0.2f;
        private const float NextMarkBlinkSpeed = 2.5f;

        [Header("데이터")]
        [SerializeField] private TextAsset cutsceneJson;

        [Header("재생")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool playOnlyOnce;
        [SerializeField] private string nextSceneName = "";

        [Header("화면")]
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject stage;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private CutsceneMapView mapView;

        [Header("타이틀 카드")]
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("내레이션")]
        [SerializeField] private CanvasGroup narrationGroup;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private CanvasGroup nextMark;
        [SerializeField] private DialogueVoice voice;

        [Header("스킵")]
        [SerializeField] private CanvasGroup skipHint;
        [SerializeField, Min(0.5f)] private float skipConfirmSeconds = 2f;

        [Header("시간")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 28f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 1f;
        [SerializeField, Min(0.05f)] private float layerFadeSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float defaultMove = 1.5f;
        [SerializeField, Min(0f)] private float defaultHold = 2f;
        [SerializeField, Min(0f)] private float holdPerCharacter = 0.04f;
        [SerializeField, Min(0f)] private float startInputDelay = 0.25f;
        [SerializeField, Min(0f)] private float shotInputDelay = 0.12f;

        private CutsceneData data;
        private Typewriter typewriter;
        private Coroutine routine;
        private float inputBlockedUntil;
        private float skipArmedUntil = float.NegativeInfinity;
        private bool waitingNext;
        private bool ending;

        public event Action Finished;

        public bool IsPlaying { get; private set; }
        public string CutsceneId => data != null ? data.id : "";

        private void Awake()
        {
            if (bodyText != null)
            {
                typewriter = new Typewriter(bodyText);
                typewriter.CharacterRevealed += OnCharacterRevealed;
            }

            if (root != null) root.SetActive(false);
        }

        private void Start()
        {
            if (!playOnStart || !TryLoad()) return;

            if (playOnlyOnce && HasSeen(data.id))
            {
                Debug.Log($"[Cutscene] '{data.id}'는 이미 봤습니다 — 재생하지 않습니다.");
                if (!string.IsNullOrEmpty(nextSceneName)) LoadNextScene();
                return;
            }

            Begin();
        }

        private void OnDisable()
        {
            if (!IsPlaying) return;

            IsPlaying = false;
            UiStack.Remove(this);
        }

        private void Update()
        {
            if (!IsPlaying) return;

            TickSkipHint();
            TickNextMark();

            if (ending || Time.unscaledTime < inputBlockedUntil || !DialogueInput.SkipPressed()) return;

            if (Time.unscaledTime <= skipArmedUntil) Skip();
            else skipArmedUntil = Time.unscaledTime + skipConfirmSeconds;
        }

        public bool Play()
        {
            if (IsPlaying)
            {
                Debug.LogWarning("[Cutscene] 이미 재생 중입니다.", this);
                return false;
            }

            if (!TryLoad()) return false;
            Begin();
            return true;
        }

        public void Skip()
        {
            if (!IsPlaying || ending) return;

            Debug.Log($"[Cutscene] '{data.id}' 건너뜀");
            Restart(EndRoutine(SkipFadeSeconds));
        }

        public static bool HasSeen(string cutsceneId)
        {
            return !string.IsNullOrEmpty(cutsceneId) && PlayerPrefs.GetInt(SeenPrefix + cutsceneId, 0) == 1;
        }

        public static void ResetSeen(string cutsceneId)
        {
            if (!string.IsNullOrEmpty(cutsceneId)) PlayerPrefs.DeleteKey(SeenPrefix + cutsceneId);
        }

        private bool TryLoad()
        {
            if (root == null || fade == null)
            {
                Debug.LogError("[Cutscene] Root 또는 Fade가 연결되지 않았습니다.", this);
                return false;
            }

            if (cutsceneJson == null)
            {
                Debug.LogError("[Cutscene] Cutscene Json이 연결되지 않았습니다.", this);
                return false;
            }

            if (!CutsceneData.TryParse(cutsceneJson.text, out data, out string error))
            {
                Debug.LogError($"[Cutscene] {cutsceneJson.name}: {error}", this);
                return false;
            }

            foreach (string problem in data.Check()) Debug.LogWarning($"[Cutscene] {cutsceneJson.name} {problem}", this);
            return true;
        }

        private void Begin()
        {
            IsPlaying = true;
            ending = false;
            waitingNext = false;
            skipArmedUntil = float.NegativeInfinity;
            inputBlockedUntil = Time.unscaledTime + startInputDelay;

            root.SetActive(true);
            if (stage != null) stage.SetActive(true);
            fade.alpha = 1f;
            SetAlpha(titleGroup, 0f);
            SetAlpha(narrationGroup, 0f);
            SetAlpha(skipHint, 0f);
            SetAlpha(nextMark, 0f);
            if (mapView != null) mapView.ResetView();

            UiStack.Push(this);
            Debug.Log($"[Cutscene] '{data.id}' 재생 ({data.shots.Count}컷)");
            Restart(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            yield return FadeTo(fade, 0f, fadeSeconds);

            foreach (CutsceneShot shot in data.shots)
            {
                if (shot != null) yield return PlayShot(shot);
            }

            yield return EndRoutine(fadeSeconds);
        }

        private IEnumerator PlayShot(CutsceneShot shot)
        {
            BlockInput(shotInputDelay);
            float hold = shot.hold > 0f ? shot.hold : defaultHold;
            float move = shot.move > 0f ? shot.move : defaultMove;
            float moveEndsAt = Time.unscaledTime;

            if (mapView != null)
            {
                if (shot.HasPin || shot.HasFocus) mapView.ShowPin(shot.pin, shot.x, shot.y);
                if (shot.HasFocus)
                {
                    mapView.FocusOn(shot.x, shot.y, shot.zoom, move);
                    moveEndsAt += move;
                }
            }

            if (shot.HasTitle) yield return ShowTitle(shot, hold);

            if (shot.HasText)
            {
                yield return ShowNarration(shot, hold);
            }
            else if (!shot.HasTitle)
            {
                yield return FadeTo(narrationGroup, 0f, layerFadeSeconds);
                yield return WaitOrConfirm(moveEndsAt - Time.unscaledTime + hold);
            }
        }

        private IEnumerator ShowTitle(CutsceneShot shot, float hold)
        {
            yield return FadeTo(narrationGroup, 0f, layerFadeSeconds);
            SetText(titleText, shot.title);
            SetText(subtitleText, shot.subtitle);
            yield return FadeTo(titleGroup, 1f, layerFadeSeconds);
            yield return WaitOrConfirm(hold);
            yield return FadeTo(titleGroup, 0f, layerFadeSeconds);
        }

        private IEnumerator ShowNarration(CutsceneShot shot, float hold)
        {
            SetText(speakerText, shot.speaker);
            if (voice != null) voice.SetSpeaker(shot.voice);

            if (typewriter == null)
            {
                Debug.LogError("[Cutscene] Body Text가 연결되지 않아 자막을 표시할 수 없습니다.", this);
                yield break;
            }

            typewriter.Begin(shot.text, charactersPerSecond);
            yield return FadeTo(narrationGroup, 1f, layerFadeSeconds);

            while (typewriter.IsTyping)
            {
                if (ConfirmPressed())
                {
                    typewriter.Complete();
                    BlockInput(shotInputDelay);
                }
                else
                {
                    typewriter.Tick(Time.unscaledDeltaTime);
                }
                yield return null;
            }

            waitingNext = true;
            yield return WaitOrConfirm(hold + shot.text.Length * holdPerCharacter);
            waitingNext = false;
        }

        private IEnumerator EndRoutine(float seconds)
        {
            ending = true;
            waitingNext = false;
            yield return FadeTo(fade, 1f, seconds);

            PlayerPrefs.SetInt(SeenPrefix + data.id, 1);
            PlayerPrefs.Save();
            if (stage != null) stage.SetActive(false);

            bool loadNext = CanLoadNextScene();
            if (!loadNext) yield return FadeTo(fade, 0f, seconds);
            Complete(loadNext);
        }

        private void Complete(bool loadNext)
        {
            routine = null;
            IsPlaying = false;
            ending = false;
            UiStack.Remove(this);
            if (!loadNext) root.SetActive(false);

            Debug.Log($"[Cutscene] '{data.id}' 끝");
            Finished?.Invoke();
            if (loadNext) LoadNextScene();
        }

        private bool CanLoadNextScene()
        {
            if (string.IsNullOrEmpty(nextSceneName)) return false;
            if (Application.CanStreamedLevelBeLoaded(nextSceneName)) return true;

            Debug.LogError($"[Cutscene] 다음 씬 '{nextSceneName}'이 Build Settings에 없어 이동하지 않습니다.", this);
            return false;
        }

        private void LoadNextScene()
        {
            if (!CanLoadNextScene()) return;
            SceneManager.LoadScene(nextSceneName);
        }

        private IEnumerator WaitOrConfirm(float seconds)
        {
            float until = Time.unscaledTime + Mathf.Max(0f, seconds);
            while (Time.unscaledTime < until)
            {
                if (ConfirmPressed()) yield break;
                yield return null;
            }
        }

        private static IEnumerator FadeTo(CanvasGroup group, float target, float seconds)
        {
            if (group == null || Mathf.Approximately(group.alpha, target)) yield break;

            float start = group.alpha;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, elapsed / seconds);
                yield return null;
            }
            group.alpha = target;
        }

        private bool ConfirmPressed()
        {
            return !ending && Time.unscaledTime >= inputBlockedUntil && DialogueInput.ConfirmPressed();
        }

        private void BlockInput(float seconds)
        {
            inputBlockedUntil = Mathf.Max(inputBlockedUntil, Time.unscaledTime + seconds);
        }

        private void TickSkipHint()
        {
            if (skipHint == null) return;

            float target = !ending && Time.unscaledTime <= skipArmedUntil ? 1f : 0f;
            skipHint.alpha = Mathf.MoveTowards(skipHint.alpha, target, Time.unscaledDeltaTime / SkipHintFadeSeconds);
        }

        private void TickNextMark()
        {
            if (nextMark == null) return;
            nextMark.alpha = waitingNext ? 0.35f + 0.65f * Mathf.PingPong(Time.unscaledTime * NextMarkBlinkSpeed, 1f) : 0f;
        }

        private void OnCharacterRevealed(char character)
        {
            if (voice != null) voice.PlayCharacter(character);
        }

        private void Restart(IEnumerator next)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(next);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null) return;

            bool hasValue = !string.IsNullOrEmpty(value);
            target.text = hasValue ? value : "";
            target.gameObject.SetActive(hasValue);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        [ContextMenu("본 기록 지우기")]
        private void ClearSeen()
        {
            if (cutsceneJson == null || !CutsceneData.TryParse(cutsceneJson.text, out CutsceneData parsed, out _)) return;

            ResetSeen(parsed.id);
            Debug.Log($"[Cutscene] '{parsed.id}' 본 기록을 지웠습니다.");
        }

        [ContextMenu("처음부터 다시 재생 (Play 중)")]
        private void Replay()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Cutscene] Play 중에만 재생할 수 있습니다.", this);
                return;
            }

            Play();
        }
    }
}
