using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 러너 내부 구간. ActionPhase(NYH)보다 하나 많다 — Charge(홀드/충전).
/// 바깥(HitDetection, RobotMover, RobotView 등)에는 Charge가 Startup으로 보인다 (판정 없음, 이동 불가).
/// </summary>
public enum SkillPhase
{
    None,
    Charge,
    Startup,
    Active,
    Recovery,
}

/// <summary>
/// 스킬 실행 흐름의 템플릿.
///
/// 흐름(템플릿 메서드)은 이 클래스만 결정하고 자식은 훅만 채운다:
///
///   Begin ─▶ [비용 확정?] ─▶ OnBegin ─▶ 첫 구간 진입
///   Tick (CombatTick마다)
///     1. ElapsedFrames++
///     2. OnInput(input)                     ← 이번 틱 입력에 반응 (릴리즈, 홀드 중 강화, 취소)
///     3. 구간 종료 판정 → OnPhaseExit → 다음 구간 → OnPhaseEnter   (요청됐거나 길이를 다 채웠으면, 0길이 구간은 연속으로 건너뜀)
///     4. PhaseFrame++ → OnPhaseTick
///     5. 전진 이동(ActionData.AdvanceDistance) → ActionState에 동기화
///   끝: Finish(정상) / Cancel(취소) / FinishAndChain(후속 행동) / HandleInterrupted(경직·사망)
///
/// 구간 길이 규칙: 길이 N인 구간은 정확히 N틱 동안 유지된다 (PhaseFrame 1..N).
///
/// 비용(쿨타임 + 실린더) 확정 시점은 자식이 정한다:
///   CommitOnBegin = true  → 시작할 때 (기존 행동과 같음, 탭형)
///   CommitOnBegin = false → 자식이 원하는 순간 Commit() 호출 (홀드형: 뗄 때 / 홀드 중 D)
///   끝날 때까지 Commit을 안 했으면 안전장치로 쿨타임만 건다.
///
/// 이 클래스는 SkillDriver만 생성·호출한다. 스킬 하나에 로봇마다 인스턴스 1개, 매번 Begin에서 초기화해 재사용한다.
/// </summary>
public abstract class SkillRunner
{
    // 한 틱에 구간이 연속으로 넘어갈 수 있는 최대 횟수. 0길이 구간이 여러 개여도 4개면 충분하고,
    // 자식 훅이 잘못돼서 무한히 넘기면 여기서 끊는다
    private const int MAX_TRANSITIONS_PER_TICK = 8;

    /// <summary>슬롯에서 시작된 원본 스킬 (러너를 만든 SkillData)</summary>
    public SkillData Skill { get; private set; }

    /// <summary>지금 판정·연출에 쓰이는 행동. 강화/차지 단계로 바뀌면 이 값이 바뀐다 (= ActionState.CurrentAction)</summary>
    public ActionData Current { get; private set; }

    /// <summary>이 스킬이 시작된 슬롯</summary>
    public ActionSlot Slot { get; private set; }

    public SkillPhase Phase { get; private set; }

    /// <summary>현재 구간에서 몇 틱째인지 (구간 첫 틱 = 1). 구간에 들어온 직후(아직 틱 전)는 0</summary>
    public int PhaseFrame { get; private set; }

    /// <summary>시작부터 지난 틱 수 (홀드 시간 포함). ActionState.GlobalFrame으로 동기화된다</summary>
    public int ElapsedFrames { get; private set; }

    public bool IsRunning { get; private set; }

    /// <summary>쿨타임·탄 소모가 이미 확정됐는가</summary>
    public bool IsCommitted { get; private set; }

    /// <summary>끝나면서 이어서 실행해 달라고 요청한 행동. SkillDriver가 꺼내 간다</summary>
    internal ActionData ChainRequest { get; private set; }

    protected SkillContext Ctx { get; private set; }

    /// <summary>Current에 장비 보정까지 반영된 선딜/활성/후딜 프레임 (ActionState.Timeline)</summary>
    protected ResolvedAction Timeline => Ctx.State.Timeline;

    private bool exitRequested;

    // =====================================================================
    // 템플릿 메서드 — SkillDriver만 호출한다. 자식은 override할 수 없다.
    // =====================================================================

