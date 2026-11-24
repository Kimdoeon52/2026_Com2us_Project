using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「원형 음탄」 수치 (2026-11-24, 사용자 시트 '음표 폭풍 / 원형 음탄', 2악장 후보 — 기획서에 아직 없음).
/// 흐름: 두 팔을 펼치면 음표가 보스 둘레에 고리로 모여 돈다 → 박자마다 하나씩 바깥으로 발사(고리가 돌고 있어 나선으로 퍼짐) →
/// 마지막 박에 남은 음표가 한꺼번에 터진다. 수치는 전부 TODO 임시값(기획 수치 없음).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_OrbitData", menuName = "NYH/Boss/Maestro Orbit Data")]
public class MaestroOrbitData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("고리에 모이는 음표 수")]
        [Min(3)] public int notes = 10;
        [Tooltip("고리 회전 속도 (도/초)")]
        public float spinDegPerSec = 50f;
        [Tooltip("하나씩 발사하는 간격 (초)")]
        [Min(0.05f)] public float interval = 0.45f;
        [Tooltip("음표 비행 속도 (m/s)")]
        [Min(0.5f)] public float speed = 4.5f;
        [Tooltip("마지막 박에 한꺼번에 터지는 음표 수")]
        [Min(0)] public int finalBurst = 3;
    }

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬) — TODO 전부 임시값")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { notes = 8,  spinDegPerSec = 40f, interval = 0.5f,  speed = 4f,   finalBurst = 2 },
        new DifficultyRow { notes = 10, spinDegPerSec = 50f, interval = 0.45f, speed = 4.5f, finalBurst = 3 },
        new DifficultyRow { notes = 12, spinDegPerSec = 60f, interval = 0.4f,  speed = 5f,   finalBurst = 3 },
        new DifficultyRow { notes = 14, spinDegPerSec = 75f, interval = 0.35f, speed = 5.5f, finalBurst = 4 },
        new DifficultyRow { notes = 16, spinDegPerSec = 90f, interval = 0.3f,  speed = 6f,   finalBurst = 4 },
    };

    [Header("단계 (TODO 임시값)")]
    [Tooltip("① 두 팔을 펼치며 음표가 고리로 모이는 시간 (초)")]
    [Min(0.1f)] public float gatherSeconds = 1.0f;
    [Tooltip("② 발사 전 고리가 도는 시간 (초) — 예고")]
    [Min(0f)] public float spinSeconds = 1.0f;
    [Tooltip("마지막 한꺼번에 터지기 전 쉼 (초)")]
    [Min(0f)] public float burstPause = 0.3f;

    [Header("고리 · 음표")]
    [Tooltip("고리 반지름 = 보스 키 × 이 값")]
    [Min(0.1f)] public float ringRadiusRatio = 0.55f;
    [Tooltip("고리 세로 납작함 (1 = 원)")]
    [Range(0.3f, 1f)] public float ringFlatten = 0.75f;
    [Tooltip("판정 반경 (m)")]
    [Min(0.05f)] public float hitRadius = 0.2f;
    [Tooltip("음표 그림 크기 배율 (8분음표 모양)")]
    [Min(0.2f)] public float visualScale = 0.8f;
    [Range(0f, 4f)] public float glow = 1.8f;
    [Tooltip("최대 비행 시간 (초)")]
    [Min(0.5f)] public float maxFlightSeconds = 5f;
    [Tooltip("흩어진 모래가 재흡수되기까지 (초)")]
    [Min(0f)] public float reabsorbDelay = 0.8f;
    [Range(0f, 1f)] public float volume = 0.5f;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
