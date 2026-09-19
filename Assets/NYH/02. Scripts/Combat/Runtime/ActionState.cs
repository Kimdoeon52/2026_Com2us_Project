using System;

/// <summary>
/// 로봇 한 마리가 "지금 뭘 하고 있는가"를 담는 유일한 상태 (2층 · 런타임 상태).
/// 순수 C# 클래스 — MonoBehaviour가 아니다. 전투 중에만 존재하고 캐릭터당 정확히 1개.
///
/// 이 클래스는 지금 진행 중인 게 잽인지 보스 전용 기술인지 전혀 모른다.
/// ActionData에서 프레임 숫자만 읽어서 Idle → Startup → Active → Recovery → Idle 을 돈다.
///
/// 스킬(CSH) 연동은 아래 이벤트 3개로만 이루어진다 — CLAUDE.md §13 참조.
/// 여기서 "훅이면 ~한다" 같은 기술 이름 분기는 절대 넣지 않는다.
/// 이벤트는 어떤 ActionData든 무조건 발생시키고, 반응 여부는 구독하는 쪽이 판단한다.
/// </summary>
public class ActionState
{
    /// <summary>지금 진행 중인 행동. null이면 Idle.</summary>
    public ActionData CurrentAction { get; private set; }
    // <summary>지금 진행 중인 행동의 상태</summary>
    public ActionPhase Phase { get; private set; } = ActionPhase.Idle;
    // <summary>지금 진행 중인 행동의 진입프레임</summary>
    public int FrameInPhase { get; private set; }

    // <summary> Idle일 때만 이동 가능 선후딜/경직/다운 중에는 이동 불가능</summary>
    public bool CanMove => Phase == ActionPhase.Idle;
    /// <summary>Idle일 때만 새 행동을 받는다. Recovery 중 재입력 무시 검증은 이 값으로 한다 (§11-4)</summary>
    public bool CanAcceptNewAction => Phase == ActionPhase.Idle;

    /// <summary>행동이 시작되는 순간 (Startup 진입)</summary>
    public event Action<ActionData> OnActionBegin;

    /// <summary>판정이 켜지는 순간 (Active 진입). 스킬 이펙트 타점으로 주로 쓰인다</summary>
    public event Action<ActionData> OnActionActiveStart;

    /// <summary>회수까지 끝나고 Idle로 돌아가는 순간</summary>
    public event Action<ActionData> OnActionEnd;

    // 호출: ActionExecutor.ExecuteTick. 받음: 시작할 ActionData. 전달: OnActionBegin 이벤트 → RobotView.PlayAction이 애니메이션 재생
    public void Begin(ActionData action)
    {
        if (action == null) return;
        if (!CanAcceptNewAction) return;   // Idle이 아니면 무시 — 후딜 중 재입력 씹힘은 여기서 보장됨

        CurrentAction = action;
        FrameInPhase = 0;
        Phase = ActionPhase.Startup;
        OnActionBegin?.Invoke(action);
    }

    /// <summary>CombatClock.CombatTick()마다 정확히 1번 호출된다. 프레임을 직접 세지, Time.deltaTime을 곱하지 않는다</summary>
    // 호출: ActionExecutor.ExecuteTick (틱마다 1회). 구간이 바뀔 때 OnActionActiveStart / OnActionEnd 이벤트를 발생시킨다
    public void Advance()
    {
        if (CurrentAction == null) return;

        FrameInPhase++;

        switch (Phase)
        {
            case ActionPhase.Startup:
                if (FrameInPhase >= CurrentAction.StartupFrames)
                {
                    Phase = ActionPhase.Active;
                    FrameInPhase = 0;
                    OnActionActiveStart?.Invoke(CurrentAction);
                }
                break;

            case ActionPhase.Active:
                if (FrameInPhase >= CurrentAction.ActiveFrames)
                {
                    Phase = ActionPhase.Recovery;
                    FrameInPhase = 0;
                }
                break;

            case ActionPhase.Recovery:
                if (FrameInPhase >= CurrentAction.RecoveryFrames)
                {
                    ActionData finished = CurrentAction;
                    Phase = ActionPhase.Idle;
                    CurrentAction = null;
                    FrameInPhase = 0;
                    OnActionEnd?.Invoke(finished);
                }
                break;
        }
    }
}
