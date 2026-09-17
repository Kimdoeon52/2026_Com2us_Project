using UnityEngine;

/// <summary>
/// 스킬의 기본구조 클래스
/// </summary>
public class SkillBase : MonoBehaviour
{
    [SerializeField]// 스킬 데이터
    private SkillData skillData;

    public SkillData SkillData => skillData;


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

    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;
    public int StartupFrames => startupFrames;
    public int ActiveFrames => activeFrames;
    public int RecoveryFrames => recoveryFrames;


    public virtual void UseSkill()
    {

    }
}
