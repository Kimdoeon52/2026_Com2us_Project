using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [정크 휠러(1보스) 전용 사운드 재생기 (JunkWheelerSfx)]
    /// 1. 16채널 원샷 AudioSource 풀링 + 위치 기반 2D 스테레오 패닝.
    /// 2. 전용 큐가 비어 있을 경우 지정된 폴백 큐(및 피치/볼륨 보정)로 자동 재생하여 빈 사운드 방지.
    /// 3. 루프 핸들(Loop/UpdateLoop/Stop/StopAllLoops)로 가속 전조(피치 상승), 톱날 회전, 그로기 루프를 안전하게 제어.
    /// 4. BGM 채널 및 대형 타격 시 순간 BGM 덕킹(DuckBgm) 지원.
    /// </summary>
    public static class JunkWheelerSfx
    {
        private const int OneShotVoiceCount = 16;

        private static JunkWheelerSfxData _data;
        private static GameObject _host;
        private static AudioSource _bgmSource;
        private static readonly List<AudioSource> _oneShotVoices = new();
        private static readonly List<AudioSource> _activeLoops = new();
        private static int _nextVoiceIndex;
        private static CancellationTokenSource _bgmDuckCts;
        private static CancellationTokenSource _bgmFadeCts;

        public static JunkWheelerSfxData Data => _data;

        public static void Bind(JunkWheelerSfxData sfxData)
        {
            _data = sfxData;
        }

        public static void Unbind()
        {
            StopAllLoops();
            StopBgmImmediate();
            _data = null;
        }

        // ========================================================================
        // 1. 단발(OneShot) 효과음
        // ========================================================================

        public static void Play(BossSfxCue cue, Vector3 worldPos, float pitch = 1f, float volumeScale = 1f)
        {
            if (!CanPlay(cue)) return;
            PlayInternal(cue, worldPos, pitch, volumeScale);
        }

        public static void PlayOrFallback(
            BossSfxCue primary,
            BossSfxCue fallback,
            Vector3 worldPos,
            float primaryPitch = 1f,
            float fallbackPitch = 1f,
            float fallbackVolumeScale = 1f)
        {
            if (!Application.isPlaying || _data == null) return;

            if (primary != null && primary.HasValidClip)
            {
                PlayInternal(primary, worldPos, primaryPitch, 1f);
            }
            else if (fallback != null && fallback.HasValidClip)
            {
                PlayInternal(fallback, worldPos, fallbackPitch, fallbackVolumeScale);
            }
        }

        private static void PlayInternal(BossSfxCue cue, Vector3 worldPos, float pitch, float volumeScale)
        {
            AudioClip clip = cue.PickRandomClip();
            if (clip == null) return;

            AudioSource source = AcquireOneShotVoice();
            if (source == null) return;

            ApplyCueToSource(source, cue, clip, worldPos, pitch, volumeScale);
            source.loop = false;
            source.time = 0f;
            source.Play();
        }

        // ========================================================================
        // 2. 지속/루프(Loop) 효과음
        // ========================================================================

        public static AudioSource Loop(
            BossSfxCue cue,
            Vector3 worldPos,
            float pitch = 1f,
            float volumeScale = 1f,
            bool randomStartTime = true)
        {
            if (!CanPlay(cue)) return null;
            return LoopInternal(cue, worldPos, pitch, volumeScale, randomStartTime);
        }

        public static AudioSource LoopOrFallback(
            BossSfxCue primary,
            BossSfxCue fallback,
            Vector3 worldPos,
            float primaryPitch = 1f,
            float fallbackPitch = 1f,
            float fallbackVolumeScale = 1f,
            bool randomStartTime = true)
        {
            if (!Application.isPlaying || _data == null) return null;

            if (primary != null && primary.HasValidClip)
            {
                return LoopInternal(primary, worldPos, primaryPitch, 1f, randomStartTime);
            }
            if (fallback != null && fallback.HasValidClip)
            {
                return LoopInternal(fallback, worldPos, fallbackPitch, fallbackVolumeScale, randomStartTime);
            }
            return null;
        }

        private static AudioSource LoopInternal(
            BossSfxCue cue,
            Vector3 worldPos,
            float pitch,
            float volumeScale,
            bool randomStartTime)
        {
            AudioClip clip = cue.PickRandomClip();
            if (clip == null) return null;

            EnsureHost();
            var source = _host.AddComponent<AudioSource>();
            ApplyCueToSource(source, cue, clip, worldPos, pitch, volumeScale);
            source.loop = true;
            if (randomStartTime && clip.length > 0.2f)
            {
                source.time = Random.Range(0f, clip.length * 0.8f);
            }
            source.Play();

            _activeLoops.Add(source);
            return source;
        }

        /// <summary>
        /// 재생 중인 루프 사운드의 좌우 스테레오 위치, 피치(가속 연출 등), 볼륨을 실시간 갱신함.
        /// </summary>
        public static void UpdateLoop(AudioSource loopSource, Vector3 worldPos, float pitch = -1f, float volume = -1f)
        {
            if (loopSource == null || _data == null) return;
            loopSource.panStereo = ComputeStereoPan(worldPos);
            if (pitch > 0f) loopSource.pitch = pitch;
            if (volume >= 0f) loopSource.volume = volume * _data.masterSfxVolume;
        }

        public static void Stop(ref AudioSource loopSource)
        {
            if (loopSource != null)
            {
                _activeLoops.Remove(loopSource);
                loopSource.Stop();
                Object.Destroy(loopSource);
            }
            loopSource = null;
        }

        public static void StopAllLoops()
        {
            for (int i = _activeLoops.Count - 1; i >= 0; i--)
            {
                AudioSource s = _activeLoops[i];
                if (s != null)
                {
                    s.Stop();
                    Object.Destroy(s);
                }
            }
            _activeLoops.Clear();
        }

        // ========================================================================
        // 3. 보스전 BGM & 순간 타격 덕킹(Ducking)
        // ========================================================================

        public static void PlayBgm(AudioClip clip, float fadeInDuration = 0.4f)
        {
            if (!Application.isPlaying || _data == null || clip == null) return;
            EnsureHost();
            EnsureBgmSource();

            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            CancelBgmFade();
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.panStereo = 0f;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.priority = 0;

            float targetVol = _data.bgmVolume;
            if (fadeInDuration <= 0.01f)
            {
                _bgmSource.volume = targetVol;
                _bgmSource.Play();
            }
            else
            {
                _bgmFadeCts = new CancellationTokenSource();
                FadeBgmVolumeAsync(0f, targetVol, fadeInDuration, true, _bgmFadeCts.Token).Forget();
            }
        }

        public static void StopBgm(float fadeOutDuration = 0.6f)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;

            CancelBgmFade();
            if (fadeOutDuration <= 0.01f)
            {
                StopBgmImmediate();
            }
            else
            {
                _bgmFadeCts = new CancellationTokenSource();
                FadeBgmVolumeAsync(_bgmSource.volume, 0f, fadeOutDuration, false, _bgmFadeCts.Token).Forget();
            }
        }

        public static void DuckBgm(float duration = 0.16f, float duckRatio = 0.45f)
        {
            if (!Application.isPlaying || _data == null || _bgmSource == null || !_bgmSource.isPlaying) return;

            CancelBgmDuck();
            _bgmDuckCts = new CancellationTokenSource();
            RunBgmDuckAsync(duration, Mathf.Clamp01(duckRatio), _bgmDuckCts.Token).Forget();
        }

        private static void StopBgmImmediate()
        {
            CancelBgmFade();
            CancelBgmDuck();
            if (_bgmSource != null)
            {
                _bgmSource.Stop();
            }
        }

        private static async UniTaskVoid FadeBgmVolumeAsync(
            float from,
            float to,
            float duration,
            bool startPlayback,
            CancellationToken token)
        {
            if (_bgmSource == null) return;
            if (startPlayback)
            {
                _bgmSource.volume = from;
                _bgmSource.Play();
            }

            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
                if (_bgmSource != null)
                {
                    _bgmSource.volume = to;
                    if (to <= 0.001f && !startPlayback) _bgmSource.Stop();
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private static async UniTaskVoid RunBgmDuckAsync(float duration, float duckRatio, CancellationToken token)
        {
            if (_bgmSource == null || _data == null) return;
            float baseVol = _data.bgmVolume;
            _bgmSource.volume = baseVol * duckRatio;

            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null && _data != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                    _bgmSource.volume = Mathf.Lerp(baseVol * duckRatio, _data.bgmVolume, t);
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: token);
                }
                if (_bgmSource != null && _data != null)
                {
                    _bgmSource.volume = _data.bgmVolume;
                }
            }
            catch (System.OperationCanceledException) { }
        }

        // ========================================================================
        // 4. 내부 유틸리티
        // ========================================================================

        private static bool CanPlay(BossSfxCue cue)
        {
            return Application.isPlaying && _data != null && cue != null && cue.HasValidClip;
        }

        private static void ApplyCueToSource(
            AudioSource source,
            BossSfxCue cue,
            AudioClip clip,
            Vector3 worldPos,
            float pitch,
            float volumeScale)
        {
            source.clip = clip;
            source.volume = Mathf.Clamp01(cue.volume * volumeScale * _data.masterSfxVolume);
            float jitter = cue.pitchJitter > 0f ? Random.Range(-cue.pitchJitter, cue.pitchJitter) : 0f;
            source.pitch = Mathf.Clamp(pitch * (1f + jitter), 0.35f, 2.5f);
            source.panStereo = ComputeStereoPan(worldPos);
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }

        private static float ComputeStereoPan(Vector3 worldPos)
        {
            if (_data == null) return 0f;
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * _data.stereoSpread;
        }

        private static AudioSource AcquireOneShotVoice()
        {
            EnsureHost();
            _oneShotVoices.RemoveAll(v => v == null);
            while (_oneShotVoices.Count < OneShotVoiceCount)
            {
                var voice = _host.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                _oneShotVoices.Add(voice);
            }

            for (int i = 0; i < _oneShotVoices.Count; i++)
            {
                int idx = (_nextVoiceIndex + i) % _oneShotVoices.Count;
                if (!_oneShotVoices[idx].isPlaying)
                {
                    _nextVoiceIndex = (idx + 1) % _oneShotVoices.Count;
                    return _oneShotVoices[idx];
                }
            }

            AudioSource oldest = _oneShotVoices[_nextVoiceIndex];
            _nextVoiceIndex = (_nextVoiceIndex + 1) % _oneShotVoices.Count;
            return oldest;
        }

        private static void EnsureHost()
        {
            if (_host != null) return;

            _host = new GameObject("[JunkWheeler_AudioHost]");
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                _host.AddComponent<AudioListener>();
            }
            _oneShotVoices.Clear();
            _activeLoops.Clear();
            _bgmSource = null;
            _nextVoiceIndex = 0;
        }

        private static void EnsureBgmSource()
        {
            if (_bgmSource != null) return;
            EnsureHost();
            _bgmSource = _host.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
        }

        private static void CancelBgmFade()
        {
            if (_bgmFadeCts != null)
            {
                if (!_bgmFadeCts.IsCancellationRequested) _bgmFadeCts.Cancel();
                _bgmFadeCts.Dispose();
                _bgmFadeCts = null;
            }
        }

        private static void CancelBgmDuck()
        {
            if (_bgmDuckCts != null)
            {
                if (!_bgmDuckCts.IsCancellationRequested) _bgmDuckCts.Cancel();
                _bgmDuckCts.Dispose();
                _bgmDuckCts = null;
            }
        }
    }
}
