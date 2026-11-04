using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 쿄다이 촉수 하나 (2026-11-04 해킹 대체 기믹). KyodaiTentacleSpawner가 만들고 전투 틱마다 돌린다.
///
///   · 땅에서 솟아남 → 흐느적거리며 대기 → 플레이어가 가까이 오면 뒤로 젖힘(예고) → 휘두름(판정) → 돌아옴 → 쉼
///   · 플레이어 공격에 맞는다(IPlayerHitTarget — 보스 HP와 별개의 자기 체력). 체력이 0이면 쓰러져 들어간다
///
/// 그림: 뼈 Transform 사슬(뿌리 → 끝, 각 뼈가 앞 뼈의 자식)을 코드로 꺾는다. 지금은 마디마다 임시 그림이 붙어 있고,
/// 진짜 그림이 나오면 같은 뼈에 SpriteSkin(2D Animation)으로 입히면 이 코드는 그대로 쓴다.
/// 꺾임(bend)은 전투 틱에서 스프링으로 계산(판정 타이밍과 맞게), 흐느적거림·색은 Update에서 그림만.
/// </summary>
public class KyodaiTentacle : MonoBehaviour, IPlayerHitTarget
{
    private enum State { Emerge, Idle, WindUp, Swing, Recover, Dying, Dead }

    [Tooltip("뼈 사슬 — 뿌리부터 끝까지. 각 뼈는 앞 뼈의 자식")]
    [SerializeField] private Transform[] bones;

    [Tooltip("맞는 자리 (이 오브젝트의 BoxCollider2D, 트리거)")]
    [SerializeField] private BoxCollider2D hurtBox;

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private KyodaiTentacleSpawner owner;
    private KyodaiTentacleData data;
    private State state = State.Dead;
    private int frame, cooldown;
    private float hp;
    private int dir = 1;                 // 휘두르는 방향 (플레이어 쪽 +1 = 오른쪽)
    private float bend, bendVel, prevBend;
    private float grow, prevGrow;
    private bool swingHitDone, paused;

    // 그림
    private float swayClock, seed, flashUntil;
    private SpriteRenderer[] renderers = new SpriteRenderer[0];
    private Color[] baseColors = new Color[0];

    public bool IsDead => state == State.Dead;

    /// <summary>디버프 수에 들어가는지 (쓰러지는 중이면 이미 빠진다)</summary>
    public bool Counts => state != State.Dying && state != State.Dead;

    public bool IsHittable => Counts && grow > 0.5f;

    private float S => owner != null ? owner.Size : 1f;
    private Vector2 Root => transform.position;

    public void Init(KyodaiTentacleSpawner spawner, KyodaiTentacleData tentacleData, float randomSeed)
    {
        owner = spawner;
        data = tentacleData;
        seed = randomSeed;
        hp = data.hp;
        state = State.Emerge;
        frame = cooldown = 0;
        bend = bendVel = prevBend = 0f;
        grow = prevGrow = 0f;
        swingHitDone = paused = false;
        dir = owner.PlayerX >= Root.x ? 1 : -1;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        if (hurtBox != null)
        {
            hurtBox.isTrigger = true;
            hurtBox.size = data.hurtSize;                        // 로컬 = 보스 단위 (보스 루트 밑이라 월드는 Size배)
            hurtBox.offset = new Vector2(0f, data.hurtSize.y * 0.5f);
            hurtBox.enabled = false;
        }
        UpdateVisual(0f, 1f);
    }

    /// <summary>그로기 동안: 새로 휘두르지 않고, 젖히던 중이면 돌아온다</summary>
    public void SetPaused(bool value)
    {
        paused = value;
        if (paused && state == State.WindUp) Begin(State.Recover);
    }

    /// <summary>격파 등 — 쓰러져 들어간다</summary>
    public void Kill()
    {
        if (Counts) Begin(State.Dying);
    }

    // ───────────────────────── 전투 틱 ─────────────────────────

