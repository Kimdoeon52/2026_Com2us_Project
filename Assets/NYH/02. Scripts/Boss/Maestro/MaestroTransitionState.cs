using System.Threading;
using RealSteel.Boss.Core;
using RealSteel.Boss.FSM;

/// <summary>
/// 마에스트로 악장 전환 중 KKH FSM을 세워 두는 빈 상태 (보스3_월광_기획 0.4).
/// 이 상태에 있는 동안 KKH 딜타임 → 패턴 루프가 돌지 않는다. 전환 진행(무너짐 → 다시 쌓기 + HP 회복)은
/// MaestroBossController가 전투 틱(CombatClock)에서 세고, 끝나면 컨트롤러가 딜타임 상태로 바꾼다.
/// </summary>
public class MaestroTransitionState : BossStateBase
{
    public override string StateName => "MaestroTransitionState (악장 전환)";

    public MaestroTransitionState(BossControllerBase boss, CancellationToken token) : base(boss, token) { }
}
