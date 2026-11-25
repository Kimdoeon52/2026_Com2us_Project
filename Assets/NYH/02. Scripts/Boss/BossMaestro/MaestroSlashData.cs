using System;
using UnityEngine;

/// <summary>
/// 마에스트로 「지휘봉 베기」 수치 (2026-11-25 사용자 — 예전 거대 지휘봉(8자·무한대 궤적)을 꺼 두고 대신 쓰는 1악장 패턴).
/// 지휘봉을 길게 늘려 검처럼, 보스 둘레를 수평으로 크게 휘두른다: 오른쪽→왼쪽, 왼쪽→오른쪽, 오른쪽→왼쪽 (3번).
/// 검이 화면 평면을 지나는 순간 그쪽 화면 끝까지 가슴 높이 띠가 맞는다 — 점프로 못 넘고 회피 무적으로 피하는 구조.
/// 수치는 전부 TODO 임시값(기획 수치 없음).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_SlashData", menuName = "NYH/Boss/Maestro Slash Data")]
public class MaestroSlashData : ScriptableObject
{
    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("첫 베기 전 감아올리는 시간 (초) — 예고")]
        [Min(0.1f)] public float firstWindup = 0.8f;
        [Tooltip("베기 사이 다시 감아올리는 시간 (초) — 예고")]
        [Min(0.1f)] public float windup = 0.55f;
        [Tooltip("(예전 제자리 베기용) 한 번 휘두르는 시간 (초)")]
        [Min(0.1f)] public float swing = 0.36f;
        [Tooltip("돌진 시간 (초) — 화면 한쪽 끝에서 반대쪽 끝까지 베며 돌진 (짧을수록 빠름). 0 이하면 휘두르기 × 1.5")]
        public float dash = 0.5f;
    }

    // 예전 에셋(돌진 칸이 없던 때)용 난이도 기본 돌진 시간 (아주 쉬움 → 헬)
    public static readonly float[] DefaultDash = { 0.62f, 0.56f, 0.5f, 0.44f, 0.38f };

    public float Dash(BossDifficulty d, DifficultyRow row)
    {
        if (row != null && row.dash > 0.05f) return row.dash;
        return DefaultDash[Mathf.Clamp((int)d, 0, DefaultDash.Length - 1)];
    }

    [Header("빠르기 · 크기 (11-25 사용자: 너무 느리고 너무 커서 안 맞음 → 플레이어 크기·카메라에 맞춤)")]
    [Tooltip("모든 시간(준비·전환·베기)을 이 값으로 나눈다 — 1.4 = 1.4배 빠르게")]
    [Range(0.5f, 3f)] public float tempo = 1.4f;
    [Tooltip("초승달 중심 높이 = 바닥 + 플레이어 키 × 이 값")]
    [Range(0.2f, 1.5f)] public float centerPerPlayer = 0.5f;
    [Tooltip("초승달 세로 반지름 = 플레이어 키 × 이 값 — 위쪽 띠는 머리 높이, 아래쪽 띠는 발밑을 지나게")]
    [Range(0.3f, 2f)] public float heightPerPlayer = 0.6f;
    [Tooltip("초승달 가로 반지름 = 런지 거리 절반 + 플레이어 키 × 이 값")]
    [Range(0.5f, 6f)] public float reachPerPlayer = 2.2f;
    [Tooltip("초승달 가로 반지름 최대 = 화면 폭 × 이 값")]
    [Range(0.1f, 1f)] public float maxReachScreen = 0.45f;
    [Tooltip("준비 동안 미리 보이는 초승달 윤곽 진하기 (전조)")]
    [Range(0f, 1f)] public float telegraphAlpha = 0.32f;
    [Tooltip("베기 직전 지휘봉 끝이 번쩍이는 시점 (초, 베기 시작 전)")]
    [Min(0f)] public float glintLead = 0.18f;

    [Header("가까이 베기 (11-25 사용자: 안쪽이 비고 멀리만 때려서 붙어 있으면 안 맞음) — TODO 임시값")]
    [Tooltip("초승달 가로 반지름 = 런지 거리 절반 + 플레이어 키 × 이 값 (예전 reachPerPlayer 대신)")]
    [Range(0.5f, 4f)] public float nearReachPerPlayer = 1.3f;
    [Tooltip("초승달 중심 = 보스 자리 + 진행 방향 × 가로 반지름 × 이 값 — 작을수록 보스 몸을 감싸듯 가까이")]
    [Range(0f, 1f)] public float nearCenterAhead = 0.45f;
    [Tooltip("오른쪽→왼쪽 베기 각도 (도, 시작 → 끝). 시작을 수평 아래로 내려 보스 바로 옆 가슴 높이부터 벤다. 왼쪽→오른쪽은 거울")]
    public Vector2 sweepAngles = new Vector2(-15f, 235f);
    [Tooltip("초승달 안쪽(칼이 쓸고 지나간 부채꼴)도 맞는다 — 보스에 붙어 있어도 맞음")]
    public bool fillHit = true;
    [Tooltip("안쪽 채움 진하기 (맞는 곳을 보여줌)")]
    [Range(0f, 0.6f)] public float fillAlpha = 0.16f;

    [Header("딜레이 · 경고 (11-25 사용자: 연속 공격 사이 딜레이와 사전 경고를 명확하게) — TODO 임시값")]
    [Tooltip("베기가 끝나고 다음 준비를 시작하기 전 멈춤 (초, 빠르기 tempo 영향 없음)")]
    [Min(0f)] public float strikeGap = 0.22f;
    [Tooltip("준비·전환 시간 배율 (난이도 표 값 ÷ tempo 뒤에 곱함) — 클수록 경고를 오래 본다")]
    [Range(0.5f, 3f)] public float windupScale = 1.3f;
    [Tooltip("준비하는 동안 이번 초승달 윤곽 진하기 — 처음 → 끝 (점점 진해짐)")]
    public Vector2 warnAlphaRange = new Vector2(0.12f, 0.5f);
    [Tooltip("베기 직전 윤곽이 깜박이는 시간 (초)")]
    [Min(0f)] public float warnBlinkSeconds = 0.22f;

    [Header("초승달 횡베기 (11-25 사용자 시트 '지휘봉 패턴 모션 — 3연속 횡베기')")]
    [Tooltip("한 번 벨 때 나아가는 거리 = 화면 폭 × 이 값 (시트: 몇 걸음 런지)")]
    [Range(0f, 0.8f)] public float dashDistanceRatio = 0.28f;
    [Tooltip("초승달 가로 반지름 = 나아가는 거리 절반 + 보스 키 × 이 값 (베기가 닿는 앞쪽 거리)")]
    [Min(0.2f)] public float crescentReachRatio = 1.1f;
    [Tooltip("초승달 세로 반지름 = 보스 키 × 이 값 (위는 머리 위, 아래는 바닥까지)")]
    [Min(0.1f)] public float crescentHeightRatio = 0.55f;
    [Tooltip("초승달 가장 굵은 곳 두께 (반지름 비율) — 판정 띠 두께도 이것")]
    [Range(0.05f, 0.8f)] public float crescentThickness = 0.32f;
    [Tooltip("오른쪽→왼쪽 베기 각도 (도, 시작 → 끝, 반시계 +). 왼쪽→오른쪽은 거울")]
    public Vector2 crescentAngles = new Vector2(35f, 250f);
    [Tooltip("마지막(3타) 초승달 크기 · 두께 배율 — 시트: 가장 크고 두꺼운 마무리")]
    public Vector2 finalScale = new Vector2(1.15f, 1.4f);
    [Tooltip("초승달이 다 그려진 뒤 사라지는 시간 (초)")]
    [Min(0.05f)] public float crescentFade = 0.35f;
    [Tooltip("준비하는 동안 이번 초승달 윤곽을 옅게 미리 보여줌 (공정한 예고) — 진하기")]
    [Range(0f, 1f)] public float previewAlpha = 0.18f;

    [Header("(예전) 돌진 — 화면 끝까지 돌진하던 판")]
    [Tooltip("돌진 끝 자리 = 화면 끝에서 이만큼 안쪽 (m)")]
    [Min(0f)] public float edgeMargin = 1.6f;
    [Tooltip("검 길이 = 보스 키 × 이 값")]
    [Min(0.2f)] public float bladeLengthRatio = 0.9f;
    [Tooltip("시작 자리로 미끄러져 가는 시간 · 끝나고 원래 자리로 돌아가는 시간 (초)")]
    public Vector2 positionReturnSeconds = new Vector2(0.6f, 0.8f);

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬) — TODO 전부 임시값")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { firstWindup = 0.95f, windup = 0.65f, swing = 0.44f, dash = 0.62f },
        new DifficultyRow { firstWindup = 0.85f, windup = 0.6f,  swing = 0.4f, dash = 0.56f },
        new DifficultyRow { firstWindup = 0.8f,  windup = 0.55f, swing = 0.36f, dash = 0.5f },
        new DifficultyRow { firstWindup = 0.7f,  windup = 0.48f, swing = 0.32f, dash = 0.44f },
        new DifficultyRow { firstWindup = 0.6f,  windup = 0.42f, swing = 0.28f, dash = 0.38f },
    };

    [Header("방향 (11-25 사용자: 좌우 번갈아가 아니라 플레이어 쪽으로 3번)")]
    [Tooltip("켜면 베기마다 준비 시작 순간의 플레이어 쪽으로 벤다 (아래 방향 순서는 횟수만 씀)")]
    public bool towardPlayer = true;

    [Header("베기 (사용자: 오른쪽→왼쪽, 왼쪽→오른쪽, 오른쪽→왼쪽)")]
    [Tooltip("휘두르는 방향 순서 — -1 = 오른쪽→왼쪽, +1 = 왼쪽→오른쪽")]
    public int[] directions = { -1, 1, -1 };
    [Tooltip("검이 생기는(지휘봉이 늘어나는) 시간 (초)")]
    [Min(0.1f)] public float formSeconds = 0.5f;
    [Tooltip("마지막 베기 뒤 멈춰 버티는 시간 (초)")]
    [Min(0f)] public float endHoldSeconds = 0.45f;

    [Header("크기 · 판정 (TODO 임시값)")]
    [Tooltip("검 높이 (바닥에서, m) — 가슴 높이")]
    public float bladeHeight = 1.0f;
    [Tooltip("판정 띠 반높이 (m) — 검 높이 ± 이 값. 점프로 못 넘게 넉넉히")]
    [Min(0.1f)] public float hitHalfHeight = 0.7f;
    [Tooltip("검 길이 = 화면 반폭 × 이 값 (1 넘으면 화면 끝까지 닿음)")]
    [Min(0.3f)] public float reachRatio = 1.15f;
    [Tooltip("검이 화면 평면을 지나는 것으로 치는 범위 — |cos| 가 이 값보다 크면 그쪽이 맞는다")]
    [Range(0.5f, 0.99f)] public float planeCos = 0.9f;
    [Tooltip("검 두께 (손잡이 · 끝 반지름, m). 봇이 모자라면 엔진이 가늘게 맞춘다")]
    public Vector2 thickness = new Vector2(0.12f, 0.035f);

    [Header("그림")]
    [Range(0f, 4f)] public float glow = 2f;
    [Tooltip("예고 빛줄기 진하기")]
    [Range(0f, 1f)] public float warnAlpha = 0.7f;

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
