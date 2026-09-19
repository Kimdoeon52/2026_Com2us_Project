using UnityEngine;

/// <summary>
/// 플레이어 키보드 입력을 ActionData로 변환하는 IInputSource 구현체 (CLAUDE.md §8, §11-9).
/// ActionExecutor는 이 클래스가 무엇인지 몰라도 된다 — IInputSource 인터페이스로만 다룬다.
///
/// 키 배치는 기획서 494~511행 기준 (CLAUDE.md §8):
///   이동 ←→ / 잽 D / 스트레이트 Q / 훅 A / 어퍼컷 W / 백스핀 엘보우 S / 가드 C / 위닝 Space
/// </summary>
public class PlayerInputSource : MonoBehaviour, IInputSource
{
    // GetKeyDown은 누른 렌더 프레임에서만 true라, 그 프레임에 전투 틱이 없으면 입력이 유실된다.
    // 그래서 Update에서 잡아두고 다음 틱에서 소비한다. 2틱이 지나면 만료 — 후딜 중 재입력은 무시되어야 한다 (§11-4)
    private const float INPUT_BUFFER_SECONDS = CombatClock.TICK * 2f;

    [Header("행동 에셋 연결 (인스펙터에서 프레임표와 이름이 같은 .asset 드래그)")]
    [SerializeField] private ActionData jab;             // D
    [SerializeField] private ActionData straight;        // Q
    [SerializeField] private ActionData hook;            // A
    [SerializeField] private ActionData uppercut;        // W
    [SerializeField] private ActionData backspinElbow;   // S
    [SerializeField] private ActionData guard;           // C
    [SerializeField] private ActionData weaving;         // Space

    private ActionData pendingAction;
    private float pendingTime;

    // 호출: Unity 매 렌더 프레임. GetKeyDown은 누른 프레임에서만 true라, 틱이 없는 프레임의 입력이 사라지지 않게 pendingAction에 잡아 둔다
    private void Update()
    {
        ActionData pressed = ReadKeyDown();
        if (pressed == null) return;

        pendingAction = pressed;
        pendingTime = Time.time;
    }

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 반환: 잡아 둔 행동을 1회 돌려주고 비운다. 2틱이 지나 만료됐으면 null
    public ActionData GetDesiredAction()
    {
        // 주의 — 여기서 "지금 이 부위가 파괴됐는지" 같은 판정은 하지 않는다.
        // 그건 §6-1 규칙대로 RuntimeRobot.availableActions가 판단할 몫이다.
        // (RuntimeRobot 완성 전까지는 그냥 눌린 키 → ActionData 매핑만 한다)

        if (pendingAction == null) return null;

        ActionData action = pendingAction;
        pendingAction = null;

        bool isExpired = Time.time - pendingTime > INPUT_BUFFER_SECONDS;
        return isExpired ? null : action;
    }

    // 호출: RobotMover.Update(이동), RobotView.OnActionEnd(Walk/Idle 선택). 반환: -1(왼쪽) ~ 1(오른쪽)
    public float GetMoveInput()
    {
        if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
        if (Input.GetKey(KeyCode.RightArrow)) return 1f;

        return 0f;
    }

    // 호출: Update. 이번 프레임에 눌린 키를 인스펙터에 연결된 ActionData로 바꿔서 돌려준다
    private ActionData ReadKeyDown()
    {
        if (Input.GetKeyDown(KeyCode.D)) return jab;
        if (Input.GetKeyDown(KeyCode.Q)) return straight;
        if (Input.GetKeyDown(KeyCode.A)) return hook;
        if (Input.GetKeyDown(KeyCode.W)) return uppercut;
        if (Input.GetKeyDown(KeyCode.S)) return backspinElbow;
        if (Input.GetKeyDown(KeyCode.C)) return guard;
        if (Input.GetKeyDown(KeyCode.Space)) return weaving;

        return null;
    }
}
