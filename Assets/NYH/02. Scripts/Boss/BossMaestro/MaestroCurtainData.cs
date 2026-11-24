using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「달빛 커튼」 수치 (보스3_월광_기획 1.5, 2026-11-19 3단계).
/// 기획서에 있는 값: 단계 시간(흐름 1 / 예고 1 / 공격 6 / 붕괴 1초), 난이도별 틈 수·틈 너비·방향 전환 횟수.
/// 기획서에 문장만 있는 값(이동 속도 느림/보통/빠름)과 없는 값(낙하 속도, 봇 수, 피격 뒤 무적 등)은 TODO 임시값.
/// </summary>
[CreateAssetMenu(fileName = "Maestro_CurtainData", menuName = "NYH/Boss/Maestro Curtain Data")]
public class MaestroCurtainData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("틈(안전지대) 수 — 기획서: 2 / 2 / 1 / 1 / 1")]
        [Range(1, 4)] public int gapCount = 1;

        [Tooltip("틈 너비 (m) — 기획서: 3 / 2.5 / 2.5 / 2 / 1.5")]
        [Min(0.3f)] public float gapWidth = 2.5f;

        [Tooltip("TODO: 기획서는 문장(느림/보통/빠름)뿐. 틈 이동 속도 (m/s)")]
        [Min(0f)] public float gapSpeed = 1f;

        [Tooltip("방향 전환 횟수 — 기획서: 없음 0 / 0 / 1 / 2. 헬은 '무작위'(아래 randomTurns)")]
        [Min(0)] public int turns;

        [Tooltip("켜면 방향 전환이 무작위 (헬) — turns 대신 randomTurnSeconds 간격 근처에서 아무 때나")]
        public bool randomTurns;
    }

    [Header("단계 시간 (초) — 기획서 1.5 임시값")]
    [Min(0.1f)] public float flowSeconds = 1f;      // ① 흐름: 봇이 천장으로 흘러올라 달빛 띠
    [Min(0.1f)] public float tellSeconds = 1f;      // ② 예고: 틈 위치에 빛 기둥
    [Min(0.1f)] public float attackSeconds = 6f;    // ③ 공격: 커튼이 흘러내림, 틈이 좌우로 이동
    [Min(0.1f)] public float collapseSeconds = 1f;  // ④ 붕괴: 바닥에 모래로 쌓였다가 재흡수

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬 순서)")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { gapCount = 2, gapWidth = 3f,   gapSpeed = 0.6f, turns = 0 },
        new DifficultyRow { gapCount = 2, gapWidth = 2.5f, gapSpeed = 0.6f, turns = 0 },
        new DifficultyRow { gapCount = 1, gapWidth = 2.5f, gapSpeed = 1.0f, turns = 1 },
        new DifficultyRow { gapCount = 1, gapWidth = 2f,   gapSpeed = 1.0f, turns = 2 },
        new DifficultyRow { gapCount = 1, gapWidth = 1.5f, gapSpeed = 1.5f, randomTurns = true },
    };

    [Tooltip("TODO: 임시값. 헬 무작위 방향 전환 — 평균 간격 (초). 최소 간격은 이 값의 절반")]
    [Min(0.2f)] public float randomTurnSeconds = 1.5f;

    [Header("커튼 (TODO 전부 임시값)")]
    [Tooltip("천장 = 화면 위 끝에서 이만큼 아래 (m). 카메라가 없으면 바닥 + fallbackCeiling")]
    public float ceilingPadding = 0.4f;
    [Min(1f)] public float fallbackCeiling = 6f;

    [Tooltip("커튼 입자 낙하 속도 (m/s)")]
    [Min(0.5f)] public float fallSpeed = 9f;

    [Tooltip("커튼에 쓰는 봇 수 (최대). 몸이 bodyMinRatio 아래로 줄지 않게 자동으로 깎인다")]
    [Min(16)] public int curtainBots = 3000;

    [Tooltip("몸에 최소한 남길 봇 비율 (전체 봇 수 기준) — 기획서 '본체 최소 크기 상한 필요'")]
    [Range(0.1f, 0.9f)] public float bodyMinRatio = 0.5f;

    [Tooltip("입자 한 줄기를 이루는 봇 수 (세로로 늘어선 빗줄기)")]
    [Range(1, 32)] public int streakBots = 8;

    [Tooltip("벽 쪽 여유 (m) — 틈이 화면 끝에 붙지 않게")]
    [Min(0f)] public float wallMargin = 0.5f;

    [Header("판정")]
    [Tooltip("TODO: 임시값. 1회 피격 뒤 커튼에 다시 맞지 않는 시간 (초) — 기획서 '1회 피격 후 무적 시간 적용'")]
    [Min(0f)] public float hitCooldown = 1f;

    [Tooltip("입자 판정 너비 (m) — 줄기 하나의 가로 폭")]
    [Min(0.01f)] public float streakHitWidth = 0.08f;

    [Header("그림")]
    [Range(0f, 4f)] public float glow = 1.2f;
    public Color pillarColor = new Color(0.75f, 0.85f, 1f, 0.28f);
    [Tooltip("빛 기둥 그리기 순서")]
    public int pillarSortingOrder = 3;
    [Tooltip("TODO: 임시값. 모래가 몸으로 재흡수되기 시작하기까지 (초)")]
    [Min(0f)] public float reabsorbDelay = 0.6f;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
