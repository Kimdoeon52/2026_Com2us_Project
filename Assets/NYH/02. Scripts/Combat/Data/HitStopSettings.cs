using UnityEngine;

/// <summary>
/// 히트스톱(맞는 순간 공격자·피격자가 잠깐 멈추는 연출) 공식 값 (1층 · 데이터). 씬에 하나, HitDetection이 들고 있다.
///
/// 정지 프레임 = (기본 프레임 + 공격력 × 공격력당 프레임) × 기술 계수(ActionData.hitStopScale) × 박스 계수(FrameBox.damageScale)
///
/// 공격력에 비례하게 둔 이유: 데미지를 바꾸면 손맛이 자동으로 따라온다 — 기술마다 정지 프레임을 따로 적으면
/// 데미지 밸런스를 바꿀 때마다 같이 고쳐야 하고 빠뜨린다. 그래도 "칼끝이 더 아프다"처럼 일부러 다르게 주고 싶은
/// 곳은 기술 계수·박스 계수로 조절한다.
///
/// 기본값은 전부 0 = 히트스톱 없음(기존 동작 그대로). 기획 수치가 없어서 지어내지 않는다(§12) —
/// 느낌을 볼 때만 인스펙터에서 값을 넣는다. 기획서1011 치명타 연출의 "0.04초"는 약 2~3프레임.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Hit Stop Settings", fileName = "HitStopSettings")]
public class HitStopSettings : ScriptableObject
{
    [Header("정지 프레임 공식 — 값 전부 임시 (기획 확정 전, 0이면 히트스톱 없음)")]
    [Tooltip("TODO: 기획 수치 없음. 공격력과 무관하게 항상 더하는 프레임")]
    [Min(0f)] [SerializeField] private float baseFrames;

    [Tooltip("TODO: 기획 수치 없음. 공격력(ActionData.Damage) 1당 더하는 프레임. 예: 0.3이면 데미지 10 = 3프레임")]
    [Min(0f)] [SerializeField] private float framesPerDamage;

    [Tooltip("아무리 세게 맞아도 이 프레임을 넘지 않는다 — 큰 숫자 하나 잘못 넣어서 게임이 굳어 보이는 걸 막는 안전장치")]
    [Min(0)] [SerializeField] private int maxFrames = 20;

    [Header("예외 규칙")]
    [Tooltip("다단히트 기술(화염 등)은 첫 타에만 멈춘다. 끄면 맞을 때마다 멈춰서 계속 버벅거린다")]
    [SerializeField] private bool multiHitFirstOnly = true;

    [Tooltip("투사체에 맞으면 쏜 쪽도 멈출지. 기본은 꺼짐 — 멀리서 쏜 쪽이 이유 없이 굳은 것처럼 보이기 때문")]
    [SerializeField] private bool projectileFreezesAttacker;

    // 2026-10-28 추가 (플레이 피드백): 맞은 쪽이 경직(피격 모션)에 안 들어간 타격 — 공격 중이라 버틴 경우 등 — 까지
    // 멈추면 "공격하다가 이유 없이 굳는" 버그처럼 보였다. 피격 모션이 나오는 타격에서만 멈춘다
    [Tooltip("켜면 맞은 쪽이 피격 모션(경직)에 들어갈 때만 멈춘다. 끄면 맞을 때마다 멈춘다")]
    [SerializeField] private bool onlyWhenHurtReaction = true;

    [Header("진동 — 그림만 흔든다(판정 박스는 고정). 크기는 화면 픽셀 기준이라 카메라 줌과 무관하게 같은 크기로 보인다")]
    [Tooltip("맞은 쪽 진동 크기(화면 픽셀). 처음이 가장 크고 정지가 끝날수록 줄어든다")]
    [Min(0f)] [SerializeField] private float victimShakePixels = 12f;

    [Tooltip("때린 쪽 진동 크기(화면 픽셀). 맞은 쪽보다 작게 — 타격감은 맞은 쪽 흔들림에서 나온다")]
    [Min(0f)] [SerializeField] private float attackerShakePixels = 4f;

    // 2026-10-28 (플레이 피드백): 때린 쪽이 완전히 굳으면 부자연스럽다 — 정지 동안에도 평소의 몇 %로 계속 움직인다.
    // 공중에서 때렸으면 때리는 모션과 떨어지는 것 둘 다 이 속도로 이어진다. 맞은 쪽은 0(완전 정지) 그대로
    [Tooltip("때린 쪽이 정지 동안 움직이는 속도(평소 대비). 0.03 = 3%. 애니메이션과 이동·낙하에 같이 적용. 0이면 완전 정지")]
    [Range(0f, 0.2f)] [SerializeField] private float attackerTimeScale = 0.03f;

    public bool MultiHitFirstOnly => multiHitFirstOnly;
    public bool ProjectileFreezesAttacker => projectileFreezesAttacker;
    public bool OnlyWhenHurtReaction => onlyWhenHurtReaction;
    public float VictimShakePixels => victimShakePixels;
    public float AttackerShakePixels => attackerShakePixels;
    public float AttackerTimeScale => attackerTimeScale;

    /// <summary>공격력과 계수(기술 × 박스)로 정지 프레임을 계산한다. 0이면 히트스톱 없음</summary>
    public int ComputeFrames(float attackPower, float scale)
    {
        if (scale <= 0f) return 0;
        float frames = (baseFrames + Mathf.Max(0f, attackPower) * framesPerDamage) * scale;
        return Mathf.Clamp(Mathf.RoundToInt(frames), 0, maxFrames);
    }
}
