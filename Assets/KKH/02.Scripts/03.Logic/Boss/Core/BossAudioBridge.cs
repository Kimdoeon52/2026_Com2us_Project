using UnityEngine;
using UnityEngine.Audio;
using RealSteel.Sound;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// 보스 전용 오디오 시스템(JunkWheelerSfx / KyoshoSfx)과 공용 SoundManager를 연동하는 브릿지 헬퍼.
    /// 1. SoundManager 초기화 및 저장된 Master/BGM/SFX 음량 연동(실시간 슬라이더 변경 반영 포함)
    /// 2. SoundManager가 사용 중인 BGM / SFX AudioMixerGroup 자동 상속 라우팅
    /// 3. 보스전 BGM 개시 시 기존 씬 SoundManager BGM 페이드 아웃 및 외부 신규 BGM 전환 시 양보 처리
    /// </summary>
    public static class BossAudioBridge
    {
        private static SoundManager _cachedManager;
        private static AudioMixerGroup _cachedBgmGroup;
        private static AudioMixerGroup _cachedSfxGroup;
        private static AudioClip _stoppedExternalBgm;
        private static float _externalStopGraceUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cachedManager = null;
            _cachedBgmGroup = null;
            _cachedSfxGroup = null;
            _stoppedExternalBgm = null;
            _externalStopGraceUntil = 0f;
        }

        public static SoundManager GetManager(bool createIfMissing = true)
        {
            if (!Application.isPlaying) return null;
            if (!createIfMissing && !SoundManager.Exists) return null;
            return SoundManager.Instance;
        }

        public static float GetBgmVolume()
        {
            var sm = GetManager(createIfMissing: true);
            return sm != null ? Mathf.Clamp01(sm.BgmVolume) : 1f;
        }

        public static float GetSfxVolume()
        {
            var sm = GetManager(createIfMissing: true);
            return sm != null ? Mathf.Clamp01(sm.SfxVolume) : 1f;
        }

        public static void ApplyBgmRouting(AudioSource source)
        {
            if (source == null) return;
            var group = GetBgmMixerGroup();
            if (group != null && source.outputAudioMixerGroup != group)
            {
                source.outputAudioMixerGroup = group;
            }
        }

        public static void ApplySfxRouting(AudioSource source)
        {
            if (source == null) return;
            var group = GetSfxMixerGroup();
            if (group != null && source.outputAudioMixerGroup != group)
            {
                source.outputAudioMixerGroup = group;
            }
        }

        /// <summary>
        /// 보스 전용 BGM을 시작하기 전에 공용 SoundManager에서 재생 중이던 기존 씬 BGM을 부드럽게 정지해 중첩을 방지함.
        /// </summary>
        public static void StopExternalSoundManagerBgm(float fadeSeconds)
        {
            if (!Application.isPlaying) return;

            var sm = GetManager(createIfMissing: true);
            if (sm == null) return;

            AudioClip activeExternal = sm.CurrentBgm;
            if (activeExternal == null)
            {
                _stoppedExternalBgm = null;
                _externalStopGraceUntil = 0f;
                return;
            }

            _stoppedExternalBgm = activeExternal;
            _externalStopGraceUntil = Time.unscaledTime + Mathf.Max(0.05f, fadeSeconds) + 0.15f;
            SoundManager.StopBgm(Mathf.Max(0f, fadeSeconds));
        }

        /// <summary>
        /// 외부 시스템이나 다음 씬의 SoundManager가 새로운 BGM을 시작한 경우 true를 반환해 보스 BGM이 양보할 수 있게 함.
        /// </summary>
        public static bool HasExternalBgmTakenOver()
        {
            if (!Application.isPlaying || !SoundManager.Exists) return false;
            var sm = SoundManager.Instance;
            if (sm == null) return false;

            AudioClip currentExternal = sm.CurrentBgm;
            if (currentExternal == null)
            {
                _stoppedExternalBgm = null;
                return false;
            }

            if (currentExternal == _stoppedExternalBgm && Time.unscaledTime <= _externalStopGraceUntil)
            {
                return false;
            }

            return true;
        }

        public static bool EnsureAudioHost(ref GameObject host, string hostName)
        {
            if (host != null) return false;

            EnsureSoundManagerInitialized();
            host = new GameObject(hostName);
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                host.AddComponent<AudioListener>();
            }
            return true;
        }

        public static void EnsureSoundManagerInitialized()
        {
            _ = GetManager(createIfMissing: true);
        }

        public static float ComputeStereoPan(Vector3 worldPos, float stereoSpread)
        {
            if (stereoSpread <= 0f) return 0f;
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * stereoSpread;
        }

        private static AudioMixerGroup GetBgmMixerGroup()
        {
            var sm = GetManager(createIfMissing: true);
            if (sm == null) return null;
            RefreshMixerCacheIfNeeded(sm);
            if (_cachedBgmGroup == null)
            {
                _cachedBgmGroup = FindChildMixerGroup(sm.transform, "BGM 1");
            }
            return _cachedBgmGroup;
        }

        private static AudioMixerGroup GetSfxMixerGroup()
        {
            var sm = GetManager(createIfMissing: true);
            if (sm == null) return null;
            RefreshMixerCacheIfNeeded(sm);
            if (_cachedSfxGroup == null)
            {
                _cachedSfxGroup = FindChildMixerGroup(sm.transform, "SFX 1");
            }
            return _cachedSfxGroup;
        }

        private static void RefreshMixerCacheIfNeeded(SoundManager sm)
        {
            if (_cachedManager == sm) return;
            _cachedManager = sm;
            _cachedBgmGroup = FindChildMixerGroup(sm.transform, "BGM 1");
            _cachedSfxGroup = FindChildMixerGroup(sm.transform, "SFX 1");
        }

        private static AudioMixerGroup FindChildMixerGroup(Transform root, string childName)
        {
            if (root == null) return null;
            Transform child = root.Find(childName);
            if (child == null) return null;
            var src = child.GetComponent<AudioSource>();
            return src != null ? src.outputAudioMixerGroup : null;
        }
    }
}
