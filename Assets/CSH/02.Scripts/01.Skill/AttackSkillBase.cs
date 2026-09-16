using UnityEngine;

public class AttackSkillBase : SkillBase
{
    [SerializeField]
    private float defenseDuration = 0f;

    [SerializeField]
    [Tooltip("스킬의 시작 프레임")]
    [Min(0)]
    private int startupFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 활성 프레임")]
    [Min(0)]
    private int activeFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 회수 프레임")]
    [Min(0)]
    private int recoveryFrames = 0;

    public float DefenseDuration => defenseDuration;
    public int StartupFrames => startupFrames;
    public int ActiveFrames => activeFrames;
    public int RecoveryFrames => recoveryFrames;

    public override void UseSkill()
    {
        base.UseSkill();
    }
}