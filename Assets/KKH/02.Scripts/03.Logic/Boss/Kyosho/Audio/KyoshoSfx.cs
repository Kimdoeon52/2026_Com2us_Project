using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 전용 리듬/오케스트라 사운드 재생기]
    /// 16채널 AudioSource 풀 + 스테레오 패닝 + BGM 덕킹 + SO 미할당 슬롯용 절차적 신디사이저 폴백 내장.
    /// </summary>
    public static class KyoshoSfx
    {
        private const int OneShotVoiceCount = 28;
        private const int SampleRate = 22050;
        private const float ReferenceC3Hz = 130.81f;
        private const float ReferenceC4Hz = 261.63f;
        private const float ReferenceC5Hz = 523.25f;

        // C Major / A Minor 다이어토닉 기준 옥타브(C4~B4) 및 확장 스케일 테이블
        private static readonly float[] DiatonicOctaveHz =
        {
            261.63f, 293.66f, 329.63f, 349.23f, 392.00f, 440.00f, 493.88f
        };

        private static readonly float[] MelodyScaleHz =
        {
            261.63f, 293.66f, 329.63f, 349.23f, 392.00f, 440.00f, 493.88f,
            523.25f, 587.33f, 659.25f, 698.46f, 783.99f, 880.00f, 987.77f, 1046.50f
        };

        // 4마디 클래식 화성 진행 (I: C, V: G, vi: Am, IV: F) — (베이스 degree, 3화음 아르페지오 degree[3])
        private static readonly int[] BarBassDegrees = { -7, -3, -2, -4 }; // C3, G3, A3, F3
        private static readonly int[,] BarArpeggioDegrees =
        {
            { 7, 9, 11 }, // Bar 0 (I:  C5, E5, G5)
            { 4, 6, 8 },  // Bar 1 (V:  G4, B4, D5)
            { 5, 7, 9 },  // Bar 2 (vi: A4, C5, E5)
            { 3, 5, 7 }   // Bar 3 (IV: F4, A4, C5)
        };

        // 피날레 압박 탄막 16단계 심포닉 오스티나토 (Am -> F/C -> Dm -> E7 -> E5 해결)
        private static readonly (float leadHz, float harmHz, float bassHz)[] FinaleOstinatoTable =
        {
            (659.25f, 523.25f, 220.00f), // 0:  E5 + C5 / A3 (Am)
            (587.33f, 440.00f, 220.00f), // 1:  D5 + A4 / A3
            (523.25f, 440.00f, 220.00f), // 2:  C5 + A4 / A3
            (493.88f, 392.00f, 220.00f), // 3:  B4 + G4 / A3
            (440.00f, 349.23f, 174.61f), // 4:  A4 + F4 / F3 (F)
            (493.88f, 392.00f, 196.00f), // 5:  B4 + G4 / G3
            (523.25f, 440.00f, 261.63f), // 6:  C5 + A4 / C4 (C)
            (659.25f, 523.25f, 261.63f), // 7:  E5 + C5 / C4
            (698.46f, 587.33f, 293.66f), // 8:  F5 + D5 / D4 (Dm)
            (659.25f, 523.25f, 293.66f), // 9:  E5 + C5 / D4
            (587.33f, 440.00f, 293.66f), // 10: D5 + A4 / D4
            (523.25f, 440.00f, 293.66f), // 11: C5 + A4 / D4
            (493.88f, 415.30f, 164.81f), // 12: B4 + G#4 / E3 (E7)
            (523.25f, 440.00f, 164.81f), // 13: C5 + A4 / E3
            (587.33f, 493.88f, 196.00f), // 14: D5 + B4 / G3
            (659.25f, 493.88f, 164.81f)  // 15: E5 + B4 / E3
        };

        private static KyoshoSfxData _data;
        private static GameObject _host;
        private static AudioSource _bgmSource;
        private static readonly List<AudioSource> _voices = new();
        private static readonly Dictionary<string, AudioClip> _synthCache = new();
        private static int _nextVoiceIndex;
        private static CancellationTokenSource _duckCts;
        private static CancellationTokenSource _fadeCts;
        private static bool _bgmIsBeatSynced;
        private static float _bgmSyncedLoopBeats = 32f;
        private static float _bgmFadeLevel = 1f;
        private static float _bgmDuckLevel = 1f;
        private static float _lastSmBgmVol = -1f;

        public static KyoshoSfxData Data => _data;

        public static void Bind(KyoshoSfxData data)
        {
            _data = data;
            if (Application.isPlaying)
            {
                BossAudioBridge.EnsureSoundManagerInitialized();
                _lastSmBgmVol = BossAudioBridge.GetBgmVolume();
            }
            PopulateMissingCuesAtRuntime(data);
        }

        public static void Unbind()
        {
            _bgmIsBeatSynced = false;
            StopBgmImmediate();
            _data = null;
            _lastSmBgmVol = -1f;
        }

        // ====================================================================
        // 특화 재생 API (SO 클립 우선, 미할당 시 절차적 음원 폴백 + 다성부 화성)
        // ====================================================================

        public static void PlayBeatTick(bool isDownbeat, int barIndex = 0, int beatInBar = 0)
        {
            if (!Application.isPlaying) return;
            int barMod = ((barIndex % 4) + 4) % 4;
            int subBeatMod = ((beatInBar - 1) % 3 + 3) % 3;

            if (isDownbeat)
            {
                int bassDeg = BarBassDegrees[barMod];
                PlayHarmonicCueVoice(_data?.beatDownbeat, "beat_down", Vector3.zero, GetScalePitchRatio(bassDeg, ReferenceC3Hz), 0.50f, fallbackDefaultVol: 0.70f);

                // 강박 위에 코드 5도를 은은하게 얹어 마디 전환의 화성감을 부여
                int chordFifthDeg = BarArpeggioDegrees[barMod, 2];
                PlayHarmonicCueVoice(_data?.beatSub, "beat_sub", new Vector3(-0.6f, 0f, 0f), GetScalePitchRatio(chordFifthDeg, ReferenceC5Hz), 0.24f, fallbackDefaultVol: 0.45f);
                return;
            }

            int arpDeg = BarArpeggioDegrees[barMod, subBeatMod];
            float panX = (subBeatMod - 1) * 0.7f;
            PlayHarmonicCueVoice(_data?.beatSub, "beat_sub", new Vector3(panX, 0f, 0f), GetScalePitchRatio(arpDeg, ReferenceC5Hz), 0.36f, fallbackDefaultVol: 0.45f);
        }

        public static void PlaySolfegeNote(int symbolIndex, Vector3 worldPos, float volumeScale = 1f, int phraseStep = -1)
        {
            if (!Application.isPlaying) return;
            int idx = Mathf.Clamp(symbolIndex, 0, KyoshoDataConstants.SolfegeFrequencies.Length - 1);

            KyoshoSfxCue leadCue = (_data != null && _data.variationMelodyNotes != null && idx < _data.variationMelodyNotes.Length)
                ? _data.variationMelodyNotes[idx]
                : null;
            float baseFreq = KyoshoDataConstants.SolfegeFrequencies[idx];

            // 리드 멜로디 (원음)
            PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos, 1f, 0.78f * volumeScale, fallbackDefaultVol: 0.70f);

            // 변주곡 출제/타격 단계(0 -> 1 -> 2)에 따라 화성 층위를 점진적으로 추가
            int step = phraseStep;
            if (step < 0)
            {
                // 단일 호출 시에도 3도 화음을 살짝 덧대 풍성한 공명을 부여
                float thirdPitch = GetDiatonicHz(idx + 2) / baseFreq;
                PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos + Vector3.right * 0.65f, thirdPitch, 0.36f * volumeScale, fallbackDefaultVol: 0.70f);
                return;
            }

            float thirdRatio = GetDiatonicHz(idx + 2) / baseFreq;
            PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos + Vector3.left * 0.65f, thirdRatio, 0.44f * volumeScale, fallbackDefaultVol: 0.70f);

            if (step == 0)
            {
                float bassFifthRatio = GetDiatonicHz(idx - 3) / baseFreq;
                PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos + Vector3.right * 0.65f, bassFifthRatio, 0.32f * volumeScale, fallbackDefaultVol: 0.70f);
            }
            else if (step == 1)
            {
                float fifthRatio = GetDiatonicHz(idx + 4) / baseFreq;
                PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos + Vector3.right * 0.75f, fifthRatio, 0.40f * volumeScale, fallbackDefaultVol: 0.70f);
            }
            else
            {
                float fifthRatio = GetDiatonicHz(idx + 4) / baseFreq;
                float octaveRatio = GetDiatonicHz(idx + 7) / baseFreq;
                PlayHarmonicCueVoice(leadCue, $"solfege_{idx}", worldPos + Vector3.right * 0.75f, fifthRatio, 0.44f * volumeScale, fallbackDefaultVol: 0.70f);
                PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos, octaveRatio * (baseFreq / ReferenceC5Hz), 0.38f * volumeScale, fallbackDefaultVol: 0.65f);
            }
        }

        public static void PlayStaccatoCall(int pitchIndex, Vector3 worldPos, int noteOrderInBar = 0, KyoshoNoteType noteType = KyoshoNoteType.Quarter)
        {
            if (!Application.isPlaying) return;
            int degree = Mathf.Clamp(pitchIndex, 0, 7);

            // 1마디 예고: 리드 음 + 다이어토닉 3도 화성 (+ 4분음표·첫 박일 때 5도/옥타브 화성)
            PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos, GetScalePitchRatio(degree, ReferenceC5Hz), 0.88f, fallbackDefaultVol: 0.68f);
            PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.right * 0.8f, GetScalePitchRatio(degree + 2, ReferenceC5Hz), 0.54f, fallbackDefaultVol: 0.68f);

            if (noteType == KyoshoNoteType.Quarter || noteOrderInBar == 0)
            {
                int harmDeg = (noteOrderInBar % 2 == 0) ? (degree + 4) : (degree + 7);
                PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.left * 0.8f, GetScalePitchRatio(harmDeg, ReferenceC5Hz), 0.38f, fallbackDefaultVol: 0.68f);
            }
        }

        public static void PlayStaccatoFireMelody(int pitchIndex, KyoshoNoteType noteType, int noteOrderInBar, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            int degree = Mathf.Clamp(pitchIndex, 0, 7);

            // 2마디 발사: 악보의 pitchIndex 멜로디를 그대로 승계 + 빠른 음표는 3도/6도 교대 화음, 4분음표는 3화음+저음 보강
            PlayHarmonicCueVoice(_data?.staccatoFire, "staccato_fire", worldPos, GetScalePitchRatio(degree, ReferenceC5Hz), 0.90f, fallbackDefaultVol: 0.72f);

            int thirdOrSixth = (noteType == KyoshoNoteType.Eighth || noteType == KyoshoNoteType.Triplet)
                ? ((noteOrderInBar % 2 == 0) ? (degree + 2) : (degree + 5))
                : (degree + 2);
            PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.right * 0.7f, GetScalePitchRatio(thirdOrSixth, ReferenceC5Hz), 0.50f, fallbackDefaultVol: 0.68f);

            if (noteType == KyoshoNoteType.Quarter)
            {
                PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.left * 0.7f, GetScalePitchRatio(degree + 4, ReferenceC5Hz), 0.42f, fallbackDefaultVol: 0.68f);
                PlayHarmonicCueVoice(_data?.beatDownbeat, "beat_down", worldPos, GetScalePitchRatio(degree - 7, ReferenceC3Hz), 0.35f, fallbackDefaultVol: 0.55f);
            }
        }

        public static void PlayStringsCounterMelody(int basePitchIndex, int bonusOrderInBar, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            int clamped = Mathf.Clamp(basePitchIndex, 0, 7);
            // 주 선율 0.5박 뒤에서 옥타브+3도(또는 옥타브+5도)를 오가는 현악 대선율
            int counterDeg = clamped + ((bonusOrderInBar % 2 == 0) ? 9 : 11);
            PlayHarmonicCueVoice(_data?.stringsRapidFire, "strings_fire", worldPos, GetScalePitchRatio(counterDeg, ReferenceC5Hz), 0.82f, fallbackDefaultVol: 0.65f);
            PlayHarmonicCueVoice(_data?.stringsRapidFire, "strings_fire", worldPos + Vector3.left * 0.9f, GetScalePitchRatio(counterDeg - 2, ReferenceC5Hz), 0.44f, fallbackDefaultVol: 0.65f);
        }

        public static void PlayConductBeatHarmony(int stepIndex, int totalSteps, bool isPhase2, bool isPhase3, bool isCompound68, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;

            if (isPhase3 && isCompound68)
            {
                // 6/8 복합박자 6연음 상승 심포닉 아르페지오 화음
                int[] rootDegrees = { 7, 8, 9, 10, 11, 14 };
                int s = Mathf.Clamp(stepIndex, 0, rootDegrees.Length - 1);
                int root = rootDegrees[s];
                PlayHarmonicChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.85f,
                    (root, 0.85f, 0f),
                    (root + 2, 0.55f, 0.75f),
                    (root - 7, 0.48f, -0.75f));
                return;
            }

            if (isPhase3)
            {
                // 3/4 왈츠 3화음 진행 (I -> IV -> V7/G)
                int[][] waltzChords =
                {
                    new[] { 7, 9, 11, 0 },
                    new[] { 10, 12, 7, 3 },
                    new[] { 11, 13, 8, 4 }
                };
                int s = Mathf.Clamp(stepIndex, 0, waltzChords.Length - 1);
                var c = waltzChords[s];
                PlayHarmonicChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.88f,
                    (c[0], 0.82f, 0f),
                    (c[1], 0.56f, 0.75f),
                    (c[2], 0.48f, -0.75f),
                    (c[3], 0.42f, 0f));
                return;
            }

            if (isPhase2)
            {
                // 2페이즈 2/4 행진곡: Am -> Dm -> E7 -> Am9
                int s = ((stepIndex % 4) + 4) % 4;
                switch (s)
                {
                    case 0:
                        PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.88f,
                            (440.00f, 0.75f, 0f), (523.25f, 0.56f, 0.7f), (659.25f, 0.52f, -0.7f), (220.00f, 0.45f, 0f));
                        break;
                    case 1:
                        PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.88f,
                            (587.33f, 0.78f, 0f), (698.46f, 0.56f, 0.7f), (440.00f, 0.50f, -0.7f), (293.66f, 0.45f, 0f));
                        break;
                    case 2:
                        PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.90f,
                            (415.30f, 0.72f, 0f), (493.88f, 0.58f, 0.7f), (587.33f, 0.52f, -0.7f), (329.63f, 0.48f, 0f));
                        break;
                    default:
                        PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.92f,
                            (523.25f, 0.75f, 0f), (659.25f, 0.58f, 0.7f), (987.77f, 0.46f, -0.7f), (440.00f, 0.50f, 0f));
                        break;
                }
                return;
            }

            // 1페이즈 4/4 상승 선율 화음: C -> Am7 -> Fmaj7 -> G7/C5
            int p1Step = ((stepIndex % 4) + 4) % 4;
            switch (p1Step)
            {
                case 0:
                    PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.85f,
                        (523.25f, 0.76f, 0f), (659.25f, 0.54f, 0.7f), (783.99f, 0.46f, -0.7f), (261.63f, 0.40f, 0f));
                    break;
                case 1:
                    PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.85f,
                        (440.00f, 0.74f, 0f), (523.25f, 0.56f, 0.7f), (659.25f, 0.48f, -0.7f), (783.99f, 0.36f, 0f));
                    break;
                case 2:
                    PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.86f,
                        (523.25f, 0.74f, 0f), (659.25f, 0.56f, 0.7f), (880.00f, 0.48f, -0.7f), (349.23f, 0.42f, 0f));
                    break;
                default:
                    PlayFrequencyChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.90f,
                        (587.33f, 0.76f, 0f), (698.46f, 0.52f, 0.7f), (987.77f, 0.48f, -0.7f), (392.00f, 0.44f, 0f));
                    break;
            }
        }

        public static void PlayPhase3FanBurstHarmony(Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            PlayHarmonicChordVoices(_data?.staccatoFire, "staccato_fire", ReferenceC5Hz, worldPos, 0.88f,
                (7, 0.78f, -0.8f),
                (9, 0.65f, 0f),
                (11, 0.62f, 0.8f));
        }

        public static void PlayBloomSeedHarmony(int seedIndex, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            // 펜타토닉 하프 상승 화음: C5, D5, E5, G5, A5, C6 + 3도 화성
            int[] pentatonicDegrees = { 7, 8, 9, 11, 12, 14 };
            int deg = pentatonicDegrees[((seedIndex % pentatonicDegrees.Length) + pentatonicDegrees.Length) % pentatonicDegrees.Length];

            if (seedIndex == 0)
            {
                PlayCue(_data?.bloomSeedToss, "bloom_toss", worldPos, 1f, 0.65f);
            }
            PlayHarmonicCueVoice(_data?.bloomOpenNote, "bloom_note", worldPos, GetScalePitchRatio(deg, ReferenceC5Hz), 0.60f, fallbackDefaultVol: 0.72f);
            PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.right * 0.6f, GetScalePitchRatio(deg + 2, ReferenceC5Hz), 0.36f, fallbackDefaultVol: 0.68f);
        }

        public static void PlayBloomOpenHarmony(KyoshoBloomType bloomType, int openIndex, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            switch (bloomType)
            {
                case KyoshoBloomType.Note:
                {
                    bool alt = (openIndex % 2) == 1;
                    int root = alt ? 10 : 7; // Fmaj vs Cmaj
                    PlayHarmonicChordVoices(_data?.bloomOpenNote, "bloom_note", ReferenceC5Hz, worldPos, 0.90f,
                        (root, 0.80f, 0f),
                        (root + 2, 0.56f, 0.7f),
                        (root + 4, 0.50f, -0.7f));
                    break;
                }
                case KyoshoBloomType.Trumpet:
                    PlayCue(_data?.bloomOpenTrumpet, "bloom_trumpet", worldPos, 1f, 0.75f);
                    PlayFrequencyChordVoices(_data?.staccatoFire, "staccato_fire", ReferenceC5Hz, worldPos, 0.75f,
                        (349.23f, 0.60f, -0.7f), (523.25f, 0.68f, 0f), (659.25f, 0.45f, 0.7f));
                    break;
                case KyoshoBloomType.Cymbal:
                    PlayCue(_data?.bloomOpenCymbal, "bloom_cymbal", worldPos, 1f, 0.85f);
                    PlayHarmonicChordVoices(_data?.bloomOpenNote, "bloom_note", ReferenceC5Hz, worldPos, 0.65f,
                        (9, 0.65f, -0.8f), (12, 0.55f, 0f), (13, 0.45f, 0.8f));
                    break;
            }
        }

        public static void PlayFermataSpawnHarmony(int count, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            // 공중 음표 전개 시 상행 코드(G4 + B4 + D5 + G5)로 긴장감 있는 도미넌트 서스펜션 형성
            PlayHarmonicChordVoices(_data?.fermataSpawn, "fermata_spawn", ReferenceC5Hz, worldPos, 0.85f,
                (4, 0.65f, -1.2f),
                (6, 0.60f, -0.4f),
                (8, 0.68f, 0.4f),
                (11, 0.62f, 1.2f));
        }

        public static void PlayFermataHoldHarmony(Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            // 정지(⌒) 구간: 풍성한 홀드 화음 + 상단 9도/13도 서스펜션 공명 중첩
            PlayHarmonicCueVoice(_data?.fermataHoldChord, "fermata_hold", worldPos, 1f, 0.90f, fallbackDefaultVol: 0.80f);
            PlayHarmonicChordVoices(_data?.conductTipFlash, "conduct_flash", ReferenceC5Hz, worldPos, 0.55f,
                (4, 0.52f, -0.9f),
                (8, 0.48f, 0.9f),
                (12, 0.40f, 0f));
        }

        public static void PlayFermataDropResolution(Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            PlayCue(_data?.fermataSlamDrop, "fermata_drop", worldPos, 1f, 1f);
            // 강렬한 종지 화음(C3 - G3 - C4 - E4 - G4)
            PlayFrequencyChordVoices(_data?.staccatoFire, "staccato_fire", ReferenceC5Hz, worldPos, 0.82f,
                (261.63f, 0.72f, -0.6f), (329.63f, 0.58f, 0.6f), (392.00f, 0.62f, 0f), (523.25f, 0.54f, 0f));
        }

        public static void PlayFinalePressureMelody(int stepIndex, bool isHell, Vector3 worldPos)
        {
            if (!Application.isPlaying) return;
            int idx = ((stepIndex % FinaleOstinatoTable.Length) + FinaleOstinatoTable.Length) % FinaleOstinatoTable.Length;
            var entry = FinaleOstinatoTable[idx];
            float masterScale = isHell ? 0.82f : 0.88f;

            // 피날레 2~3마디 압박 탄막의 교향곡 리드 멜로디 + 3도/6도 화성 + 베이스
            PlayHarmonicCueVoice(_data?.staccatoFire, "staccato_fire", worldPos, entry.leadHz / ReferenceC5Hz, 0.85f * masterScale, fallbackDefaultVol: 0.72f);
            PlayHarmonicCueVoice(_data?.staccatoCallNote, "staccato_call", worldPos + Vector3.right * 0.85f, entry.harmHz / ReferenceC5Hz, 0.56f * masterScale, fallbackDefaultVol: 0.68f);

            if ((stepIndex % 2) == 0 || isHell)
            {
                PlayHarmonicCueVoice(_data?.beatDownbeat, "beat_down", worldPos + Vector3.left * 0.85f, entry.bassHz / ReferenceC3Hz, 0.48f * masterScale, fallbackDefaultVol: 0.60f);
            }
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

        private static float GetDiatonicHz(int degree)
        {
            if (degree >= 0 && degree < MelodyScaleHz.Length)
            {
                return MelodyScaleHz[degree];
            }
            int oct = Mathf.FloorToInt(degree / 7f);
            int step = degree - oct * 7;
            return DiatonicOctaveHz[step] * Mathf.Pow(2f, oct);
        }

        private static float GetScalePitchRatio(int degree, float referenceHz = ReferenceC4Hz)
        {
            return GetDiatonicHz(degree) / Mathf.Max(1f, referenceHz);
        }

        private static void PlayHarmonicChordVoices(KyoshoSfxCue cue, string fallbackKey, float baseHz, Vector3 worldPos, float volumeScale, params (int degree, float gain, float panOffsetX)[] voices)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var v = voices[i];
                PlayHarmonicCueVoice(cue, fallbackKey, worldPos + new Vector3(v.panOffsetX, 0f, 0f), GetScalePitchRatio(v.degree, baseHz), volumeScale * v.gain);
            }
        }

        private static void PlayFrequencyChordVoices(KyoshoSfxCue cue, string fallbackKey, float baseHz, Vector3 worldPos, float volumeScale, params (float freqHz, float gain, float panOffsetX)[] voices)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                var v = voices[i];
                PlayHarmonicCueVoice(cue, fallbackKey, worldPos + new Vector3(v.panOffsetX, 0f, 0f), v.freqHz / Mathf.Max(1f, baseHz), volumeScale * v.gain);
            }
        }

        private static void PlayHarmonicCueVoice(KyoshoSfxCue cue, string fallbackKey, Vector3 worldPos, float pitchRatio, float volumeScale = 1f, float fallbackDefaultVol = 0.70f)
        {
            if (cue != null && cue.HasValidClip)
            {
                PlayClip(cue.PickRandomClip(), worldPos, cue.volume * volumeScale, pitchRatio);
                return;
            }

            AudioClip synth = GetOrCreateFallbackCue(fallbackKey);
            if (synth != null)
            {
                float baseVol = cue != null ? cue.volume : fallbackDefaultVol;
                PlayClip(synth, worldPos, baseVol * volumeScale, pitchRatio);
            }
        }

        // ====================================================================
        // BGM & 덕킹
        // ====================================================================

        public static void PlayPhase2Bgm(float fadeIn = 0.6f, float targetBpm = 132f, double currentTotalBeats = 0.0)
        {
            AudioClip clip = null;
            if (Mathf.Abs(targetBpm - 132f) <= 1f && _data != null && _data.bgmPhase2 != null && _data.bgmPhase2 != _data.bgmMain)
            {
                clip = _data.bgmPhase2;
            }
            if (clip == null)
            {
                clip = GetOrCreateSyncedBgmClip(2, targetBpm);
            }
            if (clip != null) PlayBgm(clip, fadeIn, beatSynced: true, currentTotalBeats: currentTotalBeats, loopBeats: 32f);
        }

        public static void PlayPhase3Bgm(float fadeIn = 0.25f, float targetBpm = 160f, double currentTotalBeats = 0.0)
        {
            AudioClip clip = null;
            if (Mathf.Abs(targetBpm - 160f) <= 1f && _data != null && _data.bgmPhase3 != null
                && _data.bgmPhase3 != _data.bgmMain && _data.bgmPhase3 != _data.bgmPhase2)
            {
                clip = _data.bgmPhase3;
            }
            if (clip == null)
            {
                clip = GetOrCreateSyncedBgmClip(3, targetBpm);
            }
            if (clip != null) PlayBgm(clip, fadeIn, beatSynced: true, currentTotalBeats: currentTotalBeats, loopBeats: 32f);
        }

        public static void PlayBgm(AudioClip clip, float fadeIn = 0.4f, bool beatSynced = false, double currentTotalBeats = 0.0, float loopBeats = 32f)
        {
            if (!Application.isPlaying || clip == null) return;
            BossAudioBridge.StopExternalSoundManagerBgm(fadeIn);
            EnsureHost();
            EnsureBgmSource();
            if (_bgmSource.clip == clip && _bgmSource.isPlaying && _fadeCts == null)
            {
                _bgmFadeLevel = 1f;
                ApplyCurrentBgmVolume();
                return;
            }

            CancelFade();
            CancelDuck();
            _bgmDuckLevel = 1f;
            _bgmSource.clip = clip;
            _bgmSource.loop = true;
            _bgmSource.panStereo = 0f;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.pitch = Time.timeScale > 0f ? Time.timeScale : 0f;
            _bgmIsBeatSynced = beatSynced;
            _bgmSyncedLoopBeats = Mathf.Max(4f, loopBeats);

            if (beatSynced && clip.samples > 0)
            {
                double wrapped = ((currentTotalBeats % _bgmSyncedLoopBeats) + _bgmSyncedLoopBeats) % _bgmSyncedLoopBeats;
                int startSample = Mathf.Clamp((int)((wrapped / _bgmSyncedLoopBeats) * clip.samples), 0, Mathf.Max(0, clip.samples - 1));
                _bgmSource.timeSamples = startSample;
            }
            else
            {
                _bgmSource.timeSamples = 0;
            }

            if (fadeIn <= 0.01f)
            {
                _bgmFadeLevel = 1f;
                ApplyCurrentBgmVolume();
                _bgmSource.Play();
            }
            else
            {
                _bgmFadeLevel = 0f;
                _fadeCts = new CancellationTokenSource();
                FadeBgmAsync(0f, 1f, fadeIn, true, _fadeCts.Token).Forget();
            }
        }

        public static void SyncBgmWithBeatClock(IKyoshoBeatClock clock)
        {
            if (_bgmSource == null || _bgmSource.clip == null) return;
            if (_bgmSource.isPlaying && BossAudioBridge.HasExternalBgmTakenOver())
            {
                StopBgmImmediate();
                return;
            }

            float smBgm = BossAudioBridge.GetBgmVolume();
            if (!Mathf.Approximately(_lastSmBgmVol, smBgm))
            {
                _lastSmBgmVol = smBgm;
                ApplyCurrentBgmVolume();
            }

            float targetPitch = Time.timeScale > 0f ? Time.timeScale : 0f;
            if (!Mathf.Approximately(_bgmSource.pitch, targetPitch))
            {
                _bgmSource.pitch = targetPitch;
            }

            if (!_bgmIsBeatSynced || clock == null || !clock.IsRunning || !_bgmSource.isPlaying) return;
            int totalSamples = _bgmSource.clip.samples;
            int sampleRate = _bgmSource.clip.frequency;
            if (totalSamples <= 0 || sampleRate <= 0) return;

            double wrapped = ((clock.TotalBeats % _bgmSyncedLoopBeats) + _bgmSyncedLoopBeats) % _bgmSyncedLoopBeats;
            int expectedSample = Mathf.Clamp((int)((wrapped / _bgmSyncedLoopBeats) * totalSamples), 0, totalSamples - 1);
            int curSample = _bgmSource.timeSamples;
            int diff = Mathf.Abs(expectedSample - curSample);
            if (diff > totalSamples / 2) diff = totalSamples - diff;

            if (diff > Mathf.RoundToInt(0.045f * sampleRate))
            {
                _bgmSource.timeSamples = expectedSample;
            }
        }

        private static AudioClip GetOrCreateSyncedBgmClip(int phase, float bpm)
        {
            float clampedBpm = Mathf.Clamp(bpm, 40f, 220f);
            string key = $"bgm_phase{phase}_bpm{Mathf.RoundToInt(clampedBpm)}";
            if (_synthCache.TryGetValue(key, out var cached) && cached != null) return cached;

            float[] samples = SynthesizeBgmLoopSamplesForBpm(phase, clampedBpm, out int sr);
            if (samples == null || samples.Length == 0) return null;

            var clip = AudioClip.Create($"Synth_{key}", samples.Length, 1, sr, false);
            clip.SetData(samples, 0);
            _synthCache[key] = clip;
            return clip;
        }

        public static void StopBgm(float fadeOut = 0.5f)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;
            CancelFade();
            if (fadeOut <= 0.01f) StopBgmImmediate();
            else
            {
                _fadeCts = new CancellationTokenSource();
                FadeBgmAsync(_bgmFadeLevel, 0f, fadeOut, false, _fadeCts.Token).Forget();
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
            _bgmFadeLevel = 1f;
            _bgmDuckLevel = 1f;
            _bgmIsBeatSynced = false;
            if (_bgmSource != null) _bgmSource.Stop();
        }

        private static void ApplyCurrentBgmVolume()
        {
            if (_bgmSource == null) return;
            float baseVol = _data != null ? _data.bgmVolume : 0.4f;
            _bgmSource.volume = Mathf.Clamp01(baseVol * _bgmFadeLevel * _bgmDuckLevel * BossAudioBridge.GetBgmVolume());
        }

        private static async UniTaskVoid FadeBgmAsync(float fromLevel, float toLevel, float duration, bool startPlay, CancellationToken token)
        {
            if (_bgmSource == null) return;
            _bgmFadeLevel = fromLevel;
            ApplyCurrentBgmVolume();
            if (startPlay) _bgmSource.Play();
            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    _bgmFadeLevel = Mathf.Lerp(fromLevel, toLevel, Mathf.Clamp01(elapsed / duration));
                    ApplyCurrentBgmVolume();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                if (_bgmSource != null)
                {
                    _bgmFadeLevel = toLevel;
                    ApplyCurrentBgmVolume();
                    if (toLevel <= 0.001f && !startPlay)
                    {
                        _bgmSource.Stop();
                        _bgmIsBeatSynced = false;
                    }
                }
            }
            catch (System.OperationCanceledException) { }
        }

        private static async UniTaskVoid RunDuckAsync(float duration, float ratio, CancellationToken token)
        {
            _bgmDuckLevel = ratio;
            ApplyCurrentBgmVolume();
            float elapsed = 0f;
            try
            {
                while (elapsed < duration && _bgmSource != null)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    _bgmDuckLevel = Mathf.Lerp(ratio, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                    ApplyCurrentBgmVolume();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                _bgmDuckLevel = 1f;
                ApplyCurrentBgmVolume();
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

            BossAudioBridge.ApplySfxRouting(src);
            float master = _data != null ? _data.masterSfxVolume : 0.85f;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * master * BossAudioBridge.GetSfxVolume());
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
            return BossAudioBridge.ComputeStereoPan(worldPos, spread);
        }

        private static AudioSource AcquireVoice()
        {
            EnsureHost();
            _voices.RemoveAll(v => v == null);
            while (_voices.Count < OneShotVoiceCount)
            {
                var s = _host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                BossAudioBridge.ApplySfxRouting(s);
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
            if (BossAudioBridge.EnsureAudioHost(ref _host, "[Kyosho_AudioHost]"))
            {
                _voices.Clear();
                _bgmSource = null;
                _nextVoiceIndex = 0;
            }
        }

        private static void EnsureBgmSource()
        {
            if (_bgmSource != null)
            {
                BossAudioBridge.ApplyBgmRouting(_bgmSource);
                return;
            }
            EnsureHost();
            _bgmSource = _host.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            BossAudioBridge.ApplyBgmRouting(_bgmSource);
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

        private static void PopulateMissingCuesAtRuntime(KyoshoSfxData data)
        {
            if (data == null || !Application.isPlaying) return;
            void WarmIfMissing(KyoshoSfxCue cue, string key)
            {
                if (cue == null || !cue.HasValidClip) GetOrCreateFallbackCue(key);
            }

            WarmIfMissing(data.beatDownbeat, "beat_down");
            WarmIfMissing(data.beatSub, "beat_sub");
            WarmIfMissing(data.conductWhoosh, "conduct_whoosh");
            WarmIfMissing(data.conductTipFlash, "conduct_flash");
            WarmIfMissing(data.percussionShockwave, "percussion_shock");
            WarmIfMissing(data.staccatoCallNote, "staccato_call");
            WarmIfMissing(data.staccatoFire, "staccato_fire");
            WarmIfMissing(data.stringsRapidFire, "strings_fire");
            WarmIfMissing(data.bloomSeedToss, "bloom_toss");
            WarmIfMissing(data.bloomStemGrow, "bloom_grow");
            WarmIfMissing(data.bloomStemCut, "bloom_cut");
            WarmIfMissing(data.bloomOpenNote, "bloom_note");
            WarmIfMissing(data.bloomOpenTrumpet, "bloom_trumpet");
            WarmIfMissing(data.bloomOpenCymbal, "bloom_cymbal");
            WarmIfMissing(data.fermataSpawn, "fermata_spawn");
            WarmIfMissing(data.fermataHoldChord, "fermata_hold");
            WarmIfMissing(data.fermataSlamDrop, "fermata_drop");
            WarmIfMissing(data.brassBeam, "brass_beam");
            WarmIfMissing(data.variationCorrect, "variation_ok");
            WarmIfMissing(data.variationFailDissonance, "variation_fail");
            WarmIfMissing(data.finaleGather, "finale_gather");
            WarmIfMissing(data.finaleFullBlast, "finale_blast");
            WarmIfMissing(data.sectionBroken, "section_broken");
            WarmIfMissing(data.encoreApplause, "encore_applause");
            WarmIfMissing(data.encoreAwaken, "encore_awaken");

            if (data.bgmPhase2 == null || data.bgmPhase2 == data.bgmMain)
            {
                GetOrCreateFallbackCue("bgm_phase2");
            }
            if (data.bgmPhase3 == null || data.bgmPhase3 == data.bgmMain || data.bgmPhase3 == data.bgmPhase2)
            {
                GetOrCreateFallbackCue("bgm_phase3");
            }

            if (data.variationMelodyNotes != null)
            {
                for (int i = 0; i < data.variationMelodyNotes.Length && i < KyoshoDataConstants.SolfegeFrequencies.Length; i++)
                {
                    WarmIfMissing(data.variationMelodyNotes[i], $"solfege_{i}");
                }
            }
        }

        private static AudioClip GetOrCreateFallbackCue(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_synthCache.TryGetValue(key, out var cached) && cached != null) return cached;

            float[] samples = SynthesizeCueSamples(key, out int sr);
            if (samples == null || samples.Length == 0) return null;

            var clip = AudioClip.Create($"Synth_{key}", samples.Length, 1, sr, false);
            clip.SetData(samples, 0);
            _synthCache[key] = clip;
            return clip;
        }

        /// <summary>
        /// 런타임 폴백과 에디터 WAV 익스포터가 공용으로 사용하는 결정적 절차적 음원 샘플 생성기.
        /// </summary>
        public static float[] SynthesizeCueSamples(string key, out int sampleRate)
        {
            sampleRate = SampleRate;
            if (key == "bgm_phase2") return SynthesizeBgmLoopSamples(2, out sampleRate);
            if (key == "bgm_phase3") return SynthesizeBgmLoopSamples(3, out sampleRate);
            if (key != null && key.StartsWith("solfege_") && int.TryParse(key.Substring(8), out int sIdx))
            {
                sIdx = Mathf.Clamp(sIdx, 0, KyoshoDataConstants.SolfegeFrequencies.Length - 1);
                float freq = KyoshoDataConstants.SolfegeFrequencies[sIdx];
                return BuildOrchestralToneSamples(freq, 0.44f, 0.50f, harmonic2: 0.36f, harmonic3: 0.18f, octaveShimmer: 0.18f, chorusDetune: 0.0035f, vibratoDepth: 0.0035f, expDecay: 5.8f);
            }

            return key switch
            {
                "beat_down" => BuildOrchestralToneSamples(ReferenceC3Hz, 0.14f, 0.52f, harmonic2: 0.38f, harmonic3: 0.10f, octaveShimmer: 0.04f, chorusDetune: 0.002f, vibratoDepth: 0f, expDecay: 18f),
                "beat_sub" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.095f, 0.40f, harmonic2: 0.18f, harmonic3: 0.05f, octaveShimmer: 0.10f, chorusDetune: 0.002f, vibratoDepth: 0f, expDecay: 28f),
                "conduct_whoosh" => BuildNoiseWhooshSamples(0.16f, 0.38f),
                "conduct_flash" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.26f, 0.48f, harmonic2: 0.36f, harmonic3: 0.18f, octaveShimmer: 0.16f, chorusDetune: 0.0038f, vibratoDepth: 0.0035f, expDecay: 8.5f),
                "percussion_shock" => BuildDrumImpactSamples(0.28f, 130f, 45f, 0.65f),
                "staccato_call" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.22f, 0.52f, harmonic2: 0.32f, harmonic3: 0.14f, octaveShimmer: 0.20f, chorusDetune: 0.0032f, vibratoDepth: 0.003f, expDecay: 9.5f),
                "staccato_fire" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.18f, 0.52f, harmonic2: 0.42f, harmonic3: 0.22f, octaveShimmer: 0.18f, chorusDetune: 0.0035f, vibratoDepth: 0.0025f, expDecay: 11.5f),
                "strings_fire" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.16f, 0.48f, harmonic2: 0.46f, harmonic3: 0.24f, octaveShimmer: 0.14f, chorusDetune: 0.0040f, vibratoDepth: 0.004f, expDecay: 12.5f),
                "bloom_toss" => BuildNoiseWhooshSamples(0.14f, 0.30f),
                "bloom_grow" => BuildArpeggioChordSamples(0.32f, 0.44f, 0.045f, 6.5f, 261.63f, 329.63f, 392.00f, 523.25f),
                "bloom_cut" => BuildDrumImpactSamples(0.18f, 320f, 90f, 0.55f),
                "bloom_note" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.26f, 0.52f, harmonic2: 0.30f, harmonic3: 0.12f, octaveShimmer: 0.24f, chorusDetune: 0.0032f, vibratoDepth: 0.003f, expDecay: 8.0f),
                "bloom_trumpet" => BuildRichChordSamples(0.38f, 0.50f, 0.0035f, 4.0f, 349.23f, 440.00f, 523.25f, 783.99f),
                "bloom_cymbal" => BuildNoiseWhooshSamples(0.35f, 0.50f),
                "fermata_spawn" => BuildOrchestralToneSamples(ReferenceC5Hz, 0.28f, 0.48f, harmonic2: 0.28f, harmonic3: 0.14f, octaveShimmer: 0.22f, chorusDetune: 0.0035f, vibratoDepth: 0.003f, expDecay: 7.5f),
                "fermata_hold" => BuildRichChordSamples(0.95f, 0.46f, 0.0038f, 2.2f, 261.63f, 329.63f, 392.00f, 493.88f, 587.33f),
                "fermata_drop" => BuildDrumImpactSamples(0.35f, 180f, 40f, 0.72f),
                "brass_beam" => BuildRichChordSamples(0.50f, 0.52f, 0.0035f, 3.5f, 220.00f, 329.63f, 440.00f, 554.37f, 659.25f),
                "variation_ok" => BuildArpeggioChordSamples(0.58f, 0.55f, 0.045f, 4.0f, 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f),
                "variation_fail" => BuildRichChordSamples(0.55f, 0.60f, 0.004f, 4.2f, 138.59f, 196.00f, 277.18f, 405.00f),
                "finale_gather" => BuildArpeggioChordSamples(0.95f, 0.52f, 0.055f, 2.4f, 220.00f, 261.63f, 329.63f, 392.00f, 493.88f),
                "finale_blast" => BuildOrchestralImpactSamples(0.65f, 220f, 32f, 0.85f, 110.00f, 164.81f, 261.63f, 329.63f),
                "section_broken" => BuildDrumImpactSamples(0.32f, 260f, 55f, 0.70f),
                "encore_applause" => BuildApplauseBurstSamples(0.42f, 0.75f),
                "encore_awaken" => BuildAwakenFanfareSamples(1.05f, 0.82f),
                _ => null
            };
        }

        private static float[] BuildOrchestralToneSamples(
            float freq,
            float dur,
            float amp,
            float harmonic2 = 0.30f,
            float harmonic3 = 0.14f,
            float octaveShimmer = 0.14f,
            float chorusDetune = 0.0035f,
            float vibratoDepth = 0.003f,
            float expDecay = 9f)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float tailWindow = Mathf.Min(0.02f, dur * 0.15f);

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float vibRamp = Mathf.Clamp01(t / 0.06f);
                float vibrato = 1f + vibratoDepth * vibRamp * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                float fCenter = freq * vibrato;
                float fLow = fCenter * (1f - chorusDetune);
                float fHigh = fCenter * (1f + chorusDetune);

                float env = Mathf.Min(1f, t / 0.007f) * Mathf.Exp(-expDecay * t);
                float rem = dur - t;
                if (rem < tailWindow) env *= Mathf.Clamp01(rem / tailWindow);

                float chorusFundamental = (Mathf.Sin(2f * Mathf.PI * fCenter * t)
                                        + 0.45f * Mathf.Sin(2f * Mathf.PI * fLow * t)
                                        + 0.45f * Mathf.Sin(2f * Mathf.PI * fHigh * t)) / 1.9f;
                float overtones = harmonic2 * Mathf.Sin(4f * Mathf.PI * fCenter * t)
                                + harmonic3 * Mathf.Sin(6f * Mathf.PI * fCenter * t)
                                + octaveShimmer * Mathf.Exp(-24f * t) * Mathf.Sin(8f * Mathf.PI * fCenter * t);

                data[i] = Mathf.Clamp((chorusFundamental + overtones) * env * amp * 0.92f, -0.98f, 0.98f);
            }
            return data;
        }

        private static float[] BuildRichChordSamples(float dur, float amp, float chorusDetune = 0.003f, float decayRate = 3.2f, params float[] freqs)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float inv = 1f / Mathf.Max(1, freqs.Length);
            float tailWindow = Mathf.Min(0.03f, dur * 0.12f);

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / 0.018f) * Mathf.Exp(-decayRate * t);
                float rem = dur - t;
                if (rem < tailWindow) env *= Mathf.Clamp01(rem / tailWindow);

                float vib = 1f + 0.0025f * Mathf.Clamp01(t / 0.08f) * Mathf.Sin(2f * Mathf.PI * 5.2f * t);
                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float hz = freqs[f] * vib;
                    float det = hz * chorusDetune;
                    sum += 0.55f * Mathf.Sin(2f * Mathf.PI * hz * t)
                         + 0.225f * Mathf.Sin(2f * Mathf.PI * (hz - det) * t)
                         + 0.225f * Mathf.Sin(2f * Mathf.PI * (hz + det) * t)
                         + 0.26f * Mathf.Sin(4f * Mathf.PI * hz * t)
                         + 0.10f * Mathf.Sin(6f * Mathf.PI * hz * t);
                }
                data[i] = Mathf.Clamp(sum * inv * env * amp * 1.35f, -0.98f, 0.98f);
            }
            return data;
        }

        private static float[] BuildArpeggioChordSamples(float dur, float amp, float staggerSec, float decayRate, params float[] freqs)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float inv = 1f / Mathf.Max(1, freqs.Length);
            float tailWindow = Mathf.Min(0.035f, dur * 0.12f);

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float rem = dur - t;
                float globalTail = rem < tailWindow ? Mathf.Clamp01(rem / tailWindow) : 1f;
                float vib = 1f + 0.0028f * Mathf.Clamp01(t / 0.07f) * Mathf.Sin(2f * Mathf.PI * 5.4f * t);

                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteT = t - f * staggerSec;
                    if (noteT <= 0f) continue;

                    float hz = freqs[f] * vib;
                    float det = hz * 0.0032f;
                    float noteEnv = Mathf.Min(1f, noteT / 0.010f) * Mathf.Exp(-decayRate * noteT);
                    float harpShimmer = 0.20f * Mathf.Exp(-18f * noteT) * Mathf.Sin(8f * Mathf.PI * hz * noteT);
                    float tone = 0.60f * Mathf.Sin(2f * Mathf.PI * hz * noteT)
                               + 0.20f * Mathf.Sin(2f * Mathf.PI * (hz - det) * noteT)
                               + 0.20f * Mathf.Sin(2f * Mathf.PI * (hz + det) * noteT)
                               + 0.28f * Mathf.Sin(4f * Mathf.PI * hz * noteT)
                               + harpShimmer;
                    sum += tone * noteEnv;
                }
                data[i] = Mathf.Clamp(sum * inv * globalTail * amp * 1.75f, -0.98f, 0.98f);
            }
            return data;
        }

        private static float[] BuildOrchestralImpactSamples(float dur, float startFreq, float endFreq, float amp, params float[] chordFreqs)
        {
            float[] drum = BuildDrumImpactSamples(dur, startFreq, endFreq, 1f);
            float[] chord = BuildRichChordSamples(dur, 0.55f, 0.0035f, 2.8f, chordFreqs);
            float[] data = new float[drum.Length];
            for (int i = 0; i < drum.Length; i++)
            {
                float c = i < chord.Length ? chord[i] : 0f;
                data[i] = Mathf.Clamp((drum[i] * 0.72f + c * 0.45f) * amp, -0.98f, 0.98f);
            }
            return data;
        }

        private static float[] BuildDrumImpactSamples(float dur, float startFreq, float endFreq, float amp)
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
            return data;
        }

        private static float[] BuildNoiseWhooshSamples(float dur, float amp)
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
            return data;
        }

        private static float[] BuildApplauseBurstSamples(float dur, float amp)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            uint seed = 0xCAFEBABEu;
            float lp = 0f;
            float hp = 0f;
            float prev = 0f;
            // 다중 박수 트랜지언트 + 홀 잔향 느낌 합성
            float[] clapTimes = { 0.00f, 0.035f, 0.07f, 0.115f, 0.16f, 0.21f, 0.265f };
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                seed = seed * 1664525u + 1013904223u;
                float n = (seed & 0xFFFF) / 32768f - 1f;
                lp = Mathf.Lerp(lp, n, 0.35f);
                hp = lp - prev * 0.6f;
                prev = lp;

                float clapEnv = 0.22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur));
                for (int c = 0; c < clapTimes.Length; c++)
                {
                    float dt = t - clapTimes[c];
                    if (dt >= 0f && dt < 0.08f)
                    {
                        clapEnv += Mathf.Exp(-42f * dt) * 0.45f;
                    }
                }
                data[i] = Mathf.Clamp(hp * clapEnv * amp, -1f, 1f);
            }
            return data;
        }

        private static float[] BuildAwakenFanfareSamples(float dur, float amp)
        {
            int samples = Mathf.Max(256, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[samples];
            float[] freqs = { 146.83f, 220.00f, 293.66f, 369.99f, 440.00f, 587.33f };
            float tailWindow = Mathf.Min(0.035f, dur * 0.10f);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / dur;
                float riseEnv = Mathf.Min(1f, t / 0.025f) * Mathf.Exp(-2.1f * Mathf.Max(0f, t - 0.18f));
                float rem = dur - t;
                if (rem < tailWindow) riseEnv *= Mathf.Clamp01(rem / tailWindow);

                float sum = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteT = t - f * 0.025f;
                    if (noteT < 0f) noteT = 0f;
                    float sweep = freqs[f] * Mathf.Lerp(0.88f, 1.0f, Mathf.Clamp01(k * 2.6f));
                    float vib = 1f + 0.0025f * Mathf.Sin(2f * Mathf.PI * 5.2f * t);
                    sum += Mathf.Sin(2f * Mathf.PI * sweep * vib * t) + 0.32f * Mathf.Sin(4f * Mathf.PI * sweep * vib * t);
                }
                float bassHit = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 55f, Mathf.Clamp01(t / 0.3f)) * t) * Mathf.Exp(-7f * t);
                data[i] = Mathf.Clamp(((sum / freqs.Length) * riseEnv + bassHit * 0.65f) * amp, -0.98f, 0.98f);
            }
            return data;
        }

        /// <summary>
        /// Phase 2(132 BPM 기본 D단조 긴장감 있는 오케스트라) 및 Phase 3(160 BPM 프레스토 헬 앙코르 투티) 루프 BGM 생성기.
        /// </summary>
        public static float[] SynthesizeBgmLoopSamples(int phase, out int sampleRate)
        {
            float defaultBpm = phase >= 3 ? 160f : 132f;
            return SynthesizeBgmLoopSamplesForBpm(phase, defaultBpm, out sampleRate);
        }

        public static float[] SynthesizeBgmLoopSamplesForBpm(int phase, float bpm, out int sampleRate)
        {
            sampleRate = SampleRate;
            bool isPresto = phase >= 3;
            bpm = Mathf.Clamp(bpm, 40f, 220f);
            const int totalBars = 8;
            const int beatsPerBar = 4;
            float beatDur = 60f / bpm;
            float barDur = beatDur * beatsPerBar;
            float totalDur = barDur * totalBars;
            int totalSamples = Mathf.Max(SampleRate, Mathf.RoundToInt(totalDur * SampleRate));
            float[] data = new float[totalSamples];

            // 8마디 화성 진행 (D단조): Dm - Bb - Gm - A7 | Dm - F - Bb - A7
            float[] bassRoots = isPresto
                ? new[] { 73.42f, 58.27f, 98.00f, 55.00f, 73.42f, 87.31f, 58.27f, 55.00f }
                : new[] { 73.42f, 58.27f, 49.00f, 55.00f, 73.42f, 65.41f, 58.27f, 55.00f };

            float[][] chordTriads =
            {
                new[] { 293.66f, 349.23f, 440.00f }, // Dm
                new[] { 233.08f, 293.66f, 349.23f }, // Bb
                new[] { 196.00f, 233.08f, 293.66f }, // Gm
                new[] { 220.00f, 277.18f, 329.63f }, // A
                new[] { 293.66f, 349.23f, 440.00f }, // Dm
                new[] { 261.63f, 349.23f, 440.00f }, // F
                new[] { 233.08f, 293.66f, 349.23f }, // Bb
                new[] { 220.00f, 277.18f, 392.00f }  // A7
            };

            // 멜로디 인덱스 (트라이어드 순환 + 옥타브 도약)
            int[] melodyPattern = isPresto
                ? new[] { 0, 1, 2, 1, 0, 2, 1, 2, 0, 1, 2, 2, 1, 0, 2, 1 }
                : new[] { 0, 2, 1, 2, 0, 1, 2, 1 };

            int subdivsPerBar = isPresto ? 16 : 8;
            float stepDur = barDur / subdivsPerBar;
            int noiseSeed = isPresto ? 907 : 313;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SampleRate;
                int barIdx = Mathf.Clamp((int)(t / barDur), 0, totalBars - 1);
                float barLocal = t - barIdx * barDur;
                int beatIdx = Mathf.Clamp((int)(barLocal / beatDur), 0, beatsPerBar - 1);
                float beatLocal = barLocal - beatIdx * beatDur;

                int stepIdx = Mathf.Clamp((int)(barLocal / stepDur), 0, subdivsPerBar - 1);
                float stepLocal = barLocal - stepIdx * stepDur;

                float bassHz = bassRoots[barIdx];
                float[] triad = chordTriads[barIdx];

                // 1. 저음 첼로/팀파니 펄스 (박자 정박 타격 + 서스테인)
                float bassHitEnv = Mathf.Min(1f, beatLocal / 0.008f) * Mathf.Exp(-(isPresto ? 7.5f : 5.2f) * beatLocal);
                float bassWave = Mathf.Sin(2f * Mathf.PI * bassHz * t)
                               + 0.45f * Mathf.Sin(4f * Mathf.PI * bassHz * t);
                float drumPitch = Mathf.Lerp(isPresto ? 130f : 105f, 52f, Mathf.Clamp01(beatLocal / 0.09f));
                float timpani = (beatIdx == 0 || beatIdx == 2 || isPresto)
                    ? Mathf.Sin(2f * Mathf.PI * drumPitch * beatLocal) * Mathf.Exp(-14f * beatLocal) * (beatIdx == 0 ? 0.34f : 0.22f)
                    : 0f;

                // 2. 스타카토 현악 아르페지오 오스티나토 + 멜로디 리드
                float arpHz = triad[melodyPattern[stepIdx % melodyPattern.Length]] * (isPresto && (stepIdx % 4 == 3) ? 2f : 1f);
                float arpEnv = Mathf.Min(1f, stepLocal / 0.005f) * Mathf.Exp(-(isPresto ? 18f : 11f) * stepLocal);
                float arpWave = 0.65f * Mathf.Sin(2f * Mathf.PI * arpHz * t)
                              + 0.28f * Mathf.Sin(4f * Mathf.PI * arpHz * t)
                              + 0.14f * Mathf.Sin(6f * Mathf.PI * arpHz * t);

                // 3. 브라스/현악 화성 패드 (마디 1, 5, 7에 웅장한 강조)
                float chordSum = 0f;
                for (int c = 0; c < triad.Length; c++)
                {
                    float f = triad[c] * (isPresto ? 2f : 1f);
                    chordSum += Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t);
                }
                float padEnv = 0.25f + 0.30f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(barLocal / barDur));

                // 4. 프레스토 전용 스네어/하이햇 리듬 노이즈
                float perc = 0f;
                if (isPresto && (stepIdx % 2 == 1 || beatIdx == 1 || beatIdx == 3))
                {
                    noiseSeed = unchecked(noiseSeed * 1664525 + 1013904223);
                    float rnd = ((noiseSeed >> 8) & 0xFFFF) / 32768f - 1f;
                    float decay = (beatIdx == 1 || beatIdx == 3) && (stepIdx % 4 == 0) ? 28f : 55f;
                    perc = rnd * Mathf.Exp(-decay * stepLocal) * 0.14f;
                }

                // 루프 시작/끝 25ms 미세 페이드로 클릭음 제로화
                float loopFade = Mathf.Clamp01(t / 0.025f) * Mathf.Clamp01((totalDur - t) / 0.025f);
                float mix = bassWave * bassHitEnv * 0.34f
                          + timpani
                          + arpWave * arpEnv * (isPresto ? 0.34f : 0.28f)
                          + (chordSum / 3f) * padEnv * (isPresto ? 0.22f : 0.20f)
                          + perc;

                data[i] = Mathf.Clamp(mix * 0.85f * loopFade, -0.96f, 0.96f);
            }
            return data;
        }
    }
}
