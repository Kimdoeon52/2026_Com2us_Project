using System;
using UnityEngine;

/// <summary>
/// 전투 로직을 모니터 주사율과 분리해 1/60초마다 정확히 한 번 돌리는 시계 (CLAUDE.md §3, §11-3).
/// 프레임 카운트, 판정, 넉백, 경직도 감소는 전부 OnCombatTick 구독자에서만 돈다.
/// 렌더·보간·UI는 이 바깥(Update)에서 주사율대로 돌아도 된다.
/// </summary>
public class CombatClock : MonoBehaviour
{
    // 1틱의 길이(초). const로 선언해서 컴파일 타임에 고정된 값으로 박히고, 다른 스크립트(FrameStepper,
    // RobotView.AdvanceOneTick 등)에서도 "1틱이 몇 초인지"를 이 상수 하나로 통일해서 참조한다.
    // 여기저기서 1f/60f를 직접 타이핑하면, 나중에 프레임 레이트 기준을 바꿀 때 전부 찾아 고쳐야 한다
    public const float TICK = 1f / 60f;

    // 프레임 드랍(예: 순간적으로 0.5초가 걸린 렌더 프레임)이 나면 accumulator에 밀린 시간이 잔뜩 쌓여서
    // 한 Update()에서 틱을 수십 번 몰아 돌리게 되는데, 그러면 그 틱들을 처리하느라 다음 렌더 프레임도
    // 오래 걸리고, 그게 또 다음 accumulator를 더 불리는 악순환(death spiral)에 빠질 수 있다.
    // 한 Update당 최대 5틱까지만 처리하도록 상한을 둬서 이 악순환을 막는다 — 대신 심하게 밀린 경우엔
    // 전투 로직이 살짝 느려진 것처럼 보일 수 있지만, 게임이 아예 멈추는 것보단 낫다
    private const int MAX_TICKS_PER_UPDATE = 5;

    // 싱글톤 — 씬에 로봇이 몇 마리든 CombatClock은 딱 하나만 있으면 되므로(모두가 같은 틱을 공유해야
    // 동기화가 맞음), static 인스턴스로 관리한다
    private static CombatClock instance;

    /// <summary>씬에 없으면 만들어서 반환한다. 구독할 때만 사용</summary>
    // "없으면 만든다"까지 여기서 처리하는 이유: PlayerRobotBootstrap 같은 소비자 입장에서
    // "CombatClock이 씬에 미리 배치돼 있어야 한다"는 전제 조건을 신경 안 써도 되게 하려는 편의 기능
    public static CombatClock Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<CombatClock>();
                if (instance == null)
                    instance = new GameObject("CombatClock").AddComponent<CombatClock>();
            }
            return instance;
        }
    }

    /// <summary>만들지 않고 현재 인스턴스만 반환. 종료 중 구독 해제용</summary>
    // Instance와 별도로 이 프로퍼티를 둔 이유: 오브젝트가 파괴되는 시점(OnDisable 등)에 Instance를
    // 부르면, 이미 없어진 CombatClock을 "없으니 새로 만들어야지"하고 엉뚱하게 새로 생성해버릴 수 있다.
    // 구독 해제할 땐 "진짜 있으면 있는 거 쓰고, 없으면 그냥 포기"가 맞는 동작이라 null도 그대로 반환하는 이 프로퍼티를 따로 둔다
    public static CombatClock Existing => instance;

    /// <summary>1/60초마다 1회 발생. 로직은 전부 여기서 돈다</summary>
    public event Action OnCombatTick;

    // Update()의 Time.deltaTime을 여기 계속 쌓아뒀다가, TICK(1/60초)을 넘길 때마다 그만큼 덜어내며 틱을 돈다.
    // 이 방식(고정 타임스텝 누산기)을 쓰는 이유는 §3에 있는 그대로: 프레임 카운터를 Update()에서
    // 그냥 +1 하면 모니터 주사율이 곧 게임 속도가 되어버리기 때문 — 144Hz 모니터에서 게임이 더 빨리 도는 걸 막기 위함
    private float accumulator;

    // 도메인 리로드(에디터에서 스크립트를 다시 컴파일하거나, Enter Play Mode Options에서
    // "Reload Domain"을 꺼둔 상태로 플레이를 다시 시작하는 경우)를 하면 static 필드가 이전 값을
    // 그대로 들고 있을 수 있다. 그러면 이미 파괴된 예전 CombatClock을 가리키는 instance가 남아서
    // "씬을 다시 시작했는데 이상하게 새 CombatClock이 안 만들어짐" 같은 버그가 생긴다.
    // SubsystemRegistration 시점(플레이 시작 아주 초반)에 강제로 null로 되돌려서 이 문제를 막는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;

    // 호출: Unity 매 렌더 프레임. deltaTime을 누적해 1/60초가 찰 때마다 Tick()을 호출한다 (주사율과 분리)
    private void Update()
    {
        // timeScale = 0이면 deltaTime도 0이라 자연히 정지한다 (FrameStepper가 이 성질을 이용)
        accumulator += Time.deltaTime;

        int ticks = 0;
        // while로 도는 이유: 한 렌더 프레임(Update 한 번) 사이에 1/60초보다 긴 시간이 지나있으면
        // (예: 렌더가 느려서 이번 Update까지 1/30초가 걸렸다면) 그 사이에 논리적으로는 틱이 2번
        // 지났어야 하므로, 쌓인 만큼 여러 번 Tick()을 돌려서 따라잡는다. MAX_TICKS_PER_UPDATE로
        // 무한정 따라잡지 않게 상한을 걸어둔 이유는 위 필드 설명 참고
        while (accumulator >= TICK && ticks < MAX_TICKS_PER_UPDATE)
        {
            Tick();            // 프레임 +1, 판정, 넉백, 경직도 — 전부 여기
            accumulator -= TICK;
            ticks++;
        }

        // 상한에 걸려서 다 못 따라잡은 나머지 시간이 accumulator에 계속 쌓이는 걸 막는다.
        // 안 이렇게 하면, 한 번 심하게 밀린 뒤로 계속 TICK을 넘는 값이 남아 있어서 매 Update마다
        // MAX_TICKS_PER_UPDATE만큼 계속 몰아 돌게 되는(즉, 못 따라잡은 시간을 영영 못 버리는) 문제가 생긴다
        if (accumulator > TICK) accumulator = TICK;
    }

    /// <summary>정확히 1틱 전진. FrameStepper의 F3가 수동으로 호출한다</summary>
    // 호출: Update(자동) / FrameStepper F3(수동, 예정). 전달: OnCombatTick을 구독한 함수를 전부 호출
    // 굳이 이 함수를 public으로 따로 뽑아둔 이유: 자동(Update)이든 수동(F3)이든 "틱 1개를 돌린다"는
    // 동작 자체는 완전히 똑같아야 한다. 만약 Update() 안에 로직을 직접 박아뒀다면 FrameStepper가
    // 똑같은 코드를 복붙해야 했을 것이고, 나중에 틱 처리 방식이 바뀌면 두 군데를 다 고쳐야 하는 위험이 생긴다
    public void Tick() => OnCombatTick?.Invoke();
}
