using UnityEngine;

/// <summary>
/// 회피 성공 연출 값 (2026-11-16 사용자: 회피 성공 시 MISS + 줌인 + 잠깐 느려짐, DOTween으로 자연스럽게).
/// 에셋 하나를 Resources 폴더에 "DodgeSuccessSettings" 이름으로 두면(지금: 04. SO/Combat/Resources/DodgeSuccessSettings) 자동으로 쓴다.
/// 없으면 아래 기본값. 순수 연출 값 — 판정과 무관. 값은 전부 임시(눈으로 맞출 것).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Dodge Success Settings", fileName = "DodgeSuccessSettings")]
public class DodgeSuccessSettings : ScriptableObject
{
    public const string ResourcePath = "DodgeSuccessSettings";

    [Tooltip("끄면 회피 성공 연출 없음 (MISS만 뜸)")]
    public bool enabled = true;

    [Header("슬로우모션 (실제 시간 기준)")]
    [Tooltip("가장 느릴 때 시간 배율 (1 = 안 느려짐, 0.3 = 30% 속도)")]
    [Range(0.05f, 1f)] public float slowTimeScale = 0.3f;

    [Tooltip("느려지는 데 걸리는 시간(초)")]
    [Min(0f)] public float slowInSeconds = 0.06f;

    [Tooltip("가장 느린 상태로 버티는 시간(초)")]
    [Min(0f)] public float slowHoldSeconds = 0.25f;

    [Tooltip("원래 속도로 돌아오는 시간(초)")]
    [Min(0.01f)] public float slowOutSeconds = 0.35f;

    [Header("줌인 (그릴 때만 — 게임 판정·경기장 계산과 무관)")]
    [Tooltip("시야각을 이 비율만큼 줄인다 (0.15 = 15% 확대)")]
    [Range(0f, 0.5f)] public float zoomAmount = 0.12f;

    [Tooltip("확대하면서 카메라가 플레이어 쪽으로 따라가는 정도 (0 = 화면 가운데로 확대, 1 = 플레이어가 화면 가운데로)")]
    [Range(0f, 1f)] public float followPlayer = 0.35f;

    [Tooltip("확대되는 시간(초)")]
    [Min(0.01f)] public float zoomInSeconds = 0.08f;

    [Tooltip("확대된 채 버티는 시간(초)")]
    [Min(0f)] public float zoomHoldSeconds = 0.22f;

    [Tooltip("원래대로 돌아오는 시간(초)")]
    [Min(0.01f)] public float zoomOutSeconds = 0.4f;

    [Header("반복 제한")]
    [Tooltip("한 번 연출한 뒤 이 시간(실제 초) 동안은 다시 안 함 — 한 번 회피에 여러 공격이 닿아도 한 번만")]
    [Min(0f)] public float cooldownSeconds = 0.8f;

    private static DodgeSuccessSettings shared;
    private static DodgeSuccessSettings fallback;

    /// <summary>Resources의 설정. 없으면 기본값으로 만든 임시 설정</summary>
    public static DodgeSuccessSettings Current
    {
        get
        {
            if (shared == null) shared = Resources.Load<DodgeSuccessSettings>(ResourcePath);
            if (shared != null) return shared;
            if (fallback == null) fallback = CreateInstance<DodgeSuccessSettings>();
            return fallback;
        }
    }
}
