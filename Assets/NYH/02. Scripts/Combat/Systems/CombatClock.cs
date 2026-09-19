using System;
using UnityEngine;

/// <summary>
/// 전투 로직을 모니터 주사율과 분리해 1/60초마다 정확히 한 번 돌리는 시계 (CLAUDE.md §3, §11-3).
/// 프레임 카운트, 판정, 넉백, 경직도 감소는 전부 OnCombatTick 구독자에서만 돈다.
/// 렌더·보간·UI는 이 바깥(Update)에서 주사율대로 돌아도 된다.
/// </summary>
public class CombatClock : MonoBehaviour
{
    public const float TICK = 1f / 60f;

    // 프레임 드랍 시 틱이 한꺼번에 몰려 더 느려지는 악순환을 막는 상한
    private const int MAX_TICKS_PER_UPDATE = 5;

    private static CombatClock instance;

    /// <summary>씬에 없으면 만들어서 반환한다. 구독할 때만 사용</summary>
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
    public static CombatClock Existing => instance;

    /// <summary>1/60초마다 1회 발생. 로직은 전부 여기서 돈다</summary>
    public event Action OnCombatTick;

    private float accumulator;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;

    // 호출: Unity 매 렌더 프레임. deltaTime을 누적해 1/60초가 찰 때마다 Tick()을 호출한다 (주사율과 분리)
    private void Update()
    {
        // timeScale = 0이면 deltaTime도 0이라 자연히 정지한다 (FrameStepper가 이 성질을 이용)
        accumulator += Time.deltaTime;

        int ticks = 0;
        while (accumulator >= TICK && ticks < MAX_TICKS_PER_UPDATE)
        {
            Tick();
            accumulator -= TICK;
            ticks++;
        }

        if (accumulator > TICK) accumulator = TICK;
    }

    /// <summary>정확히 1틱 전진. FrameStepper의 F3가 수동으로 호출한다</summary>
    // 호출: Update(자동) / FrameStepper F3(수동, 예정). 전달: OnCombatTick을 구독한 함수를 전부 호출
    public void Tick() => OnCombatTick?.Invoke();
}
