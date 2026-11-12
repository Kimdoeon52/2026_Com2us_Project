using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    public enum KyoshoPhaseState
    {
        Intro = 0,
        Phase1 = 1,
        Variation1 = 2,
        Phase2 = 3,
        Variation2 = 4,
        Finale = 5,
        FinalGroggy = 6,
        Defeated = 7
    }

    public enum KyoshoSectionType
    {
        Strings = 0,
        Percussion = 1,
        Brass = 2
    }

    public enum KyoshoBloomType
    {
        Note = 0,
        Trumpet = 1,
        Cymbal = 2
    }

    public enum KyoshoNoteType
    {
        Rest = 0,
        Quarter = 1,
        Eighth = 2,
        Sixteenth = 3,
        Triplet = 4
    }

    /// <summary>
    /// 지역 3 보스 '쿄쇼 (Kyosho)' 공용 상수, ID, 색상 팔레트 및 난이도 파라미터 헬퍼.
    /// </summary>
    public static class KyoshoConstants
    {
        public const string BossID = "BOSS_REGION_03";
        public const string BossDisplayName = "쿄쇼 (나노봇 오케스트라 마에스트로)";

        // 패턴 식별 ID
        public const string PatternConductID = "PATTERN_KYOSHO_CONDUCT";
        public const string PatternStaccatoID = "PATTERN_KYOSHO_STACCATO";
        public const string PatternBloomID = "PATTERN_KYOSHO_BLOOM";
        public const string PatternFermataID = "PATTERN_KYOSHO_FERMATA";
        public const string PatternFinaleID = "PATTERN_KYOSHO_FINALE";
        public const string PatternVariationID = "PATTERN_KYOSHO_VARIATION";

        // 악기 섹션 기믹 ID
        public const string SectionStringsID = "SECTION_STRINGS";
        public const string SectionPercussionID = "SECTION_PERCUSSION";
        public const string SectionBrassID = "SECTION_BRASS";

        // 대사/바크 식별자 (§18.19)
        public const string BarkStringsBroken = "PART_KYOSHO_STRINGS";
        public const string BarkPercussionBroken = "PART_KYOSHO_PERCUSSION";
        public const string BarkBrassBroken = "PART_KYOSHO_BRASS";
        public const string BarkVariationSymbol = "PART_KYOSHO_VARIATION_SYMBOL";
        public const string BarkBloomStem = "PART_KYOSHO_BLOOM_STEM";

        // HP 게이트 비율
        public const float Variation1HpRatio = 0.60f;
        public const float Variation2HpRatio = 0.30f;
        public const float FinaleHpRatio = 0.10f;

        // 섹션 & 본체 컬러 팔레트 (§2.2)
        public static readonly Color OriginalPatternColor = new Color(0.910f, 0.957f, 1.000f, 1f); // #E8F4FF
        public static readonly Color StringsMintColor = new Color(0.251f, 1.000f, 0.667f, 1f);     // #40FFAA
        public static readonly Color PercussionAmberColor = new Color(1.000f, 0.533f, 0.125f, 1f); // #FF8820
        public static readonly Color BrassMagentaColor = new Color(1.000f, 0.267f, 0.667f, 1f);    // #FF44AA
        public static readonly Color ConductP1TipColor = Color.white;                              // #FFFFFF
        public static readonly Color ConductP2TipColor = new Color(1.000f, 0.878f, 0.200f, 1f);    // #FFE033
        public static readonly Color SafeZoneGoldColor = new Color(1.000f, 0.840f, 0.280f, 1f);

        // 변주곡 6종 문양 고유 계이름 주파수 (도~라: C4, D4, E4, F4, G4, A4)
        public static readonly float[] SolfegeFrequencies =
        {
            261.63f, // 0: 𝄞 도 (C4)
            293.66f, // 1: 𝄢 레 (D4)
            329.63f, // 2: ♯ 미 (E4)
            349.23f, // 3: ♭ 파 (F4)
            392.00f, // 4: 𝄽 솔 (G4)
            440.00f  // 5: ⌒ 라 (A4)
        };

        public static readonly string[] SymbolGlyphs = { "𝄞", "𝄢", "♯", "♭", "𝄽", "⌒" };
        public static readonly string[] SymbolNames = { "높은음자리표", "낮은음자리표", "샵", "플랫", "쉼표", "페르마타" };
        public static readonly string[] SolfegeNames = { "도", "레", "미", "파", "솔", "라" };

        public static readonly Color[] SymbolBorderColors =
        {
            new Color(0.35f, 0.85f, 1.00f, 1f), // 0: 하늘색
            new Color(0.35f, 0.95f, 0.55f, 1f), // 1: 초록색
            new Color(1.00f, 0.86f, 0.25f, 1f), // 2: 노란색
            new Color(1.00f, 0.56f, 0.20f, 1f), // 3: 주황색
            new Color(0.75f, 0.42f, 1.00f, 1f), // 4: 보라색
            new Color(1.00f, 0.42f, 0.72f, 1f), // 5: 분홍색
        };

        public static float GetBpmForDifficulty(BossDifficulty difficulty)
        {
            return difficulty switch
            {
                BossDifficulty.VeryEasy => 70f,
                BossDifficulty.Easy => 88f,
                BossDifficulty.Normal => 112f,
                BossDifficulty.Hard => 132f,
                BossDifficulty.Hell => 160f,
                _ => 112f
            };
        }

        public static string GetTempoMarking(BossDifficulty difficulty)
        {
            return difficulty switch
            {
                BossDifficulty.VeryEasy => "Andante",
                BossDifficulty.Easy => "Moderato",
                BossDifficulty.Normal => "Allegro",
                BossDifficulty.Hard => "Vivace",
                BossDifficulty.Hell => "Presto",
                _ => "Allegro"
            };
        }

        public static int GetVariationSymbolCount(BossDifficulty difficulty)
        {
            return difficulty <= BossDifficulty.Easy ? 4 : 6;
        }

        public static float GetVariationMorphBeats(BossDifficulty difficulty)
        {
            return difficulty switch
            {
                BossDifficulty.VeryEasy => 2.0f,
                BossDifficulty.Easy => 1.5f,
                BossDifficulty.Normal => 1.0f,
                BossDifficulty.Hard => 1.0f,
                BossDifficulty.Hell => 0.5f,
                _ => 1.0f
            };
        }

        public static int GetBloomSeedCount(BossDifficulty difficulty)
        {
            return difficulty switch
            {
                BossDifficulty.VeryEasy => 3,
                BossDifficulty.Easy => 3,
                BossDifficulty.Normal => 4,
                BossDifficulty.Hard => 5,
                BossDifficulty.Hell => 6,
                _ => 4
            };
        }

        public static int GetFermataBaseBulletCount(BossDifficulty difficulty)
        {
            return difficulty switch
            {
                BossDifficulty.VeryEasy => 6,
                BossDifficulty.Easy => 6,
                BossDifficulty.Normal => 8,
                BossDifficulty.Hard => 10,
                BossDifficulty.Hell => 12,
                _ => 8
            };
        }

        public static bool SupportsBrassSection(BossDifficulty difficulty)
        {
            return difficulty >= BossDifficulty.Normal;
        }

        public static bool SupportsVariation2(BossDifficulty difficulty)
        {
            return difficulty >= BossDifficulty.Normal;
        }

        public static int GetFinaleSafeZoneCount(BossDifficulty difficulty)
        {
            return difficulty <= BossDifficulty.Easy ? 2 : 1;
        }
    }
}
