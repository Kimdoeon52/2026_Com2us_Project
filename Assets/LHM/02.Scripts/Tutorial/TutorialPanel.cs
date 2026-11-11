using System;
using System.Collections;
using System.Collections.Generic;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RealSteel.Tutorial
{
    public class TutorialPanel : MonoBehaviour
    {
        private const string SeenPrefix = "RealSteel.Tutorial.Seen.";

        [Header("연결")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform window;
        [SerializeField] private TMP_Text guideTitleText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private RectTransform progressFill;

        [Header("영상 영역")]
        [SerializeField] private CanvasGroup mediaGroup;
        [SerializeField] private RectTransform mediaArea;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text placeholderText;

        [Header("설명")]
        [SerializeField] private CanvasGroup textGroup;
        [SerializeField] private TMP_Text stepTitleText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("버튼 · 단계 점")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button doneButton;
        [SerializeField] private Button dotTemplate;
        [SerializeField] private Color dotActive = new Color32(0xFF, 0xE5, 0x00, 0xFF);
        [SerializeField] private Color dotInactive = new Color32(0x3A, 0x3D, 0x41, 0xFF);
        [SerializeField] private Vector2 dotActiveSize = new Vector2(36f, 6f);
        [SerializeField] private Vector2 dotInactiveSize = new Vector2(14f, 6f);

        [Header("동작")]
        [SerializeField, Min(0f)] private float openSeconds = 0.2f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.15f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.25f;
        [SerializeField] private float slideDistance = 12f;
        [SerializeField, Min(0f)] private float openInputDelay = 0.2f;
        [Tooltip("닫으면 (스킵 포함) 본 것으로 기록해 OpenOnce로 다시 열리지 않음")]
        [SerializeField] private bool rememberSeen = true;

        private readonly List<Button> dots = new List<Button>();
        private TutorialGuide guide;
        private Action onClosed;
        private int current;
        private float openedAt;
        private bool closing;
        private Coroutine transition;
        private Vector2 mediaHome;
        private Vector2 textHome;
        private RenderTexture videoTexture;
        private Sprite[] frames;
        private float frameRate;
        private float frameClock;
        private float progressTarget;
        private float progressShown;

        public bool IsOpen { get; private set; }
        public TutorialGuide Guide => guide;
        public int CurrentStep => current;

        private void Awake()
        {
            if (root == null) root = gameObject;
            if (mediaGroup != null) mediaHome = ((RectTransform)mediaGroup.transform).anchoredPosition;
            if (textGroup != null) textHome = ((RectTransform)textGroup.transform).anchoredPosition;

            if (prevButton != null) prevButton.onClick.AddListener(() => Clicked(Prev));
            if (nextButton != null) nextButton.onClick.AddListener(() => Clicked(Next));
            if (doneButton != null) doneButton.onClick.AddListener(() => Clicked(Close));
            if (dotTemplate != null) dotTemplate.gameObject.SetActive(false);
            if (videoPlayer != null) videoPlayer.playOnAwake = false;
            if (root != gameObject) root.SetActive(false);
        }

        private void OnDisable()
        {
            if (IsOpen) Finish();
        }

        private void OnDestroy()
        {
            ReleaseVideoTexture();
        }

        private void Update()
        {
            if (!IsOpen) return;

            TickFrames();
            TickProgress();
            if (closing || Time.unscaledTime - openedAt < openInputDelay) return;

            Keyboard keyboard = Keyboard.current;
            bool left = keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame);
            bool right = keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame);
            bool confirm, cancel;

            InputModeController input = InputModeController.Current;
            if (input != null)
            {
                confirm = input.WasPressed(InputMode.Dialogue, "Select");
                cancel = input.WasPressed(InputMode.Dialogue, "Cancel");
            }
            else
            {
                confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame);
                cancel = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            }

            if (left) Prev();
            else if (right || confirm) Next();
            else if (cancel) Close();
        }

        public static bool HasSeen(string guideId)
        {
            return !string.IsNullOrEmpty(guideId) && PlayerPrefs.GetInt(SeenPrefix + guideId, 0) == 1;
        }

        public static void ResetSeen(string guideId)
        {
            if (!string.IsNullOrEmpty(guideId)) PlayerPrefs.DeleteKey(SeenPrefix + guideId);
        }

        private static void MarkSeen(string guideId)
        {
            if (string.IsNullOrEmpty(guideId)) return;
            PlayerPrefs.SetInt(SeenPrefix + guideId, 1);
            PlayerPrefs.Save();
        }

        // 처음 한 번만 열기. 이미 봤으면 열지 않고 false
        public bool OpenOnce(TutorialGuide source, Action closed = null)
        {
            if (source == null || HasSeen(source.guideId)) return false;
            return Open(source, closed);
        }

        public bool Open(TutorialGuide source, Action closed = null)
        {
            if (IsOpen) return false;
            if (source == null || source.steps == null || source.steps.Count == 0)
            {
                Debug.LogWarning("[Tutorial] 단계가 없는 튜토리얼이라 열지 않습니다.", this);
                return false;
            }

            guide = source;
            onClosed = closed;
            closing = false;
            current = 0;
            progressShown = 0f;
            frameClock = 0f;

            BuildDots();
            SetText(guideTitleText, guide.title);
            root.SetActive(true);
            IsOpen = true;
            openedAt = Time.unscaledTime;
            UiStack.Push(this);

            RefreshControls();
            ApplyStep();
            SetFade(1f);
            Restart(OpenRoutine());

            Debug.Log($"[Tutorial] '{guide.title}' 열림 ({guide.steps.Count}단계)");
            return true;
        }

        public void Prev()
        {
            GoTo(current - 1);
        }

        public void Next()
        {
            if (!IsOpen || closing) return;
            if (current >= guide.steps.Count - 1) Close();
            else GoTo(current + 1);
        }

        public void GoTo(int index)
        {
            if (!IsOpen || closing || index == current || index < 0 || index >= guide.steps.Count) return;

            current = index;
            RefreshControls();
            Restart(StepRoutine());
        }

        public void Close()
        {
            if (!IsOpen || closing) return;

            closing = true;
            bool completed = current >= guide.steps.Count - 1;
            if (rememberSeen) MarkSeen(guide.guideId);
            Debug.Log($"[Tutorial] '{guide.title}' {(completed ? "완료" : $"닫힘 ({current + 1}/{guide.steps.Count}단계에서 스킵)")}");
            Restart(CloseRoutine());
        }

        private void Finish()
        {
            StopVideo();
            frames = null;
            if (root != null && root != gameObject) root.SetActive(false);
            IsOpen = false;
            closing = false;
            UiStack.Remove(this);

            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void RefreshControls()
        {
            int count = guide.steps.Count;
            bool first = current == 0;
            bool last = current == count - 1;

            // 첫 단계: 이전 버튼 숨김 / 마지막 단계: 다음 버튼 자리에 '완료'
            if (prevButton != null) prevButton.gameObject.SetActive(!first);
            if (nextButton != null) nextButton.gameObject.SetActive(!last);
            if (doneButton != null) doneButton.gameObject.SetActive(last);

            SetText(counterText, $"{current + 1:00} <color=#5E6166>/ {count:00}</color>");
            progressTarget = (current + 1f) / count;

            for (int i = 0; i < dots.Count; i++)
            {
                bool active = i == current;
                if (dots[i].targetGraphic != null) dots[i].targetGraphic.color = active ? dotActive : dotInactive;
                ((RectTransform)dots[i].transform).sizeDelta = active ? dotActiveSize : dotInactiveSize;
            }
        }

        private void ApplyStep()
        {
            TutorialStep step = guide.steps[current];
            SetText(stepTitleText, step.title);
            SetText(descriptionText, step.description);
            ShowMedia(step);
        }

        private void BuildDots()
        {
            if (dotTemplate == null) return;

            int count = guide.steps.Count;
            while (dots.Count < count)
            {
                int index = dots.Count;
                Button dot = Instantiate(dotTemplate, dotTemplate.transform.parent);
                dot.name = $"Dot{index + 1}";
                dot.onClick.AddListener(() => Clicked(() => GoTo(index)));
                dots.Add(dot);
            }

            for (int i = 0; i < dots.Count; i++) dots[i].gameObject.SetActive(i < count);
        }

        private void ShowMedia(TutorialStep step)
        {
            StopVideo();
            frames = null;

            bool hasClip = step.clip != null && videoPlayer != null && videoImage != null;
            bool hasFrames = !hasClip && step.frames != null && step.frames.Length > 0;
            Sprite still = !hasClip && !hasFrames ? step.image : null;
            bool empty = !hasClip && !hasFrames && still == null;

            if (videoImage != null) videoImage.gameObject.SetActive(hasClip);
            if (frameImage != null) frameImage.gameObject.SetActive(hasFrames || still != null);
            if (placeholderText != null) placeholderText.gameObject.SetActive(empty);

            if (hasClip)
            {
                PlayVideo(step.clip);
            }
            else if (hasFrames && frameImage != null)
            {
                frames = step.frames;
                frameRate = Mathf.Max(1f, step.framesPerSecond);
                frameClock = 0f;
                frameImage.sprite = frames[0];
            }
            else if (still != null && frameImage != null)
            {
                frameImage.sprite = still;
            }
            else
            {
                SetText(placeholderText, $"영상 준비 중\n<size=50%>STEP {current + 1:00}</size>");
            }
        }

        private void PlayVideo(VideoClip clip)
        {
            int width = Mathf.Max(16, (int)clip.width);
            int height = Mathf.Max(16, (int)clip.height);
            if (videoTexture == null || videoTexture.width != width || videoTexture.height != height)
            {
                ReleaseVideoTexture();
                videoTexture = new RenderTexture(width, height, 0) { name = "TutorialVideo" };
                videoTexture.Create();
            }

            // 대화처럼 게임 시간이 멈춰도 영상은 계속 재생
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.isLooping = true;
            videoPlayer.clip = clip;
            videoImage.texture = videoTexture;
            FitInsideMedia(videoImage.rectTransform, width / (float)height);
            videoPlayer.Play();
        }

        private void StopVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        }

        private void ReleaseVideoTexture()
        {
            if (videoTexture == null) return;
            if (videoPlayer != null && videoPlayer.targetTexture == videoTexture) videoPlayer.targetTexture = null;
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
        }

        private void FitInsideMedia(RectTransform rect, float aspect)
        {
            if (mediaArea == null) return;

            Vector2 area = mediaArea.rect.size - new Vector2(8f, 8f);
            Vector2 size = aspect > area.x / area.y ? new Vector2(area.x, area.x / aspect) : new Vector2(area.y * aspect, area.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private void TickFrames()
        {
            if (frames == null || frameImage == null) return;

            frameClock += Time.unscaledDeltaTime;
            frameImage.sprite = frames[(int)(frameClock * frameRate) % frames.Length];
        }

        private void TickProgress()
        {
            if (progressFill == null) return;

            progressShown = Mathf.Lerp(progressShown, progressTarget, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            progressFill.anchorMax = new Vector2(progressShown, 1f);
        }

        private void Restart(IEnumerator routine)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(routine);
        }

        private IEnumerator OpenRoutine()
        {
            for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
            {
                SetOpen(EaseOut(t / openSeconds));
                yield return null;
            }
            SetOpen(1f);
            transition = null;
        }

        private IEnumerator StepRoutine()
        {
            yield return FadeTo(0f, fadeOutSeconds);
            ApplyStep();
            yield return FadeTo(1f, fadeInSeconds);
            transition = null;
        }

        private IEnumerator CloseRoutine()
        {
            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                SetOpen(1f - t / fadeOutSeconds);
                yield return null;
            }
            transition = null;
            Finish();
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            float start = mediaGroup != null ? mediaGroup.alpha : textGroup != null ? textGroup.alpha : 1f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(start, target, EaseOut(t / seconds)));
                yield return null;
            }
            SetFade(target);
        }

        // 영상과 설명이 살짝 아래로 내려가며 사라졌다가 다시 올라옴
        private void SetFade(float alpha)
        {
            Vector2 offset = Vector2.down * ((1f - alpha) * slideDistance);
            if (mediaGroup != null)
            {
                mediaGroup.alpha = alpha;
                ((RectTransform)mediaGroup.transform).anchoredPosition = mediaHome + offset;
            }
            if (textGroup != null)
            {
                textGroup.alpha = alpha;
                ((RectTransform)textGroup.transform).anchoredPosition = textHome + offset;
            }
        }

        private void SetOpen(float amount)
        {
            if (rootGroup != null) rootGroup.alpha = amount;
            if (window != null) window.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, amount);
        }

        // 클릭한 버튼이 선택된 채로 남으면 Space · Enter가 버튼도 눌러 두 칸씩 넘어가므로 선택을 풂
        private static void Clicked(Action action)
        {
            action();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }
    }
}
