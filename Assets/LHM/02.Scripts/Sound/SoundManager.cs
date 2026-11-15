using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RealSteel.Sound
{
    // 공용 사운드 매니저 (BGM · 효과음)
    //
    // 쓰는 법
    //  - 프리팹(LHM/05.Prefab/Sound/SoundManager.prefab)을 씬마다 하나씩 넣고, 인스펙터의 '이 씬 BGM'만 바꿔 주면 됨
    //  - 처음 만들어진 매니저 하나만 씬이 바뀌어도 살아남아 실제로 소리를 냄 (BGM이 씬 전환에도 끊기지 않고 이어지거나 자연스럽게 바뀜)
    //  - 다음 씬에 놓인 매니저는 '대리'가 되어 자기 씬 BGM · 효과음 목록만 넘겨주고, 인스펙터 이벤트(버튼 · 슬라이더)도 그대로 받아 넘겨줌
    //  - 씬에 매니저가 없어도 코드에서 부르면 자동으로 하나 만들어짐
    //
    // 코드에서
    //  SoundManager.Sfx(clip);            SoundManager.Sfx("ui_click");          SoundManager.SfxAt(clip, transform.position);
    //  SoundManager.Bgm(clip);            SoundManager.Bgm("boss_theme");        SoundManager.StopBgm();
    //
    // 음량은 PlayerPrefs에 저장 (Heat 설정 슬라이더와 같은 키 형식: "Slider_" + 키). 전체 음량은 AudioListener.volume (메인 메뉴와 같은 키)
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public class SoundManager : MonoBehaviour
    {
        public const string MasterVolumeKey = "RealSteel Master Volume";
        public const string BgmVolumeKey = "RealSteel BGM Volume";
        public const string SfxVolumeKey = "RealSteel SFX Volume";
        private const string HeatSliderPrefix = "Slider_";

        [Header("이 씬 BGM")]
        [Tooltip("씬이 시작되면 이 곡으로 바꿈. 이미 같은 곡이 나오고 있으면 끊지 않고 이어감")]
        [SerializeField] private AudioClip sceneBgm;
        [Tooltip("이 씬 BGM이 비어 있으면 나오던 BGM을 멈춤 (끄면 이전 씬 BGM을 그대로 이어감)")]
        [SerializeField] private bool stopBgmIfEmpty;
        [Min(0f)] [SerializeField] private float bgmFadeSeconds = 1f;

        [Header("효과음 목록 (id로 부를 때)")]
        [SerializeField] private SoundLibrary library;

        [Header("믹서 그룹 (선택)")]
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("채널")]
        [Tooltip("동시에 낼 수 있는 효과음 수. 넘으면 가장 오래된 소리를 끊고 재생")]
        [Range(4, 64)] [SerializeField] private int sfxVoices = 16;
        [Tooltip("SfxAt(위치) 효과음이 거리에 따라 작아지는 정도 (최대 거리)")]
        [Min(1f)] [SerializeField] private float sfx3dMaxDistance = 25f;

        [Header("처음 실행 때 기본 음량")]
        [Range(0f, 1f)] [SerializeField] private float defaultBgmVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float defaultSfxVolume = 1f;

        private static SoundManager main;
        private static bool quitting;

        private readonly List<SoundLibrary> libraries = new List<SoundLibrary>();
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private readonly AudioSource[] bgmSources = new AudioSource[2];
        private int activeBgm;
        private float bgmClipVolume = 1f;
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        private Coroutine bgmFade;
        private bool isProxy;
        private bool initialized;

        // 실제로 소리를 내는 매니저. 없으면 만듦 (게임 종료 중에는 null)
        public static SoundManager Instance
        {
            get
            {
                if (main != null || quitting) return main;

                var go = new GameObject("[SoundManager]");
                main = go.AddComponent<SoundManager>();
                return main;
            }
        }

        public static bool Exists => main != null;

        public float BgmVolume => Main != null ? Main.bgmVolume : 0f;
        public float SfxVolume => Main != null ? Main.sfxVolume : 0f;
        public AudioClip CurrentBgm => Main != null && Main.initialized ? Main.bgmSources[Main.activeBgm].clip : null;
        public bool IsProxy => isProxy;

        private SoundManager Main => isProxy ? Instance : this;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            main = null;
            quitting = false;
        }

        private void Awake()
        {
            if (main != null && main != this)
            {
                isProxy = true;
                return;
            }

            main = this;
            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            Application.quitting += OnQuitting;
            Initialize();
        }

        private void Start()
        {
            SoundManager target = Main;
            if (target == null) return;

            target.AddLibrary(library);
            if (sceneBgm != null) target.PlayBgm(sceneBgm, bgmFadeSeconds);
            else if (stopBgmIfEmpty) target.StopBgmInternal(bgmFadeSeconds);
        }

        private void OnDestroy()
        {
            if (main != this) return;
            Application.quitting -= OnQuitting;
            main = null;
        }

        private static void OnQuitting()
        {
            quitting = true;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            for (int i = 0; i < bgmSources.Length; i++)
            {
                AudioSource source = CreateSource($"BGM {i + 1}", bgmGroup);
                source.loop = true;
                source.volume = 0f;
                bgmSources[i] = source;
            }

            for (int i = 0; i < sfxVoices; i++)
            {
                sfxPool.Add(CreateSource($"SFX {i + 1}", sfxGroup));
            }

            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(HeatSliderPrefix + BgmVolumeKey, defaultBgmVolume));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(HeatSliderPrefix + SfxVolumeKey, defaultSfxVolume));
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(HeatSliderPrefix + MasterVolumeKey, AudioListener.volume));
            AddLibrary(library);
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup group)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;
            source.spatialBlend = 0f;
            return source;
        }

        private void AddLibrary(SoundLibrary extra)
        {
            if (extra != null && !libraries.Contains(extra)) libraries.Add(extra);
        }

        private bool TryGetEntry(string id, out SoundLibrary.Entry entry)
        {
            // 나중에 들어온(현재 씬) 목록을 먼저 찾음 → 씬마다 같은 id를 다른 소리로 덮어쓸 수 있음
            for (int i = libraries.Count - 1; i >= 0; i--)
            {
                if (libraries[i] != null && libraries[i].TryGet(id, out entry)) return true;
            }
            entry = null;
            return false;
        }

        // ── 코드용 짧은 이름 ─────────────────────────────

        public static void Sfx(AudioClip clip, float volume = 1f, float pitch = 1f) => Instance?.PlaySfx(clip, volume, pitch);
        public static void Sfx(string id) => Instance?.PlaySfxById(id);
        public static void SfxAt(AudioClip clip, Vector3 position, float volume = 1f) => Instance?.PlaySfxAt(clip, position, volume);
        public static void SfxAt(string id, Vector3 position) => Instance?.PlaySfxAtById(id, position);
        public static void Bgm(AudioClip clip, float fadeSeconds = 1f) => Instance?.PlayBgm(clip, fadeSeconds);
        public static void Bgm(string id, float fadeSeconds = 1f) => Instance?.PlayBgmById(id, fadeSeconds);
        public static void StopBgm(float fadeSeconds = 1f) => Instance?.StopBgmInternal(fadeSeconds);

        // ── 효과음 ─────────────────────────────

        // 인스펙터 이벤트용 (버튼 OnClick 등에 클립을 끌어 넣기)
        public void PlaySfx(AudioClip clip) => PlaySfx(clip, 1f, 1f);

        public void PlaySfx(AudioClip clip, float volume, float pitch)
        {
            if (isProxy) { Main?.PlaySfx(clip, volume, pitch); return; }
            if (clip == null) return;

            AudioSource source = NextSfxSource();
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            Play(source, clip, volume, pitch);
        }

        public void PlaySfxById(string id)
        {
            if (isProxy) { Main?.PlaySfxById(id); return; }
            if (!TryGetEntry(id, out SoundLibrary.Entry entry))
            {
                Debug.LogWarning($"[Sound] 효과음 목록에 '{id}'가 없습니다.", this);
                return;
            }
            PlaySfx(entry.PickClip(), entry.volume, entry.PickPitch());
        }

        public void PlaySfxAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (isProxy) { Main?.PlaySfxAt(clip, position, volume); return; }
            if (clip == null) return;

            AudioSource source = NextSfxSource();
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = sfx3dMaxDistance;
            Play(source, clip, volume, 1f);
        }

        public void PlaySfxAtById(string id, Vector3 position)
        {
            if (isProxy) { Main?.PlaySfxAtById(id, position); return; }
            if (!TryGetEntry(id, out SoundLibrary.Entry entry))
            {
                Debug.LogWarning($"[Sound] 효과음 목록에 '{id}'가 없습니다.", this);
                return;
            }

            AudioClip clip = entry.PickClip();
            if (clip == null) return;
            PlaySfxAt(clip, position, entry.volume);
            sfxPool[lastSfx].pitch = entry.PickPitch();
        }

        public void StopAllSfx()
        {
            if (isProxy) { Main?.StopAllSfx(); return; }
            foreach (AudioSource source in sfxPool) source.Stop();
        }

        private int lastSfx = -1;

        private AudioSource NextSfxSource()
        {
            Initialize();

            int oldest = 0;
            float longest = -1f;
            for (int i = 0; i < sfxPool.Count; i++)
            {
                AudioSource source = sfxPool[i];
                if (!source.isPlaying)
                {
                    lastSfx = i;
                    return source;
                }

                if (source.time > longest)
                {
                    longest = source.time;
                    oldest = i;
                }
            }

            lastSfx = oldest;
            return sfxPool[oldest];
        }

        private void Play(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * sfxVolume;
            source.pitch = pitch;
            source.Play();
        }

        // ── BGM ─────────────────────────────

        public void PlayBgm(AudioClip clip) => PlayBgm(clip, bgmFadeSeconds);

        public void PlayBgm(AudioClip clip, float fadeSeconds)
        {
            if (isProxy) { Main?.PlayBgm(clip, fadeSeconds); return; }
            PlayBgmInternal(clip, 1f, fadeSeconds);
        }

        public void PlayBgmById(string id) => PlayBgmById(id, bgmFadeSeconds);

        public void PlayBgmById(string id, float fadeSeconds)
        {
            if (isProxy) { Main?.PlayBgmById(id, fadeSeconds); return; }
            if (!TryGetEntry(id, out SoundLibrary.Entry entry))
            {
                Debug.LogWarning($"[Sound] 소리 목록에 BGM '{id}'가 없습니다.", this);
                return;
            }
            PlayBgmInternal(entry.PickClip(), entry.volume, fadeSeconds);
        }

        // 인스펙터 이벤트용
        public void FadeOutBgm() => StopBgmInternal(bgmFadeSeconds);

        public void StopBgmInternal(float fadeSeconds)
        {
            if (isProxy) { Main?.StopBgmInternal(fadeSeconds); return; }
            Initialize();
            StartBgmFade(null, 0f, fadeSeconds);
        }

        public void PauseBgm()
        {
            if (isProxy) { Main?.PauseBgm(); return; }
            Initialize();
            bgmSources[activeBgm].Pause();
        }

        public void ResumeBgm()
        {
            if (isProxy) { Main?.ResumeBgm(); return; }
            Initialize();
            bgmSources[activeBgm].UnPause();
        }

        private void PlayBgmInternal(AudioClip clip, float clipVolume, float fadeSeconds)
        {
            Initialize();
            if (clip == null) return;

            AudioSource current = bgmSources[activeBgm];
            if (current.clip == clip && current.isPlaying)
            {
                bgmClipVolume = clipVolume;
                if (bgmFade == null) current.volume = bgmClipVolume * bgmVolume;
                return;
            }

            StartBgmFade(clip, clipVolume, fadeSeconds);
        }

        private void StartBgmFade(AudioClip next, float clipVolume, float fadeSeconds)
        {
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(CrossFade(next, clipVolume, fadeSeconds));
        }

        // 나오던 곡은 줄이고 새 곡은 키움 (일시정지 · 슬로모션 중에도 같은 속도로)
        private IEnumerator CrossFade(AudioClip next, float clipVolume, float seconds)
        {
            AudioSource from = bgmSources[activeBgm];
            AudioSource to = null;
            float fromStart = from.volume;

            if (next != null)
            {
                activeBgm = 1 - activeBgm;
                to = bgmSources[activeBgm];
                to.Stop();
                to.clip = next;
                to.volume = 0f;
                to.Play();
                bgmClipVolume = clipVolume;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                from.volume = Mathf.Lerp(fromStart, 0f, t);
                if (to != null) to.volume = Mathf.Lerp(0f, bgmClipVolume * bgmVolume, t);
                yield return null;
            }

            from.volume = 0f;
            from.Stop();
            if (to != null) to.volume = bgmClipVolume * bgmVolume;
            else from.clip = null;
            bgmFade = null;
        }

        // ── 음량 (설정 슬라이더 OnValueChanged에 연결) ─────────────────────────────

        public void SetMasterVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(HeatSliderPrefix + MasterVolumeKey, AudioListener.volume);
        }

        public void SetBgmVolume(float volume)
        {
            if (isProxy) { Main?.SetBgmVolume(volume); return; }
            Initialize();
            bgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(HeatSliderPrefix + BgmVolumeKey, bgmVolume);
            if (bgmFade == null) bgmSources[activeBgm].volume = bgmClipVolume * bgmVolume;
        }

        public void SetSfxVolume(float volume)
        {
            if (isProxy) { Main?.SetSfxVolume(volume); return; }
            Initialize();
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(HeatSliderPrefix + SfxVolumeKey, sfxVolume);
        }
    }
}
