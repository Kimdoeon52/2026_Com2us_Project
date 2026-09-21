using UnityEngine;

// 미니게임 중 플레이어 이동·상호작용을 막는다.
// 미니게임이 플레이어를 직접 참조하지 않으려고 전역 플래그로 둔다
public static class ScrapInputGate
{
    private static int _blockCount;

    public static bool IsBlocked => _blockCount > 0;

    public static void Push() => _blockCount++;

    public static void Pop() => _blockCount = Mathf.Max(0, _blockCount - 1);

    // 도메인 리로드를 끈 상태에서도 플레이 진입 시 풀려 있어야 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _blockCount = 0;
}
