// 이동속도 버프 경계 (RobotMover 쪽 구현 필요)
public interface ISpeedBuffTarget
{
    // 배율, 지속 틱, 달리기 판정 유지
    void ApplySpeedBuff(float multiplier, int durationTicks, bool keepRunning);
}
