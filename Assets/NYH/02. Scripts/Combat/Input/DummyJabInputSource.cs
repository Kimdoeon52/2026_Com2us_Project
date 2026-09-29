using UnityEngine;

/// <summary>
/// [임시/테스트용] 적이 제자리에서 같은 공격만 계속 날리게 하는 IInputSource 구현체.
/// 정식 AIInputSource(§11-9)가 나오기 전에 "적이 때리면 플레이어 체력이 닳는다"를 눈으로 보여주기 위한 용도.
/// (2026-09-29: 원래 잽을 반복시키던 용도였으나 잽 자체가 폐기됨 — 지금은 인스펙터에 꽂은 아무 ActionData나
/// 반복 실행하는 범용 더미로 쓴다. §15)
///
/// ActionExecutor는 Idle일 때만 GetDesiredAction()을 부르므로, 여기서 매번 같은 행동을 돌려주기만 해도
/// 행동 → 후딜 → Idle → 다시 행동이 자동으로 반복된다. 이동은 하지 않는다(GetMoveInput 항상 0).
///
/// 정식 AIInputSource가 생기면 이 파일은 통째로 삭제한다.
/// </summary>
public class DummyJabInputSource : MonoBehaviour, IInputSource
{
    [Tooltip("계속 반복할 행동. 테스트용 ActionData 아무거나 드래그")]
    [SerializeField] private ActionData repeatAction;

    [Tooltip("Idle로 돌아온 뒤 다음 행동까지 기다릴 틱 수(60틱 = 1초). 0이면 쉬지 않고 연타")]
    [SerializeField] private int idleTicksBetweenActions = 0;

    // Idle 상태에서 몇 틱째 기다리고 있는지. GetDesiredAction은 Idle일 때만 틱마다 불리므로 여기서 세면 된다
    private int idleTicks;

    // 호출: ActionExecutor.ExecuteTick (Idle일 때만). 대기 틱을 다 채웠으면 행동을 돌려주고 카운터를 초기화한다
    public ActionData GetDesiredAction()
    {
        if (repeatAction == null) return null; // 인스펙터 연결을 깜빡했으면 아무것도 안 함

        if (idleTicks < idleTicksBetweenActions)
        {
            idleTicks++;
            return null;
        }

        idleTicks = 0;
        return repeatAction;
    }

    // 호출: RobotMover / RobotView. 테스트용 허수아비라 움직이지 않는다
    public float GetMoveInput() => 0f;
}
