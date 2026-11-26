using UnityEngine;
using UnityEngine.Audio;
using RealSteel.Sound;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// 공용 LHM SoundManager를 KKH.Boss.Audio.Core의 IBossAudioBackend에 자동 등록하는 런타임 어댑터.
    /// </summary>
    internal sealed class BossSoundManagerBackend : IBossAudioBackend
    {
        private SoundManager _cachedManager;
        private AudioMixerGroup _cachedBgmGroup;
        private AudioMixerGroup _cachedSfxGroup;
        private AudioClip _stoppedExternalBgm;
        private float _externalStopGraceUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnStartup()
        {
            BossAudioBridge.RegisterBackend(new BossSoundManagerBackend());
        }

        private SoundManager GetManager(bool createIfMissing = true)
        {
            if (!Application.isPlaying) return null;
            if (!createIfMissing && !SoundManager.Exists) return null;
            return SoundManager.Instance;
        }

        public void EnsureInitialized()
        {
            _ = GetManager(createIfMissing: true);
        }

        public float GetBgmVolume()
        {
            var sm = GetManager(createIfMissing: true);
            return sm != null ? Mathf.Clamp01(sm.BgmVolume) : 1f;
        }

        public float GetSfxVolume()
        {
            var sm = GetManager(createIfMissing: true);
            return sm != null ? Mathf.Clamp01(sm.SfxVolume) : 1f;
        }

        public AudioMixerGroup GetBgmMixerGroup()
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

        public AudioMixerGroup GetSfxMixerGroup()
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

        public void StopExternalBgm(float fadeSeconds)
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

        public bool HasExternalBgmTakenOver()
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

        private void RefreshMixerCacheIfNeeded(SoundManager sm)
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
