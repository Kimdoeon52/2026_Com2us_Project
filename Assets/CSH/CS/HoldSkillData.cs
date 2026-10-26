using UnityEngine;

/// <summary>홀드가 어느 구간을 늘리는가</summary>
public enum HoldMode
{
    /// <summary>
    /// 누르는 동안 Charge 구간(판정 없음)에 머물다가, 떼면 선딜 → 활성 → 후딜.
    /// 차지 샷, 레이저 샷처럼 "모았다가 쏘는" 스킬
    /// </summary>
    ChargeThenRelease,

    /// <summary>
    /// 선딜 뒤 활성 구간이 누르는 동안 계속 유지되고, 떼면 후딜.
    /// 활성은 최소 데이터의 ActiveFrames만큼은 보장된다 (그래서 ActiveFrames는 1 이상이어야 한다).
    /// 연속 펀치처럼 "누르는 동안 계속 때리는" 스킬. 다단히트 간격은 ActionData.hitIntervalFrames
    /// </summary>
    SustainActive,
}

/// <summary>홀드 중 강화 키(기본 D)를 눌렀을 때</summary>
public enum EnhanceMode
{
    /// <summary>강화판정으로 바꾸고 홀드는 그대로 계속 (뗄 때 강화판정으로 나감)</summary>
    SwapAndContinue,

    /// <summary>강화판정으로 바꾸고 홀드를 바로 끝냄 (= 그 자리에서 뗀 것처럼)</summary>
    SwapAndRelease,

    /// <summary>지금 행동을 끝내고 강화판정을 후속 행동으로 바로 이어서 실행 (연속 펀치 → 피니시 블로우)</summary>
    FinishAndChain,
}

/// <summary>
/// 홀드형 키 베이스 (기획서 "스킬 키 유형 - 홀드형": 누르고 있다가 뗌, 홀드 중 D 입력 시 탄 소모·강화 확정).
///
/// 누른 순간 행동이 시작되고(이동 불가), 뗄 때 쿨타임이 시작된다.
/// 공통 규칙(최소/최대 홀드, 조기 취소 쿨 배율, 홀드 중 강화)은 전부 이 데이터의 값으로 정하고,
/// 스킬마다 다른 부분(차지 단계별 판정, 공중 정지 등)은 HoldSkillRunner를 상속해 훅으로 채운다.
/// </summary>
[CreateAssetMenu(menuName = "CSH/Skill/Hold Skill (홀드형 베이스)", fileName = "NewHoldSkill")]
public class HoldSkillData : SkillData
{
    [Header("홀드형 — 홀드")]
    [SerializeField] private HoldMode holdMode = HoldMode.ChargeThenRelease;

    [Tooltip("이 틱 수보다 짧게 누르고 떼면 취소된다(판정 없이 끝남). 0 = 취소 없음. 예: 레이저 게이지 1초 = 60")]
    [Min(0)][SerializeField] private int minHoldFrames;

    [Tooltip("최소 홀드 전에 떼서 취소됐을 때 쿨타임 배율. 1 = 전체, 0.5 = 절반, 0 = 쿨 없음")]
    [Range(0f, 1f)][SerializeField] private float earlyReleaseCooldownScale = 1f;

    [Tooltip("이 틱 수만큼 누르고 있으면 떼지 않아도 자동으로 뗀 것으로 처리. 0 = 무제한. 예: 차지 샷 2.5초 = 150, 연속 펀치 4초 = 240")]
    [Min(0)][SerializeField] private int maxHoldFrames;

    [Header("홀드형 — 홀드 중 강화")]
    [Tooltip("홀드 중 강화 키를 누르면 나갈 강화판정. 비우면 강화 없음. 탄이 모자라면 강화 키 입력은 무시된다")]
    [SerializeField] private ActionData enhancedVariant;

    [Tooltip("홀드 중 강화 키. 기획서 기준 D")]
    [SerializeField] private ActionSlot enhanceSlot = ActionSlot.Basic;

    [SerializeField] private EnhanceMode enhanceMode = EnhanceMode.SwapAndContinue;

    [Header("홀드형 — 끊김")]
    [Tooltip("쿨타임 시작(뗌) 전에 경직·사망으로 끊겼을 때 쿨타임 배율. 0 = 쿨 없음")]
    [Range(0f, 1f)][SerializeField] private float interruptCooldownScale = 1f;

    public HoldMode HoldMode => holdMode;
    public int MinHoldFrames => minHoldFrames;
    public float EarlyReleaseCooldownScale => earlyReleaseCooldownScale;
    public int MaxHoldFrames => maxHoldFrames;
    public ActionData EnhancedVariant => enhancedVariant;
    public ActionSlot EnhanceSlot => enhanceSlot;
    public EnhanceMode EnhanceMode => enhanceMode;
    public float InterruptCooldownScale => interruptCooldownScale;

    // 누른 순간 시작 (SkillData 기본) — 홀드 동안 행동 중이어야 이동이 막히고 차지 포즈가 나온다

    public override SkillRunner CreateRunner() => new HoldSkillRunner();
}
