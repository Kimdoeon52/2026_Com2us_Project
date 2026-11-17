using System;

/// <summary>
/// 한 행동에 대해 합산이 끝난 보정값. ActionState.Begin()에서 한 번 계산되어 그 행동이 끝날 때까지 유지된다.
/// </summary>
public sealed class ResolvedModifiers
{
    private static readonly int Count = Enum.GetValues(typeof(SkillParamId)).Length;

    private readonly float[] mul = new float[Count];

    public ResolvedModifiers() => Reset();

    /// 행동 시작 때마다 호출. 새로 할당하지 않고 값만 되돌린다 (GC 방지)
    public void Reset()
    {
        for (int i = 0; i < mul.Length; i++) mul[i] = 1f;
    }

    public void Multiply(SkillParamId id, float value) => mul[(int)id] *= value;

    public float Apply(SkillParamId id, float baseValue) => baseValue * mul[(int)id];
}
