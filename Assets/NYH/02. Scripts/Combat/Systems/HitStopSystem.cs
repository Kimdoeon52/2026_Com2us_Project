/// <summary>
/// 맞는 순간 공격자·피격자를 몇 프레임 멈출지 정하는 유일한 지점 (2026-10-28, KnockbackSystem·HitReactionSystem 옆자리).
///
/// 정지 길이는 HitStopSettings 공식(공격력 비례) × 기술 계수 × 박스 계수. 실제로 멈추는 일은 각 로봇의
/// ActionExecutor(프레임·발사)·RobotMover(이동)·RobotView(애니메이션)가 "정지 남은 프레임"을 보고 한다.
/// 판정 박스는 고정 — 진동은 RobotView가 그림(Visual 자식)만 흔든다. 때린 쪽의 느린 움직임은 실제 몸 이동(낙하 등)이라 박스도 같이 아주 조금 움직인다.
///
/// 규칙:
///   · 피격 모션(경직)이 나오는 타격만 — 공격 중이라 버틴 타격까지 멈추면 이유 없이 굳는 버그처럼 보였다(플레이 피드백, 설정으로 끌 수 있음)
///   · 근접 — 맞은 쪽은 완전히 멈추고 크게 흔들린다. 때린 쪽은 작게 흔들리며 평소의 몇 %(attackerTimeScale) 속도로 계속 움직인다
///   · 투사체 — 맞은 쪽만 멈춘다(설정으로 쏜 쪽도 멈추게 할 수 있음)
///   · 다단히트(화염 등) — 첫 타만 멈춘다(설정으로 끌 수 있음)
///   · 회피 성공(MISS) — 안 멈춘다
/// </summary>
public static class HitStopSystem
{
    // 호출: HitDetection(로봇끼리 근접·투사체, ProcessHit과 경직·넉백 처리 직후).
    // attacker: 쏜/때린 로봇. boxScale: 맞힌 Hit박스의 계수(박스당 피해 행동이면 겹친 박스 계수의 합).
    // isRepeatHit: 다단히트 기술의 두 번째 이후 타격인지. defenderReacted: 이번 타격으로 피격 모션에 들어갔는지
    public static void Apply(HitStopSettings settings, ActionExecutor attacker, ActionExecutor defender,
                             ActionData action, HitResolutionResult result, float boxScale,
                             bool fromProjectile, bool isRepeatHit, bool defenderReacted)
    {
        if (settings == null || defender == null || action == null || result == null) return;
        if (result.IsEvaded) return;
        if (settings.OnlyWhenHurtReaction && !defenderReacted) return;
        if (isRepeatHit && settings.MultiHitFirstOnly) return;

        int frames = settings.ComputeFrames(action.Damage, action.HitStopScale * boxScale);
        if (frames <= 0) return;

        // 맞은 쪽은 완전 정지(0), 때린 쪽은 평소의 몇 %로 계속 움직인다 — 둘 다 굳으면 부자연스럽다(플레이 피드백)
        defender.StartHitStop(frames, settings.VictimShakePixels, 0f);
        if (attacker != null && (!fromProjectile || settings.ProjectileFreezesAttacker))
            attacker.StartHitStop(frames, settings.AttackerShakePixels, settings.AttackerTimeScale);
    }

    // 2026-10-28 보스전: 플레이어가 KKH 보스를 때렸을 때 — 보스는 ActionExecutor가 아니라 멈출 수 없으니 때린 쪽만 느려진다.
    // "피격 모션이 나올 때만" 규칙은 맞은 쪽 기준이라 여기엔 적용하지 않는다(보스엔 우리 경직이 없음)
    // 호출: HitDetection(보스 근접·투사체)
    public static void ApplyAttackerOnly(HitStopSettings settings, ActionExecutor attacker, ActionData action,
                                         HitResolutionResult result, float boxScale, bool fromProjectile, bool isRepeatHit)
    {
        if (settings == null || attacker == null || action == null || result == null) return;
        if (result.IsEvaded) return;
        if (isRepeatHit && settings.MultiHitFirstOnly) return;
        if (fromProjectile && !settings.ProjectileFreezesAttacker) return;

        int frames = settings.ComputeFrames(action.Damage, action.HitStopScale * boxScale);
        if (frames <= 0) return;
        attacker.StartHitStop(frames, settings.AttackerShakePixels, settings.AttackerTimeScale);
    }

    // 2026-10-28 보스전: KKH 보스 공격이 허브에서 플레이어 HP를 깎았을 때 — 우리 쪽엔 공격 ActionData가 없어서
    // 깎인 HP를 공격력 자리에 넣는다. 때린 보스는 멈추지 않는다
    // 호출: HitDetection.OnPlayerHpChanged
    public static void ApplyVictimOnly(HitStopSettings settings, ActionExecutor defender, int damage, bool defenderReacted)
    {
        if (settings == null || defender == null) return;
        if (settings.OnlyWhenHurtReaction && !defenderReacted) return;

        int frames = settings.ComputeFrames(damage, 1f);
        if (frames <= 0) return;
        defender.StartHitStop(frames, settings.VictimShakePixels, 0f);
    }
}
