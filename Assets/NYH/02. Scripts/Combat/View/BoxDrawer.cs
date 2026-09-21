using UnityEngine;

/// <summary>
/// 지금 이 캐릭터에게 활성화된 판정 박스를 Scene 뷰에 그려서 눈으로 확인할 수 있게 하는 디버그 컴포넌트.
/// OnDrawGizmos로 그리는 내용은 유니티가 빌드할 때 자동으로 빠지기 때문에, 실제 게임 실행 파일에는
/// 영향을 주지 않는다 — 개발 중 Scene 뷰에서만 보이는 보조 그림이라고 생각하면 된다.
/// </summary>
public class BoxDrawer : MonoBehaviour
{
    // 박스 종류별 표시 색. static readonly로 선언해서 프레임마다 새로 만들지 않고 하나씩만 재사용한다
    private static readonly Color HitColor = Color.red;               // 공격이 닿는 범위 — 빨강
    private static readonly Color HurtColor = new Color(0.4f, 0.8f, 1f); // 내가 맞는 범위 — 하늘색
    private static readonly Color PushColor = Color.green;            // 몸통 충돌 범위 — 초록

    // 지금 이 로봇이 뭘 하고 있는지 아는 상태 객체. 여기서 GetActiveBoxes()를 불러서 "지금 뭘 그려야 하는지" 얻는다
    private ActionState state;
    // 지금 어느 방향을 보고 있는지 알아야 좌우 반전을 적용한 위치에 박스를 그릴 수 있다 (BoxResolver로 넘겨줄 값)
    private RobotMover mover;

    // 호출: PlayerRobotBootstrap.Awake(플레이 모드 진입 시). 그 전(에디터에서 씬만 열어본 상태)에는
    // 이 함수가 아직 안 불린 상태라 state가 null인 채로 OnDrawGizmos가 먼저 돌 수 있다 — 그래서 거기서 null 체크를 한다
    public void Init(ActionState state, RobotMover mover)
    {
        this.state = state;
        this.mover = mover;
    }

    // 유니티가 Scene 뷰를 다시 그릴 때마다 자동으로 호출해주는 콜백. 여기 안에서는 "그리기"만 해야 하고
    // 게임 상태를 바꾸는 로직을 넣으면 안 된다 (호출 타이밍이 게임 로직과 무관하게 에디터 마음대로 결정되기 때문)
    private void OnDrawGizmos()
    {
        // 플레이 모드 시작 전이라 Init이 아직 안 불렸으면 state가 비어있다 — 그릴 데이터가 없으니 조용히 리턴
        if (state == null) return;

        // mover가 아직 배선 전(null)이면 일단 오른쪽을 본다고 가정한다 — 최소한 뭔가는 그려지게 하기 위한 안전한 기본값
        bool facingRight = mover == null || mover.FacingRight;

        // 지금 활성화된 박스 전부를 순서대로 그린다. 행동 중이면 그 행동의 박스, 대기·이동 중이면
        // ActionState.IdleAction의 박스로 자동 대체된다(GetActiveBoxes 내부에서 처리됨).
        // HitDetection도 나중에 이 GetActiveBoxes()를 똑같이 부르게 만들 것이므로,
        // "화면에 보이는 박스 = 실제로 판정에 쓰이는 박스"가 항상 일치하게 된다 (따로 관리하면 표시만 하고 실제로는 다른 값을 판정하는 거짓말 버그가 생김)
        foreach (var box in state.GetActiveBoxes())
        {
            // FrameBox에 저장된 로컬 좌표를, 지금 위치·방향 기준 월드 좌표로 바꾼다 — 이 계산은 반드시 BoxResolver 하나만 거친다
            Rect worldRect = BoxResolver.ToWorldRect(transform.position, box.rect, facingRight);
            Gizmos.color = ColorFor(box.type);
            // z를 0.01로 살짝 띄운 이유: 이 게임은 2D라 z가 실제로 안 쓰이는데, 두께가 완전히 0인 큐브는
            // 일부 환경에서 와이어프레임이 제대로 안 그려질 수 있어서 아주 살짝 두께를 준 것뿐 (판정에는 영향 없음)
            Gizmos.DrawWireCube(worldRect.center, new Vector3(worldRect.width, worldRect.height, 0.01f));
        }
    }

    // 박스 타입 → 색 매핑을 한곳에 모아둔 함수. ActionDataEditor에도 같은 색 규칙이 따로 있는데,
    // 두 에디터/런타임 도구가 서로 다른 색을 쓰면 "Scene 뷰에서 편집할 때 본 색"과 "플레이 중 본 색"이
    // 달라 보여서 혼란스러우니, 값을 바꿀 땐 두 곳 다 같이 맞춰야 한다
    private static Color ColorFor(BoxType type)
    {
        switch (type)
        {
            case BoxType.Hit: return HitColor;
            case BoxType.Hurt: return HurtColor;
            case BoxType.Push: return PushColor;
            default: return Color.white; // BoxType이 3종류뿐이라 이론상 여기 안 옴 — 방어적으로만 넣어둠
        }
    }
}
