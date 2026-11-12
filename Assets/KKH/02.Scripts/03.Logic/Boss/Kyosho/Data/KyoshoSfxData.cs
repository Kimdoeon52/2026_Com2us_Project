using System;
using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    [Serializable]
    public class KyoshoSfxCue
    {
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1.5f)]
        public float volume = 1f;

        [Range(0f, 0.35f)]
        public float pitchJitter = 0.03f;

        public bool HasValidClip
        {
            get
            {
                if (clips == null || clips.Length == 0) return false;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null) return true;
                }
                return false;
            }
        }

        public AudioClip PickRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            int start = UnityEngine.Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip c = clips[(start + i) % clips.Length];
                if (c != null) return c;
            }
            return null;
        }
    }

    /// <summary>
    /// [지역 3 보스 '쿄쇼' 전용 사운드 데이터 SO (§8.2)]
    /// AudioClip이 미할당된 슬롯은 KyoshoSfx가 절차적 오디오(신디사이저)로 자동 폴백 재생함.
    /// </summary>
    [CreateAssetMenu(menuName = "RealSteel/Boss/Kyosho Sfx Data", fileName = "KyoshoSfxData", order = 11)]
    public class KyoshoSfxData : ScriptableObject
    {
        [Header("0. 글로벌 오디오 밸런스")]
        [Range(0f, 1.5f)] public float masterSfxVolume = 0.85f;
        [Range(0f, 1f)] public float bgmVolume = 0.40f;
        [Range(0f, 1f)] public float stereoSpread = 0.50f;

        [Header("1. 보스전 BGM")]
        public AudioClip bgmMain;
        public AudioClip bgmPhase2;

        [Header("2. 박자 / 공통 메트로놈")]
        public KyoshoSfxCue beatDownbeat = new KyoshoSfxCue { volume = 0.75f, pitchJitter = 0f };
        public KyoshoSfxCue beatSub = new KyoshoSfxCue { volume = 0.45f, pitchJitter = 0f };

        [Header("3. 지휘 도형 (Conduct)")]
        public KyoshoSfxCue conductWhoosh = new KyoshoSfxCue { volume = 0.80f, pitchJitter = 0.05f };
        public KyoshoSfxCue conductTipFlash = new KyoshoSfxCue { volume = 0.65f, pitchJitter = 0.02f };
        public KyoshoSfxCue percussionShockwave = new KyoshoSfxCue { volume = 0.90f, pitchJitter = 0.04f };

        [Header("4. 스타카토 (Staccato)")]
        public KyoshoSfxCue staccatoCallNote = new KyoshoSfxCue { volume = 0.70f, pitchJitter = 0f };
        public KyoshoSfxCue staccatoFire = new KyoshoSfxCue { volume = 0.75f, pitchJitter = 0.04f };
        public KyoshoSfxCue stringsRapidFire = new KyoshoSfxCue { volume = 0.70f, pitchJitter = 0.05f };

        [Header("5. 개화 (Bloom)")]
        public KyoshoSfxCue bloomSeedToss = new KyoshoSfxCue { volume = 0.75f, pitchJitter = 0.04f };
        public KyoshoSfxCue bloomStemGrow = new KyoshoSfxCue { volume = 0.60f, pitchJitter = 0.04f };
        public KyoshoSfxCue bloomStemCut = new KyoshoSfxCue { volume = 0.90f, pitchJitter = 0.05f };
        public KyoshoSfxCue bloomOpenNote = new KyoshoSfxCue { volume = 0.80f, pitchJitter = 0.03f };
        public KyoshoSfxCue bloomOpenTrumpet = new KyoshoSfxCue { volume = 0.85f, pitchJitter = 0.03f };
        public KyoshoSfxCue bloomOpenCymbal = new KyoshoSfxCue { volume = 0.85f, pitchJitter = 0.04f };

        [Header("6. 페르마타 (Fermata)")]
        public KyoshoSfxCue fermataSpawn = new KyoshoSfxCue { volume = 0.75f, pitchJitter = 0.03f };
        public KyoshoSfxCue fermataHoldChord = new KyoshoSfxCue { volume = 0.80f, pitchJitter = 0f };
        public KyoshoSfxCue fermataSlamDrop = new KyoshoSfxCue { volume = 0.95f, pitchJitter = 0.03f };
        public KyoshoSfxCue brassBeam = new KyoshoSfxCue { volume = 0.90f, pitchJitter = 0.02f };

        [Header("7. 변주곡 (Variation — 6음계 도~라)")]
        public KyoshoSfxCue[] variationMelodyNotes = new KyoshoSfxCue[6];
        public KyoshoSfxCue variationCorrect = new KyoshoSfxCue { volume = 0.85f, pitchJitter = 0f };
        public KyoshoSfxCue variationFailDissonance = new KyoshoSfxCue { volume = 0.95f, pitchJitter = 0f };

        [Header("8. 피날레 & 섹션 파괴")]
        public KyoshoSfxCue finaleGather = new KyoshoSfxCue { volume = 0.85f, pitchJitter = 0f };
        public KyoshoSfxCue finaleFullBlast = new KyoshoSfxCue { volume = 1.00f, pitchJitter = 0f };
        public KyoshoSfxCue sectionBroken = new KyoshoSfxCue { volume = 0.95f, pitchJitter = 0.04f };
        public KyoshoSfxCue encoreApplause = new KyoshoSfxCue { volume = 0.80f, pitchJitter = 0f };
        public KyoshoSfxCue encoreAwaken = new KyoshoSfxCue { volume = 0.90f, pitchJitter = 0f };
    }
}
