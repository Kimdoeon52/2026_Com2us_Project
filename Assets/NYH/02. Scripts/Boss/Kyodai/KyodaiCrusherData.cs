using UnityEngine;

/// <summary>
/// 쿄다이 내려찍는 기물 수치 (2026-11-04, 1페이즈부터). 기획 수치가 전부 없어 임시값 — 주기·데미지·파괴 가능 여부 미정.
/// 높이·크기는 보스 단위(보스 크기 Size를 곱해 쓴다). 데미지·부위 피해는 기물의 패턴 데이터(KKH BossPatternData).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Crusher Data", fileName = "KyodaiCrusherData")]
public class KyodaiCrusherData : ScriptableObject
{
    [Header("주기 (60fps 프레임)")]
    [Tooltip("TODO: 기획 수치 없음. 전투 시작(또는 그로기·전환 연출이 끝난 뒤) 첫 내려찍기까지")]
    [Min(0)] public int firstDelayFrames = 120;

    [Tooltip("TODO: 기획 수치 없음. 한 번 올라간 뒤 다음 내려찍기까지 (임시 6초)")]
    [Min(0)] public int intervalFrames = 360;

    /// <summary>
    /// 다음 내려찍기까지 대기 = intervalFrames ÷ 빈도 배율. 난이도별 배율은 쿄다이 난이도 데이터(KyodaiDifficultyData.crusherFrequency)로
    /// 옮겼다 (2026-11-12 — 난이도 차이를 한 에셋에서)
    /// </summary>
    public int IntervalFrames(float frequency) => Mathf.Max(1, Mathf.RoundToInt(intervalFrames / Mathf.Max(0.01f, frequency)));

    [Header("단계 길이 (60fps 프레임)")]
    [Tooltip("화면 위에서 플레이어를 따라 내려오는 시간 (그림자 예고)")]
    [Min(1)] public int descendFrames = 70;

    [Tooltip("멈추고 떨리는 시간 — 플레이어가 피할 틈")]
    [Min(0)] public int lockFrames = 24;

    [Tooltip("찍는 시간 (판정)")]
    [Min(1)] public int slamFrames = 6;

    [Tooltip("박혀 있는 시간")]
    [Min(0)] public int stuckFrames = 45;

    [Tooltip("다시 올라가는 시간")]
    [Min(1)] public int riseFrames = 50;

    [Header("움직임")]
    [Tooltip("내려오는 동안 플레이어 x를 따라가는 스프링 빠르기(Hz)")]
    [Min(0.1f)] public float trackFrequency = 1.2f;

    [Range(0.1f, 1.5f)] public float trackDamping = 0.85f;

    [Tooltip("멈췄을 때 떠 있는 높이 — 기물 중심의 바닥 위 높이(보스 단위)")]
    public float hoverHeight = 4f;

    [Tooltip("찍었을 때 기물 중심의 바닥 위 높이(보스 단위) — 그림 아래 끝이 바닥에 닿는 자리")]
    public float contactHeight = 1f;

    [Tooltip("멈춤 단계 떨림 크기(보스 단위)")]
    [Min(0f)] public float lockShake = 0.06f;

    [Header("판정·연출")]
    [Tooltip("판정 상자 크기(보스 단위)")]
    public Vector2 hitSize = new Vector2(2.2f, 1.8f);

    [Tooltip("바닥 예고 너비(보스 단위)")]
    [Min(0f)] public float telegraphWidth = 2.6f;

    [Tooltip("찍는 순간 화면 흔들림(월드)")]
    [Min(0f)] public float impactShake = 0.45f;

    [Tooltip("찍는 순간 멈추는 프레임(히트스톱)")]
    [Min(0)] public int impactFreezeFrames = 5;
}