    /// <summary>
    /// 시작. startAs는 SkillData.EvaluateTrigger가 고른 행동(자기 자신 또는 변형).
    /// prepaid = true면 비용을 이미 낸 행동(후속 체인)이라 비용을 다시 받지 않는다.
    /// 실패하면(탄 부족, ActionState가 Idle이 아님 등) false — 아무 상태도 바뀌지 않는다.
    /// </summary>
    internal bool Begin(SkillData skill, ActionData startAs, ActionSlot slot, SkillContext ctx, bool prepaid)
    {
        if (skill == null || startAs == null || ctx == null) return false;

        Ctx = ctx;
        Skill = skill;
        Slot = slot;
        ResetRuntime();

        ActionData action = startAs;
        int cost = 0;
        bool payNow = !prepaid && CommitOnBegin;
        if (payNow)
        {
            // 기존 ActionExecutor 순서와 같다: 탄 게이트(모자라면 폴백) → Begin → 쿨/탄 확정
            action = ctx.GateCylinder(action, out cost);
            if (action == null) return false;
        }

        if (!ctx.State.BeginExternal(action, ctx.Driver)) return false;

        Current = action;
        IsRunning = true;

        if (prepaid)
        {
            IsCommitted = true;
        }
        else if (payNow)
        {
            ctx.ApplyCost(action, cost);
            IsCommitted = true;
        }

        Debug.Log($"[SkillRunner] {ctx.FighterId}: '{action.ActionName}' 시작 (슬롯 {slot}, 러너 {GetType().Name}, 비용 확정={IsCommitted})");

        OnBegin();
        if (IsRunning) EnterPhase(FirstPhase);
        return IsRunning;
    }

    /// <summary>CombatTick마다 1회. 실행 순서는 클래스 설명 참고</summary>
    internal void Tick(in SkillInputFrame input)
    {
        if (!IsRunning) return;

        ElapsedFrames++;

        OnInput(input);
        if (!IsRunning) return;

        int transitions = 0;
        while (IsRunning && Phase != SkillPhase.None && (exitRequested || PhaseFrame >= GetPhaseLength(Phase)))
        {
            if (++transitions > MAX_TRANSITIONS_PER_TICK)
            {
                Debug.LogWarning($"[SkillRunner] {Current?.ActionName}: 한 틱에 구간 전환이 {MAX_TRANSITIONS_PER_TICK}회를 넘음 — GetPhaseLength/GetNextPhase 확인 필요. 강제 종료");
                Finish();
                return;
            }

            exitRequested = false;
            SkillPhase from = Phase;
            OnPhaseExit(from);
            if (!IsRunning) return;
            EnterPhase(GetNextPhase(from));
        }
        if (!IsRunning) return;

        PhaseFrame++;
        OnPhaseTick(Phase, input);
        if (!IsRunning) return;

        ApplyAdvance();
        Sync();
    }

    /// <summary>ActionState.Interrupt(경직) / Kill(사망)로 밖에서 끊겼을 때. ActionState는 이미 다음 상태로 넘어간 뒤다</summary>
    internal void HandleInterrupted()
    {
        if (!IsRunning) return;

        IsRunning = false;
        if (!IsCommitted)
        {
            Ctx.StartCooldown(Current, InterruptCooldownScale);
            IsCommitted = true;
        }
        Phase = SkillPhase.None;

        OnInterrupted();
        OnEnd();
    }

    internal ActionData ConsumeChainRequest()
    {
        ActionData next = ChainRequest;
        ChainRequest = null;
        return next;
    }

    // =====================================================================
    // 훅 — 자식이 override한다. 기본값은 "기존 ActionData와 똑같이 선딜 → 활성 → 후딜"
    // =====================================================================

    /// <summary>시작할 때 비용(쿨·탄)을 확정하는가. false면 자식이 Commit()을 직접 불러야 한다</summary>
    protected virtual bool CommitOnBegin => true;

    /// <summary>비용 확정 전에 경직/사망으로 끊겼을 때 쿨타임 배율. 0이면 쿨 없음</summary>
    protected virtual float InterruptCooldownScale => 1f;

