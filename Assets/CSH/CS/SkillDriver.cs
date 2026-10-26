using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로봇 하나의 스킬 실행 담당 (플레이어 전용). ActionExecutor가 입력원이 ISkillInputSource일 때만 만든다.
/// 기존 ExecuteTick의 "Idle이면 GetDesiredAction → 게이트 → Begin" 블록을 이 클래스가 대신한다.
///
/// 틱 순서 (ActionExecutor.ExecuteTick 안, state.Advance 직전):
///   1. 입력 샘플 (틱당 1회)
///   2. 시작 판정: 슬롯마다 바인딩 행동의 EvaluateTrigger → 결과를 입력 버퍼에 넣는다
///      (실행 중인 러너가 듣고 있는 슬롯은 건너뜀)
///   3. Idle이면 버퍼의 행동을 시작
///        SkillData → 러너 경로 (ActionState 외부 구동)
///        일반 ActionData → 기존 경로 (ActionExecutor.TryBeginRequested — 기존과 완전히 같은 게이트/비용)
///   4. 실행 중인 러너 Tick. 러너가 후속 행동(체인)을 요청하며 끝났으면 같은 틱에 이어서 시작
/// </summary>
public sealed class SkillDriver : IExternalActionDriver
{
    // 같은 틱에 여러 슬롯이 동시에 시작 조건을 만족하면 앞에 있는 것이 이긴다.
    // 회피를 맨 앞에 둔 이유: 공격과 동시에 눌렸을 때 생존 행동이 씹히는 쪽이 더 억울하다
    private static readonly ActionSlot[] TriggerOrder =
    {
        ActionSlot.Dodge,
        ActionSlot.Basic,
        ActionSlot.LeftArm,
        ActionSlot.RightArm,
        ActionSlot.LeftLeg,
        ActionSlot.RightLeg,
    };

    private readonly ActionExecutor executor;
    private readonly ISkillInputSource input;
    private readonly ISkillLoadout loadout;
    private readonly SkillContext context;
    private readonly Dictionary<SkillData, SkillRunner> runners = new Dictionary<SkillData, SkillRunner>();

    private SkillRunner current;

    // 입력 버퍼 — 행동 중에 들어온 시작 입력을 Idle이 될 때까지 몇 틱 들고 있는다
    private struct Pending
    {
        public bool Valid;
        public ActionSlot Slot;
        public ActionData Bound;   // 슬롯 행동(장비 대체 반영)
        public ActionData StartAs; // EvaluateTrigger 결과 (강화판정일 수 있음)
        public int TicksLeft;
    }

    private Pending pending;

    /// <summary>지금 실행 중인 러너. 없거나 일반 ActionData 실행 중이면 null. UI(차지 게이지 등)가 읽는다</summary>
    public SkillRunner CurrentRunner => current != null && current.IsRunning ? current : null;

    /// <summary>러너로 스킬이 시작된 순간</summary>
    public event Action<SkillRunner> OnSkillBegin;

    public SkillDriver(ActionExecutor executor, ISkillInputSource input, ISkillLoadout loadout)
    {
        this.executor = executor;
        this.input = input;
        this.loadout = loadout;
        context = new SkillContext(executor, this);

        if (loadout == null)
            Debug.LogWarning($"[SkillDriver] {executor.name}: ISkillLoadout(PlayerSkillLoadout)이 없음 — 슬롯에 행동이 없어 스킬이 나가지 않는다");
    }

    /// <summary>호출: ActionExecutor.ExecuteTick (CombatTick마다 1회)</summary>
    public void Tick()
    {
        SkillInputFrame frame = input.SampleSkillInput();
        ActionState state = executor.State;

        EvaluateTriggers(frame);

        if (pending.Valid)
        {
            if (state.CanAcceptNewAction)
            {
                Pending p = pending;
                pending = default;
                StartFromSlot(p, frame);
            }
            else if (--pending.TicksLeft < 0)
            {
                pending = default; // 기존 PlayerInputSource처럼 버퍼 시간이 지나면 버린다
            }
        }

        TickCurrent(frame);
    }

