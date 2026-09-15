public class Rng
{
    private uint _state;

    public Rng(int seed)
    {
        _state = (uint)(seed == 0 ? 1 : seed);
    }

    public uint NextUInt()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    // 0 이상 1 미만
    public float Value => (float)(NextUInt() * (1.0 / 4294967296.0));

    // min 이상 max 미만. 역순이면 뒤집고, 폭이 0이면 min
    public int Range(int minInclusive, int maxExclusive)
    {
        if (minInclusive > maxExclusive)
        {
            int swap = minInclusive;
            minInclusive = maxExclusive;
            maxExclusive = swap;
        }

        long width = (long)maxExclusive - minInclusive;
        if (width <= 0)
            return minInclusive;

        return (int)(minInclusive + (long)(NextUInt() % (ulong)width));
    }

    // 역순이면 뒤집는다
    public float Range(float min, float max)
    {
        if (min > max)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        return min + Value * (max - min);
    }

    // p가 범위 밖이면 난수를 소비하지 않는다
    public bool Chance(float p)
    {
        if (p <= 0f)
            return false;
        if (p >= 1f)
            return true;

        return Value < p;
    }
}