    /// <summary>첫 구간. 홀드형은 Charge부터 시작할 수 있다</summary>
    protected virtual SkillPhase FirstPhase => SkillPhase.Startup;

    /// <summary>
    /// 구간 길이(틱). 이만큼 지나면 다음 구간으로 넘어간다. 0이면 그 구간은 건너뛴다.
    /// 플레이어 입력으로만 끝나는 구간(홀드)은 int.MaxValue를 돌려주고 RequestPhaseExit()로 끝낸다.
    /// </summary>
    protected virtual int GetPhaseLength(SkillPhase phase)
    {
        ResolvedAction t = Timeline;
        switch (phase)
        {
            case SkillPhase.Startup: return t != null ? t.StartupFrames : 0;
            case SkillPhase.Active: return t != null ? t.ActiveFrames : 0;
            case SkillPhase.Recovery: return t != null ? t.RecoveryFrames : 0;
            default: return 0; // Charge는 기본적으로 없음
        }
    }

    /// <summary>다음 구간. None을 돌려주면 정상 종료</summary>
    protected virtual SkillPhase GetNextPhase(SkillPhase phase)
    {
        switch (phase)
        {
            case SkillPhase.Charge: return SkillPhase.Startup;
            case SkillPhase.Startup: return SkillPhase.Active;
            case SkillPhase.Active: return SkillPhase.Recovery;
            default: return SkillPhase.None;
        }
    }

    /// <summary>
    /// 지금 이 러너가 해석 중인 슬롯인가. true인 슬롯의 입력은 새 행동 시작(버퍼)으로 쓰이지 않는다.
    /// 예: 홀드형은 홀드 중에 자기 슬롯(뗌 감지)과 D(강화)를 가져간다 — 안 그러면 홀드 중 D가 끝난 뒤 기본기로 또 나간다.
    /// </summary>
    public virtual bool ListensTo(ActionSlot slot) => false;

    protected virtual void OnBegin() { }

    /// <summary>매 틱, 구간 전환 판정보다 먼저 불린다. 입력 반응은 여기서 한다 (반응 지연 0틱)</summary>
    protected virtual void OnInput(in SkillInputFrame input) { }

    protected virtual void OnPhaseEnter(SkillPhase phase) { }

    /// <summary>매 틱, 구간 전환과 PhaseFrame 증가가 끝난 뒤 불린다</summary>
    protected virtual void OnPhaseTick(SkillPhase phase, in SkillInputFrame input) { }

    protected virtual void OnPhaseExit(SkillPhase phase) { }

    /// <summary>경직·사망으로 끊겼을 때만 불린다 (그다음 OnEnd도 불린다)</summary>
    protected virtual void OnInterrupted() { }

    /// <summary>어떤 식으로 끝나든 마지막에 한 번 (정상·취소·체인·끊김)</summary>
    protected virtual void OnEnd() { }

    /// <summary>
    /// 지금 켜진 판정 박스. 기본 규칙:
    /// - 선딜/활성/후딜: 확정 프레임(Timeline) 위의 위치로 박스 창(ResolvedAction.GetWindow)을 계산 — 기존과 같은 결과
    /// - 활성이 데이터보다 길게 유지되면(지속형) 활성 마지막 프레임 위치에 고정 → 활성 박스가 계속 켜져 있다
    /// - Charge: 첫 프레임 위치의 박스 중 Hit 박스는 뺀다 (몸통 Hurt/Push만)
    /// </summary>
    public virtual IEnumerable<FrameBox> GetActiveBoxes()
    {
        if (!IsRunning || Current == null) yield break;

        ResolvedAction t = Timeline;
        if (t == null) yield break;

        bool charging = Phase == SkillPhase.Charge;
        int frame = GetTimelineFrame();

        foreach (FrameBox source in Current.FrameBoxes)
        {
            if (charging && source.type == BoxType.Hit) continue;

            FrameBox box = source; // 구조체 복사 — SO 원본은 안 바뀐다
            ResolvedAction.GetWindow(box, t.StartupFrames, t.ActiveFrames, t.RecoveryFrames, out box.startFrame, out box.endFrame);
            if (frame >= box.startFrame && frame <= box.endFrame)
                yield return box;
        }
    }

