// 시간 시스템 붙기 전 진행용 더미
public class DummyGameClock : IGameClock
{
    private int _month;
    private float _consumed;

    public int CurrentMonth => _month;

    // 누적 소모량. 실제 제한은 없다
    public float Consumed => _consumed;

    public bool TryConsume(float amount)
    {
        if (amount > 0f)
            _consumed += amount;

        return true;
    }

    public void AdvanceMonth()
    {
        _month++;
    }

    public void SetMonth(int month)
    {
        _month = month;
    }
}
