using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「거대 지휘봉」 수치 (보스3_월광_기획 1.3, 2026-11-24 4단계).
/// 기획서에 있는 값: 궤적 반복(2/2/3/3/3), 예고선(1.0/1.0/1.0/0.8/0.6초), 지휘봉 체력(보스 최대 HP의 5%), 그로기(4초), 헬 회차마다 가속.
/// 기획서에 문장만 있는 값(궤적 속도 느림/보통/빠름)과 없는 값(궤적 크기, 지휘봉 길이·두께, 단계 시간 등)은 TODO 임시값.
/// </summary>
[CreateAssetMenu(fileName = "Maestro_BatonData", menuName = "NYH/Boss/Maestro Baton Data")]
public class MaestroBatonData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("궤적 반복 횟수 — 기획서: 2 / 2 / 3 / 3 / 3")]
        [Min(1)] public int loops = 2;

        [Tooltip("TODO: 기획서는 문장(느림/보통/빠름)뿐. 궤적 한 바퀴 시간 (초) — 짧을수록 빠름")]
        [Min(0.3f)] public float loopSeconds = 2.4f;

        [Tooltip("회차마다 한 바퀴 시간에 곱함 (1 = 가속 없음). 기획서: 헬만 '회차마다 가속'")]
        [Range(0.5f, 1f)] public float loopSpeedUp = 1f;

        [Tooltip("③ 예고선 시간 (초) — 기획서: 1.0 / 1.0 / 1.0 / 0.8 / 0.6")]
        [Min(0.1f)] public float tellSeconds = 1f;
    }

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬 순서)")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { loops = 2, loopSeconds = 3.0f, tellSeconds = 1.0f },
        new DifficultyRow { loops = 2, loopSeconds = 3.0f, tellSeconds = 1.0f },
        new DifficultyRow { loops = 3, loopSeconds = 2.4f, tellSeconds = 1.0f },
        new DifficultyRow { loops = 3, loopSeconds = 1.8f, tellSeconds = 0.8f },
        new DifficultyRow { loops = 3, loopSeconds = 1.8f, tellSeconds = 0.6f, loopSpeedUp = 0.85f },
    };

    [Header("단계 시간 (TODO 임시값)")]
    [Tooltip("① 흐름 — 몸과 지휘봉이 봇을 끌어모아 거대해짐 (보스 피격 가능)")]
    [Min(0.1f)] public float gatherSeconds = 1.0f;
    [Tooltip("② 이동 — 보스가 배경(뒤판)으로 빠짐 (보스 무적)")]
    [Min(0.1f)] public float moveSeconds = 0.8f;
    [Tooltip("⑥ 붕괴 — 보스와 지휘봉이 모래로 무너져 있는 시간")]
    [Min(0.1f)] public float collapseSeconds = 0.6f;
    [Tooltip("⑥ 재생성 — 모래가 제자리에서 원래 크기로 다시 쌓이는 시간 (봇을 이 동안 나눠 되붙임)")]
    [Min(0.1f)] public float rebuildSeconds = 1.2f;

    [Header("궤적 (리사주) — 기준점 = 패턴 시작 순간의 플레이어 위치(고정)")]
    [Tooltip("TODO: 임시값. 무한대(가로) x = A·sin(t), y = B·sin(2t) 의 A, B (m)")]
    public Vector2 infinitySize = new Vector2(3.2f, 1.0f);
    [Tooltip("TODO: 임시값. 8자(세로) x = A·sin(2t), y = B·sin(t) 의 A, B (m)")]
    public Vector2 eightSize = new Vector2(1.2f, 1.1f);
    [Tooltip("TODO: 임시값. 궤적 중심 높이 = 바닥 + 이 값 (m)")]
    public float centerHeight = 1.35f;
    [Tooltip("8자가 나올 확률 (나머지는 무한대)")]
    [Range(0f, 1f)] public float eightChance = 0.5f;

    [Header("화면 맞춤 (11-24 사용자: 너무 피하기 쉬움 → 카메라 기준으로 화면 전체를 뒤덮게)")]
    [Tooltip("켜면 궤적·지휘봉 크기를 카메라에 보이는 범위로 정한다 (위의 m 값 대신). 궤적 중심 = 화면 가운데")]
    public bool fitToCamera = true;
    [Tooltip("궤적 가로 반폭 = 화면 반폭 × 이 값 — x 무한대(가로), y 8자(세로)")]
    public Vector2 screenWidthRatio = new Vector2(0.9f, 0.55f);
    [Tooltip("궤적 세로 반높이 = 화면 높이 × 이 값")]
    [Range(0.1f, 0.5f)] public float screenHeightRatio = 0.37f;
    [Tooltip("궤적 중심 높이 = 바닥 + 화면 높이 × 이 값")]
    [Range(0.1f, 0.9f)] public float screenCenterRatio = 0.43f;
    [Tooltip("지휘봉 길이 = 화면 높이 × 이 값. 봇이 모자라면 길이 대신 굵기를 줄인다")]
    [Range(0.1f, 1f)] public float screenLengthRatio = 0.55f;

    [Header("지휘봉 (TODO 임시값)")]
    [Tooltip("지휘봉 길이 (m). 봇이 모자라면 굵기 대신 길이를 줄인다 (기획서 1.3)")]
    [Min(0.5f)] public float length = 3.5f;
    [Tooltip("손잡이 · 끝 두께 반지름 (m) — 테이퍼 (손잡이 굵고 끝 가늘게)")]
    public Vector2 thickness = new Vector2(0.22f, 0.05f);
    [Tooltip("지휘봉을 쥔 손 자리 = 궤적 중심 + 이 높이 (m). 끝은 궤적, 손잡이는 이 점 쪽을 향한다")]
    public float gripHeight = 4f;
    [Tooltip("지휘봉에 쓸 봇 최대 수 (몸 최소 비율은 지킨다)")]
    [Min(16)] public int maxBots = 2400;
    [Tooltip("몸에 최소한 남길 봇 비율 (전체 봇 수 기준)")]
    [Range(0.1f, 0.9f)] public float bodyMinRatio = 0.5f;

    [Header("보스 몸")]
    [Tooltip("TODO: 임시값. ① 거대해질 때 몸 크기 배율")]
    [Min(1f)] public float growScale = 1.3f;
    [Tooltip("TODO: 임시값. ② 배경에서 몸 위치 (원래 자리 기준, m)")]
    public Vector2 backOffset = new Vector2(0f, 0.5f);

    [Header("판정")]
    [Tooltip("지휘봉 체력 = 보스 악장 최대 HP × 이 값 — 기획서 임시 5%")]
    [Range(0.01f, 0.5f)] public float hpRatio = 0.05f;
    [Tooltip("TODO: 임시값. 플레이어가 지휘봉을 때리는 판정 두께 (m) — 그림보다 조금 두껍게")]
    [Min(0.05f)] public float hurtThickness = 0.5f;
    [Tooltip("TODO: 임시값. 지휘봉이 플레이어를 해치는 판정 두께 (m)")]
    [Min(0.05f)] public float hitThickness = 0.3f;
    [Tooltip("TODO: 임시값. 1회 피격 뒤 지휘봉에 다시 맞지 않는 시간 (초)")]
    [Min(0f)] public float hitCooldown = 1f;

    [Header("파괴 보상")]
    [Tooltip("그로기 시간 (초) — 기획서 임시 4초")]
    [Min(0.1f)] public float groggySeconds = 4f;
    [Tooltip("그로기 중 보스가 받는 피해 배율 — KKH 기본 1.5 (기획서 수치 없음)")]
    [Min(1f)] public float groggyDamageMultiplier = 1.5f;

    [Header("그림")]
    [Range(0f, 4f)] public float glow = 1.2f;
    public Color tellColor = new Color(1f, 1f, 1f, 0.25f);
    [Min(0.01f)] public float tellWidth = 0.06f;
    public int tellSortingOrder = 3;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
