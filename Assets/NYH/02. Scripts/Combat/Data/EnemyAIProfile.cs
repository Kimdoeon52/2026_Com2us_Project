using System;
using UnityEngine;

/// <summary>
/// 일반 적(몹) 한 종류의 "성격"을 담는 데이터 (1층 · 데이터). AIInputSource가 이것만 읽고 판단한다.
///
/// 몹이 늘어나도 AI 클래스는 그대로고 이 에셋만 늘어난다(§1) — "어떤 기술을 어느 거리에서 얼마나
/// 자주 쓰는가"는 코드가 아니라 데이터의 차이이기 때문이다. 기획서1009 §6-10 "일반 적 AI"의
/// NPC A/B/C 구분도 결국 이 에셋 3개로 나뉘게 된다.
///
/// 판단 방식 = 가중치 선택: 생각할 때마다 "지금 할 수 있는 선택지"(사거리·쿨타임·부위 파손을 통과한
/// 기술 + 접근 + 대기 + 점프)를 모아 가중치 비율로 하나를 뽑는다. 상태기계(FSM)를 따로 두지 않는 이유는
/// "공격 중/경직 중/공중/사망"이 이미 ActionState·RobotMover에 있어서, AI가 또 들고 있으면 둘이 어긋나기 때문.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Enemy AI Profile", fileName = "NewEnemyAIProfile")]
public class EnemyAIProfile : ScriptableObject
{
    /// <summary>기술 하나를 AI가 언제·얼마나 쓰는지</summary>
    [Serializable]
    public class SkillEntry
    {
        [Tooltip("쓸 기술. 같은 로봇의 ActionExecutor.allActions에도 들어 있어야 한다(부위 파손 체크 대상)")]
        public ActionData action;

        [Tooltip("상대와의 x거리가 이 범위 안일 때만 후보가 된다")]
        [Min(0f)] public float minRange;
        [Min(0f)] public float maxRange = 1.2f;

        [Tooltip("후보들 사이의 상대적인 뽑힐 확률. 클수록 자주 쓴다")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("한 번 쓴 뒤 다시 후보가 되기까지의 프레임(60fps)")]
        [Min(0)] public int cooldownFrames;

        [Tooltip("상대가 지상/공중일 때만 쓰게 제한")]
        public AITargetCondition targetCondition = AITargetCondition.Any;

        [Tooltip("상대가 공중에 있으면 weight에 곱하는 배율. 높은 타점 기술이 대공기 역할을 하게 할 때 쓴다")]
        [Min(0f)] public float airborneTargetWeightMultiplier = 1f;
    }

    [Header("기술 — 값 전부 임시 (기획 확정 전)")]
    [SerializeField] private SkillEntry[] skills = new SkillEntry[0];

    [Header("판단 템포")]
    [Tooltip("TODO: 임시값. 다음 판단까지 기다리는 프레임. 행동·경직이 끝난 뒤의 반응 속도이기도 하다")]
    [Min(1)] [SerializeField] private int thinkIntervalFrames = 15;

    [Header("이동")]
    [Tooltip("TODO: 임시값. 접근을 고르면 이 거리까지 다가간 뒤 멈춘다")]
    [Min(0f)] [SerializeField] private float preferredRange = 1.0f;

    [Tooltip("상대가 preferredRange보다 멀 때 '접근'이 뽑힐 가중치")]
    [Min(0f)] [SerializeField] private float approachWeight = 4f;

    [Tooltip("아무것도 안 하고 한 템포 쉬는 선택지의 가중치. 0이면 쉬지 않고 몰아붙인다")]
    [Min(0f)] [SerializeField] private float waitWeight = 1f;

    [Header("점프")]
    [Tooltip("상대 쪽으로 점프하는 선택지의 가중치. 0이면 점프 안 함")]
    [Min(0f)] [SerializeField] private float jumpWeight = 0.3f;

    [Tooltip("TODO: 임시값. 점프 후 다시 점프 후보가 되기까지의 프레임")]
    [Min(0)] [SerializeField] private int jumpCooldownFrames = 180;

    public SkillEntry[] Skills => skills;
    public int ThinkIntervalFrames => thinkIntervalFrames;
    public float PreferredRange => preferredRange;
    public float ApproachWeight => approachWeight;
    public float WaitWeight => waitWeight;
    public float JumpWeight => jumpWeight;
    public int JumpCooldownFrames => jumpCooldownFrames;
}
