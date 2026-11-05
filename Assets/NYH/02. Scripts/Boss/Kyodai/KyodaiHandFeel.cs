using UnityEngine;

/// <summary>
/// 쿄다이 손의 "움직임 느낌" (2026-11-04 2D 개편). 그림이 한 장이라 움직임만으로 살아 보여야 해서,
/// 떠다니기·기울기·늘어남·찌그러짐·그림자를 전부 여기 값으로 조절한다.
/// 전부 연출 값이다(판정·타이밍과 무관) — 눈으로 보며 맞추면 된다. 기본값은 임시.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Hand Feel", fileName = "KyodaiHandFeel")]
public class KyodaiHandFeel : ScriptableObject
{
    [Header("떠다니기 (상시)")]
    [Tooltip("흔들림 크기(월드) — x 좌우, y 위아래")]
    public Vector2 floatAmplitude = new Vector2(0.18f, 0.32f);

    [Tooltip("위아래 흔들림 주기(초)")]
    [Min(0.1f)] public float floatPeriodA = 2.7f;

    [Tooltip("좌우 흔들림 주기(초). 위아래와 다르게 두면 8자처럼 돌아 덜 기계적이다")]
    [Min(0.1f)] public float floatPeriodB = 4.3f;

    [Tooltip("느린 노이즈 흔들림 크기(월드) — 사인만 쓰면 반복이 보인다")]
    [Min(0f)] public float noiseAmplitude = 0.12f;

    [Tooltip("노이즈 속도")]
    [Min(0f)] public float noiseSpeed = 0.35f;

    [Tooltip("떠다니며 기우뚱하는 각도(도)")]
    [Min(0f)] public float floatRotation = 4f;

    [Tooltip("흔들림 세기가 키 값으로 바뀌는 빠르기(초당)")]
    [Min(0.1f)] public float floatBlendSpeed = 2.5f;

    [Header("기울기·늘어남 (속도에 따라)")]
    [Tooltip("속도를 부드럽게 읽는 정도. 클수록 즉각 반응")]
    [Min(0.1f)] public float velocitySmoothing = 12f;

    [Tooltip("가로 속도 1당 기울기(도) — 움직이는 쪽으로 몸을 기울인다")]
    public float leanPerSpeed = 1.6f;

    [Tooltip("최대 기울기(도)")]
    [Min(0f)] public float maxLean = 22f;

    [Tooltip("속도 1당 늘어나는 비율 — 빨리 움직일 때 움직이는 방향으로 길어진다")]
    [Min(0f)] public float stretchPerSpeed = 0.012f;

    [Tooltip("최대 늘어남")]
    [Range(0f, 0.6f)] public float maxStretch = 0.22f;

    [Tooltip("키 회전을 따라 도는 시간(초)")]
    [Min(0.01f)] public float rotationSmoothTime = 0.12f;

    [Tooltip("키 크기를 따라 커지는 시간(초)")]
    [Min(0.01f)] public float scaleSmoothTime = 0.1f;

    [Header("찌그러짐 (충격·모양 바뀜·피격)")]
    [Tooltip("충격 순간 세로로 찌그러지는 비율")]
    [Range(0f, 0.8f)] public float impactSquash = 0.32f;

    [Tooltip("찌그러졌다 돌아오는 빠르기(Hz)")]
    [Min(0.5f)] public float squashFrequency = 5f;

    [Tooltip("찌그러짐 탄성 — 작을수록 여러 번 출렁인다")]
    [Range(0.05f, 1f)] public float squashDamping = 0.28f;

    [Tooltip("세로로 찌그러질 때 가로로 퍼지는 비율 (부피 유지 느낌)")]
    [Range(0f, 1.5f)] public float squashWiden = 0.7f;

    [Tooltip("주먹 ↔ 손바닥 그림이 바뀔 때 톡 튀는 세기 — 그림 교체를 가린다")]
    [Min(0f)] public float posePop = 1.6f;

    [Tooltip("맞았을 때 움찔하는 세기")]
    [Min(0f)] public float hitKick = 1.2f;

    [Tooltip("맞았을 때 색 깜빡임 시간(초)")]
    [Min(0f)] public float hitFlashSeconds = 0.08f;

    public Color hitFlashColor = new Color(1f, 0.55f, 0.55f);

    [Header("대기 자리로 돌아가기")]
    [Tooltip("대기 자리를 따라가는 스프링 빠르기(Hz)")]
    [Min(0.1f)] public float idleFrequency = 1.1f;

    [Tooltip("대기 스프링 탄성")]
    [Range(0.1f, 1.5f)] public float idleDamping = 0.65f;

    [Header("그로기·격파 (손이 바닥으로 떨어짐)")]
    [Tooltip("쓰러진 손 중심의 바닥 위 높이(월드)")]
    public float downHeight = 1f;

    [Range(0f, 1f)] public float downFloatWeight = 0.15f;
    public Color downTint = new Color(0.55f, 0.55f, 0.6f);

    [Header("레이저에 맞음 — 바닥으로 떨어져 누움 (2026-11-04)")]
    public Color frozenTint = new Color(0.6f, 0.75f, 1f);

    [Tooltip("누워 있는 동안 지직거리는 크기(보스 단위)")]
    [Min(0f)] public float frozenShake = 0.04f;

    [Tooltip("떨어지는 중력(보스 단위/초², 음수)")]
    public float fallGravity = -45f;

    [Tooltip("바닥에서 튕기는 비율 (0 = 안 튕김)")]
    [Range(0f, 0.9f)] public float fallBounce = 0.3f;

    [Tooltip("이 속도(보스 단위/초)보다 느리게 닿으면 더 안 튕기고 눕는다")]
    [Min(0f)] public float fallBounceMinSpeed = 2f;

    [Tooltip("처음 바닥에 닿을 때 화면 흔들림(월드)")]
    [Min(0f)] public float fallShake = 0.2f;

    [Tooltip("누운 손의 기울기(도) — 왼손·오른손이 서로 반대로")]
    public float fallRotation = 25f;

    [Header("바닥 그림자")]
    [Tooltip("손이 바닥에 붙었을 때 그림자 크기(월드)")]
    public Vector2 shadowSize = new Vector2(2.4f, 0.5f);

    [Tooltip("가장 높이 떴을 때 그림자 크기 배율")]
    [Range(0.05f, 1f)] public float shadowMinScale = 0.45f;

    [Tooltip("이 높이 이상이면 그림자가 가장 작고 옅다(월드)")]
    [Min(0.1f)] public float shadowFadeHeight = 7f;

    [Range(0f, 1f)] public float shadowMaxAlpha = 0.5f;
    [Range(0f, 1f)] public float shadowMinAlpha = 0.15f;

    [Header("기타")]
    [Tooltip("충격 화면 흔들림 시간(초)")]
    [Min(0.01f)] public float shakeSeconds = 0.3f;

    [Tooltip("맞는 자리 크기 = 그림 크기 × 이 값")]
    [Range(0.2f, 1.2f)] public float hurtScale = 0.85f;

    [Header("이펙트 (2026-11-05 pixelfx 도트 플립북 — 비우면 없음)")]
    [Tooltip("충격(찍기) 순간 바닥에 — 먼지·충격파·돌 튐")]
    public PixelFxData impactFx;

    [Tooltip("플레이어에게 맞은 순간 손 위에")]
    public PixelFxData hitFx;
}
