using UnityEngine;

/// <summary>
/// 홀드형 러너. HoldSkillData의 값으로 공통 홀드 규칙을 처리하고, 스킬별 차이는 아래 훅으로 연다.
///
///   ChargeThenRelease : Charge(누르는 동안) ─뗌─▶ 선딜 → 활성 → 후딜
///   SustainActive     : 선딜 → 활성(누르는 동안, 최소 ActiveFrames) ─뗌─▶ 후딜
///
/// 뗌 처리 순서 (HandleRelease):
///   최소 홀드 미달 → Cancel(조기 취소 쿨 배율)
///   그 외 → SelectReleaseVariant(누른 시간)로 나갈 행동 결정 → Commit(쿨 시작 + 탄) → OnHoldReleased → 다음 구간
///
/// 스킬별 확장 훅
///   SelectReleaseVariant : 누른 시간에 따라 다른 판정 (차지 샷 3단계)
///   OnHoldTick           : 홀드 중 매 틱 (게이지 연출, 공중 정지)
///   OnHoldReleased       : 뗀 순간 (반동 이동 등)
///   OnEnhanced           : 홀드 중 강화가 확정된 순간
/// </summary>
public class HoldSkillRunner : SkillRunner
{
    private bool released;   // 손을 뗐다 (또는 강화로 뗀 것으로 처리)
    private bool holdEnded;  // 홀드 구간 처리가 끝났다 (뗌 또는 최대 시간)
    private bool enhanced;

    protected HoldSkillData Hold => (HoldSkillData)Skill;

    /// <summary>시작부터 실제로 누르고 있던 틱 수</summary>
    protected int HeldFrames { get; private set; }

    /// <summary>홀드 중 강화가 확정됐는가</summary>
    public bool IsEnhanced => enhanced;

    /// <summary>지금 홀드 구간에서 누르고 있는 중인가 (UI 게이지용)</summary>
    public bool IsHolding => IsRunning && !holdEnded && Phase == HoldPhase;

    /// <summary>홀드가 늘리는 구간</summary>
    protected SkillPhase HoldPhase => Hold.HoldMode == HoldMode.ChargeThenRelease ? SkillPhase.Charge : SkillPhase.Active;

    // ---- SkillRunner 훅 ----

    protected override bool CommitOnBegin => false; // 쿨타임은 뗄 때(또는 홀드 중 강화 때) 시작

    protected override float InterruptCooldownScale => Hold.InterruptCooldownScale;

    protected override SkillPhase FirstPhase => Hold.HoldMode == HoldMode.ChargeThenRelease ? SkillPhase.Charge : SkillPhase.Startup;

    protected override int GetPhaseLength(SkillPhase phase)
    {
        // 홀드 구간은 입력으로만 끝난다 (RequestPhaseExit)
        if (phase == HoldPhase && !holdEnded) return int.MaxValue;
        return base.GetPhaseLength(phase);
    }

    public override bool ListensTo(ActionSlot slot)
    {
        if (!IsRunning || released) return false;
        if (slot == Slot) return true;
        return !enhanced && Hold.EnhancedVariant != null && slot == Hold.EnhanceSlot;
    }

    protected override void OnBegin()
    {
        released = false;
        holdEnded = false;
        enhanced = false;
        HeldFrames = 0;

        if (Hold.HoldMode == HoldMode.SustainActive && Current.ActiveFrames <= 0)
            Debug.LogWarning($"[HoldSkillRunner] '{Current.ActionName}': SustainActive인데 ActiveFrames가 0 — 활성 박스 창이 없어 판정이 안 나온다");
    }

