using UnityEngine;

public class AttackSkillBase : SkillBase
{

    protected Vector3 hitBox;
    protected Vector3 hurtBox;

    void Start()
    {
        if (hitBox == Vector3.zero)
        {
            hitBox = new Vector3(1f, 0.5f, 0.5f);
        }

        if (hurtBox == Vector3.zero)
        {
            hurtBox = new Vector3(0.9f, 0.5f, 0.5f);
        }
    }

    public override void BoxCast()
    {
        base.BoxCast();
        RaycastHit[] hit = Physics.BoxCastAll(transform.position, hitBox, Vector3.forward);        
        RaycastHit[] hurt = Physics.BoxCastAll(transform.position, hurtBox, Vector3.forward);

        foreach (RaycastHit h in hit)
        {
            Debug.Log($"Hit: {h.collider.name}");
        }
    }

    public override void GizmosDraw()
    {
        base.GizmosDraw();
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawCube(transform.position, hitBox);
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawCube(transform.position, hurtBox);
    }

}