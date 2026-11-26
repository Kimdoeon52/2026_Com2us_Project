using System;

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
        Defeated = 7,
        EncoreAwakening = 8,
        Phase3 = 9
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
    /// 쿄쇼 박자 클럭 읽기 전용 인터페이스 (Audio/Visual 어셈블리가 Assembly-CSharp에 역의존하지 않도록 분리).
    /// </summary>
    public interface IKyoshoBeatClock
    {
        bool IsRunning { get; }
        double TotalBeats { get; }
        event Action<int, int> OnBeat;
    }

    public static class KyoshoDataConstants
    {
        // 변주곡 6종 문양 고유 계이름 주파수 (도~라: C4, D4, E4, F4, G4, A4)
        public static readonly float[] SolfegeFrequencies =
        {
            261.63f, // 0: 도 (C4)
            293.66f, // 1: 레 (D4)
            329.63f, // 2: 미 (E4)
            349.23f, // 3: 파 (F4)
            392.00f, // 4: 솔 (G4)
            440.00f  // 5: 라 (A4)
        };
    }
}
