using UnityEngine;

public class AttackSkillBase : SkillBase
{
    protected int facingDirection = 1; // 1: 우, -1: 좌

    protected CustomAABB hitBox;
    protected CustomAABB hurtBox;

    protected override void Start()
    {
        if (hitBox.size == Vector2.zero)
        {
            hitBox = new CustomAABB { size = new Vector2(1f, 0.5f) };
        }

        if (hurtBox.size == Vector2.zero)
        {
            hurtBox = new CustomAABB { size = new Vector2(0.9f, 0.5f) };
        }
    }

    public override void BoxCast()
    {
        base.BoxCast();
        RaycastHit[] hit = GetHitBoxHits();
        RaycastHit[] hurt = GetHurtBoxHits();

    }

    public virtual RaycastHit[] GetHitBoxHits()
    {
        return Physics.BoxCastAll(transform.position, hitBox.size, Vector3.forward);
    }

    public virtual RaycastHit[] GetHurtBoxHits()
    {
        return Physics.BoxCastAll(transform.position, hurtBox.size, Vector3.forward);
    }   

    public override void GizmosDraw()
    {
        base.GizmosDraw();
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawCube(transform.position, hitBox.size);
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawCube(transform.position, hurtBox.size);
    }

}