using System;
using System.Collections.Generic;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    [Serializable]
    public struct KyoshoNoteEvent
    {
        [Tooltip("마디 내 발사 시점 (박 단위, 0.0 ~ 3.99)")]
        public float beatOffset;
        public KyoshoNoteType noteType;
        [Tooltip("예고 및 발사 시 멜로디 계이름 인덱스 (0~7: 도~높은도)")]
        [Range(0, 7)] public int pitchIndex;

        public KyoshoNoteEvent(float offset, KyoshoNoteType type, int pitch = 0)
        {
            beatOffset = offset;
            noteType = type;
            pitchIndex = pitch;
        }
    }

    [Serializable]
    public class KyoshoRhythmBar
    {
        public string label = "1마디 악보";
        public List<KyoshoNoteEvent> notes = new List<KyoshoNoteEvent>();
    }

    [Serializable]
    public class KyoshoVariationSymbolDef
    {
        public int symbolIndex;
        public string symbolName;
        public string glyph;
        public string solfegeName;
        public float frequencyHz = 261.63f;
        public Color borderColor = Color.cyan;
        public RSNanoSpriteShape spriteShape;
    }

    /// <summary>
    /// [쿄쇼 리듬 악보 & 변주곡 문양 테이블 SO]
    /// 난이도별 스타카토 1마디 악보 풀과 변주곡 6종 문양/음계/스프라이트 매핑을 보관함.
    /// </summary>
    [CreateAssetMenu(fileName = "KyoshoRhythmTable", menuName = "RealSteel/Boss/Kyosho Rhythm Table", order = 10)]
    public class KyoshoRhythmTableSO : ScriptableObject
    {
        [Header("1. 스타카토 리듬 악보 풀 (난이도별)")]
        public List<KyoshoRhythmBar> veryEasyAndEasyBars = new List<KyoshoRhythmBar>();
        public List<KyoshoRhythmBar> normalBars = new List<KyoshoRhythmBar>();
        public List<KyoshoRhythmBar> hardBars = new List<KyoshoRhythmBar>();
        public List<KyoshoRhythmBar> hellBars = new List<KyoshoRhythmBar>();

        [Header("2. 변주곡 6종 문양 (0~5: 도~라) & 오케스트라 · 악기 섹션 스프라이트")]
        public List<KyoshoVariationSymbolDef> variationSymbols = new List<KyoshoVariationSymbolDef>();
        public RSNanoSpriteShape orchestraBodyShape;
        public RSNanoSpriteShape stringsSectionShape;
        public RSNanoSpriteShape percussionSectionShape;
        public RSNanoSpriteShape brassSectionShape;

        public void EnsureDefaults()
        {
            if (veryEasyAndEasyBars.Count == 0)
            {
                veryEasyAndEasyBars.Add(MakeBar("기본 1 [♩ ♩ 𝄽 ♩]",
                    (0f, KyoshoNoteType.Quarter, 0), (1f, KyoshoNoteType.Quarter, 2), (2f, KyoshoNoteType.Rest, 0), (3f, KyoshoNoteType.Quarter, 4)));
                veryEasyAndEasyBars.Add(MakeBar("기본 2 [♩ 𝄽 ♩ ♩]",
                    (0f, KyoshoNoteType.Quarter, 7), (1f, KyoshoNoteType.Rest, 0), (2f, KyoshoNoteType.Quarter, 4), (3f, KyoshoNoteType.Quarter, 2)));
                veryEasyAndEasyBars.Add(MakeBar("기본 3 [♩ ♩ ♩ 𝄽]",
                    (0f, KyoshoNoteType.Quarter, 2), (1f, KyoshoNoteType.Quarter, 4), (2f, KyoshoNoteType.Quarter, 7), (3f, KyoshoNoteType.Rest, 0)));
                veryEasyAndEasyBars.Add(MakeBar("기본 4 [♩ ♩ ♩ ♩]",
                    (0f, KyoshoNoteType.Quarter, 4), (1f, KyoshoNoteType.Quarter, 5), (2f, KyoshoNoteType.Quarter, 6), (3f, KyoshoNoteType.Quarter, 7)));
            }

            if (normalBars.Count == 0)
            {
                normalBars.Add(MakeBar("보통 1 [♩ ♪♪ 𝄽 ♩]",
                    (0f, KyoshoNoteType.Quarter, 0), (1f, KyoshoNoteType.Eighth, 2), (1.5f, KyoshoNoteType.Eighth, 4), (2f, KyoshoNoteType.Rest, 0), (3f, KyoshoNoteType.Quarter, 7)));
                normalBars.Add(MakeBar("보통 2 [♪♪ ♩ ♪♪ 𝄽]",
                    (0f, KyoshoNoteType.Eighth, 4), (0.5f, KyoshoNoteType.Eighth, 5), (1f, KyoshoNoteType.Quarter, 7), (2f, KyoshoNoteType.Eighth, 6), (2.5f, KyoshoNoteType.Eighth, 4), (3f, KyoshoNoteType.Rest, 0)));
                normalBars.Add(MakeBar("보통 3 [♩ ♪♪ ♪♪ 𝄽]",
                    (0f, KyoshoNoteType.Quarter, 2), (1f, KyoshoNoteType.Eighth, 4), (1.5f, KyoshoNoteType.Eighth, 5), (2f, KyoshoNoteType.Eighth, 7), (2.5f, KyoshoNoteType.Eighth, 4), (3f, KyoshoNoteType.Rest, 0)));
                normalBars.Add(MakeBar("보통 4 [♪♪ ♪♪ ♩ 𝄽]",
                    (0f, KyoshoNoteType.Eighth, 5), (0.5f, KyoshoNoteType.Eighth, 4), (1f, KyoshoNoteType.Eighth, 2), (1.5f, KyoshoNoteType.Eighth, 4), (2f, KyoshoNoteType.Quarter, 7), (3f, KyoshoNoteType.Rest, 0)));
            }

            if (hardBars.Count == 0)
            {
                hardBars.Add(MakeBar("어려움 1 [♩. ♪ ♪♪ ♩]",
                    (0f, KyoshoNoteType.Quarter, 0), (1.5f, KyoshoNoteType.Eighth, 4), (2f, KyoshoNoteType.Eighth, 5), (2.5f, KyoshoNoteType.Eighth, 6), (3f, KyoshoNoteType.Quarter, 7)));
                hardBars.Add(MakeBar("어려움 2 [♪♪ ♩. ♪ ♩]",
                    (0f, KyoshoNoteType.Eighth, 7), (0.5f, KyoshoNoteType.Eighth, 6), (1f, KyoshoNoteType.Quarter, 4), (2.5f, KyoshoNoteType.Eighth, 2), (3f, KyoshoNoteType.Quarter, 0)));
                hardBars.Add(MakeBar("어려움 3 [♩. ♪ 𝄽 ♪♪]",
                    (0f, KyoshoNoteType.Quarter, 2), (1.5f, KyoshoNoteType.Eighth, 4), (2f, KyoshoNoteType.Rest, 0), (3f, KyoshoNoteType.Eighth, 6), (3.5f, KyoshoNoteType.Eighth, 7)));
                hardBars.Add(MakeBar("어려움 4 [♪♪ ♪♪ ♩ ♩]",
                    (0f, KyoshoNoteType.Eighth, 4), (0.5f, KyoshoNoteType.Eighth, 7), (1f, KyoshoNoteType.Eighth, 6), (1.5f, KyoshoNoteType.Eighth, 5), (2f, KyoshoNoteType.Quarter, 4), (3f, KyoshoNoteType.Quarter, 7)));
            }

            if (hellBars.Count == 0)
            {
                hellBars.Add(MakeBar("헬 1 [3連 ♩ 3連 𝄽]",
                    (0f, KyoshoNoteType.Triplet, 0), (1f / 3f, KyoshoNoteType.Triplet, 2), (2f / 3f, KyoshoNoteType.Triplet, 4),
                    (1f, KyoshoNoteType.Quarter, 7),
                    (2f, KyoshoNoteType.Triplet, 6), (2f + 1f / 3f, KyoshoNoteType.Triplet, 4), (2f + 2f / 3f, KyoshoNoteType.Triplet, 2),
                    (3f, KyoshoNoteType.Rest, 0)));
                hellBars.Add(MakeBar("헬 2 [♩. ♪ 3連 ♩]",
                    (0f, KyoshoNoteType.Quarter, 4), (1.5f, KyoshoNoteType.Eighth, 2),
                    (2f, KyoshoNoteType.Triplet, 4), (2f + 1f / 3f, KyoshoNoteType.Triplet, 5), (2f + 2f / 3f, KyoshoNoteType.Triplet, 6),
                    (3f, KyoshoNoteType.Quarter, 7)));
                hellBars.Add(MakeBar("헬 3 [3連 3連 ♪♪ ♩]",
                    (0f, KyoshoNoteType.Triplet, 7), (1f / 3f, KyoshoNoteType.Triplet, 6), (2f / 3f, KyoshoNoteType.Triplet, 5),
                    (1f, KyoshoNoteType.Triplet, 4), (1f + 1f / 3f, KyoshoNoteType.Triplet, 2), (1f + 2f / 3f, KyoshoNoteType.Triplet, 4),
                    (2f, KyoshoNoteType.Eighth, 5), (2.5f, KyoshoNoteType.Eighth, 6),
                    (3f, KyoshoNoteType.Quarter, 7)));
            }

            if (variationSymbols.Count < 6)
            {
                variationSymbols.Clear();
                for (int i = 0; i < 6; i++)
                {
                    variationSymbols.Add(new KyoshoVariationSymbolDef
                    {
                        symbolIndex = i,
                        symbolName = KyoshoConstants.SymbolNames[i],
                        glyph = KyoshoConstants.SymbolGlyphs[i],
                        solfegeName = KyoshoConstants.SolfegeNames[i],
                        frequencyHz = KyoshoConstants.SolfegeFrequencies[i],
                        borderColor = KyoshoConstants.SymbolBorderColors[i]
                    });
                }
            }
        }

        public KyoshoRhythmBar PickRandomBar(BossDifficulty difficulty)
        {
            EnsureDefaults();
            List<KyoshoRhythmBar> pool = difficulty switch
            {
                BossDifficulty.VeryEasy => veryEasyAndEasyBars,
                BossDifficulty.Easy => veryEasyAndEasyBars,
                BossDifficulty.Normal => normalBars,
                BossDifficulty.Hard => hardBars,
                BossDifficulty.Hell => hellBars,
                _ => normalBars
            };

            if (pool == null || pool.Count == 0) pool = normalBars;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        public KyoshoVariationSymbolDef GetSymbolDef(int index)
        {
            EnsureDefaults();
            int clamped = Mathf.Clamp(index, 0, variationSymbols.Count - 1);
            return variationSymbols[clamped];
        }

        public RSNanoSpriteShape GetSectionShape(KyoshoSectionType type)
        {
            return type switch
            {
                KyoshoSectionType.Strings => stringsSectionShape,
                KyoshoSectionType.Percussion => percussionSectionShape,
                KyoshoSectionType.Brass => brassSectionShape,
                _ => null
            };
        }

        private static KyoshoRhythmBar MakeBar(string label, params (float offset, KyoshoNoteType type, int pitch)[] items)
        {
            var bar = new KyoshoRhythmBar { label = label };
            foreach (var item in items)
            {
                bar.notes.Add(new KyoshoNoteEvent(item.offset, item.type, item.pitch));
            }
            return bar;
        }
    }
}
