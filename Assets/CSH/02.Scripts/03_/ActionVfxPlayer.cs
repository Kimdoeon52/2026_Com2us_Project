using System.Collections.Generic;
using RealSteel.Boss.Visual;
using UnityEngine;

/// <summary>
/// ActionData.Vfx(구간별 이펙트 프리팹)를 ActionState 이벤트에 맞춰 생성·종료한다 (View).
///
/// 흐름
///   OnActionBegin        : 이전 행동 이펙트 정리 → 시작 구간이 선딜인 항목 생성
///   OnActionActiveStart  : 끝 구간이 선딜인 항목 종료 → 시작 구간이 활성인 항목 생성
///   OnActionRecoveryStart: 끝 구간이 활성 이하인 항목 종료 → 시작 구간이 후딜인 항목 생성
///   OnActionEnd / OnDead : 전부 종료
/// 끊김(Interrupt)은 OnActionEnd 없이 OnActionBegin이 다시 오므로 Begin에서 정리된다.
/// 순수 연출이라 재생·수명은 실제 시간으로 돈다. RobotView.EnsureEffectComponents가 자동으로 붙인다.
/// </summary>
public class ActionVfxPlayer : MonoBehaviour
{
    private sealed class Live
    {
        public GameObject go;
        public ActionVfxEntry entry;
        public Vector3 followOffset;

        // 차지 단계별 크기 — baseScale(생성 시 크기, 좌우 반전 부호 포함)에 chargeScale을 곱한다
        public Vector3 baseScale;
        public float chargeScale = 1f;
        public float targetChargeScale = 1f;
        public float chargeBlendSpeed;   // 초당 배율 변화량 — 단계가 바뀔 때 정해진다
    }

    // 반복 생성 항목 — 구간 시작 GlobalFrame부터 interval 틱마다 1개씩 더 만든다
    private sealed class Repeater
    {
        public ActionVfxEntry entry;
        public int startFrame;
        public int interval;
        public int spawned;
    }

    private const float DefaultLifetime = 2f;

    private ActionState state;
    private RobotMover mover;
    private readonly List<Live> live = new List<Live>();
    private readonly List<Repeater> repeaters = new List<Repeater>();

    // ActionState는 ActionExecutor.Init(PlayerRobotBootstrap.Awake)에서 만들어지므로 Start에서 구독 (AfterImageEmitter와 같은 이유)
    private void Start()
    {
        mover = GetComponent<RobotMover>();
        var executor = GetComponent<ActionExecutor>();
        state = executor != null ? executor.State : null;
        if (state == null)
        {
            Debug.LogWarning($"[ActionVfxPlayer] {name}: ActionExecutor.State가 없음 — 구간 이펙트를 재생하지 않음");
            return;
        }

        state.OnActionBegin += OnBegin;
        state.OnActionActiveStart += OnActiveStart;
        state.OnActionRecoveryStart += OnRecoveryStart;
        state.OnActionEnd += OnEnd;
        state.OnDead += OnDead;
        state.OnChargeLevelChanged += OnChargeLevelChanged;
    }

    private void OnDestroy()
    {
        if (state != null)
        {
            state.OnActionBegin -= OnBegin;
            state.OnActionActiveStart -= OnActiveStart;
            state.OnActionRecoveryStart -= OnRecoveryStart;
            state.OnActionEnd -= OnEnd;
            state.OnDead -= OnDead;
            state.OnChargeLevelChanged -= OnChargeLevelChanged;
        }
        EndAll(ActionVfxEndMode.Destroy);
    }

    private void OnBegin(ActionData action)
    {
        EndAll(null);
        EnterPhase(action, 0);
    }

    private void OnActiveStart(ActionData action) => EnterPhase(action, 1);
    private void OnRecoveryStart(ActionData action) => EnterPhase(action, 2);
    private void OnEnd(ActionData action) => EndAll(null);
    private void OnDead() => EndAll(null);

    // 차지 단계가 바뀌었다: 단계별 크기가 설정된 살아 있는 이펙트의 목표 크기를 바꾼다 (실제 변화는 LateUpdate에서 부드럽게)
    private void OnChargeLevelChanged(int level)
    {
        foreach (var l in live)
        {
            if (!l.entry.ScalesWithCharge) continue;
            l.targetChargeScale = l.entry.ChargeScale(level);
            float blend = l.entry.chargeScaleBlendTime;
            if (blend <= 0f)
            {
                l.chargeScale = l.targetChargeScale;
                if (l.go != null) l.go.transform.localScale = l.baseScale * l.chargeScale;
            }
            else
            {
                // 지금 크기에서 목표까지를 blendTime 동안 일정한 속도로 간다
                l.chargeBlendSpeed = Mathf.Abs(l.targetChargeScale - l.chargeScale) / blend;
            }
        }
    }

