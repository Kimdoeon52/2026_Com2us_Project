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
        Vector3 pos = BoxResolver.ToWorldPoint(transform.position, entry.offset, facingRight);

        // 좌우 반전(음수 스케일)과 같이 쓰면 각도도 반대로 돌려야 오른쪽 볼 때와 대칭이 된다
        bool flipped = entry.flipWithFacing && !facingRight;
        float angle = flipped ? -entry.rotation : entry.rotation;
        Quaternion rot = entry.prefab.transform.rotation * Quaternion.Euler(0f, 0f, angle);

        GameObject go = Instantiate(entry.prefab, pos, rot);
        BossVfxHelper.ConfigureVisualOnly(go); // 콜라이더·리지드바디 끄고 Ignore Raycast로 격리 (판정 오염 방지)

        Vector3 s = entry.prefab.transform.localScale * entry.scale;
        if (flipped) s.x = -s.x;
        go.transform.localScale = s;

        var robotRenderer = RobotView.FindRenderer(this);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (robotRenderer != null)
            {
                r.sortingLayerID = robotRenderer.sortingLayerID;
                r.sortingOrder = robotRenderer.sortingOrder + entry.sortingOrderOffset + r.sortingOrder;
            }
        }

        // 음수 스케일(좌우 반전)이 자식 파티클까지 먹도록 Hierarchy 스케일로 맞춘다
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (entry.playbackSpeed > 0f) main.simulationSpeed *= entry.playbackSpeed; // 인스펙터 + 버튼으로 생긴 항목은 0일 수 있음

            // 카메라를 보는 빌보드(Alignment=View)는 Transform 회전을 무시하고 음수 스케일 반전과 섞이면 각도가 틀어진다.
            // 2D 화면 평면(XY)에 놓인 빌보드는 Local 정렬로 바꿔 Transform 회전·반전을 그대로 따르게 한다
            if (IsViewAlignedBillboard(ps) && Mathf.Abs(Vector3.Dot(ps.transform.forward, Vector3.forward)) > 0.99f)
                ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
        }

        live.Add(new Live { go = go, entry = entry, followOffset = go.transform.position - transform.position });
    }

    private static bool IsViewAlignedBillboard(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        return r != null
            && r.renderMode == ParticleSystemRenderMode.Billboard
            && r.alignment == ParticleSystemRenderSpace.View;
    }

    private void LateUpdate()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var l = live[i];
            if (l.go == null) { live.RemoveAt(i); continue; }
            if (l.entry.followOwner) l.go.transform.position = transform.position + l.followOffset;
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
