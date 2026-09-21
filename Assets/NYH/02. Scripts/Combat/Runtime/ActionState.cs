using System;
using System.Collections.Generic;

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
    /// <summary>
    /// 대기·이동 중(=CurrentAction이 null)일 때 대신 쓸 허트/푸시박스 소스.
    /// 걷기는 3구간 상태기계를 안 거치므로 ActionData가 따로 없다 — 그렇다고 그 동안
    /// 맞을 수도 없고 몸통도 없는 건 말이 안 되므로, Hit박스 없이 Hurt/Push만 담은
    /// 전용 ActionData(예: Idle.asset)를 여기에 연결해서 GetActiveBoxes()가 항상 뭔가를 반환하게 한다.
    /// </summary>
    public ActionData IdleAction { get; }

    public ActionState(ActionData idleAction = null)
    {
        IdleAction = idleAction;
    }

    /// <summary>지금 진행 중인 행동. null이면 Idle.</summary>
    public ActionData CurrentAction { get; private set; }
    // <summary>지금 진행 중인 행동의 상태</summary>
    public ActionPhase Phase { get; private set; } = ActionPhase.Idle;
    // <summary>지금 진행 중인 행동의 진입프레임</summary>
    public int FrameInPhase { get; private set; }

    /// <summary>
    /// 이 행동의 전체 타임라인 기준 경과 프레임 (Begin 후 1틱째가 1). Phase가 바뀌어도 리셋되지 않는다.
    /// FrameBox.startFrame/endFrame이 이 값과 같은 기준이다 — GetActiveBoxes(GlobalFrame)로 조회한다 (§4).
    /// </summary>
    public int GlobalFrame { get; private set; }

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
        GlobalFrame = 0;
        Phase = ActionPhase.Startup;
        OnActionBegin?.Invoke(action);
    }

    /// <summary>CombatClock.CombatTick()마다 정확히 1번 호출된다. 프레임을 직접 세지, Time.deltaTime을 곱하지 않는다</summary>
    // 호출: ActionExecutor.ExecuteTick (틱마다 1회). 구간이 바뀔 때 OnActionActiveStart / OnActionEnd 이벤트를 발생시킨다
    public void Advance()
    {
        if (CurrentAction == null) return;

        FrameInPhase++;
        GlobalFrame++;

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

    /// <summary>
    /// 지금 이 순간 활성화된 판정 박스. BoxDrawer(표시)와 HitDetection(판정)이 둘 다 이 함수 하나만 보게 한다 (§4).
    /// 행동 중이면 그 행동의 현재 프레임 박스, 대기·이동 중이면 IdleAction의 박스(보통 프레임 1 고정 — 서 있는 자세라 프레임별로 안 바뀜)를 반환한다.
    /// </summary>
    public IEnumerable<FrameBox> GetActiveBoxes()
    {
        if (CurrentAction != null)
            return CurrentAction.GetActiveBoxes(GlobalFrame);

        return IdleAction != null ? IdleAction.GetActiveBoxes(1) : Array.Empty<FrameBox>();
    }
}
