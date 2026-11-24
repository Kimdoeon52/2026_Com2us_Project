using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「크레셴도」 수치 (2026-11-24, 2악장 후보 — 기획서에 아직 없음. 타격 부위 오른다리 = 기획서 0.3 '2악장에서 배정' 자리).
/// 흐름: 지휘봉을 내리칠 때마다 발밑 모래가 파도가 되어 좌우로 바닥을 따라 밀려간다. 박자마다 한 파도, 작게(pp) 시작해 점점 크게(ff).
/// 낮은 파도는 점프로 넘고, 마지막 큰 파도는 회피 무적으로. 수치는 전부 TODO 임시값(기획 수치 없음).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_WaveData", menuName = "NYH/Boss/Maestro Wave Data")]
public class MaestroWaveData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("파도 수")]
        [Min(1)] public int waves = 4;
        [Tooltip("파도 사이 간격 (초) — 내리치는 박자")]
        [Min(0.2f)] public float interval = 0.85f;
        [Tooltip("첫 파도 → 마지막 파도 높이 (m). 점프 높이를 넘으면 회피 무적으로만 피한다")]
        public Vector2 height = new Vector2(0.5f, 0.9f);
        [Tooltip("파도가 밀려가는 속도 (m/s)")]
        [Min(0.5f)] public float speed = 7f;
    }

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬) — TODO 전부 임시값")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { waves = 3, interval = 1.0f,  height = new Vector2(0.4f, 0.7f), speed = 6f },
        new DifficultyRow { waves = 4, interval = 0.95f, height = new Vector2(0.4f, 0.8f), speed = 6.5f },
        new DifficultyRow { waves = 4, interval = 0.85f, height = new Vector2(0.5f, 0.9f), speed = 7f },
        new DifficultyRow { waves = 5, interval = 0.75f, height = new Vector2(0.5f, 1.2f), speed = 8f },
        new DifficultyRow { waves = 5, interval = 0.7f,  height = new Vector2(0.6f, 1.4f), speed = 9f },
    };

    [Header("단계 · 모양 (TODO 임시값)")]
    [Tooltip("① 첫 내리치기 전 치켜드는 시간 (초) — 예고")]
    [Min(0.1f)] public float windupSeconds = 0.7f;
    [Tooltip("내리치기 → 파도가 출발하기까지 (초). 바닥에 고리가 번쩍이는 예고")]
    [Min(0f)] public float launchDelay = 0.15f;
    [Tooltip("파도 폭 (m, 앞뒤 길이)")]
    [Min(0.2f)] public float width = 1.0f;
    [Tooltip("판정 폭 = 파도 폭 × 이 값 (가장자리는 성겨서 조금 좁게)")]
    [Range(0.2f, 1f)] public float hitWidthRatio = 0.6f;
    [Tooltip("파도 그림 봇 간격 (m) — 클수록 봇이 덜 든다")]
    [Min(0.01f)] public float botPitch = 0.05f;
    [Tooltip("파도가 지나가며 뒤로 흘리는 모래 (초당 봇)")]
    [Min(0f)] public float spray = 60f;
    [Range(0f, 4f)] public float crestGlow = 1.6f;
    [Tooltip("흩어진 모래가 재흡수되기까지 (초)")]
    [Min(0f)] public float reabsorbDelay = 0.8f;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