    // =====================================================================
    // 자식이 쓰는 도구
    // =====================================================================

    /// <summary>현재 구간을 이번 틱에 끝낸다. OnInput에서 부르면 같은 틱에 다음 구간으로 넘어간다</summary>
    protected void RequestPhaseExit() => exitRequested = true;

    /// <summary>
    /// 비용 확정 (쿨타임 시작 + 탄 소모). variant를 주면 그 행동으로 바꾸면서 확정한다.
    /// 탄이 모자라면 variant.FallbackAction으로 바뀐다. 그것도 안 되면 null을 돌려주고 아무것도 바뀌지 않는다.
    /// 이미 확정했으면 아무것도 하지 않고 Current를 돌려준다.
    /// </summary>
    protected ActionData Commit(ActionData variant = null)
    {
        if (IsCommitted) return Current;

        ActionData want = variant != null ? variant : Current;
        ActionData paid = Ctx.GateCylinder(want, out int cost);
        if (paid == null) return null;

        if (paid != Current) SwapTo(paid);
        Ctx.ApplyCost(paid, cost);
        IsCommitted = true;
        return paid;
    }

    /// <summary>
    /// 판정·연출 행동을 바꾼다 (차지 단계, 강화판정 등). 장비 보정과 Timeline을 새 행동 기준으로 다시 계산한다.
    /// restartPresentation = true면 ActionState.OnActionBegin을 다시 쏴서 애니메이션/이펙트가 새 행동 것으로 바뀐다.
    /// 구간과 PhaseFrame은 그대로 이어진다.
    /// </summary>
    protected void SwapTo(ActionData action, bool restartPresentation = true)
    {
        if (action == null || action == Current) return;
        Current = action;
        Ctx.State.SwapExternalAction(action, restartPresentation);
    }

    /// <summary>
    /// 취소하고 바로 끝낸다. 비용 확정 전이면 쿨타임만 cooldownScale 배로 건다 (탄은 안 깎는다).
    /// 예: 레이저 게이지 충전 전에 떼면 Cancel(0.5f)
    /// </summary>
    protected void Cancel(float cooldownScale = 1f)
    {
        if (!IsRunning) return;
        if (!IsCommitted)
        {
            Ctx.StartCooldown(Current, cooldownScale);
            IsCommitted = true;
        }
        Finish();
    }

    /// <summary>
    /// 지금 행동을 끝내고 next를 바로 이어서 실행한다 (같은 틱, 이동 가능한 빈 틱 없음).
    /// payForNext = true면 next의 비용(쿨·탄)을 여기서 받는다. 탄이 모자라면 false를 돌려주고 아무것도 안 한다.
    /// next는 다시 게이트(쿨타임·부위)를 거치지 않는다 — 이미 이 스킬의 일부로 확정된 후속 동작이기 때문.
    /// 예: 연속 펀치 홀드 중 D → 피니시 블로우
    /// </summary>
    protected bool FinishAndChain(ActionData next, bool payForNext)
    {
        if (!IsRunning || next == null) return false;

        if (payForNext && !Ctx.CanAffordCylinder(next)) return false;

        // 이 스킬의 쿨타임이 아직 안 돌고 있으면 지금 건다 (연속 펀치: 피니시로 끝나도 쿨은 돈다).
        // next가 같은 쿨 키(cooldownOwner)에 자기 쿨타임을 갖고 있으면 아래 ApplyCost가 그 값으로 덮어쓴다
        if (!IsCommitted)
        {
            Ctx.StartCooldown(Current, 1f);
            IsCommitted = true;
        }

        if (payForNext)
        {
            ActionData paid = Ctx.GateCylinder(next, out int cost);
            if (paid == null) return false;
            Ctx.ApplyCost(paid, cost);
            next = paid;
        }

        ChainRequest = next;
        Finish();
        return true;
    }

