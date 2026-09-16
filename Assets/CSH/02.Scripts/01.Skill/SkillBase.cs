using UnityEngine;

/// <summary>
/// 스킬의 기본구조 클래스
/// </summary>
public class SkillBase : MonoBehaviour
{
    [SerializeField]// 스킬 데이터
    private SkillData skillData;

    public SkillData SkillData => skillData;

    public virtual void UseSkill()
    {

    }
}
