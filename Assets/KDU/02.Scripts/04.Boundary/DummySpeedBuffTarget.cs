using UnityEngine;

// 이동속도 버프 구현 전 진행용 더미. 로그만 남긴다
public class DummySpeedBuffTarget : MonoBehaviour, ISpeedBuffTarget
{
    public void ApplySpeedBuff(float multiplier, int durationTicks, bool keepRunning)
    {
        Debug.Log($"[DummySpeedBuff] {name}: x{multiplier} {durationTicks}틱 달리기유지={keepRunning}");
    }
}