    /// <summary>
    /// 확정 프레임(Timeline) 위에서 지금 위치. 선딜 첫 프레임 = 1. 박스 창 계산·전진 이동이 이 값을 쓴다.
    /// Charge는 1, 데이터보다 길게 유지된 구간은 그 구간의 마지막 프레임에 고정된다.
    /// </summary>
    protected int GetTimelineFrame()
    {
        ResolvedAction t = Timeline;
        if (t == null) return 1;

        int pf = Mathf.Max(1, PhaseFrame);
        switch (Phase)
        {
            case SkillPhase.Startup: return Mathf.Min(pf, Mathf.Max(1, t.StartupFrames));
            case SkillPhase.Active: return t.StartupFrames + Mathf.Min(pf, t.ActiveFrames);
            case SkillPhase.Recovery: return t.StartupFrames + t.ActiveFrames + Mathf.Min(pf, t.RecoveryFrames);
            default: return 1;
        }
    }

    /// <summary>
    /// ActionData.AdvanceDistance 전진. 기존 ActionExecutor.ApplyAdvance와 같은 규칙인데, 홀드 시간이 끼어도
    /// 어긋나지 않게 Timeline 기준으로 센다. 외부 구동 중에는 ActionExecutor.ApplyAdvance가 건너뛴다.
    /// 다른 이동 방식이 필요한 스킬은 override한다.
    /// </summary>
    protected virtual void ApplyAdvance()
    {
        ActionData action = Current;
        if (action == null || action.AdvanceDistance == 0f) return;

        ResolvedAction t = Timeline;
        if (t == null) return;

        bool activeOnly = action.AdvanceDuringActiveOnly;
        int frames = activeOnly ? t.ActiveFrames : t.StartupFrames + t.ActiveFrames;
        if (frames <= 0) return;

        bool inWindow = (Phase == SkillPhase.Startup && !activeOnly)
                        || (Phase == SkillPhase.Active && PhaseFrame <= t.ActiveFrames);
        if (!inWindow) return;

        float direction = Ctx.FacingRight ? 1f : -1f;
        Vector3 pos = Ctx.Transform.position;
        pos.x += direction * action.AdvanceDistance / frames;
        Ctx.Transform.position = pos;
    }

    /// <summary>바깥(ActionState)에 보일 구간. Charge는 Startup으로 보인다</summary>
    protected virtual ActionPhase ToActionPhase(SkillPhase phase)
    {
        switch (phase)
        {
            case SkillPhase.Charge:
            case SkillPhase.Startup: return ActionPhase.Startup;
            case SkillPhase.Active: return ActionPhase.Active;
            case SkillPhase.Recovery: return ActionPhase.Recovery;
            default: return ActionPhase.Idle;
        }
    }

    // =====================================================================
    // 내부
    // =====================================================================

    private void ResetRuntime()
    {
        Current = null;
        Phase = SkillPhase.None;
        PhaseFrame = 0;
        ElapsedFrames = 0;
        IsRunning = false;
        IsCommitted = false;
        ChainRequest = null;
        exitRequested = false;
    }

    private void EnterPhase(SkillPhase phase)
    {
        if (phase == SkillPhase.None)
        {
            Finish();
            return;
        }

        Phase = phase;
        PhaseFrame = 0;
        OnPhaseEnter(phase);
    }

    private void Finish()
    {
        if (!IsRunning) return;

        if (!IsCommitted)
        {
            // 안전장치: 비용 확정 없이 끝난 스킬도 쿨타임은 돈다 (탄은 안 깎음)
            Ctx.StartCooldown(Current, 1f);
            IsCommitted = true;
        }

        IsRunning = false;
        Phase = SkillPhase.None;
        OnEnd();
        Ctx.State.EndExternal();
    }

    // FrameInPhase는 0부터 넘긴다 — 기존 활성 구간 규칙(진입 틱 = 0)과 맞춰야
    // ActionExecutor.EmitProjectiles / AfterImageEmitter의 "% 간격 == 0" 판정이 첫 틱에 터진다
    private void Sync()
    {
        Ctx.State.SyncExternal(ToActionPhase(Phase), Mathf.Max(0, PhaseFrame - 1), ElapsedFrames);
    }
}

/// <summary>
/// 기본 러너: 선딜 → 활성 → 후딜, 시작할 때 비용 확정. 기존 ActionData와 같은 흐름이다.
/// 탭형 스킬이나 특별한 흐름이 없는 스킬은 이걸 쓴다.
/// </summary>
public class StandardSkillRunner : SkillRunner
{
}
