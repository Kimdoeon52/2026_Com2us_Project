using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이저 샷 (홀드형, ChargeThenRelease).
///
/// 공통 규칙은 HoldSkillData 값으로 처리된다:
///   - 게이지 1.0초 충전            : minHoldFrames = 60
///   - 충전 전에 떼면 취소, 쿨 50%   : earlyReleaseCooldownScale = 0.5
///   - 홀드 중 D → 지속 조사        : enhancedVariant = 강화 에셋(이 클래스로 만든 에셋), enhanceMode = SwapAndContinue
///
/// 이 클래스가 추가하는 것 두 가지:
///   1) 부채꼴 Hit박스를 저장하지 않고 설정값(fan)에서 계산한다 (BoostShotActionData와 같은 방식).
///      레이저는 "광선 여러 개가 겹쳐도 한 번만 맞는" 행동이라 PerBoxDamage는 끈 채로 둔다 (BoostShot과 다른 점).
///   2) 공중에서 시작하면 충전 ~ 발사(활성) 동안 공중에 정지하고, 후딜부터 떨어진다.
///
/// 강화판정 에셋도 이 클래스로 만들어야 부채꼴 박스가 생긴다 (일반 ActionData로 만들면 박스가 비어 판정이 안 나온다).
/// 강화 에셋의 홀드 관련 칸은 쓰이지 않는다 — 러너는 슬롯에 꽂힌 일반 에셋이 만든다.
/// </summary>
[CreateAssetMenu(menuName = "CSH/Skill/Laser Shot", fileName = "LaserShot")]
public class LaserShotData : HoldSkillData
{
    [System.Serializable]
    public struct FanSettings
    {
        [Min(1)] public int rays;                       // 광선 수
        [Range(0f, 180f)] public float spreadDegrees;   // 전체 각도. 정면 기준 좌우로 절반씩
        public float range;                             // 사거리 (총구에서 광선 끝까지)
        public float thickness;                         // 광선 두께. 사거리 끝에서 광선 사이가 비지 않게 충분히
        [Min(0)] public int hitLength;                  // 박스가 켜져 있는 프레임 수. 0 = 활성 구간 전체

        public static FanSettings Default => new FanSettings
        {
            rays = 11,
            spreadDegrees = 90f,
            range = 7f,
            thickness = 0.6f,
            hitLength = 0
        };
    }

    [Header("부채꼴 빔")]
    [SerializeField] private FanSettings fan = FanSettings.Default;

    [Header("공중 정지")]
    [Tooltip("켜면 공중에서 시작했을 때 충전 ~ 발사 동안 공중에 멈춘다")]
    [SerializeField] private bool hoverInAir = true;

    [System.NonSerialized] private FrameBox[] cache;

    public FanSettings Fan => fan;
    public bool HoverInAir => hoverInAir;

    public override FrameBox[] FrameBoxes => cache ??= Build();

    private void OnValidate() => cache = null;
    private void OnEnable() => cache = null;

    public override SkillRunner CreateRunner() => new Runner(this);

    // BoostShotActionData.Build와 같은 규칙에서 거리 감쇠만 뺀 것 (레이저는 거리 무관 동일 피해)
    private FrameBox[] Build()
    {
        var list = new List<FrameBox>();

        // 직접 저장된 박스 중 Hit는 버린다 (부채꼴이 대신함). Hurt/Push는 유지
        foreach (var box in StoredFrameBoxes)
            if (box.type != BoxType.Hit) list.Add(box);

        int rays = Mathf.Max(1, fan.rays);
        Vector2 muzzle = ProjectileMuzzle;
        float length = fan.range;

        for (int i = 0; i < rays; i++)
        {
            float t = rays == 1 ? 0f : (float)i / (rays - 1) - 0.5f;
            float angle = t * fan.spreadDegrees;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 모든 광선이 총구에서 시작하도록 중심을 광선 방향으로 length/2 만큼 민다
            Vector2 center = muzzle + dir * (length * 0.5f);
            list.Add(new FrameBox
            {
                type = BoxType.Hit,
                bodyPart = BodyPart.Core,
                rect = new Rect(center.x - length * 0.5f, center.y - fan.thickness * 0.5f, length, fan.thickness),
                rotation = angle,
                damageScale = 1f,
                anchor = FrameAnchor.Active,
                startOffset = 0,
                length = fan.hitLength,
                startFrame = 1,                      // Legacy 앵커에서만 읽히는 값
                endFrame = Mathf.Max(1, TotalFrames)
            });
        }
        return list.ToArray();
    }

    private sealed class Runner : HoldSkillRunner
    {
        private readonly LaserShotData data;
        private bool hovering;

        public Runner(LaserShotData data) { this.data = data; }

        protected override void OnBegin()
        {
            base.OnBegin(); // 홀드 상태 초기화 — 반드시 먼저

            hovering = data.hoverInAir && Ctx.Mover != null && !Ctx.Mover.IsGrounded;
            if (!hovering) return;

            Ctx.Mover.Leap(0f, 0f); // 수평 관성 제거
            Hover();
        }

        protected override void OnPhaseTick(SkillPhase phase, in SkillInputFrame input)
        {
            base.OnPhaseTick(phase, input); // OnHoldTick 호출 유지

            if (!hovering) return;
            if (phase == SkillPhase.Recovery) { hovering = false; return; } // 후딜부터 낙하
            Hover();
        }

        protected override void OnEnd() => hovering = false;

        // RobotMover.Tick이 이번 틱에 중력(gravity * dt)을 더하므로, 미리 그만큼 반대로 넣어 두면 속도가 0이 되어 제자리에 머문다.
        // 러너(ActionExecutor.ExecuteTick)가 RobotMover.Tick보다 먼저 등록돼 있어서 이 순서가 성립한다 (PlayerRobotBootstrap)
        private void Hover()
        {
            if (Ctx.Mover == null) return;
            Ctx.Mover.SetVerticalVelocity(-Ctx.Mover.Gravity * CombatClock.TICK);
        }
    }
}