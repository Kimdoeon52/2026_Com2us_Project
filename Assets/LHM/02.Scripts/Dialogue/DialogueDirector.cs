using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public class DialogueDirector : MonoBehaviour
    {
        [Serializable]
        public class SoundEntry
        {
            public string id;
            public AudioClip clip;
        }

        private const float DefaultZoom = 1.3f;
        private const float DefaultShake = 0.3f;
        private const float DefaultFade = 1f;

        [Header("연결")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource bgmSource;

        [Header("소리 목록")]
        [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

        [Header("연출 시간")]
        [SerializeField, Min(0.01f)] private float zoomDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float shakeDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float endFadeDuration = 0.5f;

        [Header("대기 제한")]
        [SerializeField, Min(0f)] private float maxWaitSeconds = 3f;

        private Vector3 cameraBasePosition;
        private float cameraBaseSize;
        private bool hasCameraBase;

        private float zoomFrom = 1f;
        private float zoomTo = 1f;
        private float zoomElapsed;
        private float activeZoomDuration = 0.35f;
        private float currentZoom = 1f;
        private bool isZooming;

        private float shakeStrength;
        private float shakeElapsed;
        private bool isShaking;

        private float fadeFrom;
        private float fadeTo;
        private float fadeDuration;
        private float fadeElapsed;
        private bool isFading;

        private AudioClip fallbackClip;

        public float MaxWaitSeconds => maxWaitSeconds;
        public string CurrentBgmId { get; private set; }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (fadeOverlay != null)
            {
                fadeOverlay.raycastTarget = false;
                SetFadeAlpha(0f);
            }

            if (sfxSource != null) sfxSource.playOnAwake = false;
            if (bgmSource != null) bgmSource.playOnAwake = false;
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            UpdateZoom(deltaTime);
            UpdateShake(deltaTime);
            UpdateFade(deltaTime);
        }

        public void BeginDialogue()
        {
            CaptureCameraBase();
        }

        public void EndDialogue()
        {
            StartZoom(1f);
            StopShake();
            if (fadeOverlay != null && fadeOverlay.color.a > 0f) StartFade(0f, endFadeDuration);
        }

        public float Play(IReadOnlyList<EffectData> effects)
        {
            if (effects == null || effects.Count == 0) return 0f;

            float wait = 0f;
            foreach (EffectData effect in effects)
            {
                if (effect == null) continue;
                float duration = Execute(effect);
                if (effect.wait) wait = Mathf.Max(wait, duration);
            }

            if (wait > maxWaitSeconds)
            {
                Debug.LogWarning($"[Director] 연출 대기 {wait:0.0}초가 최대 대기 시간 {maxWaitSeconds:0.0}초를 넘어 {maxWaitSeconds:0.0}초만 기다립니다.", this);
                wait = maxWaitSeconds;
            }

            return wait;
        }

        private float Execute(EffectData effect)
        {
            switch (effect.type)
            {
                case DialogueEffectTypes.Zoom:
                    StartZoom(effect.value > 0f ? effect.value : DefaultZoom);
                    return zoomDuration;
                case DialogueEffectTypes.Shake:
                    StartShake(effect.value > 0f ? effect.value : DefaultShake);
                    return shakeDuration;
                case DialogueEffectTypes.FadeOut:
                    return StartFade(1f, effect.value > 0f ? effect.value : DefaultFade);
                case DialogueEffectTypes.FadeIn:
                    return StartFade(0f, effect.value > 0f ? effect.value : DefaultFade);
                case DialogueEffectTypes.Sound:
                    return PlaySound(effect.id);
                case DialogueEffectTypes.Bgm:
                    PlayBgm(effect.id);
                    return 0f;
                default:
                    Debug.LogWarning($"[Director] 알 수 없는 연출 '{effect.type}'", this);
                    return 0f;
            }
        }

        private void CaptureCameraBase()
        {
            if (targetCamera == null) return;
            cameraBasePosition = targetCamera.transform.position;
            cameraBaseSize = targetCamera.orthographic ? targetCamera.orthographicSize : targetCamera.fieldOfView;
            currentZoom = 1f;
            hasCameraBase = true;
        }

        public void ZoomTo(float target, float duration)
        {
            StartZoom(target);
            activeZoomDuration = Mathf.Max(0.01f, duration);
        }

        public void PlayBgmById(string id)
        {
            PlayBgm(id);
        }

        private void StartZoom(float target)
        {
            activeZoomDuration = zoomDuration;
            if (targetCamera == null) return;
            if (!hasCameraBase) CaptureCameraBase();
            zoomFrom = currentZoom;
            zoomTo = target;
            zoomElapsed = 0f;
            isZooming = true;
        }

        private void UpdateZoom(float deltaTime)
        {
            if (!isZooming || targetCamera == null) return;

            zoomElapsed += deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(zoomElapsed / activeZoomDuration));
            currentZoom = Mathf.Lerp(zoomFrom, zoomTo, t);
            ApplyCameraSize();

            if (zoomElapsed >= activeZoomDuration) isZooming = false;
        }

        private void ApplyCameraSize()
        {
            float size = cameraBaseSize / Mathf.Max(0.01f, currentZoom);
            if (targetCamera.orthographic) targetCamera.orthographicSize = size;
            else targetCamera.fieldOfView = size;
        }

        private void StartShake(float strength)
        {
            if (targetCamera == null) return;
            if (!hasCameraBase) CaptureCameraBase();
            shakeStrength = strength;
            shakeElapsed = 0f;
            isShaking = true;
        }

        private void UpdateShake(float deltaTime)
        {
            if (!isShaking || targetCamera == null) return;

            shakeElapsed += deltaTime;
            if (shakeElapsed >= shakeDuration)
            {
                StopShake();
                return;
            }

            float fade = 1f - shakeElapsed / shakeDuration;
            float time = Time.unscaledTime * 40f;
            float x = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f;
            targetCamera.transform.position = cameraBasePosition + new Vector3(x, y, 0f) * shakeStrength * fade;
        }

        private void StopShake()
        {
            if (!isShaking) return;
            isShaking = false;
            if (targetCamera != null) targetCamera.transform.position = cameraBasePosition;
        }

        private float StartFade(float targetAlpha, float duration)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[Director] Fade Overlay가 연결되지 않아 페이드를 건너뜁니다.", this);
                return 0f;
            }

            fadeFrom = fadeOverlay.color.a;
            fadeTo = targetAlpha;
            fadeDuration = Mathf.Max(0.01f, duration);
            fadeElapsed = 0f;
            isFading = true;
            return fadeDuration;
        }

        private void UpdateFade(float deltaTime)
        {
            if (!isFading || fadeOverlay == null) return;

            fadeElapsed += deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
            SetFadeAlpha(Mathf.Lerp(fadeFrom, fadeTo, t));

            if (t >= 1f) isFading = false;
        }

        private void SetFadeAlpha(float alpha)
        {
            Color color = fadeOverlay.color;
            color.a = alpha;
            fadeOverlay.color = color;
        }

        private float PlaySound(string id)
        {
            if (sfxSource == null)
            {
                Debug.LogWarning("[Director] Sfx Source가 연결되지 않아 효과음을 건너뜁니다.", this);
                return 0f;
            }

            AudioClip clip = FindClip(id);
            if (clip == null)
            {
                Debug.LogWarning($"[Director] 소리 목록에 없는 효과음 '{id}' — 대체 소리를 재생합니다.", this);
                clip = GetFallbackClip();
            }

            sfxSource.PlayOneShot(clip);
            return clip.length;
        }

        private void PlayBgm(string id)
        {
            if (bgmSource == null)
            {
                Debug.LogWarning("[Director] Bgm Source가 연결되지 않아 BGM을 건너뜁니다.", this);
                return;
            }

            if (string.IsNullOrEmpty(id) || id == "stop")
            {
                bgmSource.Stop();
                CurrentBgmId = null;
                return;
            }

            AudioClip clip = FindClip(id);
            if (clip == null)
            {
                Debug.LogWarning($"[Director] 소리 목록에 없는 BGM '{id}'", this);
                return;
            }

            CurrentBgmId = id;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        private AudioClip FindClip(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SoundEntry entry in sounds)
            {
                if (entry != null && entry.id == id) return entry.clip;
            }
            return null;
        }

        private AudioClip GetFallbackClip()
        {
            if (fallbackClip != null) return fallbackClip;

            const int sampleRate = 44100;
            int length = Mathf.RoundToInt(sampleRate * 0.15f);
            float[] samples = new float[length];
            System.Random random = new System.Random(7);

            for (int i = 0; i < length; i++)
            {
                float envelope = 1f - (float)i / length;
                samples[i] = ((float)random.NextDouble() * 2f - 1f) * 0.25f * envelope * envelope;
            }

            fallbackClip = AudioClip.Create("DialogueFallbackSfx", length, 1, sampleRate, false);
            fallbackClip.SetData(samples, 0);
            return fallbackClip;
        }
    }
}
