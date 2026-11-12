using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 전용 리듬/오케스트라 사운드 재생기]
    /// 16채널 AudioSource 풀 + 스테레오 패닝 + BGM 덕킹 + SO 미할당 슬롯용 절차적 신디사이저 폴백 내장.
    /// </summary>
    public static class KyoshoSfx
    {
        private const int OneShotVoiceCount = 16;
        private const int SampleRate = 22050;

        private static KyoshoSfxData _data;
        private static GameObject _host;
        private static AudioSource _bgmSource;
        private static readonly List<AudioSource> _voices = new();
        private static readonly Dictionary<string, AudioClip> _synthCache = new();
        private static int _nextVoiceIndex;
        private static CancellationTokenSource _duckCts;
        private static CancellationTokenSource _fadeCts;

        public static KyoshoSfxData Data => _data;

        public static void Bind(KyoshoSfxData data)
        {
            _data = data;
        }

        public static void Unbind()
        {
            StopBgmImmediate();
            _data = null;
        }

        // ====================================================================
        // 특화 재생 API (SO 클립 우선, 미할당 시 절차적 음원 폴백)
        // ====================================================================

        public static void PlayBeatTick(bool isDownbeat)
        {
            if (!Application.isPlaying) return;
            var cue = _data != null ? (isDownbeat ? _data.beatDownbeat : _data.beatSub) : null;
            if (cue != null && cue.HasValidClip)
            {
                PlayClip(cue.PickRandomClip(), Vector3.zero, cue.volume, 1f);
                return;
            }

            string key = isDownbeat ? "beat_down" : "beat_sub";
            AudioClip synth = GetOrCreateSynth(key, () =>
                isDownbeat
                    ? BuildSynthTone(196f, 0.085f, 0.42f, harmonic2: 0.35f, expDecay: 24f) // 팀파니/우드블록 강박
                    : BuildSynthTone(880f, 0.045f, 0.22f, harmonic2: 0.10f, expDecay: 42f)); // 우드블록 약박
            PlayClip(synth, Vector3.zero, isDownbeat ? 0.55f : 0.32f, 1f);
        }

        public static void PlaySolfegeNote(int symbolIndex, Vector3 worldPos, float volumeScale = 1f)
        {
            if (!Application.isPlaying) return;
            int idx = Mathf.Clamp(symbolIndex, 0, KyoshoConstants.SolfegeFrequencies.Length - 1);
            if (_data != null && _data.variationMelodyNotes != null && idx < _data.variationMelodyNotes.Length)
            {
                var cue = _data.variationMelodyNotes[idx];
                if (cue != null && cue.HasValidClip)
                {
                    PlayClip(cue.PickRandomClip(), worldPos, cue.volume * volumeScale, 1f);
                    return;
                }
            }

            float freq = KyoshoConstants.SolfegeFrequencies[idx];
            AudioClip synth = GetOrCreateSynth($"solfege_{idx}", () =>
                BuildSynthTone(freq, 0.36f, 0.50f, harmonic2: 0.35f, harmonic3: 0.15f, expDecay: 7.5f));
            PlayClip(synth, worldPos, 0.70f * volumeScale, 1f);
        }

        public static void PlayStaccatoCall(int pitchIndex, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            if (_data != null && _data.staccatoCallNote != null && _data.staccatoCallNote.HasValidClip)
            {
                float pitch = KyoshoConstants.SolfegeFrequencies[Mathf.Clamp(pitchIndex, 0, 5)] / 261.63f;
                PlayClip(_data.staccatoCallNote.PickRandomClip(), worldPos, _data.staccatoCallNote.volume, pitch);
                return;
            }
            PlaySolfegeNote(pitchIndex, worldPos, 0.75f);
        }

        public static void PlayCue(KyoshoSfxCue cue, string fallbackKey, Vector3 worldPos, float pitch = 1f, float volumeScale = 1f)
        {
            if (!Application.isPlaying) return;
            if (cue != null && cue.HasValidClip)
            {
                float jitter = cue.pitchJitter > 0f ? Random.Range(-cue.pitchJitter, cue.pitchJitter) : 0f;
                PlayClip(cue.PickRandomClip(), worldPos, cue.volume * volumeScale, pitch * (1f + jitter));
                return;
            }

            AudioClip synth = GetOrCreateFallbackCue(fallbackKey);
            if (synth != null)
            {
                float baseVol = cue != null ? cue.volume : 0.7f;
                PlayClip(synth, worldPos, baseVol * volumeScale, pitch);
            }
        }

        // ====================================================================
        // BGM & 덕킹
        // ====================================================================

        public static void PlayBgm(AudioClip clip, float fadeIn = 0.4f)
        {
            if (!Application.isPlaying || clip == null) return;
            EnsureHost();
            EnsureBgmSource();
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

            CancelFade();
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.panStereo = 0f;
            _bgmSource.spatialBlend = 0f;
            float targetVol = _data != null ? _data.bgmVolume : 0.4f;

            if (fadeIn <= 0.01f)
            {
                _bgmSource.volume = targetVol;
                _bgmSource.Play();
            }
            else
            {
                _fadeCts = new CancellationTokenSource();
                FadeBgmAsync(0f, targetVol, fadeIn, true, _fadeCts.Token).Forget();
            }
        }

        public static void StopBgm(float fadeOut = 0.5f)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;
            CancelFade();
            if (fadeOut <= 0.01f) StopBgmImmediate();
            else
            {
                _fadeCts = new CancellationTokenSource();
                FadeBgmAsync(_bgmSource.volume, 0f, fadeOut, false, _fadeCts.Token).Forget();
            }
        }

        public static void DuckBgm(float duration = 0.2f, float duckRatio = 0.4f)
        {
            if (!Application.isPlaying || _bgmSource == null || !_bgmSource.isPlaying) return;
            CancelDuck();
            _duckCts = new CancellationTokenSource();
            RunDuckAsync(duration, Mathf.Clamp01(duckRatio), _duckCts.Token).Forget();
        }

        private static void StopBgmImmediate()
        {
            CancelFade();
            CancelDuck();
            if (_bgmSource != null) _bgmSource.Stop();
        }

        private static async UniTaskVoid FadeBgmAsync(float from, float to, float duration, bool startPlay, CancellationToken token)
        {
            if (_bgmSource == null) return;
            if (startPlay) { _bgmSource.volume = from; _bgmSource.Play(); }
            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                if (_bgmSource != null)
                {
                    _bgmSource.volume = to;
                    if (to <= 0.001f && !startPlay) _bgmSource.Stop();
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private static async UniTaskVoid RunDuckAsync(float duration, float ratio, CancellationToken token)
        {
            float baseVol = _data != null ? _data.bgmVolume : 0.4f;
            _bgmSource.volume = baseVol * ratio;
            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(baseVol * ratio, baseVol, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                if (_bgmSource != null) _bgmSource.volume = baseVol;
            }
            catch (System.OperationCanceledException) { }
        }

        // ====================================================================
        // 내부 오디오 풀링 & 절차적 음향 합성
        // ====================================================================

        private static void PlayClip(AudioClip clip, Vector3 worldPos, float volume, float pitch)
        {
            if (clip == null) return;
            AudioSource src = AcquireVoice();
            if (src == null) return;

            float master = _data != null ? _data.masterSfxVolume : 0.85f;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * master);
            src.pitch = Mathf.Clamp(pitch, 0.35f, 2.5f);
            src.panStereo = ComputePan(worldPos);
            src.spatialBlend = 0f;
            src.loop = false;
            src.time = 0f;
            src.Play();
        }

        private static float ComputePan(Vector3 worldPos)
        {
            float spread = _data != null ? _data.stereoSpread : 0.5f;
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * spread;
        }

        private static AudioSource AcquireVoice()
        {
            EnsureHost();
            _voices.RemoveAll(v => v == null);
            while (_voices.Count < OneShotVoiceCount)
            {
                var s = _host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _voices.Add(s);
            }

            for (int i = 0; i < _voices.Count; i++)
            {
                int idx = (_nextVoiceIndex + i) % _voices.Count;
                if (!_voices[idx].isPlaying)
                {
                    _nextVoiceIndex = (idx + 1) % _voices.Count;
                    return _voices[idx];
                }
            }

            AudioSource oldest = _voices[_nextVoiceIndex];
            _nextVoiceIndex = (_nextVoiceIndex + 1) % _voices.Count;
            return oldest;
        }

        private static void EnsureHost()
        {
            if (_host != null) return;
            _host = new GameObject("[Kyosho_AudioHost]");
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                _host.AddComponent<AudioListener>();
            }
            _voices.Clear();
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

        private static void CancelFade()
        {
            if (_fadeCts != null)
            {
                if (!_fadeCts.IsCancellationRequested) _fadeCts.Cancel();
                _fadeCts.Dispose();
                _fadeCts = null;
            }
        }

        private static void CancelDuck()
        {
            if (_duckCts != null)
            {
                if (!_duckCts.IsCancellationRequested) _duckCts.Cancel();
                _duckCts.Dispose();
                _duckCts = null;
            }
        }

        private static AudioClip GetOrCreateSynth(string key, System.Func<AudioClip> factory)
        {
            if (_synthCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var created = factory();
            _synthCache[key] = created;
            return created;
        }

        private static AudioClip GetOrCreateFallbackCue(string key)
        {
            return key switch
            {
                "conduct_whoosh" => GetOrCreateSynth(key, () => BuildNoiseWhoosh(0.16f, 0.38f)),
                "conduct_flash" => GetOrCreateSynth(key, () => BuildSynthTone(1046.5f, 0.09f, 0.30f, harmonic2: 0.4f, expDecay: 28f)),
                "percussion_shock" => GetOrCreateSynth(key, () => BuildDrumImpact(0.28f, 130f, 45f, 0.65f)),
                "staccato_fire" => GetOrCreateSynth(key, () => BuildSynthTone(659.25f, 0.11f, 0.35f, harmonic2: 0.45f, expDecay: 22f)),
                "strings_fire" => GetOrCreateSynth(key, () => BuildSynthTone(987.77f, 0.08f, 0.32f, harmonic2: 0.6f, expDecay: 26f)),
                "bloom_toss" => GetOrCreateSynth(key, () => BuildNoiseWhoosh(0.14f, 0.30f)),
                "bloom_grow" => GetOrCreateSynth(key, () => BuildSynthTone(330f, 0.22f, 0.24f, harmonic2: 0.3f, expDecay: 9f)),
                "bloom_cut" => GetOrCreateSynth(key, () => BuildDrumImpact(0.18f, 320f, 90f, 0.55f)),
                "bloom_note" => GetOrCreateSynth(key, () => BuildSynthTone(523.25f, 0.18f, 0.42f, harmonic2: 0.35f, expDecay: 14f)),
                "bloom_trumpet" => GetOrCreateSynth(key, () => BuildChord(0.32f, 0.45f, 349.23f, 523.25f)),
                "bloom_cymbal" => GetOrCreateSynth(key, () => BuildNoiseWhoosh(0.35f, 0.50f)),
                "fermata_spawn" => GetOrCreateSynth(key, () => BuildSynthTone(783.99f, 0.18f, 0.34f, harmonic2: 0.25f, expDecay: 12f)),
                "fermata_hold" => GetOrCreateSynth(key, () => BuildChord(0.65f, 0.36f, 261.63f, 329.63f, 392.00f)),
                "fermata_drop" => GetOrCreateSynth(key, () => BuildDrumImpact(0.35f, 180f, 40f, 0.72f)),
                "brass_beam" => GetOrCreateSynth(key, () => BuildChord(0.42f, 0.48f, 220f, 329.63f, 440f)),
                "variation_ok" => GetOrCreateSynth(key, () => BuildChord(0.45f, 0.48f, 523.25f, 659.25f, 783.99f)),
                "variation_fail" => GetOrCreateSynth(key, () => BuildChord(0.55f, 0.62f, 138.59f, 196.00f, 277.18f, 405f)),
                "finale_gather" => GetOrCreateSynth(key, () => BuildChord(0.80f, 0.40f, 220f, 277.18f, 329.63f, 440f)),
                "finale_blast" => GetOrCreateSynth(key, () => BuildDrumImpact(0.60f, 220f, 32f, 0.85f)),
                "section_broken" => GetOrCreateSynth(key, () => BuildDrumImpact(0.32f, 260f, 55f, 0.70f)),
                _ => null
            };
        }

        private static AudioClip BuildSynthTone(float freq, float dur, float amp, float harmonic2 = 0.2f, float harmonic3 = 0f, float expDecay = 10f)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.006f) * Mathf.Exp(-expDecay * t);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t)
                           + harmonic2 * Mathf.Sin(4f * Mathf.PI * freq * t)
                           + harmonic3 * Mathf.Sin(6f * Mathf.PI * freq * t);
                data[i] = Mathf.Clamp(wave * env * amp * 0.6f, -1f, 1f);
            }
            var clip = AudioClip.Create($"Synth_{freq:F0}", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildChord(float dur, float amp, params float[] freqs)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float inv = 1f / Mathf.Max(1, freqs.Length);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.015f) * Mathf.Exp(-4.5f * t);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[f] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freqs[f] * t);
                }
                data[i] = Mathf.Clamp(sum * inv * env * amp, -1f, 1f);
            }
            var clip = AudioClip.Create("SynthChord", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildDrumImpact(float dur, float startFreq, float endFreq, float amp)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float phase = 0f;
            uint seed = 0x12345678u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / dur;
                float freq = Mathf.Lerp(startFreq, endFreq, Mathf.Sqrt(k));
                phase += 2f * Mathf.PI * freq / SampleRate;
                seed = seed * 1664525u + 1013904223u;
                float noise = ((seed & 0xFFFF) / 32768f - 1f) * Mathf.Exp(-20f * t);
                float env = Mathf.Exp(-9f * t);
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.8f + noise * 0.45f) * env * amp, -1f, 1f);
            }
            var clip = AudioClip.Create("SynthImpact", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildNoiseWhoosh(float dur, float amp)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            uint seed = 0x87654321u;
            float lp = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / dur;
                float env = Mathf.Sin(Mathf.PI * k);
                seed = seed * 1664525u + 1013904223u;
                float n = (seed & 0xFFFF) / 32768f - 1f;
                lp = Mathf.Lerp(lp, n, 0.25f);
                data[i] = Mathf.Clamp(lp * env * amp, -1f, 1f);
            }
            var clip = AudioClip.Create("SynthWhoosh", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
