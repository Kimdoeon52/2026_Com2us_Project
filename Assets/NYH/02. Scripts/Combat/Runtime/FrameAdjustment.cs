/// <summary>
/// 한 행동의 프레임을 얼마나 바꿀 것인가 (2층 · 런타임). 장비·스킬이 "선딜 2프레임 단축" 같은
/// 효과를 줄 때 그 값이 담기는 자리다. 전부 0(기본값)이면 원본 프레임 그대로라는 뜻이다.
///
/// ActionData(불변 SO)를 직접 고치면 에디터 에셋이 영구히 오염되므로(§2), 보정은 항상 이 구조체로
/// 받아서 ResolvedAction이 "이번 실행분"만 확정한다. 원본은 한 번도 안 바뀐다.
///
/// CSH의 ResolvedModifiers를 그대로 쓰지 않고 NYH 쪽에 따로 둔 이유: 프레임은 전투 실행 엔진의
/// 계약이라 NYH가 소유해야 하고(§0), 상희 님 쪽에 프레임 필드가 생기기 전에도 이 구조가 먼저 서 있어야
/// 한다. 나중에 ResolvedModifiers에 필드가 추가되면 ActionState에서 한 줄로 옮겨 담으면 된다.
/// </summary>
public struct FrameAdjustment
{
    // 각 구간에 더할 프레임 수. 음수면 줄어든다
    public int StartupAdd;
    public int ActiveAdd;
    public int RecoveryAdd;

    // 세 구간 전체에 곱해지는 배율. 1이면 변화 없음.
    // default(FrameAdjustment)는 이 값이 0이 되어 행동 시간이 0이 되어버리므로,
    // "보정 없음"이 필요할 땐 반드시 아래 None을 쓴다 (CSH가 KnockbackMultiplier에서 겪은 것과 같은 함정)
    public float SpeedMultiplier;

    /// <summary>보정 없음. new FrameAdjustment()를 직접 쓰면 SpeedMultiplier가 0이 되니 항상 이걸 쓴다</summary>
    public static FrameAdjustment None => new FrameAdjustment { SpeedMultiplier = 1f };
}
