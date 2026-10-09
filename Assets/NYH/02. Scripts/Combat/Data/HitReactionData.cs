using UnityEngine;

/// <summary>
/// 맞았을 때 경직(피격 모션)에 들어가는 규칙 (1층 · 데이터).
///
/// 2026-10-09 기획서(리얼스틸 기획서1009 §6-12-10 "경직 시스템")로 경직이 다시 생겼다 —
/// "약간의 경직도가 존재 / 피격시 약간의 밀려남과 피격 애니메이션, 점멸 이펙트가 출력된다".
/// 2026-09-29에 삭제된 "경직도 게이지(누적치 100, 지속 120F)"를 되살린 게 아니라,
/// 한 대 맞으면 짧게 움찔하는 "피격 리액션"이다 — 게이지 없이 아래 규칙만으로 판단한다.
///
/// 규칙 (판단은 HitReactionSystem 한 곳에서):
///   · 대기·걷기 중에 맞음  → idleReactionChance 확률로 경직 (강한 공격이면 무조건)
///   · 행동(공격 등) 중에 맞음 → interruptDamageThreshold 이상일 때만 행동이 끊기고 경직
///   · 공중 / 사망 중        → 경직 없음 (reactInAir로 공중은 켤 수 있음)
///
/// 로봇마다 에셋을 따로 꽂는다 — 비워두면 그 로봇은 경직이 없다(점멸만 있음).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Hit Reaction Data", fileName = "NewHitReactionData")]
public class HitReactionData : ScriptableObject
{
    [Tooltip("경직 때 실행할 행동. Hit박스 없이 Hurt/Push만 담고, animationClipName에 피격 스테이트를 넣는다")]
    [SerializeField] private ActionData hurtAction;

    [Header("판단 기준 — 값 전부 임시 (기획 확정 전)")]
    [Tooltip("TODO: 임시값. 대기·걷기 중에 맞았을 때 경직에 들어갈 확률 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float idleReactionChance = 0.35f;

    [Tooltip("TODO: 임시값. 이 데미지 이상이면 공격 중이어도 끊기고, 대기 중이면 확률과 무관하게 경직")]
    [Min(0)] [SerializeField] private int interruptDamageThreshold = 15;

    [Tooltip("공중에서 맞았을 때도 경직에 들어갈지. 기본은 꺼짐 — 점프 중엔 궤적을 유지한다")]
    [SerializeField] private bool reactInAir;

    [Header("점멸 이펙트 — RobotView가 읽음")]
    [Tooltip("TODO: 임시값. 맞은 뒤 스프라이트가 깜빡이는 프레임 수. 0이면 점멸 없음")]
    [Min(0)] [SerializeField] private int flashFrames = 8;

    [SerializeField] private Color flashColor = new Color(1f, 0.45f, 0.45f, 1f);

    public ActionData HurtAction => hurtAction;
    public float IdleReactionChance => idleReactionChance;
    public int InterruptDamageThreshold => interruptDamageThreshold;
    public bool ReactInAir => reactInAir;
    public int FlashFrames => flashFrames;
    public Color FlashColor => flashColor;
}
