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
    /// 걷기·대기는 3구간(선딜→활성→후딜) 상태기계를 아예 안 거치므로 전용 ActionData가 없다.
    /// 그렇다고 그 동안 "맞을 수도 없고 몸통도 없는" 건 격투 게임으로서 말이 안 되므로,
    /// Hit박스 없이 Hurt/Push만 담은 전용 ActionData(예: Idle.asset)를 생성자에서 받아 저장해두고,
    /// GetActiveBoxes()가 CurrentAction이 없을 때 이걸 대신 반환하게 한다.
    /// </summary>
    public ActionData IdleAction { get; }

    // 생성자에서 idleAction을 받는 이유: ActionExecutor가 이 클래스를 만들 때(Init 시점) 인스펙터에
    // 연결해둔 idleAction 에셋을 넘겨주기 위함. 기본값 null을 허용해둔 건, 아직 Idle 에셋을 안 만들었거나
    // 테스트용으로 급히 ActionState만 단독으로 써보고 싶을 때 컴파일 에러 없이 넘어가게 하기 위해서다
    // (실제로 null이면 아래 GetActiveBoxes()가 빈 목록을 반환할 뿐, 터지지는 않는다)
    public ActionState(ActionData idleAction = null)
    {
        IdleAction = idleAction;
    }

    /// <summary>지금 진행 중인 행동. null이면 Idle.</summary>
    // private set인 이유: 행동을 시작/종료시키는 유일한 통로가 Begin()/Advance() 내부이길 원해서다.
    // 외부(ActionExecutor 등)가 이 값을 직접 대입해버리면 Phase/FrameInPhase/GlobalFrame과 어긋나서
    // "행동은 있는데 프레임 카운터는 0" 같은 불일치 상태가 생길 수 있다
    public ActionData CurrentAction { get; private set; }

    // <summary>지금 진행 중인 행동의 상태(Idle/Startup/Active/Recovery/...)</summary>
    // enum 하나로만 표현하는 이유: bool isAttacking, bool isGuarding처럼 플래그를 여러 개 두면
    // "공격 중이면서 동시에 가드 중"처럼 있어선 안 되는 조합이 코드상 가능해져 버그의 원인이 된다.
    // 캐릭터는 항상 이 값 하나로만 지금 상태가 결정된다
    public ActionPhase Phase { get; private set; } = ActionPhase.Idle;

    // <summary>지금 Phase에 진입한 뒤 몇 번째 프레임인지 (Phase가 바뀌면 0으로 리셋됨)</summary>
    // 이 값만으로는 "행동 전체에서 지금이 몇 번째 프레임인지"를 알 수 없다 — 선딜이 끝나고
    // 활성으로 넘어가는 순간 다시 0부터 세기 때문. 그래서 박스 판정용으로는 아래 GlobalFrame을 따로 둔다
    public int FrameInPhase { get; private set; }

    /// <summary>
    /// 이 행동의 전체 타임라인 기준 경과 프레임 (Begin 후 1틱째가 1). Phase가 바뀌어도 리셋되지 않는다.
    /// FrameBox.startFrame/endFrame이 이 값과 같은 기준이다 — GetActiveBoxes(GlobalFrame)로 조회한다 (§4).
    /// FrameInPhase와 별도로 이 값을 두는 이유: 허트박스는 선딜 중에도 존재해야 하는데(가만히 서서
    /// 주먹을 준비하는 동안에도 몸통은 맞을 수 있으니까), FrameInPhase만 있으면 "지금이 선딜 3프레임째인지
    /// 활성 3프레임째인지"를 구분할 수 없어서 박스 구간을 지정할 방법이 없다
    /// </summary>
    public int GlobalFrame { get; private set; }

    /// <summary>
    /// 이번 행동으로 상대를 이미 한 번 맞혔는지. 활성 구간이 여러 프레임(예: 스트레이트 4프레임)이면
    /// 그 프레임 내내 Hit박스와 Hurt박스가 계속 겹쳐 있을 수 있는데, HitDetection이 겹칠 때마다
    /// 매번 ProcessHit을 부르면 한 번의 공격이 여러 번 데미지를 주는 버그가 된다. 그래서 "이번
    /// 행동에서 이미 맞혔다"는 걸 여기 기록해두고, HitDetection은 이 값이 false일 때만 겹침을 검사한다.
    /// </summary>
    public bool HasHitThisAction { get; private set; }

    // <summary> Idle일 때만 이동 가능 선후딜/경직/다운 중에는 이동 불가능</summary>
    // 매 틱 새로 계산하지 않고 Phase만 보고 판단하는 이유: Phase가 이미 "지금 뭘 하고 있는지"의
    // 유일한 진실이므로, 여기서 별도 조건을 추가하면 Phase와 어긋날 여지가 생긴다
    public bool CanMove => Phase == ActionPhase.Idle;

    /// <summary>Idle일 때만 새 행동을 받는다. Recovery 중 재입력 무시 검증은 이 값으로 한다 (§11-4)</summary>
    public bool CanAcceptNewAction => Phase == ActionPhase.Idle;

    /// <summary>행동이 시작되는 순간 (Startup 진입)</summary>
    // RobotView가 이 이벤트를 구독해서 애니메이션 재생을 트리거한다 (§13, 스킬 연동도 동일 통로)
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

        // 새 행동을 시작하는 시점의 상태를 전부 초기화한다. 이 4줄이 한 세트로 같이 바뀌지 않으면
        // (예: CurrentAction만 바꾸고 GlobalFrame을 안 돌리면) 이전 행동의 프레임 카운트를 그대로
        // 물려받는 버그가 생기므로, 반드시 여기서 한꺼번에 세팅한다
        CurrentAction = action;
        FrameInPhase = 0;
        GlobalFrame = 0;
        HasHitThisAction = false; // 새 행동이 시작됐으니 "이미 맞혔음" 기록도 초기화 — 이번 공격은 아직 아무도 못 맞혔다
        Phase = ActionPhase.Startup;
        OnActionBegin?.Invoke(action);
    }

    /// <summary>
    /// 호출: HitDetection이 겹침을 감지해서 CombatDataHub.ProcessHit을 부른 직후.
    /// 이번 행동은 이걸로 끝 — 같은 활성 구간에서 또 겹쳐도 더는 데미지가 안 들어가게 막는다.
    /// </summary>
    public void MarkHit()
    {
        HasHitThisAction = true;
    }

    /// <summary>CombatClock.CombatTick()마다 정확히 1번 호출된다. 프레임을 직접 세지, Time.deltaTime을 곱하지 않는다</summary>
    // 호출: ActionExecutor.ExecuteTick (틱마다 1회). 구간이 바뀔 때 OnActionActiveStart / OnActionEnd 이벤트를 발생시킨다
    public void Advance()
    {
        // Idle 상태(행동 없음)면 셀 프레임 자체가 없으니 그냥 리턴 — 여기서 뭔가 세면 다음 Begin() 때
        // GlobalFrame이 0이 아닌 값에서 시작하는 등 꼬이게 된다
        if (CurrentAction == null) return;

        // 이번 틱이 이 행동의 몇 번째 프레임인지 두 카운터를 같이 올린다.
        // FrameInPhase는 "지금 Phase 안에서 몇 번째"(Phase 전환 판단용), GlobalFrame은 "행동 전체에서 몇 번째"(박스 판정용)
        FrameInPhase++;
        GlobalFrame++;

        switch (Phase)
        {
            case ActionPhase.Startup:
                // 데이터(ActionData.StartupFrames)에 정해둔 선딜 길이를 다 채웠으면 활성 구간으로 넘어간다.
                // ">="로 비교하는 이유: 혹시라도 한 틱에 여러 프레임이 밀려도(프레임 드랍 등) 안전하게 넘어가기 위함
                if (FrameInPhase >= CurrentAction.StartupFrames)
                {
                    Phase = ActionPhase.Active;
                    FrameInPhase = 0; // 새 Phase의 1프레임째부터 다시 세야 하므로 리셋 (GlobalFrame은 리셋 안 함!)
                    OnActionActiveStart?.Invoke(CurrentAction); // 이 순간이 보통 공격 판정/이펙트가 실제로 켜지는 타이밍
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
                    // Idle로 돌아가기 전에 "무슨 행동이 끝났는지"를 이벤트로 알려줘야 하는데,
                    // CurrentAction을 먼저 null로 만들어버리면 그 정보가 사라지므로 미리 변수에 빼둔다
                    ActionData finished = CurrentAction;
                    Phase = ActionPhase.Idle;
                    CurrentAction = null;
                    FrameInPhase = 0;
                    // GlobalFrame은 여기서 일부러 안 건드린다 — 다음 Begin()이 호출될 때 그쪽에서 0으로 리셋되고,
                    // 그 전까지(다음 행동이 없는 동안)는 이 값을 아무도 안 읽으므로 그냥 두어도 문제 없음
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
        // 행동 중이면 그 행동이 "지금 전체 타임라인 기준 몇 프레임째인지"(GlobalFrame)에 맞는 박스를 그대로 넘긴다
        if (CurrentAction != null)
            return CurrentAction.GetActiveBoxes(GlobalFrame);

        // 행동이 없으면(=Idle, 걷기 포함) IdleAction의 1번 프레임 박스를 대신 쓴다.
        // 굳이 GlobalFrame이 아니라 고정값 1을 넘기는 이유: 서 있는 자세는 프레임에 따라 박스가
        // 바뀔 이유가 없어서(팔을 뻗는 동작이 아니니까), Idle.asset에는 항상 프레임 1짜리 박스 하나만 등록해두면 된다.
        // IdleAction 자체가 없으면(아직 인스펙터에 안 넣었으면) 빈 배열을 줘서 "판정 없음"으로 안전하게 처리한다
        return IdleAction != null ? IdleAction.GetActiveBoxes(1) : Array.Empty<FrameBox>();
    }
}
