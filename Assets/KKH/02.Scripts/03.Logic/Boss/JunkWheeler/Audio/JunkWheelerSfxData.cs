using System;
using UnityEngine;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// 효과음 단일 큐 정의. 여러 클립(v1, v2...)을 등록하면 매 재생 시 무작위로 선택하고 피치 지터를 적용함.
    /// </summary>
    [Serializable]
    public class BossSfxCue
    {
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1.5f)]
        public float volume = 1f;

        [Tooltip("매 재생 시 랜덤 피치 변동폭 (0.06 = ±6%)")]
        [Range(0f, 0.35f)]
        public float pitchJitter = 0.06f;

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

            // null 엔트리를 피해 유효한 클립 선택
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
    /// [지역 1 보스 '정크 휠러(호이루)' 전용 사운드 데이터 SO]
    /// 전용 슬롯이 비어 있는 경우 JunkWheelerSfx.PlayOrFallback이 유사 음원으로 자동 폴백하므로
    /// 현재 보유한 음원만 연결해도 모든 패턴에서 자연스럽게 소리가 출력됨.
    /// </summary>
    [CreateAssetMenu(menuName = "RealSteel/Boss/JunkWheeler Sfx Data", fileName = "JunkWheelerSfxData")]
    public class JunkWheelerSfxData : ScriptableObject
    {
        [Header("0. 글로벌 오디오 밸런스")]
        [Range(0f, 1.5f)] public float masterSfxVolume = 0.85f;
        [Range(0f, 1f)] public float bgmVolume = 0.45f;
        [Tooltip("화면 좌/우 위치에 따른 스테레오 패닝 강도 (0 = 모노 중앙, 1 = 완전 좌우 분리)")]
        [Range(0f, 1f)] public float stereoSpread = 0.55f;

        [Header("1. 보스전 BGM")]
        public AudioClip bgmMain;
        public AudioClip bgmPhase2;

        [Header("2. 상시 공회전 / 주행 / V8 엔진")]
        public BossSfxCue idleLoop = new BossSfxCue { volume = 0.38f, pitchJitter = 0.02f };
        public BossSfxCue rollMove = new BossSfxCue { volume = 0.70f, pitchJitter = 0.05f };
        public BossSfxCue v8Ignition = new BossSfxCue { volume = 0.85f, pitchJitter = 0.03f };
        public BossSfxCue v8RevAggressive = new BossSfxCue { volume = 0.85f, pitchJitter = 0.05f };
        public BossSfxCue v8Backfire = new BossSfxCue { volume = 0.85f, pitchJitter = 0.08f };
        public BossSfxCue driftDonut = new BossSfxCue { volume = 0.78f, pitchJitter = 0.04f };

        [Header("3. [패턴 1] 고속 돌진 (Charge)")]
        public BossSfxCue chargeTell = new BossSfxCue { volume = 0.75f, pitchJitter = 0.02f };
        public BossSfxCue chargeRush = new BossSfxCue { volume = 0.90f, pitchJitter = 0.04f };
        public BossSfxCue chargeHitPlayer = new BossSfxCue { volume = 1.00f, pitchJitter = 0.05f };
        public BossSfxCue chargeWallSlam = new BossSfxCue { volume = 1.00f, pitchJitter = 0.04f };
        public BossSfxCue chargeRebound = new BossSfxCue { volume = 0.80f, pitchJitter = 0.06f };
        public BossSfxCue chargeDecelRoll = new BossSfxCue { volume = 0.65f, pitchJitter = 0.05f };
        public BossSfxCue chargeInterrupted = new BossSfxCue { volume = 0.95f, pitchJitter = 0.04f };
        public BossSfxCue chargeFireTrail = new BossSfxCue { volume = 0.65f, pitchJitter = 0.06f };

        [Header("4. [패턴 2] 3단 내려찍기 & 못판 유도 (Slam)")]
        public BossSfxCue slamLaunch = new BossSfxCue { volume = 0.80f, pitchJitter = 0.05f };
        public BossSfxCue slamHover = new BossSfxCue { volume = 0.65f, pitchJitter = 0.03f };
        public BossSfxCue slamDrop = new BossSfxCue { volume = 0.85f, pitchJitter = 0.05f };
        public BossSfxCue slamImpact = new BossSfxCue { volume = 1.00f, pitchJitter = 0.04f };
        public BossSfxCue nailPuncture = new BossSfxCue { volume = 1.00f, pitchJitter = 0.05f };
        public BossSfxCue tireBlowout = new BossSfxCue { volume = 0.95f, pitchJitter = 0.05f };

        [Header("5. [패턴 3] 바이킹 반원 탄막 (Barrage)")]
        public BossSfxCue barrageJump = new BossSfxCue { volume = 0.75f, pitchJitter = 0.05f };
        public BossSfxCue barrageShot = new BossSfxCue { volume = 0.75f, pitchJitter = 0.08f };
        public BossSfxCue barrageFly = new BossSfxCue { volume = 0.65f, pitchJitter = 0.08f };
        public BossSfxCue barrageHit = new BossSfxCue { volume = 0.85f, pitchJitter = 0.07f };
        public BossSfxCue barrageLand = new BossSfxCue { volume = 0.85f, pitchJitter = 0.04f };

        [Header("6. [패턴 4] 회전 톱날 당구 돌진 (Ricochet Blade)")]
        public BossSfxCue bladeDeploy = new BossSfxCue { volume = 0.90f, pitchJitter = 0.04f };
        public BossSfxCue bladeSpinLoop = new BossSfxCue { volume = 0.70f, pitchJitter = 0.02f };
        public BossSfxCue bladeBounce = new BossSfxCue { volume = 0.95f, pitchJitter = 0.08f };
        public BossSfxCue bladeSlashHit = new BossSfxCue { volume = 1.00f, pitchJitter = 0.06f };
        public BossSfxCue bladeRetract = new BossSfxCue { volume = 0.80f, pitchJitter = 0.04f };

        [Header("7. [패턴 5] 드리블 충격파 (Dribble Shockwave)")]
        public BossSfxCue dribbleTellCrack = new BossSfxCue { volume = 0.85f, pitchJitter = 0.04f };
        public BossSfxCue dribbleBounce1 = new BossSfxCue { volume = 0.78f, pitchJitter = 0.04f };
        public BossSfxCue dribbleBounce2 = new BossSfxCue { volume = 0.86f, pitchJitter = 0.04f };
        public BossSfxCue dribbleBounce3 = new BossSfxCue { volume = 0.95f, pitchJitter = 0.04f };
        public BossSfxCue dribbleBounce4 = new BossSfxCue { volume = 1.05f, pitchJitter = 0.03f };
        public BossSfxCue dribbleShockwave = new BossSfxCue { volume = 0.75f, pitchJitter = 0.05f };
        public BossSfxCue dribbleSteamExhaust = new BossSfxCue { volume = 0.80f, pitchJitter = 0.04f };

        [Header("8. 보스 피격 / 상태 변화 / 사망")]
        public BossSfxCue hitArmor = new BossSfxCue { volume = 0.80f, pitchJitter = 0.08f };
        public BossSfxCue hitRubber = new BossSfxCue { volume = 0.80f, pitchJitter = 0.08f };
        public BossSfxCue tireBreakFinal = new BossSfxCue { volume = 1.00f, pitchJitter = 0.04f };
        public BossSfxCue stateGroggy = new BossSfxCue { volume = 0.75f, pitchJitter = 0.02f };
        public BossSfxCue phase2Roar = new BossSfxCue { volume = 1.00f, pitchJitter = 0.03f };
        public BossSfxCue defeatExplosion = new BossSfxCue { volume = 1.05f, pitchJitter = 0.03f };

        [Header("9. 기계 작동음 & 필드 기믹")]
        public BossSfxCue mechGearShift = new BossSfxCue { volume = 0.80f, pitchJitter = 0.05f };
        public BossSfxCue mechTurboBlowOff = new BossSfxCue { volume = 0.80f, pitchJitter = 0.06f };
        public BossSfxCue mechHydraulicServo = new BossSfxCue { volume = 0.75f, pitchJitter = 0.05f };
        public BossSfxCue mechChainRatchet = new BossSfxCue { volume = 0.75f, pitchJitter = 0.05f };
        public BossSfxCue fieldNailBoardRise = new BossSfxCue { volume = 0.80f, pitchJitter = 0.06f };
        public BossSfxCue fieldExplosiveBoxTick = new BossSfxCue { volume = 0.75f, pitchJitter = 0.03f };
        public BossSfxCue fieldExplosiveBoxBlast = new BossSfxCue { volume = 1.00f, pitchJitter = 0.05f };
    }
}