    /// <summary>호출: KyodaiTentacleSpawner.Tick (CombatClock)</summary>
    public void Tick()
    {
        prevBend = bend;
        prevGrow = grow;
        if (state == State.Dead || data == null) return;
        frame++;
        if (cooldown > 0) cooldown--;

        switch (state)
        {
            case State.Emerge:
                grow = EaseOut(frame / (float)data.emergeFrames);
                BendTo(0f, 2f, 0.8f);
                if (frame >= data.emergeFrames) Begin(State.Idle);
                break;

            case State.Idle:
                BendTo(0f, 2f, 0.8f);
                if (!paused && cooldown == 0 && Mathf.Abs(owner.PlayerX - Root.x) <= data.triggerRange * S)
                {
                    dir = owner.PlayerX >= Root.x ? 1 : -1;
                    Begin(State.WindUp);
                }
                break;

            case State.WindUp: // 플레이어 반대쪽으로 젖힘 = 예고
                BendTo(-0.9f, 3f, 0.7f);
                if (frame >= data.windUpFrames) Begin(State.Swing);
                break;

            case State.Swing: // 빠르고 출렁이게 — 살짝 지나쳤다 돌아온다
                BendTo(1.25f, 9f, 0.45f);
                if (frame >= data.swingHitStart && frame <= data.swingHitEnd) CheckSwingHit();
                if (frame >= data.swingFrames) Begin(State.Recover);
                break;

            case State.Recover:
                BendTo(0f, 2.5f, 0.8f);
                if (frame >= data.recoverFrames)
                {
                    cooldown = data.cooldownFrames;
                    Begin(State.Idle);
                }
                break;

            case State.Dying:
                grow = 1f - EaseIn(frame / (float)data.dieFrames);
                BendTo(-0.6f, 2f, 0.8f);
                if (frame >= data.dieFrames) Begin(State.Dead);
                break;
        }
        if (hurtBox != null) hurtBox.enabled = IsHittable;
    }

    private void Begin(State next)
    {
        state = next;
        frame = 0;
        if (next == State.Swing) swingHitDone = false;
        if (next == State.Dying && hurtBox != null) hurtBox.enabled = false;
    }

    private void BendTo(float target, float frequency, float damping)
    {
        float w = 2f * Mathf.PI * frequency;
        bendVel += (w * w * (target - bend) - 2f * damping * w * bendVel) * CombatClock.TICK;
        bend += bendVel * CombatClock.TICK;
    }

    // 뿌리에서 휘두르는 쪽으로 뻗은 상자
    private void CheckSwingHit()
    {
        if (swingHitDone) return;
        Vector2 size = data.swingReach * S;
        Vector2 center = Root + new Vector2(dir * size.x * 0.5f, size.y * 0.5f);
        int count = Physics2D.OverlapBox(center, size, 0f, overlapFilter, overlap);
        for (int i = 0; i < count; i++)
        {
            if (!overlap[i].CompareTag("Player")) continue;
            if (owner.DamagePlayer(overlap[i].gameObject)) swingHitDone = true; // 회피 무적이면 false — 판정 구간 동안 계속 본다
            return;
        }
    }

    public void TakePlayerHit(float damage, Vector2 point)
    {
        if (!IsHittable) return;
        hp -= damage;
        flashUntil = Time.time + 0.08f;
        bendVel -= dir * 2f; // 맞으면 움찔
        if (hp <= 0f) Begin(State.Dying);
    }

    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    private static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }

    // ───────────────────────── 그림 ─────────────────────────

    private void Update()
    {
        if (data != null && state != State.Dead) UpdateVisual(Mathf.Min(Time.deltaTime, 1f / 30f), CombatClock.InterpolationAlpha);
    }

    private void UpdateVisual(float dt, float interpolation)
    {
        if (bones == null || bones.Length == 0 || data == null) return;
        float b = Mathf.Lerp(prevBend, bend, interpolation);
        float g = Mathf.Lerp(prevGrow, grow, interpolation);
        swayClock += dt * data.swaySpeed;

        // 솟아날 때·쓰러질 때: 뿌리 뼈를 세로로 늘리고 줄이면 사슬 전체가 따라온다
        bones[0].localScale = new Vector3(1f, Mathf.Max(0.001f, g), 1f);

        float swayWeight = state == State.Idle || state == State.Emerge ? 1f : 0.35f;
        int n = bones.Length;
        for (int i = 0; i < n; i++)
        {
            if (bones[i] == null) continue;
            float w = (i + 1f) / n; // 끝으로 갈수록 많이 꺾인다
            float rot = -dir * b * data.maxBend * w
                        + Mathf.Sin(swayClock + seed + i * 0.6f) * data.swayAngle * w * swayWeight;
            bones[i].localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        bool flash = Time.time < flashUntil;
        float alpha = state == State.Dying ? Mathf.Clamp01(g * 1.5f) : 1f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = flash ? data.hitFlashColor : baseColors[i];
            c.a = baseColors[i].a * alpha;
            renderers[i].color = c;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Vector2 size = data.swingReach * S;
        Gizmos.color = state == State.Swing ? Color.red : new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireCube(Root + new Vector2(dir * size.x * 0.5f, size.y * 0.5f), size);
    }
#endif
}
