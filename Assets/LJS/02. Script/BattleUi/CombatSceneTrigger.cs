using UnityEngine;

/// <summary>
/// 전투 씬 전용 시간 상태 트리거.
/// 이 컴포넌트가 활성화된 동안 TimeSystemManager를 Combat 상태로 유지하고,
/// 씬을 벗어나 비활성화/파괴될 때 자동으로 전투 상태를 해제한다.
/// </summary>
public class CombatSceneTrigger : MonoBehaviour
{
    private bool isCombatActivated = false;

    private void OnEnable()
    {
        ActivateCombat();
    }

    private void Start()
    {
        // 에디터에서 전투 씬만 단독 실행할 때 Awake 실행 순서로 OnEnable에서 놓친 경우 보완
        if (!isCombatActivated)
        {
            ActivateCombat();
        }
    }

    private void OnDisable()
    {
        DeactivateCombat();
    }

    private void ActivateCombat()
    {
        if (TimeSystemManager.Instance != null)
        {
            TimeSystemManager.Instance.SetCombat(true);
            isCombatActivated = true;
        }
    }

    private void DeactivateCombat()
    {
        if (isCombatActivated && TimeSystemManager.Instance != null)
        {
            TimeSystemManager.Instance.SetCombat(false);
        }
        isCombatActivated = false;
    }
}
