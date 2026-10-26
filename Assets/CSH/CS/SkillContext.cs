using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ActionState가 "외부 구동 행동"을 진행 중일 때 판정 박스와 끊김(경직·사망)을 위임받는 쪽.
/// SkillDriver가 구현한다. ActionState(NYH)는 이 인터페이스만 알고 스킬 쪽 타입은 모른다.
/// </summary>
public interface IExternalActionDriver
{
    /// <summary>지금 켜져 있는 박스 (HitDetection/BoxDrawer/RobotMover가 ActionState.GetActiveBoxes로 읽는다)</summary>
    IEnumerable<FrameBox> GetActiveBoxes();

    /// <summary>ActionState.Interrupt(경직) 또는 Kill(사망)로 행동이 밖에서 끊겼다</summary>
    void OnExternalInterrupted();
}

/// <summary>
/// 러너가 로봇의 기능(쿨타임·실린더·위치·방향)에 접근하는 유일한 창구.
/// 러너가 ActionExecutor를 직접 뒤지지 않게 해서, 나중에 실행기가 바뀌어도 러너 코드는 그대로 두기 위함이다.
/// </summary>
public sealed class SkillContext
{
    public ActionExecutor Executor { get; }
    internal IExternalActionDriver Driver { get; }

    internal SkillContext(ActionExecutor executor, IExternalActionDriver driver)
    {
        Executor = executor;
        Driver = driver;
    }

    public ActionState State => Executor.State;
    public string FighterId => Executor.FighterId;
    public Transform Transform => Executor.transform;
    public RobotMover Mover => Executor.Mover;
    public bool FacingRight => Mover == null || Mover.FacingRight;

    /// <summary>
    /// 실린더 게이트. 탄이 충분하면 action 그대로, 모자라면 action.FallbackAction(일반판정), 그것도 안 되면 null.
    /// cost에는 실제로 깎을 탄 수(장비 보정 반영)가 나온다. 아직 깎지는 않는다.
    /// </summary>
    public ActionData GateCylinder(ActionData action, out int cost) => Executor.SkillGateCylinder(action, out cost);

    /// <summary>폴백 없이 action 자체를 지금 낼 수 있을 만큼 탄이 있는가 (홀드 중 D 강화 판정용)</summary>
    public bool CanAffordCylinder(ActionData action) => Executor.SkillCanAffordCylinder(action);

    /// <summary>비용 확정: 쿨타임 시작 + 탄 소모. 쿨타임 키는 action.CooldownKey</summary>
    public void ApplyCost(ActionData action, int cost) => Executor.SkillApplyCost(action, cost);

    /// <summary>쿨타임만 건다. scale = 1이면 정상, 0.5면 절반 (레이저 조기 취소 등), 0이면 안 건다</summary>
    public void StartCooldown(ActionData action, float scale) => Executor.SkillStartCooldown(action, scale);

    public bool IsOnCooldown(ActionData action) => Executor.IsOnCooldown(action);
}
