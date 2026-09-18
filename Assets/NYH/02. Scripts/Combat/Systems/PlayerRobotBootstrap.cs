using UnityEngine;

/// <summary>
/// [임시/테스트용] 플레이어 오브젝트에 붙은 컴포넌트들을 서로 연결하고,
/// CombatClock이 없는 지금 ActionExecutor.ExecuteTick()을 대신 호출해주는 자리.
///
/// CombatClock이 완성되면:
///   - Update()의 ExecuteTick() 호출은 삭제하고 CombatClock.CombatTick()에서 호출하도록 옮긴다 (§3 원칙)
///   - Init() 와이어링만 여기 남아도 되고, 아니면 RobotAssembler(§11-8)가 대신할 수도 있음
///
/// 지금은 모니터 주사율에 프레임 카운트가 종속되는 문제(§3)를 그대로 안고 가는 임시 코드다.
/// "키 누르면 잽이 나가나" 확인용일 뿐, 이 상태로 밸런스 테스트를 하면 안 된다.
/// </summary>
public class PlayerRobotBootstrap : MonoBehaviour
{
    private ActionExecutor executor;

    private void Awake()
    {
        var input = GetComponent<PlayerInputSource>();
        executor = GetComponent<ActionExecutor>();

        executor.Init(input);
        GetComponent<RobotMover>().Init(input);
        GetComponent<RobotView>()?.Init(executor.State);
    }

    private void Update()
    {
        // TODO: CombatClock 완성되면 이 줄을 지우고 CombatTick()에서 호출하도록 옮길 것
        executor.ExecuteTick();
    }
}
