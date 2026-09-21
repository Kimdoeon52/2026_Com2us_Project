using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지금 이 캐릭터에게 활성화된 판정 박스를 그려서 눈으로 확인할 수 있게 하는 디버그 컴포넌트.
/// 두 가지 경로로 그린다:
///   1) OnDrawGizmos — Scene 뷰 전용(에디터에서 상시 표시). 빌드에는 자동으로 빠진다 (§9 1단계)
///   2) OnRenderObject(GL) — Game 뷰에도 보이게. F4(FrameStepper)로 토글한다 (§9 2단계)
/// Gizmos는 Game 뷰나 실제 빌드에는 절대 안 그려지기 때문에, "플레이하면서 바로 보고 싶다"는
/// 요구를 채우려면 GL로 따로 그려야 한다 — 이게 이 파일에 두 가지 그리기 경로가 있는 이유.
/// </summary>
public class BoxDrawer : MonoBehaviour
{
    /// <summary>F4를 누르면 FrameStepper가 이 값을 토글한다 — 모든 BoxDrawer 인스턴스가 공유하는 스위치라 static</summary>
    public static bool ShowInGameView { get; set; }

    // GL로 선을 그리려면 반드시 셰이더가 걸린 Material이 있어야 한다(SetPass 호출용).
    // "Hidden/Internal-Colored"는 유니티가 내장 제공하는, GL 즉시 모드 그리기 전용 셰이더라
    // 별도로 셰이더 에셋을 만들 필요 없이 이것만 써도 정점 색이 그대로 나온다
    private static Material lineMaterial;

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

    // 유니티가 각 카메라로 씬을 다 그린 뒤 호출해주는 콜백 — 여기서 그린 GL 내용은 Game 뷰(플레이 화면)에
    // 실제로 나타난다. OnDrawGizmos와 달리 Play 모드가 아니어도, 그리고 실제 빌드에서도 동작한다(§9 2단계)
    private void OnRenderObject()
    {
        // F4로 꺼져 있으면 아무것도 안 그린다 — 기본은 꺼둬서, 평소 플레이 화면에 박스가 항상 떠 있지 않게 함
        if (!ShowInGameView) return;
        if (state == null) return;

        EnsureLineMaterial();
        // SetPass(0)을 불러야 그 다음 GL 명령들이 이 머티리얼(셰이더)로 그려진다 — 안 부르면 색이 안 나오거나 아예 안 그려짐
        lineMaterial.SetPass(0);

        bool facingRight = mover == null || mover.FacingRight;

        GL.PushMatrix(); // 지금 그리기 전 GL 행렬 상태를 보관해뒀다가, 다 그리고 나서 PopMatrix로 되돌려 다른 렌더링에 영향 안 주게 함
        GL.Begin(GL.LINES);

        // OnDrawGizmos와 완전히 똑같은 데이터(state.GetActiveBoxes())를 그린다 — "Scene 뷰에서 본 것과
        // Game 뷰에서 본 것이 다르면" 이것도 결국 §4 "판정과 표시는 같은 데이터를 본다" 규칙 위반이 된다
        foreach (var box in state.GetActiveBoxes())
        {
            Rect worldRect = BoxResolver.ToWorldRect(transform.position, box.rect, facingRight);
            GL.Color(ColorFor(box.type));
            DrawRectOutline(worldRect);
        }

        GL.End();
        GL.PopMatrix();
    }

    // 셰이더가 걸린 Material을 딱 한 번만 만들어서 계속 재사용한다 (매 프레임 새로 만들면 낭비가 심함)
    private static void EnsureLineMaterial()
    {
        if (lineMaterial != null) return;

        var shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader);
        // HideAndDontSave: 이 머티리얼은 순전히 코드로만 쓰는 임시 객체라, 씬에 저장되거나
        // 하이어라키/프로젝트 창에 보일 필요가 없다 — 그런 것들을 막아주는 플래그
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;

        // ActionDataEditor에서 겪었던 것과 같은 문제(zTest) — GL로 그린 선도 기본 설정이면
        // 바닥이나 스프라이트 뒤에 가려질 수 있다. ZWrite를 꺼서 깊이버퍼에 기록 안 하고,
        // ZTest를 Always로 해서 항상 맨 위에 그려지게 강제한다
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
    }

    // 사각형 테두리 4변을 GL.LINES로 그린다 — GL.Begin(GL.LINES) 안에서 호출돼야 한다 (선분 하나당 정점 2개씩)
    private static void DrawRectOutline(Rect r)
    {
        Vector3 bl = new Vector3(r.xMin, r.yMin, 0f); // bottom-left
        Vector3 br = new Vector3(r.xMax, r.yMin, 0f); // bottom-right
        Vector3 tr = new Vector3(r.xMax, r.yMax, 0f); // top-right
        Vector3 tl = new Vector3(r.xMin, r.yMax, 0f); // top-left

        GL.Vertex(bl); GL.Vertex(br); // 아래
        GL.Vertex(br); GL.Vertex(tr); // 오른쪽
        GL.Vertex(tr); GL.Vertex(tl); // 위
        GL.Vertex(tl); GL.Vertex(bl); // 왼쪽
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
