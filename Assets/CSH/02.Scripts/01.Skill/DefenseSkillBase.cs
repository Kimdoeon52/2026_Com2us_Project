using UnityEngine;

public class DefenseSkillBase : SkillBase
{

    public int StartupFrames => startupFrames;
    public int ActiveFrames => activeFrames;
    public int RecoveryFrames => recoveryFrames;

    public override void UseSkill()
    {
        base.UseSkill();
    }
}