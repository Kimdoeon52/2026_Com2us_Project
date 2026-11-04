using UnityEngine;

/// <summary>
/// 쿄다이 촉수 수치 (2026-11-04 해킹 대체 기믹). 기획 수치: 3개부터 디버프, 최대 10개. 나머지는 전부 임시값.
/// 거리·크기는 보스 단위(보스 크기 Size를 곱해 쓴다). 휘두르기 데미지·부위 피해는 생성기의 패턴 데이터(KKH BossPatternData).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Tentacle Data", fileName = "KyodaiTentacleData")]
public class KyodaiTentacleData : ScriptableObject
{
    [Header("수 (기획)")]
    [Tooltip("최대 촉수 수. 기획: 10")]
    [Min(1)] public int maxCount = 10;

    [Tooltip("이 수부터 플레이어 디버프. 기획: 3")]
    [Min(1)] public int debuffStartCount = 3;

    [Header("생성 (60fps 프레임)")]
    [Tooltip("TODO: 기획 수치 없음. 전투 시작 후 첫 촉수까지")]
    [Min(0)] public int firstSpawnFrames = 600;

    [Tooltip("TODO: 기획 수치 없음. 1페이즈 생성 간격")]
    [Min(1)] public int spawnIntervalFrames = 480;

    [Tooltip("TODO: 기획 수치 없음. 2페이즈 생성 간격 (더 빠르게)")]
    [Min(1)] public int phase2SpawnIntervalFrames = 300;

    [Tooltip("플레이어에게서 이만큼은 떨어진 자리에 나온다(보스 단위)")]
    [Min(0f)] public float minPlayerDistance = 3f;

    [Tooltip("다른 촉수와 이만큼은 떨어지게 노력한다(보스 단위)")]
    [Min(0f)] public float minSpacing = 2f;

    [Header("디버프")]
    [Tooltip("TODO: 기획 수치 없음. 디버프 시작 수부터 촉수 하나마다 이동속도 감소 비율")]
    [Range(0f, 0.5f)] public float speedPenaltyPerTentacle = 0.06f;

    [Tooltip("TODO: 기획 수치 없음. 이동속도 배율 하한")]
    [Range(0.1f, 1f)] public float minSpeedMultiplier = 0.6f;

    [Header("촉수 하나")]
    [Tooltip("TODO: 기획 수치 없음. 촉수 체력 (플레이어 공격 데미지 그대로 깎임)")]
    [Min(1f)] public float hp = 40f;

    [Tooltip("땅에서 솟아나는 시간")]
    [Min(1)] public int emergeFrames = 30;

    [Tooltip("쓰러져 들어가는 시간")]
    [Min(1)] public int dieFrames = 30;

    [Header("휘두르기 (플레이어가 가까이 오면)")]
    [Tooltip("TODO: 기획 수치 없음. 플레이어가 이 가로 거리 안에 들어오면 휘두른다(보스 단위)")]
    [Min(0f)] public float triggerRange = 2.8f;

    [Tooltip("뒤로 젖히는 예고 시간")]
    [Min(1)] public int windUpFrames = 30;

    [Tooltip("휘두르는 시간")]
    [Min(1)] public int swingFrames = 10;

    [Tooltip("휘두르기 중 판정이 켜지는 구간 (시작~끝 프레임)")]
    [Min(0)] public int swingHitStart = 2;
    [Min(0)] public int swingHitEnd = 7;

    [Tooltip("휘두른 뒤 돌아오는 시간")]
    [Min(1)] public int recoverFrames = 25;

    [Tooltip("TODO: 기획 수치 없음. 다음 휘두르기까지 쉬는 시간")]
    [Min(0)] public int cooldownFrames = 120;

    [Tooltip("휘두르기 판정: 촉수 뿌리에서 앞으로 닿는 거리 · 높이(보스 단위)")]
    public Vector2 swingReach = new Vector2(2.6f, 2.6f);

    [Header("그림 (뼈 사슬 — 연출 값)")]
    [Tooltip("젖히기·휘두르기 때 뼈 하나당 최대로 꺾는 각도(도)")]
    public float maxBend = 30f;

    [Tooltip("평소 흐느적거리는 각도(도)")]
    public float swayAngle = 9f;

    [Tooltip("흐느적거리는 빠르기")]
    public float swaySpeed = 2.2f;

    [Tooltip("맞는 자리 크기(보스 단위) — 뿌리 위로 세운 상자")]
    public Vector2 hurtSize = new Vector2(1f, 3.4f);

    public Color hitFlashColor = new Color(1f, 0.55f, 0.55f);
}
