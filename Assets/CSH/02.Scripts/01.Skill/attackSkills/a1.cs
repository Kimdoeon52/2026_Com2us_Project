using UnityEngine;

public class a1 : AttackSkillBase
{
    public Bounds GetCurrentHurtBox() => hurtBox.GetWorldBounds(transform.position, facingDirection);
    public Bounds GetCurrentHitBox() => hitBox.GetWorldBounds(transform.position, facingDirection);
    
    protected override void Start()
    {
        base.Start();
    }


    public override void UseSkill()
    {
        base.UseSkill();
    }
    public override void BoxCast()
    {
        base.BoxCast();
        foreach (RaycastHit target in GetHitBoxHits())
        {
            A1Skill(target.collider.gameObject);
        }
    }

    public virtual void A1Skill(GameObject target)
    {
        Bounds myHit = GetCurrentHitBox();
        Bounds targetHurt = target.GetOrAddComponent<a1>().GetCurrentHurtBox();

        // 순수 수학적 AABB 교차 검사
        if (myHit.min.x <= targetHurt.max.x && myHit.max.x >= targetHurt.min.x &&
            myHit.min.y <= targetHurt.max.y && myHit.max.y >= targetHurt.min.y)
        {
            OnHitSuccess(target);
        }
    }


    private void OnHitSuccess(GameObject target)
    {
        Debug.Log($"{gameObject.name}의 공격이 {target.name}에 적중!");
        // 대미지, 히트스턴, 피격 이펙트 호출
    }

    public override void GizmosDraw()
    {
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Bounds currentHurt = hurtBox.GetWorldBounds(transform.position, facingDirection);
        Gizmos.DrawCube(currentHurt.center, currentHurt.size);
        

        // 공격 중일 때만 공격 박스 표시 (빨간색)
        if (Input.GetKey(KeyCode.Q))
        {
            Gizmos.color = new Color(1,0,0,0.2f);
            Bounds currentHit = hitBox.GetWorldBounds(transform.position, facingDirection);
            Gizmos.DrawCube(currentHit.center, currentHit.size);
        }
    }

}