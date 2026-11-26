using UnityEngine;
using UnityEngine.Audio;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// 보스 오디오 어셈블리(KKH.Boss.Audio.Core)와 공용 사운드 매니저 사이의 의존성 역전(DIP) 백엔드 인터페이스.
    /// </summary>
    public interface IBossAudioBackend
    {
        void EnsureInitialized();
        float GetBgmVolume();
        float GetSfxVolume();
        AudioMixerGroup GetBgmMixerGroup();
        AudioMixerGroup GetSfxMixerGroup();
        void StopExternalBgm(float fadeSeconds);
        bool HasExternalBgmTakenOver();
    }

    /// <summary>
    /// 보스 전용 오디오 시스템(JunkWheelerSfx / KyoshoSfx)과 공용 SoundManager를 연동하는 브릿지 헬퍼.
    /// 1. 등록된 IBossAudioBackend를 통한 Master/BGM/SFX 음량 연동(실시간 슬라이더 변경 반영 포함)
    /// 2. SoundManager가 사용 중인 BGM / SFX AudioMixerGroup 자동 상속 라우팅
    /// 3. 보스전 BGM 개시 시 기존 씬 SoundManager BGM 페이드 아웃 및 외부 신규 BGM 전환 시 양보 처리
    /// </summary>
    public static class BossAudioBridge
    {
        private static IBossAudioBackend _backend;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _backend = null;
        }

        public static void RegisterBackend(IBossAudioBackend backend)
        {
            _backend = backend;
        }

        public static float GetBgmVolume()
        {
            if (!Application.isPlaying || _backend == null) return 1f;
            return Mathf.Clamp01(_backend.GetBgmVolume());
        }

        public static float GetSfxVolume()
        {
            if (!Application.isPlaying || _backend == null) return 1f;
            return Mathf.Clamp01(_backend.GetSfxVolume());
        }

        public static void ApplyBgmRouting(AudioSource source)
        {
            if (source == null || !Application.isPlaying || _backend == null) return;
            var group = _backend.GetBgmMixerGroup();
            if (group != null && source.outputAudioMixerGroup != group)
            {
                source.outputAudioMixerGroup = group;
            }
        }

        public static void ApplySfxRouting(AudioSource source)
        {
            if (source == null || !Application.isPlaying || _backend == null) return;
            var group = _backend.GetSfxMixerGroup();
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
            if (!Application.isPlaying || _backend == null) return;
            _backend.StopExternalBgm(fadeSeconds);
        }

        /// <summary>
        /// 외부 시스템이나 다음 씬의 SoundManager가 새로운 BGM을 시작한 경우 true를 반환해 보스 BGM이 양보할 수 있게 함.
        /// </summary>
        public static bool HasExternalBgmTakenOver()
        {
            return Application.isPlaying && _backend != null && _backend.HasExternalBgmTakenOver();
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
            if (!Application.isPlaying || _backend == null) return;
            _backend.EnsureInitialized();
        }

        public static float ComputeStereoPan(Vector3 worldPos, float stereoSpread)
        {
            if (stereoSpread <= 0f) return 0f;
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * stereoSpread;
        }
    }
}
