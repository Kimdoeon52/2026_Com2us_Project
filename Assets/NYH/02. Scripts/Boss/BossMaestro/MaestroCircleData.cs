using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「원 음표탄」 수치 (2026-11-26 사용자 — 패턴 제조기 모션 '원그리기' + 메모).
/// 메모: 점마다 작은 음표탄 생성 → 플레이어에게 차례차례 후두둑, 유도성은 너무 높지 않게, 바닥·벽에 부딪히면 부서짐,
///       궤도는 직선적, 한 번에 쏘는 느낌보다 하나씩 빠르게 다다다닥.
/// 수치는 전부 TODO 임시값(기획 수치 없음).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_CircleData", menuName = "NYH/Boss/Maestro Circle Data")]
public class MaestroCircleData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("모션의 생성 점 중 쓰는 비율 (1 = 전부, 0.5 = 하나 건너 하나)")]
        [Range(0.2f, 1f)] public float pointRatio = 1f;
        [Tooltip("하나씩 쏘는 간격 (초) — 짧을수록 다다다닥")]
        [Min(0.02f)] public float fireInterval = 0.08f;
        [Tooltip("음표탄 속도 (m/s)")]
        [Min(1f)] public float speed = 9f;
        [Tooltip("유도 — 날아가며 플레이어 쪽으로 도는 최대 각속도 (도/초). 0 = 직선만")]
        [Min(0f)] public float turnDegPerSec = 40f;
    }

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬) — TODO 전부 임시값")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { pointRatio = 0.5f,  fireInterval = 0.14f, speed = 7f,  turnDegPerSec = 20f },
        new DifficultyRow { pointRatio = 0.67f, fireInterval = 0.11f, speed = 8f,  turnDegPerSec = 30f },
        new DifficultyRow { pointRatio = 1f,    fireInterval = 0.08f, speed = 9f,  turnDegPerSec = 40f },
        new DifficultyRow { pointRatio = 1f,    fireInterval = 0.07f, speed = 10f, turnDegPerSec = 50f },
        new DifficultyRow { pointRatio = 1f,    fireInterval = 0.055f, speed = 11f, turnDegPerSec = 60f },
    };

    [Header("모션")]
    [Tooltip("재생할 모션 (패턴 제조기 '원그리기'). 점(생성) 이벤트 = 음표가 생기는 시간·자리, 선(탄 궤적) 이벤트 시간 = 발사 시작")]
    public MaestroMotion motion;
    [Tooltip("모션에 선 이벤트가 없을 때 발사 시작 (초)")]
    [Min(0f)] public float fallbackFireTime = 1.4f;

    [Header("음표탄 (TODO 임시값)")]
    [Tooltip("음표 크기 배율 = 플레이어 키 × 이 값 (그림·판정 같이)")]
    [Range(0.05f, 1f)] public float sizePerPlayer = 0.3f;
    [Tooltip("판정 반지름 = 음표 크기 배율 × 이 값 (m)")]
    [Min(0.05f)] public float hitRadius = 0.2f;
    [Tooltip("조준 높이 = 플레이어 발밑 + 플레이어 키 × 이 값")]
    [Range(0f, 1f)] public float aimHeightPerPlayer = 0.5f;
    [Tooltip("유도하는 시간 (초) — 그 뒤엔 직선")]
    [Min(0f)] public float homingSeconds = 0.35f;
    [Tooltip("최대 비행 시간 (초)")]
    [Min(0.5f)] public float maxFlightSeconds = 4f;
    [Tooltip("부서진 봇이 몸으로 돌아가기까지 (초)")]
    [Min(0f)] public float reabsorbDelay = 0.8f;

    [Header("그림 · 소리")]
    [Min(0f)] public float glow = 1.5f;
    [Tooltip("생긴 음표가 제자리에서 두근거리는 크기 (판정과 무관)")]
    [Range(0f, 0.5f)] public float bob = 0.15f;
    [Tooltip("선율·녹음 음원 (음표 공격 수치 공용). 비우면 소리 없음")]
    public MaestroNoteData noteData;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
