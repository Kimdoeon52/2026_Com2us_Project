using UnityEngine;


[CreateAssetMenu(menuName = "CSH/Skill/Rapid Skill", fileName = "Rapid Skill")]
public class RapidData : HoldSkillData
{
    public override SkillRunner CreateRunner() => new Runner();

    private sealed class Runner : HoldSkillRunner
    {
        // 활성 구간에 들어온 뒤의 틱 수로 센다 (PhaseFrame: 활성 첫 틱 = 1)
        protected override bool IsMaxHoldReached()
            => Hold.MaxHoldFrames > 0 && Phase == SkillPhase.Active && PhaseFrame >= Hold.MaxHoldFrames;
    }
}
