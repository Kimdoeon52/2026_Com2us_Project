using UnityEngine;

public class DefenseSkillBase : SkillBase
{
    [SerializeField]
    [Tooltip("스킬의 선딜레이 프레임")]
    [Min(0)]
    private int startupFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 지속 프레임")]
    [Min(0)]
    private int activeFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 후딜레이 프레임")]
    [Min(0)]
    private int recoveryFrames = 0;

    public int StartupFrames => startupFrames;
    public int ActiveFrames => activeFrames;
    public int RecoveryFrames => recoveryFrames;

    public override void UseSkill()
    {
        base.UseSkill();
    }
}