    protected override void OnInput(in SkillInputFrame input)
    {
        if (!released)
        {
            if (input[Slot].Held) HeldFrames++;
            else released = true; // Released 틱이든, 틱 사이에 눌렀다 뗀 경우든 Held가 false면 뗀 것
        }

        if (!holdEnded && !released && !enhanced && Hold.EnhancedVariant != null && input[Hold.EnhanceSlot].Pressed)
        {
            TryEnhance();
            if (!IsRunning) return;
        }

        if (holdEnded || Phase != HoldPhase) return;

        // SustainActive는 데이터의 활성 길이만큼은 보장한다
        if (Hold.HoldMode == HoldMode.SustainActive && PhaseFrame < Timeline.ActiveFrames) return;

        bool timedOut = IsMaxHoldReached();
        if (released || timedOut) HandleRelease(timedOut);
    }

    /// <summary>
    /// 최대 홀드에 도달해 자동으로 뗀 것으로 처리할지. 홀드 구간에서 OnInput마다 불린다.
    /// 기본: 누른 순간부터 센 HeldFrames(선딜 포함)가 MaxHoldFrames 이상.
    /// 연속 펀치처럼 "때리는 시간"으로 세야 하는 스킬은 override한다.
    /// </summary>
    protected virtual bool IsMaxHoldReached() => Hold.MaxHoldFrames > 0 && HeldFrames >= Hold.MaxHoldFrames;

    protected override void OnPhaseTick(SkillPhase phase, in SkillInputFrame input)
    {
        if (phase == HoldPhase && !holdEnded) OnHoldTick(input);
    }

    // ---- 스킬별 확장 훅 ----

    /// <summary>
    /// 뗄 때 나갈 행동. 기본은 지금 행동 그대로. 차지 단계처럼 누른 시간으로 판정이 갈리면 여기서 다른 에셋을 돌려준다.
    /// 강화가 이미 확정됐으면 불리지 않는다.
    /// </summary>
    protected virtual ActionData SelectReleaseVariant(int heldFrames) => Current;

    /// <summary>홀드 구간 동안 매 틱 (PhaseFrame 증가 후)</summary>
    protected virtual void OnHoldTick(in SkillInputFrame input) { }

    /// <summary>홀드가 끝나고 비용이 확정된 직후. timedOut = 최대 홀드 시간으로 자동 발동</summary>
    protected virtual void OnHoldReleased(bool timedOut) { }

    /// <summary>홀드 중 강화가 확정된 직후 (FinishAndChain 모드에서는 불리지 않고 바로 후속 행동으로 넘어간다)</summary>
    protected virtual void OnEnhanced() { }

    // ---- 내부 ----

    private void TryEnhance()
    {
        ActionData variant = Hold.EnhancedVariant;

        // 기획서: "홀드 중 D 입력 시 탄 소모, 강화 확정" — 탄이 모자라면 D는 무시하고 홀드를 계속한다.
        // (폴백으로 일반판정이 확정돼 버리면 홀드가 의도치 않게 끝나므로 GateCylinder 전에 직접 확인)
        if (!Ctx.CanAffordCylinder(variant)) return;

        switch (Hold.EnhanceMode)
        {
            case EnhanceMode.FinishAndChain:
                enhanced = FinishAndChain(variant, payForNext: true);
                return;

            case EnhanceMode.SwapAndRelease:
                if (Commit(variant) != variant) return;
                enhanced = true;
                released = true; // 홀드 구간에 있으면 이번 틱에 바로 뗀 것으로 처리된다
                OnEnhanced();
                return;

            default: // SwapAndContinue
                if (Commit(variant) != variant) return;
                enhanced = true;
                OnEnhanced();
                return;
        }
    }

    private void HandleRelease(bool timedOut)
    {
        holdEnded = true;

        if (!enhanced && !timedOut && HeldFrames < Hold.MinHoldFrames)
        {
            Cancel(Hold.EarlyReleaseCooldownScale);
            return;
        }

        if (!enhanced)
        {
            ActionData variant = SelectReleaseVariant(HeldFrames);
            if (Commit(variant) == null)
            {
                // 탄이 모자라고 폴백도 없다 — 판정 없이 끝낸다 (쿨은 정상)
                Cancel(1f);
                return;
            }
        }

        OnHoldReleased(timedOut);
        if (!IsRunning) return;

        RequestPhaseExit();
    }
}
