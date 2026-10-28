using UnityEngine;

/// <summary>
/// 플레이어의 BoxCollider2D를 지금 행동의 Hurt박스에 맞추는 다리 (2026-10-28, 보스전 KKH 연동).
///
/// KKH 보스는 Physics2D(OverlapCircle·트리거 총알) + "Player" 태그로 플레이어를 찾는다. 콜라이더를 손으로 맞춰두면
/// 웅크리기·점프·회피처럼 Hurt박스가 바뀌는 행동을 못 따라가서, 매 프레임 Hurt박스를 읽어 콜라이더에 옮긴다.
/// → Hurt박스 데이터 하나만 고치면 우리 판정과 보스 판정이 같이 따라온다. 패턴마다 콜라이더를 넣을 필요 없음.
///
/// 규칙:
///   · Hurt박스가 여러 개면 전부 감싸는 사각형 하나로 (BoxCollider2D 하나라서)
///   · Hurt박스가 없거나 회피 무적(HitDetection.IsInvincibleNow) 중이면 콜라이더를 끈다 — 보스 공격이 그냥 지나간다
///   · 죽으면 끈다
/// Player 오브젝트에만 붙인다(적 로봇엔 필요 없음).
/// </summary>
[RequireComponent(typeof(ActionExecutor))]
public class HurtboxColliderSync : MonoBehaviour
{
    [Tooltip("Hurt박스에 맞출 콜라이더. 비우면 같은 오브젝트의 BoxCollider2D를 쓰고, 없으면 만든다")]
    [SerializeField] private BoxCollider2D target;

    [Tooltip("켜면 시작할 때 이 오브젝트 태그를 \"Player\"로 바꾼다 — KKH 보스가 CompareTag(\"Player\")로 찾기 때문")]
    [SerializeField] private bool applyPlayerTag = true;

    private ActionExecutor executor;
    private RobotMover mover;

    private void Awake()
    {
        executor = GetComponent<ActionExecutor>();
        mover = GetComponent<RobotMover>();
        if (target == null) target = GetComponent<BoxCollider2D>();
        if (target == null) target = gameObject.AddComponent<BoxCollider2D>();
        if (applyPlayerTag && !CompareTag("Player")) gameObject.tag = "Player";
    }

    // LateUpdate인 이유: 전투 틱(CombatClock.Update)이 이번 프레임 행동·위치를 정한 뒤에 맞춰야 한 틱 늦지 않는다
    private void LateUpdate()
    {
        ActionState state = executor.State;
        if (state == null || state.IsDead || HitDetection.IsInvincibleNow(executor))
        {
            target.enabled = false;
            return;
        }

        bool facingRight = mover == null || mover.FacingRight;
        bool any = false;
        Rect union = default;
        foreach (var box in state.GetActiveBoxes())
        {
            if (box.type != BoxType.Hurt) continue;
            Rect world = BoxResolver.ToWorldRect(transform.position, box.rect, facingRight);
            if (!any) { union = world; any = true; }
            else union = Rect.MinMaxRect(Mathf.Min(union.xMin, world.xMin), Mathf.Min(union.yMin, world.yMin),
                                         Mathf.Max(union.xMax, world.xMax), Mathf.Max(union.yMax, world.yMax));
        }

        target.enabled = any;
        if (!any) return;

        // 콜라이더 값은 로컬 기준이라 월드 사각형을 되돌린다 (스케일·반전이 있어도 맞게)
        Vector3 scale = transform.lossyScale;
        target.offset = transform.InverseTransformPoint(union.center);
        target.size = new Vector2(union.width / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                                  union.height / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
    }
}
