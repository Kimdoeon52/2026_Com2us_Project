using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「빛기둥」 수치 (2026-11-26 사용자 — 패턴 제조기 모션 '오픈모션' 기반. 처음엔 빛선 연타였다가 사용자 그림대로 바꿈).
/// 화면 전체를 세로 띠로 나눠 흰 칸 = 빛기둥이 떨어지는 곳, 검은 칸 = 피하는 곳. 예고 → 낙하를 1~4번 반복하고,
/// 매번 기둥 자리를 뒤바꿔(예전 안전한 곳에 떨어짐) 계속 움직이게 한다.
/// 보스는 모션의 '보스 이동'대로 옮겨 가는데, 미끄러지지 않고 순간이동(봇이 흩어졌다 새 자리에 다시 모임).
/// 수치는 전부 TODO 임시값(기획 수치 없음).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_OpenData", menuName = "NYH/Boss/Maestro Open Data")]
public class MaestroOpenData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("빛기둥을 떨구는 횟수 — 최소 · 최대 (그 사이에서 무작위)")]
        public Vector2Int waves = new Vector2Int(2, 3);
        [Tooltip("예고가 보이고 나서 떨어질 때까지 (초)")]
        [Min(0.1f)] public float warn = 0.7f;
        [Tooltip("한 번 떨어진 뒤 다음 예고까지 쉬는 시간 (초)")]
        [Min(0f)] public float gap = 0.4f;
    }

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬) — TODO 전부 임시값")]
    public DifficultyRow[] columnRows =
    {
        new DifficultyRow { waves = new Vector2Int(1, 2), warn = 1.0f,  gap = 0.5f },
        new DifficultyRow { waves = new Vector2Int(1, 3), warn = 0.85f, gap = 0.45f },
        new DifficultyRow { waves = new Vector2Int(2, 3), warn = 0.7f,  gap = 0.4f },
        new DifficultyRow { waves = new Vector2Int(3, 4), warn = 0.6f,  gap = 0.35f },
        new DifficultyRow { waves = new Vector2Int(4, 4), warn = 0.5f,  gap = 0.3f },
    };

    [Header("모션")]
    [Tooltip("재생할 모션 (패턴 제조기 '오픈모션'). 보스 이동도 이 모션의 '보스 이동' 선을 따른다")]
    public MaestroMotion motion;
    [Tooltip("x = 빛기둥을 떨구는 동안 멈춰 둘 모션 시간(두 팔 활짝), y = 다 떨군 뒤 이어서 재생할 모션 시간 (초)")]
    public Vector2 holdTimes = new Vector2(1.0f, 2.5f);

    [Header("순간이동 (11-26 사용자: 특정 위치로 옮겨 갈 때)")]
    [Tooltip("켜면 모션의 '보스 이동'을 미끄러지듯 따라가지 않고, 그 이동이 시작되는 순간 도착 자리로 순간이동")]
    public bool teleport = true;
    [Tooltip("흩어진 봇이 새 자리에 다 모이는 시간 (초)")]
    [Min(0.1f)] public float teleportSeconds = 0.45f;

    [Header("빛기둥 (TODO 임시값)")]
    [Tooltip("빛기둥(흰 칸) 폭 = 플레이어 키 × 이 값")]
    [Range(0.3f, 4f)] public float pillarWidthPerPlayer = 1.1f;
    [Tooltip("피하는 곳(검은 칸) 폭 = 플레이어 키 × 이 값")]
    [Range(0.3f, 4f)] public float safeWidthPerPlayer = 1.3f;
    [Tooltip("켜면 다음 번엔 기둥 자리를 뒤바꾼다 (예전 안전한 곳에 떨어짐)")]
    public bool alternate = true;
    [Tooltip("하늘에서 바닥까지 떨어지는 시간 (초)")]
    [Min(0.02f)] public float dropSeconds = 0.1f;
    [Tooltip("바닥에 닿은 뒤 판정이 있는 시간 (초)")]
    [Min(0.02f)] public float activeSeconds = 0.3f;
    [Tooltip("사라지는 시간 (초)")]
    [Min(0.05f)] public float fadeSeconds = 0.25f;

    [Header("그림")]
    [Tooltip("예고 칸 진하기 — 처음 → 끝 (점점 진해지고 마지막 0.2초 깜박임)")]
    public Vector2 warnAlpha = new Vector2(0.08f, 0.3f);

    public DifficultyRow Row(BossDifficulty d)
    {
        if (columnRows == null || columnRows.Length == 0) return null;
        return columnRows[Mathf.Clamp((int)d, 0, columnRows.Length - 1)];
    }
}
