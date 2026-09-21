using UnityEngine;

/// <summary>
/// 판정 박스를 Scene 뷰에 그리는 디버그 도구 (§9 1단계 — OnDrawGizmos, 빌드에는 안 들어감).
/// ActionData.GetActiveBoxes를 HitDetection과 똑같이 호출한다 — 표시기가 별도 데이터를 보면
/// "거짓말하는 표시기"가 된다 (§4 "판정과 표시는 같은 데이터를 본다").
/// </summary>
public class BoxDrawer : MonoBehaviour
{
    private static readonly Color HitColor = Color.red;
    private static readonly Color HurtColor = new Color(0.4f, 0.8f, 1f);
    private static readonly Color PushColor = Color.green;

    private ActionState state;
    private RobotMover mover;

    // 호출: PlayerRobotBootstrap.Awake. 받음: state(현재 행동·프레임 조회용), mover(좌우 반전 판정의 단일 기준)
    public void Init(ActionState state, RobotMover mover)
    {
        this.state = state;
        this.mover = mover;
    }

    private void OnDrawGizmos()
    {
        if (state == null) return;

        bool facingRight = mover == null || mover.FacingRight;

        // 행동 중이든 대기·이동 중이든 상관없이 항상 같은 함수를 본다 (§4) — 대기 중엔 ActionState.IdleAction의 박스로 대체됨
        foreach (var box in state.GetActiveBoxes())
        {
            Rect worldRect = BoxResolver.ToWorldRect(transform.position, box.rect, facingRight);
            Gizmos.color = ColorFor(box.type);
            Gizmos.DrawWireCube(worldRect.center, new Vector3(worldRect.width, worldRect.height, 0.01f));
        }
    }

    private static Color ColorFor(BoxType type)
    {
        switch (type)
        {
            case BoxType.Hit: return HitColor;
            case BoxType.Hurt: return HurtColor;
            case BoxType.Push: return PushColor;
            default: return Color.white;
        }
    }
}
