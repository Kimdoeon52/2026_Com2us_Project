using UnityEngine;

/// <summary>
/// 타격이 확정된 뒤 맞은 로봇을 실제로 밀어내고(넉백) 띄우는(런치) 시스템 (§11-7).
/// 판정(HitDetection)과 역할을 분리한 이유: HitDetection은 "맞았는지 아닌지·얼마나 아픈지"까지만
/// KKH에게 계산시키고, "맞으면 실제로 몸이 어떻게 움직이는지"는 이 클래스가 전담한다.
/// 경직(피격 모션)은 옆의 HitReactionSystem이 맡는다.
///
/// 2026-10-09: 구 "SF2 방식(거리로 콤보를 끊는다)" 근거는 §6에서 삭제됐다. 지금 근거는
/// 기획서1009 §6-12-10 "피격시 약간의 밀려남"이다 — 수치는 ActionData.KnockbackDistance 그대로 쓴다.
///
/// Update가 필요 없는 순수 계산이라 static 클래스다. 밀리는 과정을 여러 프레임에 걸쳐 부드럽게
/// 보여줘야 하는 요구가 생기면 상태(진행 중인 넉백 목록)가 필요해지므로 그때 바꿔야 한다.
/// </summary>
public static class KnockbackSystem
{
    // 호출: HitDetection(근접·투사체 공통). sourceX: 밀어내는 기준 x좌표 — 근접이면 공격자, 투사체면 총알 위치.
    // 투사체를 공격자 위치 기준으로 밀면, 멀리서 쏜 총알이 공격자 쪽으로 끌어당기는 방향이 될 수 있어서 기준점을 따로 받는다
    public static void Apply(float sourceX, ActionData sourceAction, ActionExecutor defender, HitResolutionResult result)
    {
        // 회피 무적으로 완전히 피했으면 맞은 게 아니므로 밀릴 이유가 없다
        if (result.IsEvaded) return;

        // 0 이하면 그냥 아무 일도 안 일어난다 — 기획이 값을 채우면 코드 수정 없이 자동으로 동작한다
        if (result.KnockbackDistance > 0f)
        {
            // FacingRight(스프라이트가 보는 방향)가 아니라 실제 x좌표 차이로 정하는 이유:
            // 필요한 건 "누가 나를 보고 있는지"가 아니라 "물리적으로 어느 쪽이 비어있는지"이기 때문
            float direction = defender.transform.position.x >= sourceX ? 1f : -1f;

            Vector3 pos = defender.transform.position;
            pos.x += direction * result.KnockbackDistance;
            defender.transform.position = pos;
            // 화면 밖으로 밀려도 RobotMover.Update()가 CombatCamera.ClampFighterX로 바로 다음 프레임에 되돌린다
        }

        // 어퍼컷처럼 띄우는 기술 — KKH 결과(result)에는 이 값이 없어서 행동 데이터에서 직접 읽는다
        if (sourceAction != null && sourceAction.LaunchVelocity > 0f && defender.Mover != null)
            defender.Mover.Launch(sourceAction.LaunchVelocity);
    }
}
