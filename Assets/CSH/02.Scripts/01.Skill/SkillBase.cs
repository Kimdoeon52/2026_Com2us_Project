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
    protected ActionData actionData;

    protected ActionExecutor actionExecutor;

    [SerializeField]
    [Tooltip("스킬의 선딜레이 프레임")]
    [Min(0)]
    protected int startupFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 지속 프레임")]
    [Min(0)]
    protected int activeFrames = 0;
    [SerializeField]
    [Tooltip("스킬의 후딜레이 프레임")]
    [Min(0)]
    protected int recoveryFrames = 0;

    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;
    public int StartupFrames => startupFrames;
    public int ActiveFrames => activeFrames;
    public int RecoveryFrames => recoveryFrames;

    protected virtual void Start()
    {
               actionExecutor = GetComponent<ActionExecutor>();
        if (actionExecutor == null)
        {
            Debug.LogError("ActionExecutor component not found on the GameObject.");
        }
    }

    public virtual void UseSkill()
    {
        ApplySkill();
        BoxCast();
    }
    public virtual void ApplySkill()
    {

    }

    public virtual void BoxCast()
    {

    }

    public virtual void GizmosDraw()
    {
    }

#if UNITY_EDITOR

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UseSkill();
        }
    }

    private void OnDrawGizmos()
    {
        GizmosDraw();
    }
#endif
}
