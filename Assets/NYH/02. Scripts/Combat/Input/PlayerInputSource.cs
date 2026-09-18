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
    [Header("행동 에셋 연결 (인스펙터에서 프레임표와 이름이 같은 .asset 드래그)")]
    [SerializeField] private ActionData jab;             // D
    [SerializeField] private ActionData straight;        // Q
    [SerializeField] private ActionData hook;            // A
    [SerializeField] private ActionData uppercut;        // W
    [SerializeField] private ActionData backspinElbow;   // S
    [SerializeField] private ActionData guard;           // C
    [SerializeField] private ActionData weaving;         // Space

    public ActionData GetDesiredAction()
    {
        // 주의 — 여기서 "지금 이 부위가 파괴됐는지" 같은 판정은 하지 않는다.
        // 그건 §6-1 규칙대로 RuntimeRobot.availableActions가 판단할 몫이다.
        // (RuntimeRobot 완성 전까지는 그냥 눌린 키 → ActionData 매핑만 한다)

        if (Input.GetKeyDown(KeyCode.D)) return jab;
        if (Input.GetKeyDown(KeyCode.Q)) return straight;
        if (Input.GetKeyDown(KeyCode.A)) return hook;
        if (Input.GetKeyDown(KeyCode.W)) return uppercut;
        if (Input.GetKeyDown(KeyCode.S)) return backspinElbow;
        if (Input.GetKeyDown(KeyCode.C)) return guard;
        if (Input.GetKeyDown(KeyCode.Space)) return weaving;

        return null;
    }

    public float GetMoveInput()
    {
        // TODO: 좌우 화살표 입력을 -1 ~ 1로 반환
        // Input.GetKey(KeyCode.LeftArrow) / RightArrow 조합으로 처리
        
        if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
        if (Input.GetKey(KeyCode.RightArrow)) return 1f;

        return 0f;
    }
}
