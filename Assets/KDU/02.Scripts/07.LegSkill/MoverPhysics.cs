using UnityEngine;

// RobotMover 점프 물리 환산
public static class MoverPhysics
{
    // 정점이 정확히 height가 되는 초기 상승 속도 (틱 적분 기준)
    public static float JumpVelocityFor(RobotMover mover, float height)
    {
        if (mover == null || height <= 0f)
            return 0f;

        float g = Mathf.Abs(mover.Gravity);
        float gdt = g * CombatClock.TICK;
        return (gdt + Mathf.Sqrt(gdt * gdt + 8f * g * height)) * 0.5f;
    }
}
