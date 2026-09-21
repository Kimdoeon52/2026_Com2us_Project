
/// <summary>
/// 한 행동에 대해 합산이 끝난 장비 효과. ActionState.Begin()에서 한 번 계산되어 그 행동이 끝날 때까지 유지된다.
/// </summary>
public struct ResolvedModifiers
{
    public float HitBoxWidthAdd;
    public float HitBoxHeightAdd;
    public float KnockbackMultiplier;

    // default(ResolvedModifiers)는 KnockbackMultiplier가 0이 되어 넉백이 사라지므로,
    // "보정 없음" 상태가 필요할 땐 반드시 이 Identity를 쓴다
    public static ResolvedModifiers Identity => new ResolvedModifiers { KnockbackMultiplier = 1f };
}
