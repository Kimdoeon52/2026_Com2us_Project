using UnityEngine;

/// <summary>
/// [임시/테스트용] 적이 제자리에서 잽만 계속 날리게 하는 IInputSource 구현체.
/// 정식 AIInputSource(§11-9)가 나오기 전에 "적이 때리면 플레이어 체력이 닳는다"를 눈으로 보여주기 위한 용도.
///
/// ActionExecutor는 Idle일 때만 GetDesiredAction()을 부르므로, 여기서 매번 잽을 돌려주기만 해도
/// 잽 → 후딜 → Idle → 다시 잽이 자동으로 반복된다. 이동은 하지 않는다(GetMoveInput 항상 0).
///
/// 정식 AIInputSource가 생기면 이 파일은 통째로 삭제한다.
/// </summary>
public class DummyJabInputSource : MonoBehaviour, IInputSource
{
    [Tooltip("계속 날릴 행동. 프레임표 [기술] 이름과 같은 잽 에셋을 드래그")]
    [SerializeField] private ActionData jab;

    [Tooltip("Idle로 돌아온 뒤 다음 잽까지 기다릴 틱 수(60틱 = 1초). 0이면 쉬지 않고 연타")]
    [SerializeField] private int idleTicksBetweenJabs = 0;

    // Idle 상태에서 몇 틱째 기다리고 있는지. GetDesiredAction은 Idle일 때만 틱마다 불리므로 여기서 세면 된다
    private int idleTicks;

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 대기 틱을 다 채웠으면 잽을 돌려주고 카운터를 초기화한다
    public ActionData GetDesiredAction()
    {
        if (jab == null) return null; // 인스펙터 연결을 깜빡했으면 아무것도 안 함

        if (idleTicks < idleTicksBetweenJabs)
        {
            idleTicks++;
            return null;
        }

        idleTicks = 0;
        return jab;
    }

    // 호출: RobotMover / RobotView. 테스트용 허수아비라 움직이지 않는다
    public float GetMoveInput() => 0f;
}