    // phaseIndex 구간에 들어섰다: 이 구간 전에 끝나는 이펙트를 정리하고, 이 구간에서 시작하는 이펙트를 만든다
    private void EnterPhase(ActionData action, int phaseIndex)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i].entry.EndIndex < phaseIndex)
            {
                Finish(live[i], live[i].entry.endMode);
                live.RemoveAt(i);
            }
        }
        repeaters.RemoveAll(r => r.entry.EndIndex < phaseIndex);

        if (action == null || action.Vfx == null) return;
        foreach (var entry in action.Vfx)
        {
            if (entry == null || entry.prefab == null || entry.StartIndex != phaseIndex) continue;
            int interval = entry.ResolveInterval(action);
            if (interval <= 0)
            {
                Spawn(entry);
                continue;
            }

            // 반복 항목은 시작 오프셋만큼 늦게 첫 생성 — 오프셋이 0이면 기존처럼 구간 시작 틱에 바로 1개
            int delay = Mathf.Max(0, entry.repeatStartOffsetFrames);
            bool spawnNow = delay == 0;
            if (spawnNow) Spawn(entry);
            repeaters.Add(new Repeater { entry = entry, startFrame = state.GlobalFrame + delay, interval = interval, spawned = spawnNow ? 1 : 0 });
        }
    }

    // 전투 틱은 렌더 프레임과 따로 돌므로 "지난 틱 수 / 간격"으로 밀린 개수만큼 따라잡는다 (EmitProjectiles와 같은 0, n, 2n... 타이밍)
    private void Update()
    {
        if (state == null) return;
        foreach (var r in repeaters)
        {
            if (state.GlobalFrame < r.startFrame) continue; // 시작 오프셋 대기 중
            int due = (state.GlobalFrame - r.startFrame) / r.interval + 1;
            while (r.spawned < due)
            {
                Spawn(r.entry);
                r.spawned++;
            }
        }
    }

    private void Spawn(ActionVfxEntry entry)
    {
        bool facingRight = mover == null || mover.FacingRight;
        GameObject go = SpawnVisual(entry.prefab, transform.position, entry.offset, entry.rotation, facingRight,
                                    entry.flipWithFacing, CombineScale(entry.scale, entry.scaleXY), entry.playbackSpeed,
                                    RobotView.FindRenderer(this), entry.sortingOrderOffset, entry.XYRotation, entry.flipByRotation);

        // 차지 도중에 생성되는 항목(반복 생성 등)은 지금 단계 크기로 바로 시작한다
        float charge = entry.ChargeScale(state != null ? state.ChargeLevel : 0);
        Vector3 baseScale = go.transform.localScale;
        if (charge != 1f) go.transform.localScale = baseScale * charge;

        live.Add(new Live
        {
            go = go, entry = entry, followOffset = go.transform.position - transform.position,
            baseScale = baseScale, chargeScale = charge, targetChargeScale = charge,
        });
    }

    /// <summary>
    /// 공통 배율(scale)과 축별 배율(scaleXY)을 합친다. 인스펙터 + 버튼으로 빈 목록에 만든 항목은 scaleXY가 (0, 0)일 수 있어서
    /// 0 이하인 축은 1로 취급한다 — 안 그러면 이펙트가 보이지 않는다
    /// </summary>
    public static Vector2 CombineScale(float scale, Vector2 scaleXY)
    {
        float x = scaleXY.x > 0f ? scaleXY.x : 1f;
        float y = scaleXY.y > 0f ? scaleXY.y : 1f;
        return new Vector2(scale * x, scale * y);
    }

    /// <summary>
    /// 이펙트 프리팹 1개를 순수 연출용으로 생성한다. 스킬 구간 이펙트(ActionVfxEntry)와 폭발 이펙트(ExplosionVfxEntry)가 같이 쓴다.
    /// origin + offset(오른쪽 기준, 좌우 반전 자동) 위치에 rotation/scale/playbackSpeed를 적용하고,
    /// sortBase가 있으면 그 렌더러의 sorting layer/order + sortingOrderOffset 앞에 그린다. scale은 x, y 축별 배율
    /// </summary>
    public static GameObject SpawnVisual(GameObject prefab, Vector3 origin, Vector2 offset, float rotation, bool facingRight,
                                         bool flipWithFacing, Vector2 scale, float playbackSpeed,
                                         Renderer sortBase, int sortingOrderOffset, Vector2 xyRotation = default,
                                         bool flipByRotation = false)
    {
        Vector3 pos = BoxResolver.ToWorldPoint(origin, offset, facingRight);

        bool flipped = flipWithFacing && !facingRight;
        bool scaleFlip = flipped && !flipByRotation;  // 음수 스케일로 반전 (2D 플립북 등)
        bool rotateFlip = flipped && flipByRotation;  // Y축 180도 회전으로 반전 (3D 파티클)

        Quaternion rot;
        if (flipByRotation)
        {
            // 거울 반전 = (Y축 180도 회전) × (깊이 z 반전). 2D 정사영 카메라에서는 깊이 반전이 화면에 안 보이므로
            // 회전만으로 좌우 반전이 된다. 음수 스케일이 없어 메시·스트레치·모양 모듈이 전부 같이 돌아간다.
            // 회전값은 오른쪽 기준 그대로 쓰고 바깥에 180도를 곱한다
            Quaternion baseRot = prefab.transform.rotation * Quaternion.Euler(xyRotation.x, xyRotation.y, rotation);
            rot = rotateFlip ? Quaternion.Euler(0f, 180f, 0f) * baseRot : baseRot;
        }
        else
        {
            // 좌우 반전(음수 스케일)과 같이 쓰면 각도도 반대로 돌려야 오른쪽 볼 때와 대칭이 된다.
            // X축 기준 거울 반전에서는 X 회전은 그대로, Y·Z 회전은 부호가 뒤집힌다
            float angle = flipped ? -rotation : rotation;
            float angleY = flipped ? -xyRotation.y : xyRotation.y;
            rot = prefab.transform.rotation * Quaternion.Euler(xyRotation.x, angleY, angle);
        }

        GameObject go = Instantiate(prefab, pos, rot);
        BossVfxHelper.ConfigureVisualOnly(go); // 콜라이더·리지드바디 끄고 Ignore Raycast로 격리 (판정 오염 방지)

        Vector3 s = prefab.transform.localScale;
        s.x *= scale.x;
        s.y *= scale.y;
        // z는 x, y 중 작은 쪽을 따라간다 — 2D라 보통 영향 없지만 3D 파티클 모양이 한쪽으로만 부풀지 않게
        s.z *= Mathf.Min(scale.x, scale.y);
        if (scaleFlip) s.x = -s.x;
        go.transform.localScale = s;

        if (sortBase != null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sortingLayerID = sortBase.sortingLayerID;
                r.sortingOrder = sortBase.sortingOrder + sortingOrderOffset + r.sortingOrder;
            }
        }

        // X/Y 회전이 있으면 화면 평면을 벗어나 기울어진 이펙트 — 자식까지 전부 그 회전을 따라야 한다
        bool tilted = xyRotation.x != 0f || xyRotation.y != 0f;

        // 음수 스케일(좌우 반전)이 자식 파티클까지 먹도록 Hierarchy 스케일로 맞춘다
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (playbackSpeed > 0f) main.simulationSpeed *= playbackSpeed; // 인스펙터 + 버튼으로 생긴 항목은 0일 수 있음

            // 카메라를 보는 빌보드(Alignment=View)는 Transform 회전을 무시하고 음수 스케일 반전과 섞이면 각도가 틀어진다.
            // 2D 화면 평면(XY)에 놓인 빌보드는 Local 정렬로 바꿔 Transform 회전·반전을 그대로 따르게 한다.
            // X/Y 회전(tilted)이 있으면 평면 여부와 상관없이 자식까지 전부 Local로 바꾼다 — 안 그러면 카메라만 보고 회전을 무시한다
            // 회전 반전은 음수 스케일이 없어서 평면 빌보드를 Local로 바꿀 필요가 없다 (바꾸면 왼쪽에서 뒷면이 보인다)
            bool planar = !flipByRotation && Mathf.Abs(Vector3.Dot(ps.transform.forward, Vector3.forward)) > 0.99f;
            if (IsViewAlignedBillboard(ps) && (tilted || planar))
                ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;

            if (rotateFlip) MirrorWorldSpaceVectors(ps);
        }

        return go;
    }

    /// <summary>끝까지 재생한 뒤 스스로 지워지게 한다 (endMode = KeepPlaying과 같음)</summary>
    public static void DestroyAfterNaturalLifetime(GameObject go)
    {
        if (go == null) return;
        Destroy(go, NaturalLifetime(go, go.GetComponentsInChildren<ParticleSystem>(true)));
    }

    /// <summary>
    /// 회전 반전(Y축 180도)일 때 월드 공간 힘·속도는 Transform을 따라 돌지 않으므로 같은 회전(x, z 부호 반전)을 직접 적용한다.
    /// 월드 y(중력 등)는 그대로
    /// </summary>
    private static void MirrorWorldSpaceVectors(ParticleSystem ps)
    {
        var force = ps.forceOverLifetime;
        if (force.enabled && force.space == ParticleSystemSimulationSpace.World)
        {
            force.x = Negate(force.x);
            force.z = Negate(force.z);
        }

        var vel = ps.velocityOverLifetime;
        if (vel.enabled && vel.space == ParticleSystemSimulationSpace.World)
        {
            vel.x = Negate(vel.x);
            vel.z = Negate(vel.z);
        }
    }

    private static ParticleSystem.MinMaxCurve Negate(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:
                c.constant = -c.constant;
                break;
            case ParticleSystemCurveMode.TwoConstants:
                float min = c.constantMin;
                c.constantMin = -c.constantMax;
                c.constantMax = -min;
                break;
            default: // Curve / TwoCurves — 배율 부호만 뒤집는다
                c.curveMultiplier = -c.curveMultiplier;
                break;
        }
        return c;
    }

    private static bool IsViewAlignedBillboard(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        // 메시 파티클도 View 정렬이면 카메라 기준으로 돌아서 Transform 회전을 무시한다
        return r != null
            && (r.renderMode == ParticleSystemRenderMode.Billboard || r.renderMode == ParticleSystemRenderMode.Mesh)
            && r.alignment == ParticleSystemRenderSpace.View;
    }

    private void LateUpdate()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var l = live[i];
            if (l.go == null) { live.RemoveAt(i); continue; }
            if (l.entry.followOwner) l.go.transform.position = transform.position + l.followOffset;

            if (l.chargeScale != l.targetChargeScale)
            {
                // 연출이라 실제 시간으로 보간
                l.chargeScale = Mathf.MoveTowards(l.chargeScale, l.targetChargeScale, l.chargeBlendSpeed * Time.deltaTime);
                l.go.transform.localScale = l.baseScale * l.chargeScale;
            }
        }
    }

    // mode가 null이면 각 항목의 endMode를 따른다
    private void EndAll(ActionVfxEndMode? mode)
    {
        foreach (var l in live) Finish(l, mode ?? l.entry.endMode);
        live.Clear();
        repeaters.Clear();
    }

    private static void Finish(Live l, ActionVfxEndMode mode)
    {
        if (l.go == null) return;

        var particles = l.go.GetComponentsInChildren<ParticleSystem>(true);
        switch (mode)
        {
            case ActionVfxEndMode.Destroy:
                Destroy(l.go);
                break;

            case ActionVfxEndMode.StopEmitting:
                if (particles.Length == 0) { Destroy(l.go); break; }
                float remain = 0f;
                foreach (var ps in particles)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    remain = Mathf.Max(remain, ps.main.startLifetime.constantMax / ps.main.simulationSpeed);
                }
                Destroy(l.go, Mathf.Max(0.1f, remain));
                break;

            case ActionVfxEndMode.KeepPlaying:
                Destroy(l.go, NaturalLifetime(l.go, particles));
                break;
        }
    }

    // 파티클(duration + 최대 수명) 또는 애니메이터 클립 길이. 반복 파티클은 루프를 끄고 한 사이클로 계산
    private static float NaturalLifetime(GameObject go, ParticleSystem[] particles)
    {
        float t = 0f;
        foreach (var ps in particles)
        {
            var main = ps.main;
            main.loop = false;
            t = Mathf.Max(t, (main.duration + main.startLifetime.constantMax) / main.simulationSpeed);
        }
        if (t <= 0f)
        {
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var c in animator.runtimeAnimatorController.animationClips)
                    if (c != null) t = Mathf.Max(t, c.length);
        }
        return t > 0.05f ? t : DefaultLifetime;
    }
}
