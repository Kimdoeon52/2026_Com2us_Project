public class Rng
{
    private uint _state;

    public Rng(int seed) => _state = (uint)(seed == 0 ? 1 : seed);

    public uint NextUInt()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }

    public int Range(int minInclusive, int maxExclusive)
        => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));

    public float Value => NextUInt() / (float)uint.MaxValue;
    public float Range(float min, float max) => min + Value * (max - min);
    public bool Chance(float p) => Value < p;
}