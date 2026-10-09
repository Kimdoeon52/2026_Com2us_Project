/// <summary>
/// 맞은 로봇이 경직(피격 모션)에 들어갈지 정하는 유일한 지점 (§11-7, KnockbackSystem 옆자리).
/// 근거: 기획서1009 §6-12-10 "경직 시스템 — 약간의 경직도가 존재, 피격시 약간의 밀려남과 피격 애니메이션".
///
/// 규칙 자체(확률·기준 데미지·피격 행동)는 로봇마다 다른 HitReactionData 에셋에 있고, 여기는 그걸
/// 읽어서 판단만 한다 — 플레이어든 적이든 같은 코드로 돈다(§8 "로봇 본체는 하나").
///
/// 경직은 "피격 ActionData를 강제로 시작하는 것"으로 표현한다. 그래서 별도 Stagger 상태값이 필요 없고,
/// 경직 길이·박스·애니메이션이 전부 그 에셋 하나에서 나온다(§2 "기술이 늘어도 클래스는 안 늘어난다").
///
/// Update가 필요 없는 순수 판단이라 KnockbackSystem과 같이 static 클래스다.
/// </summary>
public static class HitReactionSystem
{
    // 경직 확률 굴림용. UnityEngine.Random 대신 System.Random을 쓰는 이유: 전투 틱 안에서 도는 판단이라
    // 연출용 랜덤(이펙트 위치 등)과 시퀀스를 섞지 않으려고 따로 둔다
    private static readonly System.Random rng = new System.Random();

    // 호출: HitDetection(근접·투사체 공통, ProcessHit 직후 · 넉백 직전). 넉백보다 먼저 부르는 이유:
    // 넉백의 런치(띄우기)가 먼저 돌면 "공중이라 경직 없음"으로 판단돼서, 어퍼컷에 맞고도 움찔하지 않게 된다
    public static void Apply(ActionExecutor defender, HitResolutionResult result)
    {
        if (result.IsEvaded) return;

        HitReactionData data = defender.HitReaction;
        if (data == null || data.HurtAction == null) return; // 경직 규칙이 없는 로봇 — 점멸만 있음

        ActionState state = defender.State;
        if (state.IsDead) return;

        // 점프 중에는 기본적으로 궤적을 유지한다 (reactInAir로 켤 수 있음)
        RobotMover mover = defender.Mover;
        if (mover != null && !mover.IsGrounded && !data.ReactInAir) return;

        bool heavy = result.DamageToHp >= data.InterruptDamageThreshold;
        ActionData current = state.CurrentAction;

        if (current == null)
        {
            // 대기·걷기 중: 강한 공격이면 무조건, 아니면 확률로
            if (!heavy && rng.NextDouble() >= data.IdleReactionChance) return;
        }
        else if (current != data.HurtAction)
        {
            // 공격 등 다른 행동 중: 강한 공격에만 끊긴다 — 약한 공격엔 버티고 하던 걸 마저 한다
            if (!heavy) return;
        }
        // 이미 경직 중이면 처음부터 다시 시작(경직 갱신) — 연타에 맞는 동안 계속 움찔하게

        state.Interrupt(data.HurtAction);
    }
}
