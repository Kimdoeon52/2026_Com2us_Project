using UnityEngine;

/// <summary>
/// 타격이 확정된 뒤 ActionData.KnockbackDistance만큼 실제로 로봇을 밀어내는 시스템 (§6, §11-7).
/// 판정(HitDetection)과 역할을 분리한 이유: HitDetection은 "맞았는지 아닌지·얼마나 아픈지"까지만
/// KKH에게 계산시키고, "맞으면 실제로 얼마나 밀려나는지"는 이 클래스가 전담한다 — 나중에
/// 스턴/경직(StunSystem, 히트스톱 등)이 여기 옆에 나란히 들어갈 자리.
///
/// CLAUDE.md §6 SF2 방식: 콤보 제한 카운터(comboCount, maxComboHits 같은 것)를 따로 두지 않고,
/// 맞을 때마다 거리가 벌어져서 자연스럽게 다음 공격이 안 닿게 되는 것으로 콤보가 끝나게 만든다.
/// 이 클래스가 하는 일이 바로 그 "거리를 벌리는" 부분이다.
///
/// 지금은 Update가 필요 없는 순수 계산이라 MonoBehaviour가 아니라 static 클래스로 만들었다.
/// 나중에 "밀리는 과정을 순간이동이 아니라 몇 프레임에 걸쳐 부드럽게" 같은 요구가 생기면(예: 히트스톱과
/// 맞물려 타이밍을 맞춰야 하면) 그때는 상태(진행 중인 넉백 목록 등)가 필요해지므로 MonoBehaviour로 바꿔야 한다.
///
/// 주의(설계 결정서 §8): "넉백 4칸(히트/가드 × 상대/자기)"이 아직 팀 미확정이다 — ActionData엔 지금
/// KnockbackDistance 값이 하나뿐이라, 여기서는 그 값을 "방어자가 공격자 반대 방향으로 밀리는 거리"로만
/// 우선 적용한다. 가드 시 넉백을 다르게 줄지, 공격자도 같이 밀리게 할지는 팀 합의 후 확장해야 한다.
/// </summary>
public static class KnockbackSystem
{
    // 호출: HitDetection.ResolveHit(ProcessHit 직후). attacker/defender: 누가 누굴 때렸는지. result: KKH가 계산한 타격 결과
    public static void Apply(ActionExecutor attacker, ActionExecutor defender, HitResolutionResult result)
    {
        // 위빙으로 완전히 피했으면 맞은 게 아니므로 밀릴 이유가 없다
        if (result.IsEvaded) return;

        // 프레임표가 아직 확정 안 된 액션은 KnockbackDistance가 기본값 0이다(§0 "수치를 지어내지 말 것") —
        // 0 이하면 그냥 아무 일도 안 일어나는 게 맞다. 나중에 기획이 값을 채우면 코드 수정 없이 자동으로 동작한다
        if (result.KnockbackDistance <= 0f) return;

        // 공격자 기준으로 방어자가 오른쪽에 있으면 오른쪽으로, 왼쪽에 있으면 왼쪽으로 — 즉 "더 멀어지는 방향"으로 밀어낸다.
        // FacingRight(스프라이트가 보는 방향)가 아니라 실제 두 로봇의 x좌표 차이로 직접 계산하는 이유:
        // 이 값이 필요한 건 "누가 나를 보고 있는지"가 아니라 "물리적으로 어느 쪽이 비어있는지"이기 때문이다
        float direction = defender.transform.position.x >= attacker.transform.position.x ? 1f : -1f;

        Vector3 pos = defender.transform.position;
        pos.x += direction * result.KnockbackDistance;
        defender.transform.position = pos;

        // 화면 경계를 벗어나게 밀렸어도 걱정할 필요 없다 — RobotMover.Update()가 매 프레임
        // CombatCamera.ClampFighterX로 이미 위치를 다시 화면 안으로 보정해주고 있어서,
        // 바로 다음 렌더 프레임에 자동으로 화면 안으로 당겨진다
    }
}