    // ---- IExternalActionDriver ----

    public IEnumerable<FrameBox> GetActiveBoxes()
    {
        return current != null ? current.GetActiveBoxes() : Array.Empty<FrameBox>();
    }

    public void OnExternalInterrupted()
    {
        SkillRunner interrupted = current;
        current = null;
        interrupted?.HandleInterrupted();
        pending = default; // 맞아서 끊겼으면 끊기기 전에 쌓인 입력은 버린다
    }

    // ---- 내부 ----

    private void EvaluateTriggers(in SkillInputFrame frame)
    {
        if (loadout == null) return;

        SkillRunner running = CurrentRunner;

        for (int i = 0; i < TriggerOrder.Length; i++)
        {
            ActionSlot slot = TriggerOrder[i];
            SlotState s = frame[slot];
            if (!s.Any) continue;
            if (running != null && running.ListensTo(slot)) continue;

            ActionData bound = loadout.GetSlotAction(slot);
            if (bound == null) continue;

            // 장비 효과로 슬롯 행동이 바뀌었으면(ActionReplaceEffect) 바뀐 행동의 트리거 규칙을 따른다
            bound = executor.SkillResolveSlotAction(bound);

            ActionData startAs = bound is SkillData skill
                ? skill.EvaluateTrigger(s)
                : (s.Pressed ? bound : null); // 일반 ActionData는 기존처럼 누른 순간

            if (startAs == null) continue;

            pending = new Pending
            {
                Valid = true,
                Slot = slot,
                Bound = bound,
                StartAs = startAs,
                TicksLeft = input.InputBufferTicks,
            };
            return;
        }
    }

    private void StartFromSlot(Pending p, in SkillInputFrame frame)
    {
        if (p.Bound is SkillData skill)
        {
            // 부위 파손·쿨타임 게이트. 실린더 게이트는 러너가 비용 확정 시점에 한다
            ActionData gated = executor.SkillGateStart(p.StartAs);
            if (gated == null) return;

            StartRunner(skill, gated, p.Slot, prepaid: false);
        }
        else
        {
            // 일반 ActionData(D, 회피 등) — 기존 ExecuteTick과 같은 게이트·비용·로그
            current = null;
            executor.TryBeginRequested(p.StartAs, null);
        }
    }

    private void TickCurrent(in SkillInputFrame frame)
    {
        if (current == null || !current.IsRunning) return;

        SkillRunner runner = current;
        runner.Tick(frame);
        if (runner.IsRunning) return;

        current = null;
        ActionData next = runner.ConsumeChainRequest();
        if (next != null) StartChain(next, runner.Slot, frame);
    }

    // 체인은 이미 비용을 낸 후속 동작이라 게이트·비용 없이 바로 시작한다
    private void StartChain(ActionData next, ActionSlot slot, in SkillInputFrame frame)
    {
        if (next is SkillData skill)
        {
            if (StartRunner(skill, next, slot, prepaid: true))
                current.Tick(frame); // 같은 틱 첫 프레임 (일반 행동이 Begin 직후 Advance되는 것과 맞춤)
        }
        else
        {
            executor.State.Begin(next); // 다음 state.Advance(ExecuteTick)에서 첫 프레임
        }
    }

    private bool StartRunner(SkillData skill, ActionData startAs, ActionSlot slot, bool prepaid)
    {
        SkillRunner runner = GetRunner(skill);
        if (runner == null) return false;

        if (!runner.Begin(skill, startAs, slot, context, prepaid)) return false;

        current = runner;
        OnSkillBegin?.Invoke(runner);
        return true;
    }

    private SkillRunner GetRunner(SkillData skill)
    {
        if (runners.TryGetValue(skill, out SkillRunner runner)) return runner;

        runner = skill.CreateRunner();
        if (runner == null)
        {
            Debug.LogError($"[SkillDriver] '{skill.ActionName}'.CreateRunner()가 null을 돌려줌");
            return null;
        }

        runners.Add(skill, runner);
        return runner;
    }
}
